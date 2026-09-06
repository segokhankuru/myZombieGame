<#
.SYNOPSIS
  Compiles the project and runs the EditMode (or PlayMode) test suite headlessly.

.DESCRIPTION
  Unity must be closed - the project lock is exclusive. The run compiles every assembly
  first, so this doubles as a compile check: a C# error fails here before it ever
  reaches the Editor console.

  Returns a short summary, not a log. The full log stays in Logs/tests.log and the raw
  NUnit XML in Logs/test-results.xml.

  Exit codes: 0 all passed, 1 failures, 2 could not run (editor missing / project locked).

.EXAMPLE
  .\.claude\tools\unity-test.ps1
  .\.claude\tools\unity-test.ps1 -Platform PlayMode
  .\.claude\tools\unity-test.ps1 -Filter Bunker.Systems.Tests.ZombieBrainTests
#>
[CmdletBinding()]
param(
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode',
    [string]$Filter,
    [string]$ProjectRoot = ".",
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root = (Resolve-Path $ProjectRoot).Path

. (Join-Path $PSScriptRoot "unity-lock.ps1")

if (-not (Assert-UnityUnlocked -Root $root -Label "UNITY TEST")) { exit 2 }

$verFile = Join-Path $root "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $verFile)) {
    Write-Output "ERROR: not a Unity project: $root"
    exit 2
}

$version = (Get-Content -LiteralPath $verFile |
            Where-Object { $_ -cmatch '^m_EditorVersion:\s*(.+)$' } |
            ForEach-Object { $Matches[1].Trim() } | Select-Object -First 1)

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
    Write-Output "UNITY TEST: editor $version not found. Install it via Unity Hub."
    exit 2
}

$logDir = Join-Path $root "Logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

$log     = Join-Path $logDir "tests.log"
$results = Join-Path $logDir "test-results.xml"
if (Test-Path $results) { Remove-Item -LiteralPath $results -Force }

$unityArgs = @(
    '-batchmode', '-nographics',
    '-projectPath', $root,
    '-logFile', $log,
    '-runTests',
    '-testPlatform', $Platform,
    '-testResults', $results
)
if ($Filter) { $unityArgs += @('-testFilter', $Filter) }

# See unity-exec.ps1: batchmode empties the Editor's last-opened-scene record, which
# leaves the developer staring at an empty hierarchy. Restore it afterwards.
$sceneSetup = Join-Path $root "Library\LastSceneManagerSetup.txt"
$sceneSetupBackup = $null
if (Test-Path $sceneSetup) {
    $content = Get-Content -LiteralPath $sceneSetup -Raw
    if ($content -and $content.Trim() -ne 'sceneSetups: []') { $sceneSetupBackup = $content }
}

Write-Output "UNITY TEST: $Platform, editor $version. This takes a few minutes..."

$proc = Start-Process -FilePath $editor -ArgumentList $unityArgs -PassThru -NoNewWindow
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    $proc.Kill()
    Write-Output "UNITY TEST: timed out after $TimeoutMinutes minutes. See $log"
    exit 2
}

if ($sceneSetupBackup) {
    Set-Content -LiteralPath $sceneSetup -Value $sceneSetupBackup -NoNewline
}
else {
    # KENDINI ONARMA. Yedek bossa geri yazacak bir sey yok demektir - ve onceki
    # surum tam burada duruyordu, yani dosya bir kez bosaldiginda kalici olarak
    # bos kaliyordu. Bilinen oyun sahnesini yaziyoruz: en kotu ihtimalle
    # gelistirici dogru sahneyle acilir.
    $fallback = "sceneSetups:`n- path: Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity`n  isLoaded: 1`n  isActive: 1`n  isSubScene: 0`n"
    try { Set-Content -LiteralPath $sceneSetup -Value $fallback -NoNewline -Encoding UTF8 } catch { }
}

# ------------------------------------------------------------- compile errors first
$compileErrors = @()
if (Test-Path $log) {
    $compileErrors = Select-String -LiteralPath $log -Pattern '\.cs\(\d+,\d+\): error ' |
                     ForEach-Object { $_.Line.Trim() } |
                     Select-Object -Unique
}

if ($compileErrors.Count -gt 0) {
    Write-Output ""
    Write-Output "COMPILE FAILED - $($compileErrors.Count) error(s):"
    foreach ($e in ($compileErrors | Select-Object -First 25)) { Write-Output "  $e" }
    if ($compileErrors.Count -gt 25) { Write-Output "  ... and $($compileErrors.Count - 25) more" }
    Write-Output ""
    Write-Output "Full log: $log"
    exit 1
}

if (-not (Test-Path $results)) {
    Write-Output "UNITY TEST: no result file produced. Full log: $log"
    exit 2
}

# ------------------------------------------------------------- results
[xml]$xml = Get-Content -LiteralPath $results
$run = $xml.'test-run'

Write-Output ""
Write-Output ("TEST RESULT ({0})" -f $Platform)
Write-Output ("  total {0} | passed {1} | failed {2} | skipped {3} | {4}s" -f `
    $run.total, $run.passed, $run.failed, $run.skipped, $run.duration)

if ([int]$run.failed -gt 0) {
    Write-Output ""
    Write-Output "FAILURES:"
    $xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        Write-Output ("  " + $_.fullname)
        $msg = $_.failure.message
        if ($msg) {
            foreach ($line in ($msg -split "`n" | Select-Object -First 4)) {
                Write-Output ("      " + $line.Trim())
            }
        }
    }
    Write-Output ""
    Write-Output "VERDICT: FAIL"
    exit 1
}

Write-Output "VERDICT: PASS"
exit 0
