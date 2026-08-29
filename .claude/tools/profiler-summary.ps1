<#
.SYNOPSIS
  Turns a performance capture into a budget verdict, without putting raw data in context.

.DESCRIPTION
  Two inputs, both cheap:

  1. A frame-timing capture written by PerfCapture.cs (see the /perf-check skill), at
     docs/qa/performance/<name>.json. Percentiles matter, averages lie: a game that
     averages 60 fps and stutters every two seconds is a bad game with good averages.

  2. The build size breakdown from the Unity build log.

  Budgets come from docs/architecture/PERF-BUDGET.md, parsed from a fenced budget block.
  Exit code 1 means a budget is broken.

.EXAMPLE
  .\.claude\tools\profiler-summary.ps1
  .\.claude\tools\profiler-summary.ps1 -Capture docs/qa/performance/2026-08-29-town.json
  .\.claude\tools\profiler-summary.ps1 -BuildSize
#>
[CmdletBinding()]
param(
    [string]$Capture,
    [switch]$BuildSize,
    [string]$LogPath,
    [string]$ProjectRoot = "."
)

$ErrorActionPreference = 'SilentlyContinue'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root = (Resolve-Path $ProjectRoot).Path
$fail = $false

# ------------------------------------------------------------------ budgets
# PERF-BUDGET.md carries a machine-readable block:
#   ```budget
#   targetFps=60
#   frameMs=16.6
#   frameMsP99=22.0
#   gcAllocPerFrameKb=0
#   drawCalls=1200
#   loadSeconds=8
#   buildMb=4000
#   ```
$budget = @{
    targetFps = 60.0; frameMs = 16.6; frameMsP99 = 22.0
    gcAllocPerFrameKb = 0.0; drawCalls = 1200.0; loadSeconds = 10.0; buildMb = 8000.0
}
$budgetFile = Join-Path $root "docs\architecture\PERF-BUDGET.md"
$budgetSource = "defaults (no PERF-BUDGET.md yet)"
if (Test-Path $budgetFile) {
    $inBlock = $false
    foreach ($l in (Get-Content -LiteralPath $budgetFile)) {
        if ($l -cmatch '^\s*```budget')  { $inBlock = $true;  continue }
        if ($inBlock -and $l -cmatch '^\s*```') { $inBlock = $false; continue }
        if ($inBlock -and $l -cmatch '^\s*(\w+)\s*=\s*([0-9.]+)') {
            $budget[$Matches[1]] = [double]$Matches[2]
        }
    }
    $budgetSource = "docs/architecture/PERF-BUDGET.md"
}

function Show-Check($label, $actual, $cap, $unit, [switch]$LowerIsBetter) {
    $ok = if ($LowerIsBetter) { $actual -le $cap } else { $actual -ge $cap }
    $mark = if ($ok) { "OK  " } else { "OVER"; }
    if (-not $ok) { $script:fail = $true }
    Write-Output ("  {0} {1,-24} {2,10} / {3} {4}" -f $mark, $label, [math]::Round($actual,2), $cap, $unit)
}

# ------------------------------------------------------------- build size mode
if ($BuildSize) {
    if (-not $LogPath) {
        $LogPath = @((Join-Path $root "Logs\build.log"),
                     (Join-Path $env:LOCALAPPDATA "Unity\Editor\Editor.log")) |
                   Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $LogPath -or -not (Test-Path $LogPath)) {
        Write-Output "BUILD SIZE: no build log found. Run /build first."
        exit 2
    }
    Write-Output "BUILD SIZE  from $LogPath"
    $lines = Get-Content -LiteralPath $LogPath -Tail 4000
    $inReport = $false
    $rows = @()
    foreach ($l in $lines) {
        if ($l -cmatch 'Build Report') { $inReport = $true; $rows = @(); continue }
        if ($inReport) {
            if ($l -cmatch '^\s*(\d+\.?\d*)\s*(kb|mb|gb)\s+(\d+\.?\d*)%\s+(.+)$') {
                $rows += [pscustomobject]@{ Size=$Matches[1]; Unit=$Matches[2]; Pct=[double]$Matches[3]; What=$Matches[4].Trim() }
            }
            if ($l -cmatch 'Used Assets|Total User Assets') { $inReport = $false }
        }
    }
    if ($rows.Count -eq 0) {
        Write-Output "  no size breakdown in the log (build with a full report enabled)"
    } else {
        $rows | Sort-Object Pct -Descending | Select-Object -First 12 | ForEach-Object {
            Write-Output ("  {0,7} {1,-4} {2,5}%  {3}" -f $_.Size, $_.Unit, $_.Pct, $_.What)
        }
    }
    exit 0
}

