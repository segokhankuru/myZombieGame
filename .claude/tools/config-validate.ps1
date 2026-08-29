<#
.SYNOPSIS
  Validates config/balance and config/content JSON against config/schema JSON Schemas.

.DESCRIPTION
  Windows PowerShell has no JSON Schema validator, so this implements the subset that
  actually matters for game tuning: type, required, minimum/maximum, enum, nested
  properties, arrays, and unknown-key detection. Ranges are design intent - a value
  outside its range is a design bug, not a typo, and the schema's description says why.

  Called by the PostToolUse hook, so an out-of-range value is caught as it is written.
  Exit code 1 means at least one error.

.EXAMPLE
  .\.claude\tools\config-validate.ps1
  .\.claude\tools\config-validate.ps1 -Domain economy
  .\.claude\tools\config-validate.ps1 -File config/balance/economy.json
#>
[CmdletBinding()]
param(
    [string]$Domain,
    [string]$File,
    [string]$ProjectRoot = ".",
    [switch]$Quiet
)

$ErrorActionPreference = 'SilentlyContinue'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root      = (Resolve-Path $ProjectRoot).Path
$configDir = Join-Path $root "config"
$schemaDir = Join-Path $configDir "schema"

$script:errors   = @()
$script:warnings = @()
$script:checked  = 0

function Add-Err  ($file,$path,$msg) { $script:errors   += "[ERROR] ${file}: ${path} - ${msg}" }
function Add-Warn ($file,$path,$msg) { $script:warnings += "[WARN]  ${file}: ${path} - ${msg}" }

function Get-JsonType($v) {
    if ($null -eq $v) { return 'null' }
    if ($v -is [bool]) { return 'boolean' }
    if ($v -is [int] -or $v -is [long]) { return 'integer' }
    if ($v -is [double] -or $v -is [decimal] -or $v -is [single]) { return 'number' }
    if ($v -is [string]) { return 'string' }
    if ($v -is [Array] -or $v -is [System.Collections.IList]) { return 'array' }
    return 'object'
}

function Test-Node {
    param($value, $schema, [string]$path, [string]$file)

    if (-not $schema) { return }
    $script:checked++

    # --- type
    if ($schema.type) {
        $actual = Get-JsonType $value
        $expected = @($schema.type)
        $ok = $expected -contains $actual
        # an integer is an acceptable number
        if (-not $ok -and $actual -eq 'integer' -and $expected -contains 'number') { $ok = $true }
        if (-not $ok) {
            Add-Err $file $path ("expected {0}, got {1}" -f ($expected -join '|'), $actual)
            return
        }
    }

    # --- enum
    if ($schema.enum) {
        if (@($schema.enum) -notcontains $value) {
            Add-Err $file $path ("value '{0}' is not one of: {1}" -f $value, (@($schema.enum) -join ', '))
        }
    }

    # --- numeric ranges. This is the clause that protects the design.
    if ($value -is [int] -or $value -is [long] -or $value -is [double] -or $value -is [decimal]) {
        if ($null -ne $schema.minimum -and $value -lt $schema.minimum) {
            $why = if ($schema.description) { " - " + $schema.description } else { "" }
            Add-Err $file $path ("{0} is below minimum {1}{2}" -f $value, $schema.minimum, $why)
        }
        if ($null -ne $schema.maximum -and $value -gt $schema.maximum) {
            $why = if ($schema.description) { " - " + $schema.description } else { "" }
            Add-Err $file $path ("{0} is above maximum {1}{2}" -f $value, $schema.maximum, $why)
        }
        if ($null -ne $schema.exclusiveMinimum -and $value -le $schema.exclusiveMinimum) {
            Add-Err $file $path ("{0} must be greater than {1}" -f $value, $schema.exclusiveMinimum)
        }
    }

    # --- strings
    if ($value -is [string]) {
        if ($null -ne $schema.minLength -and $value.Length -lt $schema.minLength) {
            Add-Err $file $path ("string shorter than {0}" -f $schema.minLength)
        }
        if ($schema.pattern -and ($value -cnotmatch $schema.pattern)) {
            Add-Err $file $path ("does not match pattern {0}" -f $schema.pattern)
        }
    }

    # --- objects
    if ((Get-JsonType $value) -eq 'object' -and $schema.properties) {
        $props = $schema.properties
        $present = @($value.PSObject.Properties.Name)

        foreach ($r in @($schema.required)) {
            if ($r -and $present -notcontains $r) {
                Add-Err $file $path ("missing required key '{0}'" -f $r)
            }
        }
        foreach ($p in $value.PSObject.Properties) {
            if ($p.Name.StartsWith('_') -or $p.Name -eq '$schema' -or $p.Name -eq 'version') { continue }
            $sub = $props.PSObject.Properties[$p.Name]
            if (-not $sub) {
                if ($schema.additionalProperties -eq $false) {
                    Add-Err $file $path ("unknown key '{0}' - add it to the schema or remove it" -f $p.Name)
                } else {
                    Add-Warn $file $path ("key '{0}' is not in the schema - it is invisible to /tune" -f $p.Name)
                }
                continue
            }
            Test-Node $p.Value $sub.Value ("$path." + $p.Name) $file
        }
    }

    # --- arrays
    if ((Get-JsonType $value) -eq 'array' -and $schema.items) {
        $i = 0
        foreach ($item in @($value)) {
            Test-Node $item $schema.items ("$path[$i]") $file
            $i++
        }
        if ($null -ne $schema.minItems -and @($value).Count -lt $schema.minItems) {
            Add-Err $file $path ("needs at least {0} items" -f $schema.minItems)
        }
    }
}

