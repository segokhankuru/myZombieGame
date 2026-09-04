<#
.SYNOPSIS
  Tur ve ekonomi egrilerini oynamadan simule eder (CK-13, CK-14).

.DESCRIPTION
  config/balance/rounds.json, economy.json ve weapon.json'u okur ve bir oturumu
  tur tur yurutur. Unity acmaz, build gerektirmez, saniyeler surer.

  Cevapladigi iki soru:
    CK-13  Tur 10'a ulasmak ~15 dakika suruyor mu
    CK-14  Uc bolge puanla aciliyor mu, ekonomi kilitleniyor mu

  ONEMLI: bu bir MODEL, oyuncu degil. Nisan isabeti ve hareket suresi
  varsayimdir; gercek sayi oyun testinden gelir. Modelin isi "imkansiz olan"
  ile "mumkun ama zor"u ayirmak.

.PARAMETER Rounds
  Kac tur simule edilecek. Varsayilan 15.

.PARAMETER HeadshotRate
  Kafa vurusu orani (0-1). Varsayilan 0.25 - makul bir oyuncu.

.PARAMETER AccuracyRate
  Isabet orani (0-1). Varsayilan 0.8.

.PARAMETER SecondsPerKillOverhead
  Oldurme basina nisan alma + hareket sureleri, saniye. Varsayilan 1.2.

.EXAMPLE
  .claude/tools/balance-sim.ps1
  .claude/tools/balance-sim.ps1 -HeadshotRate 0.5 -SecondsPerKillOverhead 0.8
#>
[CmdletBinding()]
param(
    [int]$Rounds = 15,
    [double]$HeadshotRate = 0.25,
    [double]$AccuracyRate = 0.80,
    [double]$SecondsPerKillOverhead = 1.2
)

$ErrorActionPreference = 'Stop'
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
function Cfg([string]$name) {
    Get-Content (Join-Path $root "config/balance/$name.json") -Raw | ConvertFrom-Json
}

$r = Cfg 'rounds'
$e = Cfg 'economy'
$w = Cfg 'weapon'

# ---------------------------------------------------------------- egriler
# RoundScaling.cs ile AYNI formul. Ayrisirlarsa bu simulasyon yalan soyler;
# degistirirken ikisi birden degisir.
function TotalZombies([int]$round) {
    $c = $r.count
    if ($round -le $c.linearPhaseUntilRound) {
        $v = $c.perPlayerAtRoundOne + $c.linearAddPerPlayerPerRound * ($round - 1)
    }
    else {
        $v = $c.perPlayerAtRoundOne + $c.linearAddPerPlayerPerRound * ($c.linearPhaseUntilRound - 1)
        $v = $v * [Math]::Pow($c.growthMultiplierAfterLinear, $round - $c.linearPhaseUntilRound)
    }
    # RoundScaling: Math.Round(..., AwayFromZero), Floor DEGIL.
    return [Math]::Max(1, [Math]::Round($v, [MidpointRounding]::AwayFromZero))
}

function ZombieHealth([int]$round) {
    $h = $r.health
    if ($round -le $h.linearPhaseUntilRound) {
        $v = $h.atRoundOne + $h.linearAddPerRound * ($round - 1)
    }
    else {
        $v = $h.atRoundOne + $h.linearAddPerRound * ($h.linearPhaseUntilRound - 1)
        $v = $v * [Math]::Pow($h.growthMultiplierAfterLinear, $round - $h.linearPhaseUntilRound)
    }
    return [Math]::Min($v, $h.cap)
}

function SpawnInterval([int]$round) {
    # RoundScaling.SpawnIntervalForRound: aralik ZOMBI SAYISIYLA ters orantili
    # kisalir (intervalAtOne * countAtOne / countNow), ust uslu bir azalma DEGIL.
    # Ilk surumde 0.9^(round-1) yazmistim; yanlis formul yanlis CK-13 cevabi verir.
    $p = $r.pacing
    $countAtOne = $r.count.perPlayerAtRoundOne
    $countNow = TotalZombies $round
    $v = $p.spawnIntervalSecondsAtRoundOne * ($countAtOne / $countNow)
    return [Math]::Max($v, $p.spawnIntervalFloorSeconds)
}

