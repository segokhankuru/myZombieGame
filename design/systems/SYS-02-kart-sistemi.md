# SYS-02 — Kart Sistemi

**Durum:** taslak · **Sahibi:** game-designer
**Sütunlar:** PILLAR-01 (build kimliği) · PILLAR-02 (takım) · PILLAR-04 (okunabilirlik)

---

## 1. Üç güç katmanı ve neden ayrı durmaları gerekiyor

| Katman | Ne cevaplar | Ömrü | Seviye atlar mı |
|---|---|---|---|
| **Kartlar** | *Ben kimim?* | Run boyunca kalıcı | Hayır — tek seferlik seçim |
| **Silah alışkanlığı** (SYS-03) | *Bu silahla ne kadar vakit geçirdim?* | Run boyunca birikir | Evet — kullandıkça |
| **Drop'lar** | *Şu anda ne oluyor?* | 15–20 sn | — |

Bu ayrım Megabonk'un çalışan modeliyle aynı: silahlar seviye atlar, item'lar atlamaz —
böylece her kart seçimi tek seferlik ve ağır bir karar olur, silah ise oynadıkça büyür.
Karıştırılırsa ikisi de anlamsızlaşır.

**Dik olmaları kritik:** yıkım build'in her silahla çalışır, seviye 3 pompalın her
build'le çalışır. Kesişmezler, çarpışırlar.

---

## 2. Etiketler

Kart fikirlerinden doğal olarak beş küme çıktı:

| Etiket | Konusu |
|---|---|
| **Balistik** | Mermi davranışı: penetrasyon, sekme, RPM, hasar çarpanı, silaha özel nitelikler |
| **Yıkım** | Alan hasarı: patlama, zincirleme, nuke |
| **Kan** | Can, vampirlik, risk/ödül, diriltme |
| **Tempo** | Hareket, hız, dolum, yavaşlatma |
| **Ganimet** | Drop, şans, ekonomi, puan |

**Aynı etiketten 3 kart = etiket bonusu açılır** (build kimliğinin görünür olduğu an):

| Etiket | 3'lü bonus |
|---|---|
| Balistik | Mermiler tüm zombileri deler, karşılığında geçiş başına %20 hasar kaybı |
| Yıkım | Patlamalar zincirlenir — patlamayla ölen zombi de patlar |
| Kan | Öldürme başına can çalarsın, maks can +%25 |
| Tempo | Öldürme başına 2 sn hız buff'ı birikir (5 kata kadar) |
| Ganimet | Tüm drop'lar iki katı, +%50 puan |

Havuz ağırlıklandırması: elinde bir etiketten 2 kart varsa, o etiket sonraki draft'ta daha
sık çıkar (karar: `docs/DECISIONS.md`).

---

## 3. Matematik — iki formül, iki tuzak

### 3.1 Hasar: katman içinde topla, katmanlar arası çarp

```
finalDamage = baseWeaponDamage
            x (1 + toplam kartHasarBonusu)   <- kartlar birbirine EKLENIR
            x silahSeviyeCarpani              <- SYS-03
            x etiketBonusCarpani
            x isabetBolgesiCarpani            <- kafa 2x
            x (1 - zirh)
```

**Tuzak:** kartlar birbirini çarparsa kaçar. Beş adet +%20 kart:

- Çarpımsal: 1.2^5 = **2.49x** — üstüne diğer çarpanlar binince 20. turda denge yok olur
- Toplamsal: 1 + (5 x 0.20) = **2.00x** — öngörülebilir, tablodan ayarlanabilir

Toplamsal olan doğru. **Kartlar arasında toplama, katmanlar arasında çarpma.**

### 3.2 Hasar azaltma: yüzde değil, efektif can

`%20 daha az hasar al` kartı tek başına doğru; **istiflenince** patlıyor. Yüzdeyle
düşürürsen 5 kart = %100 = ölümsüzlük. Dahası azalan getiri değil **artan** getiri verir:
%0 → %20 efektif canı 1.25x yapar, %60 → %80 ise 2x yapar. Yani kart üst üste geldikçe
güçlenir — olması gerekenin tam tersi.

```
alinanHasar = hasar / (1 + toplam efektifCanBonusu)
```

`%20 daha az hasar` = `+0.25 efektif can` (matematiksel olarak birebir aynı: 1/1.25 = 0.80).
Dört kart = hasar/2. Asla sıfıra ulaşmaz, azalan getiri kendiliğinden gelir, tavan koymak
gerekmez. Kart metni oyuncuya yine "%20 daha az hasar" der — matematik arkada döner.

---

## 4. Kart havuzu

`[S]` = senin fikrin · `[+]` = eklenen öneri · **kalın** = kural değiştiren

### Balistik