# ---------------------------------------------------------------- collect targets
$targets = @()
if ($File) {
    if (Test-Path $File) { $targets = @((Get-Item -LiteralPath $File)) }
    else { Write-Output "ERROR: file not found: $File"; exit 1 }
} else {
    if (-not (Test-Path $configDir)) {
        Write-Output "CONFIG VALIDATE: no config/ directory. Nothing to check."
        Write-Output "  Run /data-schema <domain> to create the tuning layer."
        exit 0
    }
    $targets = Get-ChildItem -Path (Join-Path $configDir "balance"),(Join-Path $configDir "content") `
                             -Filter *.json -File -Recurse
    if ($Domain) { $targets = $targets | Where-Object { $_.BaseName -eq $Domain } }
}

if (-not $targets -or @($targets).Count -eq 0) {
    Write-Output "CONFIG VALIDATE: no config files matched."
    exit 0
}

# ---------------------------------------------------------------- validate
$noSchema = @()
foreach ($t in $targets) {
    $relName = $t.FullName.Substring($root.Length).TrimStart('\','/')
    $json = $null
    try { $json = Get-Content -LiteralPath $t.FullName -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch {
        Add-Err $relName '$' ("invalid JSON - " + $_.Exception.Message)
        continue
    }

    $schemaPath = $null
    if ($json.'$schema') {
        $cand = Join-Path (Split-Path $t.FullName -Parent) $json.'$schema'
        if (Test-Path $cand) { $schemaPath = (Resolve-Path $cand).Path }
    }
    if (-not $schemaPath) {
        $cand = Join-Path $schemaDir ($t.BaseName + ".schema.json")
        if (Test-Path $cand) { $schemaPath = $cand }
    }
    if (-not $schemaPath) { $noSchema += $relName; continue }

    $schema = $null
    try { $schema = Get-Content -LiteralPath $schemaPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { Add-Err $relName '$' "schema is not valid JSON: $schemaPath"; continue }

    if ($null -eq $json.version) {
        Add-Warn $relName '$' "no 'version' key - bump it when the shape changes (config-protocol.md section 7)"
    }
    Test-Node $json $schema '$' $relName
}

# ---------------------------------------------------------------- report
if (-not $Quiet -or $script:errors.Count -gt 0) {
    Write-Output ("CONFIG VALIDATE  {0} file(s), {1} node(s) checked" -f @($targets).Count, $script:checked)
}
foreach ($e in $script:errors)   { Write-Output ("  " + $e) }
if (-not $Quiet) {
    foreach ($w in $script:warnings) { Write-Output ("  " + $w) }
    if ($noSchema.Count -gt 0) {
        Write-Output ("  [WARN]  no schema found for: " + ($noSchema -join ', '))
        Write-Output "          A file without a schema has no ranges, so /tune is guessing."
    }
}

if ($script:errors.Count -gt 0) {
    Write-Output ("VERDICT: FAIL - {0} error(s), {1} warning(s)" -f $script:errors.Count, $script:warnings.Count)
    exit 1
}
if (-not $Quiet) {
    Write-Output ("VERDICT: PASS ({0} warning(s))" -f $script:warnings.Count)
}
exit 0