# ---------------------------------------------------------------- silah
$rpm      = $w.fire.roundsPerMinute
$dmgBody  = $w.fire.damage
$dmgHead  = $w.fire.damage * $w.fire.headshotMultiplier
$magCap   = $w.magazine.capacity
$reload   = $w.magazine.reloadSeconds
$shotTime = 60.0 / $rpm

Write-Host ""
Write-Host "DENGE SIMULASYONU" -ForegroundColor Cyan
Write-Host ("  silah      {0} hasar, kafa x{1}, {2} atis/dk, {3} mermilik sarjor" -f $dmgBody, $w.fire.headshotMultiplier, $rpm, $magCap)
Write-Host ("  oyuncu     kafa orani %{0:N0}, isabet %{1:N0}, oldurme basina +{2} sn" -f ($HeadshotRate*100), ($AccuracyRate*100), $SecondsPerKillOverhead)
Write-Host ("  MODEL UYARISI: nisan ve hareket varsayim. Gercek sayi oyun testinden gelir." ) -ForegroundColor DarkYellow
Write-Host ""

$totalSeconds = 0.0
$earned  = 0.0
$spend   = 0.0
$ammoUsed = 0
$reserve = $w.magazine.startingReserve
$ammoBuys = 0

$doorCheap = $e.prices.doorCheap
$doorMid   = $e.prices.doorMid
$doorExp   = $e.prices.doorExpensive
$ammoCost  = $e.prices.wallWeaponCheap
$ammoPerBuy = 5 * $magCap

$doorsOpen = 0
$reach10 = $null

Write-Host ("{0,4}  {1,6}  {2,7}  {3,6}  {4,6}  {5,7}  {6,7}  {7,7}  {8,6}  {9}" -f 'tur','zombi','can','mermi','tur sn','toplam','kazanc','bakiye','mermi%','olay')
Write-Host ("-" * 104)