- `[S]` **Penetrasyon** — mermi bir sonraki zombiye geçer (kademeli: 1 → 2 → sınırsız)
- `[S]` Atış hızı (RPM) +%15
- `[S]` Hasar çarpanı +%20 *(§3.1 formülüne göre toplamsal)*
- `[S]` **Silaha özel nitelik** — bkz. SYS-03; sıradan silahı özel kılar
- `[+]` **Sekme** — mermi öldürdüğü zombiden en yakına seker
- `[+]` **Son Mermi** — şarjörün son mermisi 5x hasar *(bilinçli mermi yönetimi üretir)*
- `[+]` **Şarjör Yok** — sürekli dolum, ama %40 yavaş ateş
- `[+]` **Delik Deşik** — aynı zombiye üst üste isabet hasarı yığar *(odaklı ateşi ödüllendirir)*

### Yıkım

- `[S]` **Ölüm Patlaması** — öldürülen zombi hafifçe patlar, çevresine hasar verir
- `[S]` **Temizlik** — her 50 öldürmede nuke; boss'lar büyük hasar alır, yeterse ölür
- `[+]` **Zincir** — patlamayla ölen zombi de patlar
- `[+]` **Basınç** — dar alanda (koridor, küçük oda) patlama hasarı 2x *(harita bilgisini ödüllendirir)*
- `[+]` **Şarapnel** — patlamaların parçaları barikatları tamir eder *(takıma yarayan ters mantık)*

### Kan

