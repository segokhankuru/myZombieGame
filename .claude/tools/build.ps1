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

# --- Acik sahne durumunu koru -------------------------------------------------
# Batch modda calisan Unity, cikarken Library/LastSceneManagerSetup.txt dosyasini
# BOSALTIR (sceneSetups: []). Sonucu: gelistirici Unity'yi acinca hiyerarsiyi bos
# bulur ve sahnesini kaybettigini saniyor - hicbir sey kaybolmus degil, Unity
# hangi sahneyi acacagini bilmiyor.
#
# 2026-09-04: bu dosya bir oturumda onlarca kez sifirlandi ve gelistirici
# "hierarchy yine sifirlanmis" dedi. Arac, gelistiricinin acik sahnesine
# DOKUNMAMALI.
$sceneSetupPath = Join-Path $root "Library\LastSceneManagerSetup.txt"
$sceneSetupBackup = $null
if (Test-Path $sceneSetupPath) {
    $sceneSetupBackup = Get-Content -LiteralPath $sceneSetupPath -Raw
}
$p = Start-Process -FilePath $editor -ArgumentList $unityArgs -PassThru -NoNewWindow
if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    try { $p.Kill() } catch {}
    Write-Output ""
    Write-Output "VERDICT: TIMEOUT after $TimeoutMinutes minutes. Editor killed."
    exit 3
}

# The timeout overload of WaitForExit() returns as soon as the process signals, but
# does NOT populate ExitCode on the cached process object. Reading it here gave an
# EMPTY value, and an empty $code compared "-ne 0" as true - so a build that had just
# succeeded was reported as BUILD FAILED. Calling the no-arg overload afterwards
# flushes the exit state. A tool that lies about success costs more than one that
# fails loudly.
$p.WaitForExit()
$sw.Stop()

# Acik sahne durumunu HEMEN geri yaz. Buraya konmasinin sebebi: ilk denemede bu
# blok dosyanin sonuna, "exit 0"in ALTINA eklenmisti ve hic calismiyordu - build
# yine "OK" diyor, dosya yine bosaliyordu. Unity cikar cikmaz, herhangi bir
# cikis yolundan once calismali.
if ($null -ne $sceneSetupBackup -and $sceneSetupBackup -notmatch 'sceneSetups:\s*\[\]') {
    try { Set-Content -LiteralPath $sceneSetupPath -Value $sceneSetupBackup -NoNewline -Encoding UTF8 } catch { }
}
else {
    # KENDINI ONARMA. Yedek bossa geri yazacak bir sey yok demektir - ve onceki
    # surum tam burada duruyordu, yani dosya bir kez bosaldiginda KALICI olarak
    # bos kaliyordu. Korumak yetmez, onarmak gerek.
    $fallback = "sceneSetups:`n- path: Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity`n  isLoaded: 1`n  isActive: 1`n  isSubScene: 0`n"
    try { Set-Content -LiteralPath $sceneSetupPath -Value $fallback -NoNewline -Encoding UTF8 } catch { }
}

$code = $p.ExitCode
if ($null -eq $code) { $code = 1 }

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

# ---------------------------------------------------------------- verdict
# The verdict comes from EVIDENCE, not from the exit code.
#
# Why: on this machine Unity exits non-zero after a build that demonstrably
# succeeded - the log says "[Build] TAMAM", the .exe is on disk, and there is not a
# single compile error. The install logs a licensing warning on every run and
# unity-exec.ps1 has always shown a blank exit code for runs that clearly worked.
# Trusting the code alone reported BUILD FAILED for a good 167 MB build.
#
# So the PRIMARY signal is the log marker "[Build] TAMAM", which BuildPipelineEntry
# emits ONLY when BuildReport.summary.result == Succeeded. That is the engine's own
# verdict, not a guess from file timestamps. Fresh artifacts are the second check.
# The exit code is still PRINTED, never hidden - if it disagrees you are told.
#
# An earlier version judged success from file freshness ALONE. Code review was right
# that this weakens BuildPipelineEntry's contract: a build that failed after touching
# output would have been reported as OK.
$reportedSuccess = $false
if (Test-Path $log) {
    $reportedSuccess = [bool](Select-String -LiteralPath $log -Pattern '\[Build\] TAMAM' -Quiet)
    $reportedFailure = [bool](Select-String -LiteralPath $log -Pattern '\[Build\] BASARISIZ' -Quiet)
    if ($reportedFailure) {
        Write-Output ""
        Write-Output "VERDICT: BUILD FAILED"
        Write-Output "  The build entry point reported BuildResult != Succeeded."
        Write-Output "  Details: .claude\tools\unity-log.ps1 -Path Logs\build.log -Errors"
        exit 1
    }
}

$succeeded = $false
$files = @()
$exe = $null

if (Test-Path $Out) {
    $files = @(Get-ChildItem -LiteralPath $Out -Recurse -File)
    $exe = $files | Where-Object { $_.Extension -eq '.exe' -and $_.Name -ne 'UnityCrashHandler64.exe' } |
           Select-Object -First 1

    if ($exe) {
        $mb = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1MB, 1)
        Write-Output ("  output  {0} files, {1} MB" -f $files.Count, $mb)
        Write-Output ("  exe     {0}" -f $exe.Name)

        # Freshness is judged on the NEWEST file in the output, not on the .exe.
        # Unity builds incrementally: when only assets or scenes changed, the player
        # binary is byte-identical and is NOT rewritten. Checking the .exe reported a
        # perfectly good build as "stale output from an earlier build".
        $newest = ($files | Sort-Object LastWriteTime -Descending | Select-Object -First 1)
        $age = (Get-Date) - $newest.LastWriteTime

        if ($age.TotalMinutes -le ($sw.Elapsed.TotalMinutes + 5)) {
            # BOTH must hold: the engine said Succeeded AND the output is fresh.
            $succeeded = $reportedSuccess
            Write-Output ("  newest  {0} ({1:N0} s ago)" -f $newest.Name, $age.TotalSeconds)
            if (-not $reportedSuccess) {
                Write-Output "  WARNING: output is fresh but the log has no '[Build] TAMAM' marker."
            }
        }
        else {
            Write-Output ("  WARNING: newest output file is {0:N0} min old - nothing was written this run." -f $age.TotalMinutes)
        }
    }
}

if (-not $succeeded) {
    Write-Output ""
    Write-Output "VERDICT: BUILD FAILED"
    Write-Output "  No fresh executable in $Out"
    Write-Output "  Details: .claude\tools\unity-log.ps1 -Path Logs\build.log -Errors"
    exit 1
}

if ($code -ne 0) {
    Write-Output ""
    Write-Output ("  NOTE: editor exit code was {0}, but the build artifacts are present and fresh." -f $code)
    Write-Output "        This install logs a licensing warning every run; the code is not reliable here."
}

Write-Output ""
Write-Output "VERDICT: BUILD OK"
Write-Output "  Size breakdown: .claude\tools\profiler-summary.ps1 -BuildSize"
exit 0