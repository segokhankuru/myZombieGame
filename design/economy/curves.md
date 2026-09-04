# Ekonomi Eğrileri — kırılma noktaları

**Sahibi:** `systems-designer` · **Son güncelleme:** 2026-09-04
**Kaynak:** `.claude/tools/balance-sim.ps1` (Unity açmadan koşar, saniyeler sürer)

---

## Ne simüle edildi

`rounds.json` + `economy.json` + `weapon.json`, 15 tur, iki oyuncu profili:

| Profil | Kafa oranı | İsabet | Öldürme başına ek süre |
|---|---|---|---|
| **Ortalama** | %25 | %80 | 1.2 sn |
| **İyi** | %50 | %90 | 0.8 sn |

> **Bu bir modeldir, oyuncu değil.** Nişan isabeti ve hareket süresi varsayımdır. Modelin
> işi "imkânsız olan" ile "mümkün ama zor"u ayırmak; gerçek sayı oyun testinden gelir.
> Simülasyon `RoundScaling.cs` ile **aynı formülleri** kullanır — ayrışırlarsa yalan
> söyler, ikisi birlikte değişmeli.

---

## ÇK-13 — tur 10'a ~15 dakika

| Profil | Tur 10'a süre | Değerlendirme |
|---|---|---|
| Ortalama | **12.5 dk** | Hedefte (12–18 dk) |
| İyi | **9.8 dk** | Hedefin altında |

**Okuma:** config kabaca doğru. Hedef bandı ortalama oyuncuya oturuyor; iyi bir oyuncu
%20 daha hızlı geçiyor, ki bu beklenen ve sağlıklı. **Karar telemetriye bırakılıyor** —
gerçek run'lar `telemetry.ps1` ile ölçülecek.

---

## ÇK-14 — üç bölge puanla açılıyor, ekonomi kilitlenmiyor

Her iki profilde de **3/3 kapı açıldı.** Ekonomi kilitlenmiyor.

| Profil | 1. kapı | 2. kapı | 3. kapı |
|---|---|---|---|
| Ortalama | tur 2 | tur 5 | tur 10 |
| İyi | tur 2 | tur 4 | tur 7 |

**ÇK-14 karşılanıyor** — ama iki kırılma noktasıyla birlikte.

---

## KIRILMA 1 — mermi seferleri turu parçalıyor

**En önemli bulgu.** Her iki beceri seviyesinde de var, yani oyuncu becerisiyle
kapanmıyor.

| Tur | Ortalama oyuncu | İyi oyuncu |
|---|---|---|
| 5 | 3 alım | 2 alım |
| 10 | 7 alım | 5 alım |
| 14 | **15 alım** | **11 alım** |

Tur 14'te oyuncu duvara **on beş ayrı sefer** yapıyor. Her sefer: turu bırak, duvara
koş, E'ye bas, geri dön.

**PILLAR-03 ihlali.** Sütun aynen şunu diyor: *"Ritim: 60–90 saniye baskı, ardından kısa
nefes... Oyuncu hiçbir zaman 'şimdi menü işi yapıyorum' moduna geçmemeli."* On beş sefer
tam olarak o mod.

### Kök sebep — aritmetik

Hasar **sabit 55**, can **bileşik büyüyor**. Öldürme başına atış: tur 1'de 4, tur 15'te 32.

| | Tur 1 | Tur 15 |
|---|---|---|
| Öldürme başına atış | 4 | 32 |
| Mermi maliyeti (500 puan / 60 mermi = 8.33 puan/atış) | 33 puan | 267 puan |
| Öldürme başına gelir | 90 puan | 320 puan |
| **Kâr marjı** | **%63** | **%17** |

Gelir de büyüyor (isabet puanı sayesinde), ama **maliyet daha hızlı** büyüyor: isabet
oranı %80 olduğu için atış sayısı isabet sayısından hızlı artıyor.

İkinci sıkıştırıcı: `weapon.json → reserveCapacity: 300`. Tur 15'in ihtiyacı **1024
mermi**. Oyuncu parası olsa bile stok yapamıyor; tur ortasında duvara dönmek **zorunda**.