- `[S]` **Sedye** — diriltme süresi kısalır *(co-op'a özel)*
- `[S]` Alınan hasar −%20 *(§3.2'ye göre efektif can olarak uygulanır)*
- `[+]` **Vampir** — kafa vuruşları can çalar
- `[+]` **Kan Bedeli** — can %50'nin altındayken hasar 2x
- `[+]` **İkinci Nefes** — düştükten sonra 3 sn içinde 5 öldürme = kendini kaldırırsın
  *(solo'daki tek kendini kurtarma — bkz. §6)*

### Tempo

- `[S]` **Yavaşlatma** — hasar verdiğin zombi %10 yavaşlar
- `[S]` Yürüme hızı +%20
- `[+]` **Momentum** — hareket ettikçe hasar artar, durunca sıfırlanır
  *(kamp kurmayı cezalandırır — PILLAR-02'ye doğrudan hizmet eder)*
- `[+]` **Kaygan** — kayarken hasar almazsın, zombileri devirirsin
- `[+]` **Bıçak Refleksi** — bıçakla öldürme şarjörü anında doldurur

### Ganimet

- `[S]` **Sızıntı Vergisi** — pencereden giren zombi öldürülünce %30 fazla drop
- `[+]` **Mıknatıs** — drop toplama yarıçapı 3x, otomatik toplanır
- `[+]` **Faizci** — harcanmamış puan her tur %2 faiz getirir *(ekonomi build'i)*
- `[+]` **Çöpçü** — barikat tahtası tamiri iki katı puan
- `[+]` **Kasa Hırsızı** — rastgele silah dağıtıcısı %30 indirimli

### Takım kartları (nadir, dördünü birden etkiler, yalnızca co-op)

- `[+]` **Ortak Cüzdan** — bir sonraki kapı herkese ücretsiz açılır
- `[+]` **Kan Bağı** — biri düştüğünde diğer üçü 10 sn boyunca %30 hasar bonusu alır
- `[+]` **Telsiz** — herkes birbirinin canını, mermisini ve konumunu duvar ardından görür
- `[+]` **Sigorta** — biri öldüğünde puan cezası yarıya iner

---

## 5. İki fikrine itirazım var

### 5.1 Şans kartları (`%10 tüm şanslar`, `%20 ihtimalle daha iyi kart`)

İki ayrı sorun:

**Görünmezlik.** Oyuncu %10 şans artışını hissedemez. PILLAR-04 kaosta okunabilirlik
istiyor; hissedilmeyen bir kart, seçildiği anda unutulan bir karttır. En kötü kart türü
zayıf kart değil, **fark edilmeyen** karttır.

**Baskın ilk seçim tuzağı.** *Gelecekteki kartları iyileştiren bir kart*, matematiksel
olarak neredeyse her zaman erken alınması gereken karttır — çünkü kalan bütün draft'lara
uygulanır. Bu, "hangisini alsam" sorusunu ortadan kaldırır. Roguelite'ların bilinen
tuzağı, ve PILLAR-01'in istediği build çeşitliliğini tam tersine tektipleştirir.

**Alternatif — şansı görünür ve kesikli yap:**

- Hayır: "Tüm şanslar +%10"
- Evet: **Kâhin** — sonraki draft'ta 3 yerine 4 kart görürsün. Görürsün, hissedersin,
  ölçebilirsin, ve bileşik büyüme yapmaz.
- Evet: **Kumarbaz** — bir kez ücretsiz yeniden çekme hakkı. Aynı mantık, tek seferlik.
- Evet: "%20 ihtimalle daha iyi silah bul" **kalabilir** — rastgele dağıtıcı zaten kesikli
  ve görünür bir olay; oradaki şans hissedilir.

### 5.2 Sızıntı Vergisi'nin bir korkuluğa ihtiyacı var

Bu kartı çok sevdim, çünkü **takım içinde gerçek bir gerilim üretiyor:** kartı alan oyuncu
zombilerin pencereden girmesini *ister*, takımın geri kalanı barikatları tamir etmek ister.
Tam olarak PILLAR-02'nin aradığı sosyal an.

Ama korkuluksuz bırakılırsa erken tur ekonomisini (barikat tamiri = +10 puan) çökertir ve
takımı sabote eden bir build ortaya çıkar. Öneri: bonus yalnızca **o oyuncunun tamir
ettiği** pencerelerden girenlere uygulansın. Böylece kartı alan kişi hâlâ barikat tamir
etmek zorunda — sadece farklı bir sebeple. Gerilim kalır, sabotaj gider.

---

## 6. Solo mod: bağlam filtresi

Solo modda diriltme kartları çıkmamalı. Bu tek bir istisna değil, bir **şema gereği**:
her kartta `soloValid` / `coopValid` alanı bulunur.

| Kart | Solo | Co-op |
|---|---|---|
| Sedye (diriltme süresi) | hayır | evet |
| Tüm takım kartları | hayır | evet |
| İkinci Nefes | evet *(solo'daki tek kendini kurtarma)* | evet |
| Diğer her şey | evet | evet |

**Denge notu:** solo'da diriltme hiç yok — ölüm doğrudan run sonu. Bu solo'yu belirgin
şekilde zorlaştırıyor. İkinci Nefes solo havuzunda daha sık çıkabilir; M2 testinde ölçülecek.

---

## 7. Sütunların reddettiği bir kart — örnek

> **Yalnız Kurt** — takımdan 15 m uzaktayken hasarın %50 artar.

Mekanik olarak sağlam, build kimliği veriyor, okunabilir. **PILLAR-02 reddediyor:**
oyuncuları birbirinden uzaklaştıran bir kart, co-op'u aynı odadaki dört tek kişilik oyuna
çevirir. Sütunlar tam olarak bunun içindir.

---

## 7b. Draft sıklığı — turda ne zaman gelir

**Her 3 turda bir: tur 3, 6, 9, 12…** Ayrıca boss sonrası garanti bir nadir kart.
3 karttan 1 seçim, 4 oyuncu eşzamanlı.

> **Bu sayı 2026-09-04'e kadar yalnızca `docs/reference/zombi-coop-kapsam-dokumani.md`
> §3.3'te yaşıyordu** — bir *referans* dokümanında. Sistemin kendi spesifikasyonu (bu
> dosya) sıklığı hiç söylemiyordu. SSoT kuralı gereği buraya taşındı; referans doküman
> artık bunun kopyası, kaynağı değil.

### Turda değil, DAKİKADA ne kadar sık

Sıklık turda sabit ama **dakikada değil.** `balance-sim.ps1`'in tur sürelerine göre
(ortalama oyuncu profili):

| Draft | Tur | Oyunun kaçıncı dakikası | Bir öncekinden fark |
|---|---|---|---|
| 1. | 3 | 1:35 | — |
| 2. | 6 | 4:42 | +3:07 |
| 3. | 9 | 10:05 | +5:23 |
| 4. | 12 | 18:47 | **+8:42** |
| 5. | 15 | 33:07 | **+14:20** |
| 6. | 18 | 56:52 | **+23:45** |

**Bu bir PILLAR-03 sorusudur ve henüz cevaplanmadı.** Sütun draft'ı *"ritmin zirvesi"*
diye tanımlıyor. İlk üç zirve 3–5 dakika arayla geliyor; altıncısı bir öncekinden
**24 dakika** sonra. Zirveler arası mesafe bu kadar açılınca, geç turlarda oyuncu
"bir sonraki kart ne zaman" diye bekler hâle gelir — ritim değil, bekleyiş.

**Karar `game-designer` ve `creative-director`'ın.** Seçenekler:

| Yaklaşım | Ne olur |
|---|---|
| Turda sabit kalsın (şimdiki) | Basit ve okunur: "her üç tur". Geç oyunda zirveler seyrekleşir |
| Süreye göre ayarla (ör. her ~4 dk) | Ritim sabit kalır; ama "kaçıncı turda draft var" tahmin edilemez olur |
| Turda sabit + geç turlarda sıklaşan | Tur 12'den sonra her 2 turda bir. Karma, ama kural iki parçalı olur |

Bu tablo **M-01'in tur sürelerine** dayanıyor ve o süreler oyun testiyle değişecek.
Kararı vermeden önce gerçek telemetriyi bekle — model, oyuncu değil.

---

## 8. Açık kalanlar

- Toplam kart sayısı — `<tbd>` (kapsam kararı ertelendi)
- Nadirlik kademeleri ve draft'taki dağılımı — `<tbd>`
- Denge sayıları `config/balance/cards.json` içinde yaşar, C# içinde değil
- **Temizlik** (50 öldürmede nuke) sayacı oyuncu başına mı, takım toplamı mı? Öneri:
  oyuncu başına — takım toplamı olursa 4 kat sık patlar ve kişisel build olmaktan çıkar