for ($n = 1; $n -le $Rounds; $n++) {
    $count  = TotalZombies $n
    $health = ZombieHealth $n

    # Bir zombiyi dusurmek icin gereken ISABET sayisi (kafa/govde karisimi)
    $avgDmg = $HeadshotRate * $dmgHead + (1 - $HeadshotRate) * $dmgBody
    $hitsPerKill = [Math]::Ceiling($health / $avgDmg)
    $shotsPerKill = [Math]::Ceiling($hitsPerKill / $AccuracyRate)

    $shots = $shotsPerKill * $count
    $ammoUsed += $shots

    # Sure: atis + dolum + nisan/hareket + mola + dogum kuyrugu
    $reloads   = [Math]::Floor($shots / $magCap)
    $fireTime  = $shots * $shotTime + $reloads * $reload
    $aimTime   = $count * $SecondsPerKillOverhead
    $spawnTime = $count * (SpawnInterval $n)      # son zombi dogana kadar
    $roundTime = [Math]::Max($fireTime + $aimTime, $spawnTime) + $r.pacing.breatherSeconds

    $totalSeconds += $roundTime

    # Puan: her oldurme + isabetler
    $killPts = $HeadshotRate * $e.awards.headshotKill + (1 - $HeadshotRate) * $e.awards.bodyKill
    $hitPts  = ($hitsPerKill - 1) * $e.awards.hit      # olduren vurus kill puani verir
    $gain    = $count * ($killPts + $hitPts)
    $earned += $gain

    # Harcama: once mermi (yoksa oyun durur), sonra kapi
    # Mermi TUR ICINDE tukenir ve yedek tavani var (weapon.json reserveCapacity).
    # Tavani yok saymak, oyuncunun tur basinda stok yapip rahat etmesi demekti -
    # gercekte tavan dolunca duvara TEKRAR TEKRAR donmek zorunda.
    $reserveCap = $w.magazine.reserveCapacity
    $events = @()
    $buysThisRound = 0
    $needed = $shots

    while ($needed -gt 0) {
        $use = [Math]::Min($needed, $reserve)
        $reserve -= $use
        $needed -= $use

        if ($needed -le 0) { break }

        if (($earned - $spend) -lt $ammoCost) { $events += "MERMI BITTI - puan da yok"; break }

        $spend += $ammoCost
        $reserve = [Math]::Min($reserveCap, $reserve + $ammoPerBuy)
        $ammoBuys++; $buysThisRound++

        if ($buysThisRound -gt 60) { $events += "SONSUZ DONGU KORUMASI"; break }
    }

    # Tur sonunda IHTIYAC KADAR al, tavana kadar DEGIL.
    #
    # Ilk surum tavana kadar dolduruyordu ve bu, modelin kendi acgozlulugunu
    # config'in sucu gibi gosteriyordu: tur 1'de 96/300 yedekle duran oyuncuya
    # 500 puan harcatiyor, sonra "gelirin %93'u mermiye gidiyor" diyordu. Yetkin
    # bir oyuncu oyle oynamaz - bir sonraki turu cikaracak kadar alir, gerisini
    # kapiya biriktirir.
    $nextShots = if ($n -lt $Rounds) {
        $nc = TotalZombies ($n + 1)
        $nh = ZombieHealth ($n + 1)
        [Math]::Ceiling([Math]::Ceiling($nh / $avgDmg) / $AccuracyRate) * $nc
    } else { 0 }

    while ($reserve -lt $nextShots -and $reserve -lt $reserveCap -and
           ($earned - $spend) -ge $ammoCost) {
        $spend += $ammoCost
        $reserve = [Math]::Min($reserveCap, $reserve + $ammoPerBuy)
        $ammoBuys++; $buysThisRound++
    }

    if ($buysThisRound -gt 0) { $events += "$buysThisRound x mermi" }

    $ammoSpendThisRound = $buysThisRound * $ammoCost
    $ammoShare = if ($gain -gt 0) { 100.0 * $ammoSpendThisRound / $gain } else { 0 }

    $balance = $earned - $spend
    if ($doorsOpen -eq 0 -and $balance -ge $doorCheap) { $spend += $doorCheap; $doorsOpen = 1; $events += "1. KAPI acildi" }
    elseif ($doorsOpen -eq 1 -and $balance -ge $doorMid) { $spend += $doorMid; $doorsOpen = 2; $events += "2. KAPI acildi" }
    elseif ($doorsOpen -eq 2 -and $balance -ge $doorExp) { $spend += $doorExp; $doorsOpen = 3; $events += "3. KAPI acildi" }

    $balance = $earned - $spend
    if ($n -eq 10) { $reach10 = $totalSeconds }

    $mmss = '{0}:{1:00}' -f [int]($totalSeconds/60), [int]($totalSeconds%60)

    Write-Host ("{0,4}  {1,6}  {2,7:N0}  {3,6}  {4,6:N0}  {5,7}  {6,7:N0}  {7,7:N0}  {8,5:N0}%  {9}" -f `
        $n, $count, $health, $shotsPerKill, $roundTime, $mmss, $gain, $balance, $ammoShare, ($events -join ', '))
}

Write-Host ""
Write-Host "CK-13  tur 10'a ~15 dakika" -ForegroundColor Cyan
if ($reach10) {
    $m = $reach10 / 60
    $verdict = if ($m -ge 12 -and $m -le 18) { 'HEDEFTE (12-18 dk)' }
               elseif ($m -lt 12) { 'COK HIZLI - turlar yeterince zorlasmiyor' }
               else { 'COK YAVAS - oyuncu 10. turdan once sikilir' }
    $color = if ($m -ge 12 -and $m -le 18) { 'Green' } else { 'Yellow' }
    Write-Host ("  modele gore {0:N1} dakika  ->  {1}" -f $m, $verdict) -ForegroundColor $color
}
Write-Host ""
Write-Host "CK-14  uc bolge puanla aciliyor, ekonomi kilitlenmiyor" -ForegroundColor Cyan
Write-Host ("  {0}/3 kapi acildi, {1} kez mermi alindi, {2} mermi harcandi" -f $doorsOpen, $ammoBuys, $ammoUsed)
$c14 = if ($doorsOpen -eq 3) { 'Green' } else { 'Yellow' }
Write-Host ("  {0}" -f $(if ($doorsOpen -eq 3) { 'UC KAPI DA ACILDI' } else { "SADECE $doorsOpen KAPI - kalan bolgeler $Rounds tur icinde acilmiyor" })) -ForegroundColor $c14
Write-Host ""
