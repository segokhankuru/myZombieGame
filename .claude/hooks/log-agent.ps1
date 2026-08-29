# Unity Studio - subagent run log
# Accumulates agent invocations so /status and /retro can report token behaviour.
# Append-only, one JSON line per call. ASCII-only, Windows PowerShell 5.1.

$ErrorActionPreference = 'SilentlyContinue'
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture

$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }

$logDir  = ".state"
$logPath = "$logDir/agent-log.jsonl"
if (-not (Test-Path $logDir)) { exit 0 }

$milestone = $null
$phase = $null
if (Test-Path "$logDir/project.json") {
    try {
        $state = Get-Content "$logDir/project.json" -Raw -Encoding UTF8 | ConvertFrom-Json
        $milestone = $state.milestone
        $phase = $state.phase
    } catch { }
}

$entry = [ordered]@{
    ts        = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')
    phase     = $phase
    milestone = $milestone
}

try {
    $payload = $raw | ConvertFrom-Json
    if ($payload.agent_type)    { $entry.agent = $payload.agent_type }
    if ($payload.subagent_type) { $entry.agent = $payload.subagent_type }
} catch { }

($entry | ConvertTo-Json -Compress) | Add-Content -Path $logPath -Encoding utf8
exit 0
