# Unity Studio - session start
# One line of state. Must stay cheap: reads .state/project.json and nothing else.
# ASCII-only, Windows PowerShell 5.1.

$ErrorActionPreference = 'SilentlyContinue'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$statePath = ".state/project.json"

if (-not (Test-Path $statePath)) {
    if (Test-Path "ProjectSettings/ProjectVersion.txt") {
        Write-Output "Unity Studio ready. Existing Unity project detected: /onboard"
    } else {
        Write-Output "Unity Studio ready. New game: /kickoff <your idea>   Existing project: /onboard"
    }
    exit 0
}

try {
    $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
} catch {
    Write-Output "Unity Studio: .state/project.json is unreadable. Run /status to rebuild it."
    exit 0
}

$parts = @()
if ($state.project)   { $parts += [string]$state.project }
if ($state.phase)     { $parts += "phase=" + $state.phase }
if ($state.milestone) { $parts += "milestone=" + $state.milestone }
if ($state.reviewMode){ $parts += "mode=" + $state.reviewMode }

$line = "Unity Studio: " + ($parts -join " | ")

if ($state.counters) {
    $line += " | stories " + $state.counters.done + "/" + $state.counters.stories
}
if ($state.openGateConditions -and $state.openGateConditions -gt 0) {
    $line += " | OPEN GATE ITEMS: " + $state.openGateConditions
}

Write-Output $line

# A missing style lock is worth surfacing every session: it is the difference between
# a coherent set of assets and forty images that do not belong to the same game.
if (-not (Test-Path ".claude/comfy/styles/project.json")) {
    Write-Output "  No art style locked yet - run /art-direction before generating assets."
}

Write-Output "Continue with: /status"
exit 0
