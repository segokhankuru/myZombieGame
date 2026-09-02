<#
.SYNOPSIS
  Shared helper: is the Unity project actually locked, or is the lockfile stale?

.DESCRIPTION
  Unity keeps an exclusive lock in Temp/UnityLockfile. A batch run that dies - most often
  because the project has compile errors - leaves the file behind with no process holding
  it. Every later tool then refuses to run and reports "the Editor is open", which is
  false and sends the reader looking in the wrong place.

  So: lockfile plus a live Unity process means genuinely locked. Lockfile with no Unity
  process is debris, and removing it is safe.

  Dot-source this file, then call Assert-UnityUnlocked.
#>

function Assert-UnityUnlocked {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [string]$Label = "UNITY"
    )

    $lock = Join-Path $Root "Temp\UnityLockfile"
    if (-not (Test-Path $lock)) { return $true }

    $running = @(Get-Process Unity -ErrorAction SilentlyContinue)

    if ($running.Count -gt 0) {
        Write-Output "${Label}: project is locked - the Editor is open."
        Write-Output "  Close Unity and run again, or read the console with unity-log.ps1 -Errors."
        return $false
    }

    try {
        Remove-Item -LiteralPath $lock -Force -ErrorAction Stop
        Write-Output "${Label}: removed a stale lockfile (no Unity process was running)."
        return $true
    }
    catch {
        Write-Output "${Label}: a lockfile exists and could not be removed: $_"
        return $false
    }
}
