<#
.SYNOPSIS
  Runs a static editor method headlessly (-executeMethod) and summarizes the log.

.DESCRIPTION
  Anything mechanical the studio does in the Editor - generating a blockout, building a
  prefab, baking a NavMesh, importing config - should be callable without the Editor
  being open (editor-tools.md). This is the front door for that.

  Unity must be closed; the project lock is exclusive.

  Exit codes: 0 ok, 1 the method failed or the project has compile errors, 2 could not run.

.EXAMPLE
  .\.claude\tools\unity-exec.ps1 -Method Bunker.Editor.ZombieSetup.SetupTestbedBatch
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Method,
    [string]$ProjectRoot = ".",
    [int]$TimeoutMinutes = 20,
    [switch]$FullLog
)

$ErrorActionPreference = 'Stop'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root = (Resolve-Path $ProjectRoot).Path

. (Join-Path $PSScriptRoot "unity-lock.ps1")

if (-not (Assert-UnityUnlocked -Root $root -Label "UNITY EXEC")) { exit 2 }

$verFile = Join-Path $root "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $verFile)) { Write-Output "ERROR: not a Unity project: $root"; exit 2 }

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
if (-not $editor) { Write-Output "UNITY EXEC: editor $version not found."; exit 2 }

$logDir = Join-Path $root "Logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
$log = Join-Path $logDir "exec.log"

# Batchmode overwrites Library/LastSceneManagerSetup.txt with an empty setup, so the
# next time the developer opens Unity the hierarchy is empty and their scene looks lost.
# Snapshot it and put it back - a tool must not change what the Editor opens with.
$sceneSetup = Join-Path $root "Library\LastSceneManagerSetup.txt"
$sceneSetupBackup = $null
if (Test-Path $sceneSetup) {
    $content = Get-Content -LiteralPath $sceneSetup -Raw
    if ($content -and $content.Trim() -ne 'sceneSetups: []') { $sceneSetupBackup = $content }
}

Write-Output "UNITY EXEC: $Method (editor $version). This takes a minute or two..."

$proc = Start-Process -FilePath $editor -PassThru -NoNewWindow -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', $root, '-logFile', $log,
    '-executeMethod', $Method
)

if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    $proc.Kill()
    Write-Output "UNITY EXEC: timed out after $TimeoutMinutes minutes. See $log"
    exit 2
}

$exit = $proc.ExitCode

if ($sceneSetupBackup) {
    Set-Content -LiteralPath $sceneSetup -Value $sceneSetupBackup -NoNewline
}

if (-not (Test-Path $log)) {
    Write-Output "UNITY EXEC: no log produced (exit $exit)."
    exit 2
}

$compileErrors = Select-String -LiteralPath $log -Pattern '\.cs\(\d+,\d+\): error ' |
                 ForEach-Object { $_.Line.Trim() } | Select-Object -Unique

if ($compileErrors.Count -gt 0) {
    Write-Output ""
    Write-Output "COMPILE FAILED - $($compileErrors.Count) error(s):"
    foreach ($e in ($compileErrors | Select-Object -First 25)) { Write-Output "  $e" }
    Write-Output ""
    Write-Output "Full log: $log"
    exit 1
}

# Editor log lines the tools print, plus anything that failed.
$interesting = Select-String -LiteralPath $log -Pattern '^\[(Zombi|Blockout|Bunker)\]|Exception|error CS|Failed' -Context 0, 6

Write-Output ""
if ($FullLog) {
    Get-Content -LiteralPath $log | ForEach-Object { Write-Output $_ }
} elseif ($interesting) {
    foreach ($m in $interesting) {
        Write-Output $m.Line.Trim()
        foreach ($c in $m.Context.PostContext) {
            if ($c -match '^\s{2,}\S') { Write-Output $c.TrimEnd() } else { break }
        }
    }
} else {
    Write-Output "(no tool output in log)"
}

Write-Output ""
Write-Output "EXIT: $exit   log: $log"
exit $exit
