<#
.SYNOPSIS
  Biriken run kayitlarini ozetler (M1-12).

.DESCRIPTION
  telemetry/runs.jsonl dosyasini okur ve M-01'in cikis kriterlerinin cevabini
  verir - ozellikle CK-13: "Tur 10'a ulasmak mumkun ve ~15 dakika suruyor".

  Bu arac oyunu acmaz, Unity'ye dokunmaz. Unity acikken de calisir.

.PARAMETER Last
  Yalnizca son N run'i ozetle. Varsayilan: hepsi.

.PARAMETER Runs
  Her run'i tek satirda listele.

.PARAMETER Deaths
  Olum yerlerini listele (isi haritasinin ham hali).

.EXAMPLE
  .claude/tools/telemetry.ps1
  .claude/tools/telemetry.ps1 -Last 10 -Runs
#>
[CmdletBinding()]
param(
    [int]$Last = 0,
    [switch]$Runs,
    [switch]$Deaths
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$path = Join-Path $root 'telemetry/runs.jsonl'

if (-not (Test-Path $path)) {
    Write-Host "TELEMETRI: kayit yok" -ForegroundColor Yellow
    Write-Host "  Beklenen dosya: $path"
    Write-Host "  Play'e basip bir run oyna ve ol - ilk satir o zaman yazilir."
    exit 0
}

# Bozuk son satiri (cokme aninda yarim kalmis) sessizce atlamak JSONL'in
# tasarim amaci. Kac tane atlandigini yine de soyleriz.
$lines = Get-Content $path -Encoding UTF8 | Where-Object { $_.Trim().Length -gt 0 }
$all = @()
$broken = 0

foreach ($line in $lines) {
    try   { $all += ($line | ConvertFrom-Json) }
    catch { $broken++ }
}

if ($all.Count -eq 0) {
    Write-Host "TELEMETRI: dosya var ama okunabilir satir yok ($broken bozuk)" -ForegroundColor Red
    exit 1
}

$sel = if ($Last -gt 0 -and $Last -lt $all.Count) { $all[-$Last..-1] } else { $all }

$rounds    = $sel | ForEach-Object { $_.roundReached }
$durations = $sel | ForEach-Object { $_.durationSeconds }

function Fmt-Time([double]$seconds) {
    '{0}:{1:00}' -f [int]($seconds / 60), [int]($seconds % 60)
}

Write-Host ""
Write-Host "TELEMETRI  $($sel.Count) run" -ForegroundColor Cyan
if ($broken -gt 0) { Write-Host "  ($broken bozuk satir atlandi)" -ForegroundColor DarkYellow }
Write-Host ""

$avgRound = ($rounds | Measure-Object -Average).Average
$maxRound = ($rounds | Measure-Object -Maximum).Maximum
$avgTime  = ($durations | Measure-Object -Average).Average
$totTime  = ($durations | Measure-Object -Sum).Sum

Write-Host ("  ulasilan tur      ort {0:N1}   en iyi {1}" -f $avgRound, $maxRound)
Write-Host ("  run suresi        ort {0}   toplam {1}" -f (Fmt-Time $avgTime), (Fmt-Time $totTime))

$kills = ($sel | ForEach-Object { $_.kills } | Measure-Object -Sum).Sum
$heads = ($sel | ForEach-Object { $_.headshotKills } | Measure-Object -Sum).Sum
$melee = ($sel | ForEach-Object { $_.meleeKills } | Measure-Object -Sum).Sum

$headPct  = if ($kills -gt 0) { 100 * $heads / $kills } else { 0 }
$meleePct = if ($kills -gt 0) { 100 * $melee / $kills } else { 0 }

Write-Host ("  oldurme           {0}  (kafa %{1:N0}, bicak %{2:N0})" -f $kills, $headPct, $meleePct)
Write-Host ""

# --- CK-13: tur 10'a ulasmak mumkun ve ~15 dakika suruyor mu
Write-Host "CK-13  tur 10'a ~15 dakikada ulasiliyor mu" -ForegroundColor Cyan

# F7/F8 ile tur atlanmis run'lar HESABA KATILMAZ: atlanan turlarin baslangic
# saniyesi uydurmadir ve tek bir hata ayiklama run'i ortalamayi bozar.
# @(...) sart: PowerShell 5.1'de tek sonuc donen Where-Object bir dizi degil tek
# nesne dondurur ve .Count bos gelir - "  /2 run ulasti" gibi eksik bir satir.
$clean = @($sel | Where-Object { -not $_.usedRoundSkip })
$skipped = $sel.Count - $clean.Count

if ($skipped -gt 0) {
    Write-Host "  ($skipped run tur atlamasi kullandi, hesaba katilmadi)" -ForegroundColor DarkYellow
}

$reached10 = @($clean | Where-Object { $_.roundStartSeconds.Count -ge 10 })

if ($reached10.Count -eq 0) {
    Write-Host "  CEVAPSIZ - hicbir temiz run tur 10'a ulasmadi (en iyi: tur $maxRound)" -ForegroundColor Yellow
}
else {
    $to10 = $reached10 | ForEach-Object { $_.roundStartSeconds[9] }
    $avg10 = ($to10 | Measure-Object -Average).Average
    $min10 = ($to10 | Measure-Object -Minimum).Minimum
    $max10 = ($to10 | Measure-Object -Maximum).Maximum

    Write-Host ("  {0}/{1} run ulasti   ort {2}   en hizli {3}   en yavas {4}" -f `
        $reached10.Count, $clean.Count, (Fmt-Time $avg10), (Fmt-Time $min10), (Fmt-Time $max10))

    $verdict = if ($avg10 -ge 720 -and $avg10 -le 1080) { 'HEDEFTE (12-18 dk)'; }
               elseif ($avg10 -lt 720) { 'COK HIZLI - turlar yeterince zorlasmiyor' }
               else { 'COK YAVAS - turlar uzuyor, oyuncu sikiliyor' }

    $color = if ($avg10 -ge 720 -and $avg10 -le 1080) { 'Green' } else { 'Yellow' }
    Write-Host "  $verdict" -ForegroundColor $color
}
Write-Host ""

# --- Tur basina sure: hangi tur uzuyor
$perRound = @{}
foreach ($run in $clean) {
    $starts = $run.roundStartSeconds
    for ($i = 0; $i -lt $starts.Count - 1; $i++) {
        $n = $i + 1
        if (-not $perRound.ContainsKey($n)) { $perRound[$n] = @() }
        $perRound[$n] += ($starts[$i + 1] - $starts[$i])
    }
}

if ($perRound.Count -gt 0) {
    Write-Host "TUR BASINA SURE" -ForegroundColor Cyan
    foreach ($n in ($perRound.Keys | Sort-Object)) {
        $avg = ($perRound[$n] | Measure-Object -Average).Average
        $bar = '#' * [Math]::Min(40, [int]($avg / 3))
        Write-Host ("  tur {0,2}  {1,5:N0} sn  {2}" -f $n, $avg, $bar)
    }
    Write-Host ""
}

if ($Runs) {
    Write-Host "RUNLAR" -ForegroundColor Cyan
    foreach ($run in $sel) {
        Write-Host ("  {0}  tur {1,2}  {2,5}  {3,3} oldurme  {4,6} puan" -f `
            $run.endedAtUtc, $run.roundReached, (Fmt-Time $run.durationSeconds),
            $run.kills, $run.pointsEarned)
    }
    Write-Host ""
}

if ($Deaths) {
    Write-Host "OLUM YERLERI" -ForegroundColor Cyan
    foreach ($run in $sel) {
        if ($null -eq $run.death) {
            Write-Host ("  tur {0,2}  (olum yeri kaydedilmemis)" -f $run.roundReached)
        }
        else {
            Write-Host ("  tur {0,2}  x {1,7:N1}   y {2,6:N1}   z {3,7:N1}" -f `
                $run.roundReached, $run.death.x, $run.death.y, $run.death.z)
        }
    }
    Write-Host ""
}

Write-Host "Dosya: $path" -ForegroundColor DarkGray
