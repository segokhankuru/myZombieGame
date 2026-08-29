# Unity Studio - PostToolUse validation
# Emits warnings. Does NOT block (exit 0). Purpose: make silent rule violations visible
# at the moment they happen, not three sprints later.
# ASCII-only, Windows PowerShell 5.1.

$ErrorActionPreference = 'SilentlyContinue'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$filePath = [string]$payload.tool_input.file_path
if (-not $filePath) { exit 0 }
if (-not (Test-Path -LiteralPath $filePath)) { exit 0 }

$warnings = @()
$name = Split-Path $filePath -Leaf
$ext  = [IO.Path]::GetExtension($filePath).ToLower()
$norm = $filePath -replace '\\', '/'

function Get-Text($p) { return (Get-Content -LiteralPath $p -Raw -Encoding UTF8 -ErrorAction SilentlyContinue) }

# ---------------------------------------------------------------- 1. secrets
if ($name -cmatch '^\.env' -or $norm -cmatch '/secrets/' -or $ext -in @('.pem','.key','.p12','.pfx') -or
    $name -cmatch '(?i)steam.*(password|token|sentry)') {
    $warnings += "SECRET FILE: $name - must not be committed and its contents must not be printed."
}

# ---------------------------------------------------------------- 2. C# rules
if ($ext -eq '.cs') {
    $c = Get-Text $filePath

    if ($c -cmatch '(?i)(api[_-]?key|secret|password|token)\s*=\s*"[^"]{12,}"') {
        $warnings += "POSSIBLE HARDCODED SECRET: $norm"
    }

    # The config rule. A [SerializeField] numeric with an initializer in gameplay code is
    # almost always a balance number that escaped the tuning layer.
    if ($norm -cmatch '/Code/(Gameplay|AI|Net|Systems)/' -and
        $c -cmatch '\[SerializeField\][^;]*\b(float|int)\s+\w+\s*=\s*-?\d') {
        $warnings += "TUNABLE IN CODE: $norm has a [SerializeField] number with a default. Balance numbers belong in config/ (config-protocol.md). If it is a reference or a constant, say so in a comment."
    }

    if ($norm -cmatch '/Code/(Gameplay|AI|Net)/') {
        if ($c -cmatch '\bvoid\s+Update\s*\(\s*\)' -and $c -cmatch 'GetComponent<') {
            if ($c -cmatch '(?s)void\s+Update\s*\(\s*\)\s*\{[^}]{0,800}GetComponent<') {
                $warnings += "PER-FRAME GetComponent: $norm calls GetComponent inside Update. Cache it in Awake (unity-conventions.md section 3)."
            }
        }
        if ($c -cmatch '(?s)void\s+Update\s*\(\s*\)\s*\{[^}]{0,800}(\.Where\(|\.Select\(|\.OrderBy\(|\.ToList\(|\.ToArray\()') {
            $warnings += "PER-FRAME ALLOCATION: $norm uses LINQ inside Update. Zero allocation per frame is the rule."
        }
        if ($c -cmatch 'GameObject\.Find\(|SendMessage\(|Resources\.Load\(') {
            $warnings += "BANNED API: $norm uses GameObject.Find / SendMessage / Resources.Load."
        }
    }

    if ($c -cmatch 'catch\s*\([^)]*\)\s*\{\s*\}') {
        $warnings += "SWALLOWED EXCEPTION: $norm has an empty catch block."
    }
    if ($c -cmatch '(?m)^\s*//\s*TODO(?!\s*\()') {
        $warnings += "UNOWNED TODO: $norm - write TODO(owner, story-NNN) or file it with /bug."
    }
    if ($c -cmatch 'Random\.(value|Range)' -and $norm -cmatch '/Code/(Gameplay|AI|Net)/') {
        $warnings += "UNSEEDED RANDOM: $norm - anything a player can replay, share or save needs a seeded stream (unity-conventions.md section 4)."
    }
}

# ---------------------------------------------------------------- 3. config
if ($norm -cmatch '/config/(balance|content)/.*\.json$') {
    $root = (Get-Location).Path
    $validator = Join-Path $root ".claude\tools\config-validate.ps1"
    if (Test-Path $validator) {
        $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $validator -File $filePath -Quiet
        if ($LASTEXITCODE -eq 1) {
            foreach ($line in @($out)) {
                if ($line -cmatch '^\s*\[ERROR\]') { $warnings += ("CONFIG " + $line.Trim()) }
            }
        }
    }
}

# ---------------------------------------------------------------- 4. docs hygiene
if ($norm -cmatch 'docs/CONTEXT\.md$') {
    $n = (Get-Content -LiteralPath $filePath -Encoding UTF8 | Measure-Object -Line).Lines
    if ($n -gt 200) { $warnings += "CONTEXT.md IS BLOATED: $n lines (limit 200). Run /context-compact." }
}
if ($norm -cmatch 'docs/DECISIONS\.md$') {
    $n = (Get-Content -LiteralPath $filePath -Encoding UTF8 | Measure-Object -Line).Lines
    if ($n -gt 300) { $warnings += "DECISIONS.md IS BLOATED: $n lines (limit 300). Run /context-compact." }
}

# ---------------------------------------------------------------- 5. task packets
if ($norm -cmatch '/epics/.*/story-\d+.*\.md$') {
    $c = Get-Text $filePath
    $missing = @()
    if ($c -cnotmatch '(?m)^##\s*Acceptance criteria')   { $missing += 'Acceptance criteria' }
    if ($c -cnotmatch '(?m)^##\s*Design intent')         { $missing += 'Design intent' }
    if ($c -cnotmatch '(?m)^##\s*Out of scope')          { $missing += 'Out of scope' }
    if ($c -cnotmatch '(?m)^##\s*Required evidence')     { $missing += 'Required evidence' }
    if ($c -cnotmatch '(?m)^##\s*Test scenarios')        { $missing += 'Test scenarios' }
    if ($c -cnotmatch '(?m)^##\s*Files to touch')        { $missing += 'Files to touch' }
    if ($missing.Count -gt 0) {
        $warnings += ("STORY MISSING SECTIONS: {0} - {1}. An incomplete task packet is the main cause of expensive /dev-task rounds." -f $name, ($missing -join ', '))
    }
    if ($c -cmatch '(?m)^\*\*Type:\*\*\s*Feel' -and $c -cnotmatch 'FEELS LIKE:') {
        $warnings += "FEEL STORY WITHOUT A FEEL LINE: $name - a Feel story needs 'FEELS LIKE: <one sentence>' or /feel-check has nothing to judge."
    }
}

# ---------------------------------------------------------------- 6. art naming
if ($ext -in @('.png','.jpg','.tga','.wav','.ogg','.fbx','.mat')) {
    $base = [IO.Path]::GetFileNameWithoutExtension($name)
    if ($base -cmatch '[^A-Za-z0-9_. -]') {
        $warnings += "ASSET NAME: $name has non-ASCII characters. Use <category>_<subject>_<variant>."
    }
    if ($norm -cmatch '/Art/Generated/' -and $norm -cnotmatch '_[a-z]\.') {
        # informational only
    }
}

if ($warnings.Count -gt 0) {
    Write-Output "Unity Studio check:"
    foreach ($w in $warnings) { Write-Output ("  ! " + $w) }
}
exit 0
