<#
.SYNOPSIS
  Headless Unity build. Finds the right Editor version, invokes it, summarizes the result.

.DESCRIPTION
  Reads ProjectSettings/ProjectVersion.txt, locates that exact Editor under Unity Hub,
  and runs it in batch mode against Game.Editor.BuildPipeline.BuildFromArgs (shipped by
  the /build skill). Prints a short verdict rather than the log.

  Nothing here uploads anything. Depot upload is a separate, user-approved step.

.EXAMPLE
  .\.claude\tools\build.ps1 -Target StandaloneWindows64 -Config Development
  .\.claude\tools\build.ps1 -Target StandaloneWindows64 -Config Release -Out Build\win64
  .\.claude\tools\build.ps1 -WhatIf          # show what would run, do not build
#>
[CmdletBinding()]
param(
    [ValidateSet('StandaloneWindows64','StandaloneLinux64','StandaloneOSX','Android','WebGL')]
    [string]$Target = 'StandaloneWindows64',
    [ValidateSet('Development','Release')]
    [string]$Config = 'Development',
    [string]$Out,
    [string]$Method = 'Game.Editor.BuildPipelineEntry.BuildFromArgs',
    [string]$ProjectRoot = ".",
    [int]$TimeoutMinutes = 60,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root = (Resolve-Path $ProjectRoot).Path
$verFile = Join-Path $root "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $verFile)) {
    Write-Output "ERROR: not a Unity project (no ProjectSettings/ProjectVersion.txt): $root"
    exit 1
}
$version = (Get-Content -LiteralPath $verFile | Where-Object { $_ -cmatch '^m_EditorVersion:\s*(.+)$' } |
            ForEach-Object { $Matches[1].Trim() } | Select-Object -First 1)

# ------------------------------------------------------------- locate the editor
$searchRoots = @(
    "C:\Program Files\Unity\Hub\Editor",
    "C:\Program Files (x86)\Unity\Hub\Editor",
    (Join-Path $env:LOCALAPPDATA "Unity\Hub\Editor"),
    "D:\Unity\Hub\Editor"
) | Where-Object { Test-Path $_ }

$editor = $null
foreach ($s in $searchRoots) {
    $c = Join-Path $s "$version\Editor\Unity.exe"
    if (Test-Path $c) { $editor = $c; break }
}
if (-not $editor) {
    Write-Output "UNITY BUILD: editor $version not found."
    Write-Output "  Searched:"
    foreach ($s in $searchRoots) { Write-Output ("    " + $s) }
    $installed = @()
    foreach ($s in $searchRoots) { $installed += (Get-ChildItem -LiteralPath $s -Directory | Select-Object -ExpandProperty Name) }
    if ($installed) { Write-Output ("  Installed: " + ($installed -join ', ')) }
    Write-Output "  Install $version via Unity Hub, or open the project once to migrate it."
    exit 2
}

if (-not $Out) { $Out = Join-Path $root ("Build\" + $Target.Replace('Standalone','').ToLower() + "-" + $Config.ToLower()) }
$logDir = Join-Path $root "Logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$log = Join-Path $logDir "build.log"

$unityArgs = @(
    '-batchmode', '-quit', '-nographics',
    '-projectPath', $root,
    '-logFile', $log,
    '-executeMethod', $Method,
    '--target', $Target,
    '--config', $Config,
    '--out', $Out
)

Write-Output "UNITY BUILD"
Write-Output ("  editor  {0}" -f $editor)
Write-Output ("  project {0}" -f $root)
Write-Output ("  target  {0} / {1}" -f $Target, $Config)
Write-Output ("  out     {0}" -f $Out)
Write-Output ("  log     {0}" -f $log)

if ($WhatIf) {
    Write-Output ""
    Write-Output "WHATIF - would run:"
    Write-Output ("  `"{0}`" {1}" -f $editor, ($unityArgs -join ' '))
    exit 0
}

if (Test-Path $log) { Remove-Item -LiteralPath $log -Force }
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $editor -ArgumentList $unityArgs -PassThru -NoNewWindow
if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    try { $p.Kill() } catch {}
    Write-Output ""
    Write-Output "VERDICT: TIMEOUT after $TimeoutMinutes minutes. Editor killed."
    exit 3
}
$sw.Stop()
$code = $p.ExitCode

Write-Output ""
Write-Output ("  finished in {0:N1} min, exit code {1}" -f $sw.Elapsed.TotalMinutes, $code)

# ---------------------------------------------------------------- summarize
if (Test-Path $log) {
    $tail = Get-Content -LiteralPath $log -Tail 3000
    $errs = @($tail | Where-Object { $_ -cmatch 'error\s+CS\d+|BuildFailedException|Build failed|Error building' } |
             Select-Object -Unique -First 12)
    if ($errs.Count -gt 0) {
        Write-Output ""
        Write-Output "ERRORS"
        foreach ($e in $errs) { Write-Output ("  " + $e.Trim()) }
    }
    $sizeLine = $tail | Where-Object { $_ -cmatch 'Total build size|Build completed with a result of' } | Select-Object -Last 2
    foreach ($s in $sizeLine) { Write-Output ("  " + $s.Trim()) }
}

if ($code -ne 0) {
    Write-Output ""
    Write-Output "VERDICT: BUILD FAILED"
    Write-Output "  Details: .claude\tools\unity-log.ps1 -Path Logs\build.log -Errors"
    exit 1
}

if (Test-Path $Out) {
    $files = Get-ChildItem -LiteralPath $Out -Recurse -File
    $mb = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1MB, 1)
    Write-Output ("  output  {0} files, {1} MB" -f $files.Count, $mb)
    $exe = $files | Where-Object { $_.Extension -eq '.exe' } | Select-Object -First 1
    if ($exe) { Write-Output ("  exe     {0}" -f $exe.Name) }
}

Write-Output ""
Write-Output "VERDICT: BUILD OK"
Write-Output "  Size breakdown: .claude\tools\profiler-summary.ps1 -BuildSize"
exit 0
