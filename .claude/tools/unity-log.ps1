<#
.SYNOPSIS
  Extracts compile errors, warnings and the last build result from the Unity Editor log.

.DESCRIPTION
  Editor.log is routinely 20-200 MB. Never read it. This streams the tail and reports
  only what an agent can act on. Exit code 1 means there are compile errors.

.EXAMPLE
  .\.claude\tools\unity-log.ps1 -Errors
  .\.claude\tools\unity-log.ps1 -Tail 40
  .\.claude\tools\unity-log.ps1 -Path .\Logs\build.log -Errors
#>
[CmdletBinding()]
param(
    [string]$Path,
    [switch]$Errors,
    [switch]$Warnings,
    [int]$Tail = 0,
    [int]$ScanBytes = 4MB
)

$ErrorActionPreference = 'SilentlyContinue'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

if (-not $Path) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Unity\Editor\Editor.log"),
        (Join-Path $env:LOCALAPPDATA "Unity\Editor\Editor-prev.log"),
        ".\Logs\build.log"
    )
    $Path = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $Path -or -not (Test-Path $Path)) {
    Write-Output "UNITY LOG: not found."
    Write-Output "  Looked for: %LOCALAPPDATA%\Unity\Editor\Editor.log"
    Write-Output "  If the Editor has never run on this machine, compilation cannot be verified here."
    Write-Output "  Do NOT claim the code compiles. Say so in the story instead."
    exit 2
}

$fi = Get-Item -LiteralPath $Path
$age = [int]((Get-Date) - $fi.LastWriteTime).TotalMinutes

# Read only the tail of the file.
$start = [Math]::Max(0, $fi.Length - $ScanBytes)
$lines = New-Object System.Collections.Generic.List[string]
$fs = [System.IO.File]::Open($fi.FullName, 'Open', 'Read', 'ReadWrite')
try {
    $null = $fs.Seek($start, 'Begin')
    $sr = New-Object System.IO.StreamReader($fs)
    if ($start -gt 0) { $null = $sr.ReadLine() }   # discard the partial first line
    while (($l = $sr.ReadLine()) -ne $null) { $lines.Add($l) }
} finally { $fs.Close() }

Write-Output ("UNITY LOG {0}" -f $fi.FullName)
Write-Output ("  {0} MB total, scanned last {1} MB, last written {2} min ago" -f `
    [math]::Round($fi.Length/1MB,1), [math]::Round($ScanBytes/1MB,1), $age)
if ($age -gt 30) {
    Write-Output "  WARNING: this log is stale. It may not reflect the current source."
}

# ---- compile errors: "Assets/Foo.cs(12,5): error CS1002: ; expected"
$errPattern  = 'error\s+(CS\d+|BCE\d+|Shader error)'
$warnPattern = 'warning\s+CS\d+'

$errs = @()
$seen = @{}
foreach ($l in $lines) {
    if ($l -cmatch $errPattern) {
        $t = $l.Trim()
        if (-not $seen.ContainsKey($t)) { $seen[$t] = $true; $errs += $t }
    }
}

$warns = @()
$seenW = @{}
foreach ($l in $lines) {
    if ($l -cmatch $warnPattern) {
        $t = $l.Trim()
        if (-not $seenW.ContainsKey($t)) { $seenW[$t] = $true; $warns += $t }
    }
}

# ---- last compile / reload markers, to tell fresh errors from historical ones
$lastReload = $null
for ($i = $lines.Count - 1; $i -ge 0; $i--) {
    if ($lines[$i] -cmatch 'Reloading assemblies|Finished compiling graph|- Starting compile|Refresh completed') {
        $lastReload = $i; break
    }
}

Write-Output ""
if ($errs.Count -eq 0) {
    Write-Output "COMPILE ERRORS: none in the scanned window"
} else {
    Write-Output ("COMPILE ERRORS: {0}" -f $errs.Count)
    foreach ($e in ($errs | Select-Object -First 20)) { Write-Output ("  " + $e) }
    if ($errs.Count -gt 20) { Write-Output ("  ... and " + ($errs.Count - 20) + " more") }
}

if ($Warnings) {
    Write-Output ""
    Write-Output ("WARNINGS: {0}" -f $warns.Count)
    foreach ($w in ($warns | Select-Object -First 15)) { Write-Output ("  " + $w) }
} elseif ($warns.Count -gt 0) {
    Write-Output ("WARNINGS: {0} (pass -Warnings to list)" -f $warns.Count)
}

# ---- exceptions at runtime
$exc = @()
$seenX = @{}
foreach ($l in $lines) {
    if ($l -cmatch '(NullReferenceException|IndexOutOfRangeException|MissingReferenceException|InvalidOperationException|ArgumentException|KeyNotFoundException|StackOverflowException)') {
        $t = $l.Trim()
        if ($t.Length -gt 160) { $t = $t.Substring(0,160) + "..." }
        if (-not $seenX.ContainsKey($t)) { $seenX[$t] = $true; $exc += $t }
    }
}
if ($exc.Count -gt 0) {
    Write-Output ""
    Write-Output ("RUNTIME EXCEPTIONS: {0} distinct" -f $exc.Count)
    foreach ($x in ($exc | Select-Object -First 8)) { Write-Output ("  " + $x) }
}

# ---- build result
$build = $lines | Where-Object { $_ -cmatch 'Build completed with a result of|Total build time|Build Report' } |
         Select-Object -Last 3
if ($build) {
    Write-Output ""
    Write-Output "BUILD"
    foreach ($b in $build) { Write-Output ("  " + $b.Trim()) }
}

if ($Tail -gt 0) {
    Write-Output ""
    Write-Output "TAIL ($Tail lines)"
    $lines | Select-Object -Last $Tail | ForEach-Object { Write-Output ("  " + $_) }
}

Write-Output ""
if ($errs.Count -gt 0) {
    Write-Output "VERDICT: DOES NOT COMPILE"
    exit 1
} else {
    Write-Output "VERDICT: no compile errors in the scanned window"
    exit 0
}