### Bunun M-01'de gerçek çözümü yok

Bu türde cevap **silah yükseltmesi**dir (hasar artar, atış sayısı düşer). M-01'de
kasıtlı olarak yok — M1-10 "M-01 için yalnızca mermi" diyor. Yani bu, klon tabanının
bilinen bir eksiği, sürpriz bir hata değil.

**M-01 içinde kalan seçenekler** (hiçbiri uygulanmadı, karar geliştiricinin):

| Anahtar | Şu an | Öneri | Oyuncu ne hisseder |
|---|---|---|---|
| `magazinesPerPurchase` (kod, config değil) | 5 | 12–15 | Sefer sayısı üçte bire iner; duvar bir mola olur, bir angarya değil |
| `weapon.magazine.reserveCapacity` | 300 | 600 | Tur ortasında dönmek zorunda kalmaz; sefer tur arasına taşınır |
| `rounds.health.growthMultiplierAfterLinear` | 1.10 | 1.06 | Geç turlar sünger hissini geç verir; ama turlar da uzar (ÇK-13'ü iter) |

> **Ayrıca bir SSoT kusuru:** `magazinesPerPurchase` bir `[SerializeField]` ve tooltip'i
> *"Denge değeri DEĞİL"* diyor. Ama yukarıdaki tablo tam tersini gösteriyor — bu sayı
> oyunun ritmini doğrudan belirliyor. `config-data.md`'ye göre `economy.json`'a
> taşınmalı.

---

## KIRILMA 2 — geç oyunda harcanacak bir şey kalmıyor

İyi oyuncu, tur 15'te **15.620 puan** biriktirmiş durumda. Üç kapı tur 7'de bitmiş,
mermiden başka alacak bir şey yok.

`economy.json` bir `mysteryBox: 950` fiyatı taşıyor ama **kutu M-01'de yok**.

Puan biriktiren ama harcayamayan bir oyuncu için skor bir sayaç hâline gelir — SYS-01'in
"kazanılan her puan görünür" sözü, puanın **bir şeye yaradığı** varsayımına dayanıyor.

Bu da **M-03**'ün işi (kart draft'ı, kutu, silah çeşitliliği). **M-01'de kapatılmıyor**,
kayda geçiriliyor.

---

## Karar

**Hiçbir değer değiştirilmedi.** Gerekçe `balance-check`'in kendi kuralı:

> *Bir simülasyon oyuncunun modelidir ve modeller, gerçek insanların olmadığı biçimlerde
> kendinden emin şekilde yanılır.*

Bu bulgular **oyun testinde aranacak şeyler** olarak duruyor. Özellikle: oyun testinde
"tur 10 civarında sıkıldım" denirse, sebebi büyük olasılıkla mermi seferleridir ve
o zaman `/tune` ile hissedilen etkisi yazılarak değiştirilir.

### Nasıl tekrarlanır

```bash
powershell -NoProfile -File .claude/tools/balance-sim.ps1
powershell -NoProfile -File .claude/tools/balance-sim.ps1 -HeadshotRate 0.5 -AccuracyRate 0.9 -SecondsPerKillOverhead 0.8
```

---

## Modelde düzeltilen iki kusur

Kayda değer, çünkü ikisi de **yanlış sonuç üretiyordu**:

1. **Doğum aralığı formülü yanlıştı.** `0.9^(tur-1)` yazmıştım; gerçek kod
   `intervalAtOne × countAtOne / countNow` kullanıyor. Yanlış formül yanlış ÇK-13 cevabı
   verirdi.
2. **Model tur sonunda tavana kadar mermi alıyordu.** Bu, modelin kendi açgözlülüğünü
   config'in suçu gibi gösteriyordu: tur 1'de 96/300 yedekle duran oyuncuya 500 puan
   harcatıp "gelirin %93'ü mermiye gidiyor" diyordu. Yetkin bir oyuncu öyle oynamaz —
   bir sonraki turu çıkaracak kadar alır. Düzeltince tur 1 %0'a düştü ve asıl kırılma
   (geç turlardaki sefer sayısı) net görünür oldu.
