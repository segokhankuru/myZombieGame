<#
.SYNOPSIS
  Renders the weapon and zombie models to PNGs so their orientation can be checked
  without launching the game.

.DESCRIPTION
  unity-exec.ps1 passes -nographics, which has no GPU and therefore cannot render.
  This is the same thing minus that flag, and it exists for one reason: the model
  orientation is *guessed* from mesh geometry (ArtIntegration.Measure) and a guess
  needs evidence. One playtest per attempt is the alternative.

  Unity must be closed; the project lock is exclusive.

  Output: Logs/art-preview/*.png
  Exit codes: 0 ok, 1 render failed or the project has compile errors, 2 could not run.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = ".",
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root = (Resolve-Path $ProjectRoot).Path

. (Join-Path $PSScriptRoot "unity-lock.ps1")

if (-not (Assert-UnityUnlocked -Root $root -Label "ART PREVIEW")) { exit 2 }

$verFile = Join-Path $root "ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $verFile)) { Write-Output "ERROR: not a Unity project: $root"; exit 2 }

$version = (Get-Content -LiteralPath $verFile |
            Where-Object { $_ -cmatch '^m_EditorVersion:\s*(.+)$' } |
            ForEach-Object { $Matches[1].Trim() } | Select-Object -First 1)

$editor = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $editor)) {
    Write-Output "ERROR: editor not found: $editor"
    exit 2
}

$log = Join-Path $root "Logs\art-preview.log"
$out = Join-Path $root "Logs\art-preview"

if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }

Write-Output "ART PREVIEW: rendering models (editor $version). Needs a GPU, so no -nographics."

# NO -nographics on purpose: RenderTexture readback returns an empty image without a
# graphics device, and an empty image looks exactly like a correctly rendered black one.
$unityArgs = @(
    '-batchmode', '-projectPath', $root, '-logFile', $log,
    '-executeMethod', 'Bunker.Editor.ArtPreview.RenderBatch'
)

$process = Start-Process -FilePath $editor -ArgumentList $unityArgs -PassThru -NoNewWindow
$exited = $process.WaitForExit($TimeoutMinutes * 60 * 1000)

if (-not $exited) {
    $process.Kill()
    Write-Output "ERROR: timed out after $TimeoutMinutes minutes. log: $log"
    exit 2
}

if (Test-Path $log) {
    Select-String -LiteralPath $log -Pattern '^\[Onizleme\]|error CS|Exception' |
        Select-Object -First 30 |
        ForEach-Object { Write-Output $_.Line }
}

if (Test-Path $out) {
    Write-Output ""
    Write-Output "IMAGES:"
    Get-ChildItem -LiteralPath $out -Filter *.png |
        ForEach-Object { Write-Output ("  {0}  ({1} KB)" -f $_.FullName, [int]($_.Length / 1KB)) }
}

Write-Output ""
Write-Output "EXIT: $($process.ExitCode)   log: $log"
exit $process.ExitCode