# ------------------------------------------------------------ frame capture mode
if (-not $Capture) {
    $dir = Join-Path $root "docs\qa\performance"
    if (Test-Path $dir) {
        $latest = Get-ChildItem -LiteralPath $dir -Filter *.json -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latest) { $Capture = $latest.FullName }
    }
}
if (-not $Capture -or -not (Test-Path $Capture)) {
    Write-Output "PERF: no capture found in docs/qa/performance/."
    Write-Output "  Add PerfCapture.cs (see the /perf-check skill), play the target scene,"
    Write-Output "  then re-run. Do NOT estimate frame cost by reading code - it is always wrong."
    Write-Output ""
    Write-Output ("BUDGET (from {0})" -f $budgetSource)
    foreach ($k in ($budget.Keys | Sort-Object)) { Write-Output ("  {0,-22} {1}" -f $k, $budget[$k]) }
    exit 2
}

$cap = Get-Content -LiteralPath $Capture -Raw -Encoding UTF8 | ConvertFrom-Json
$frames = @($cap.frameMs)
if ($frames.Count -lt 10) { Write-Output "PERF: capture has too few frames ($($frames.Count))."; exit 2 }

$sorted = $frames | Sort-Object
function Pct([double]$p) { $i = [int][math]::Floor(($sorted.Count - 1) * $p); return [double]$sorted[$i] }

$mean = ($frames | Measure-Object -Average).Average
$p50  = Pct 0.50
$p95  = Pct 0.95
$p99  = Pct 0.99
$worst= [double]($sorted[-1])
$overBudgetFrames = @($frames | Where-Object { $_ -gt $budget.frameMs }).Count
$hitchCount = @($frames | Where-Object { $_ -gt ($budget.frameMs * 2) }).Count

Write-Output ("PERF  {0}" -f (Split-Path $Capture -Leaf))
if ($cap.scene)    { Write-Output ("  scene: {0}" -f $cap.scene) }
if ($cap.hardware) { Write-Output ("  hardware: {0}" -f $cap.hardware) }
Write-Output ("  {0} frames, {1:N1}s captured" -f $frames.Count, (($frames | Measure-Object -Sum).Sum / 1000))
Write-Output ("  budget source: {0}" -f $budgetSource)
Write-Output ""
Write-Output "FRAME TIME"
Write-Output ("       mean {0,6:N2} ms   p50 {1,6:N2}   p95 {2,6:N2}   p99 {3,6:N2}   worst {4,6:N2}" -f $mean,$p50,$p95,$p99,$worst)
Show-Check "p50 frame time"  $p50 $budget.frameMs    "ms" -LowerIsBetter
Show-Check "p99 frame time"  $p99 $budget.frameMsP99 "ms" -LowerIsBetter
Write-Output ("       {0} of {1} frames over budget ({2:N1}%), {3} hitches over 2x budget" -f `
    $overBudgetFrames, $frames.Count, (100.0 * $overBudgetFrames / $frames.Count), $hitchCount)
if ($hitchCount -gt 0) {
    Write-Output "       Hitches are what players report as 'laggy'. Fix these before the mean."
}

if ($null -ne $cap.gcAllocPerFrameKb) {
    Write-Output ""
    Write-Output "ALLOCATION"
    Show-Check "GC alloc per frame" ([double]$cap.gcAllocPerFrameKb) $budget.gcAllocPerFrameKb "KB" -LowerIsBetter
    if ([double]$cap.gcAllocPerFrameKb -gt 0) {
        Write-Output "       Any steady per-frame allocation eventually becomes a hitch. Target zero."
    }
}
if ($null -ne $cap.drawCalls) {
    Write-Output ""
    Write-Output "RENDERING"
    Show-Check "draw calls" ([double]$cap.drawCalls) $budget.drawCalls "calls" -LowerIsBetter
    if ($null -ne $cap.triangles) { Write-Output ("       triangles {0:N0}" -f [double]$cap.triangles) }
    if ($null -ne $cap.setPassCalls) { Write-Output ("       setPass   {0:N0}" -f [double]$cap.setPassCalls) }
}
if ($null -ne $cap.loadSeconds) {
    Write-Output ""
    Write-Output "LOAD"
    Show-Check "scene load" ([double]$cap.loadSeconds) $budget.loadSeconds "s" -LowerIsBetter
}

Write-Output ""
if ($fail) { Write-Output "VERDICT: OVER BUDGET"; exit 1 }
Write-Output "VERDICT: WITHIN BUDGET"
exit 0
