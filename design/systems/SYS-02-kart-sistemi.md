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

`[S]` = senin fikrin · `[+]` = ilk turda eklenen öneri · `[G]` = 2026-09-04 genişletmesi ·
**kalın** = kural değiştiren (salt istatistik değil)

**55 kart.** Her tur draft GOAL-02 için havuzu zorluyor (§7b): yayın hedefi **70+**.

### Balistik

- `[S]` **Penetrasyon** — mermi bir sonraki zombiye geçer (kademeli: 1 → 2 → sınırsız)
- `[S]` Atış hızı (RPM) +%15
- `[S]` Hasar çarpanı +%20 *(§3.1 formülüne göre toplamsal)*
- `[S]` **Silaha özel nitelik** — bkz. SYS-03; sıradan silahı özel kılar
- `[+]` **Sekme** — mermi öldürdüğü zombiden en yakına seker
- `[+]` **Son Mermi** — şarjörün son mermisi 5x hasar *(bilinçli mermi yönetimi üretir)*
- `[+]` **Şarjör Yok** — sürekli dolum, ama %40 yavaş ateş
- `[+]` **Delik Deşik** — aynı zombiye ARKA ARKAYA isabet ettikçe o zombiye verdiğin hasar artar (55 -> 60 -> 66...); başka hedefe geçince sıfırlanır *(kalabalığa sıkmak yerine tek hedefe odaklanmayı ödüllendirir; geç turlarda kalın canlı zombiler için, kalabalıkta işe yaramaz)*
- `[G]` **Çifte Namlu** — her atış iki mermi harcar, iki kat hasar verir *(mermi ekonomisiyle doğrudan pazarlık: hızlı öldür, sık duvara git)*
- `[G]` **Sıcak Namlu** — arka arkaya isabet ettikçe atış hızı artar, ıskalayınca sıfırlanır
- `[G]` **Keskin Nişan** — kafa vuruşu çarpanı 2x yerine 3x
- `[G]` **Sekme Ustası** — sekme sayısı +2 *(yalnızca Sekme elindeyken havuzda çıkar — koşullu kart)*

### Yıkım

- `[S]` **Ölüm Patlaması** — öldürülen zombi hafifçe patlar, çevresine hasar verir
- `[S]` **Temizlik** — her 50 öldürmede nuke; boss'lar büyük hasar alır, yeterse ölür
- `[+]` **Zincir** — patlamayla ölen zombi de patlar
- `[+]` **Basınç** — dar alanda (koridor, küçük oda) patlama hasarı 2x *(harita bilgisini ödüllendirir)*
- `[+]` **Şarapnel** — patlamaların parçaları barikatları tamir eder *(takıma yarayan ters mantık)*
- `[G]` **Fitil** — öldürdüğün zombi hemen patlamaz; 1 sn daha yürür, sonra patlar *(sürüyü kendi içine çekmek için zamanlama gerektirir)*
- `[G]` **Ateş Topu** — patlamalar 3 sn yanan bir alan bırakır
- `[G]` **Sarsıntı** — patlama zombileri 1 sn devirir *(hasar değil KONTROL — barikat tamiri için pencere açar)*
- `[G]` **Ağır Sanayi** — patlama yarıçapı +%50, hasarı −%20 *(kalabalık temizleyici, tekil hedefte zayıf)*

### Kan

- `[S]` **Sedye** — diriltme süresi kısalır *(co-op'a özel)*
- `[S]` Alınan hasar −%20 *(§3.2'ye göre efektif can olarak uygulanır)*
- `[+]` **Vampir** — kafa vuruşları can çalar
- `[+]` **Kan Bedeli** — can %50'nin altındayken hasar 2x
- `[+]` **İkinci Nefes** — düştükten sonra 3 sn içinde 5 öldürme = kendini kaldırırsın
  *(solo'daki tek kendini kurtarma — bkz. §6)*
- `[G]` **Kan Kaybı** — vurduğun zombi 3 sn boyunca can kaybetmeye devam eder
- `[G]` **Adrenalin** — can %25 altına inince 5 sn hız ve dolum +%50; tur başına bir kez
- `[G]` **Kalkan** — her tur başında bir vuruşu tamamen emen kalkan
- `[G]` **Ölüm İnadı** — düşerken 3 sn daha ateş edebilirsin *(solo ve co-op)*
- `[G]` **Kan Nakli** — takım arkadaşına kendi canından verebilirsin *(co-op'a özel)*

### Tempo

- `[S]` **Yavaşlatma** — hasar verdiğin zombi %10 yavaşlar
- `[S]` Yürüme hızı +%20
- `[+]` **Momentum** — hareket ettikçe hasar artar, durunca sıfırlanır
  *(kamp kurmayı cezalandırır — PILLAR-02'ye doğrudan hizmet eder)*
- `[+]` **Kaygan** — kayarken hasar almazsın, zombileri devirirsin
- `[+]` **Bıçak Refleksi** — bıçakla öldürme şarjörü anında doldurur
- `[G]` **Tetik Parmağı** — dolum %40 hızlı
- `[G]` **Buz** — Yavaşlatma üst üste yığılır (5 kata kadar); beşincide zombi 1 sn donar
- `[G]` **Sürat Koşusu** — 3 sn kesintisiz koşunca bir hız kademesi atlarsın
- `[G]` **Kayış** — silah değiştirme anında olur

### Ganimet

- `[S]` **Sızıntı Vergisi** — pencereden giren zombi öldürülünce %30 fazla drop
- `[+]` **Mıknatıs** — drop toplama yarıçapı 3x, otomatik toplanır
- `[+]` **Faizci** — harcanmamış puan her tur %2 faiz getirir *(ekonomi build'i)*
- `[+]` **Çöpçü** — barikat tahtası tamiri iki katı puan
- `[+]` **Kasa Hırsızı** — rastgele silah dağıtıcısından (mystery box) çekiş %30 ucuz *(salt istatistik — PILLAR-01'in zayıf saydığı türden; kutu mekaniği tanımlanana kadar askıda)*
- `[G]` **Kumbara** — tur sonunda kalan her mermi puana çevrilir *(mermi biriktirmeyi bir tercih yapar)*
- `[G]` **Pazarlık** — duvardan mermi alımları %25 ucuz *(mermi seferleri sorununa doğrudan dokunur — bkz. design/economy/curves.md)*
- `[G]` **Toplayıcı** — öldürülen her 10. zombi mermi düşürür
- `[G]` **Ganimet Avcısı** — drop'lar iki kat uzun süre yerde kalır
- `[G]` **Vergi Memuru** — takım arkadaşının her öldürmesinden %10 puan alırsın *(co-op'a özel; başkasının iyi oynamasını senin de kazancın yapar — PILLAR-02)*

### Takım kartları (nadir, dördünü birden etkiler, yalnızca co-op)

- `[+]` **Kan Bağı** — biri düştüğünde diğer üçü 10 sn boyunca %30 hasar bonusu alır
- `[+]` **Sigorta** — biri öldüğünde puan cezası yarıya iner
- `[G]` **Omuz Omuza** — 5 m içinde takım arkadaşın varken herkes +%15 hasar
  *(PILLAR-02'ye en doğrudan hizmet eden kart: birlikte durmayı mekanik olarak ödüllendirir)*
- `[G]` **Kalkan Duvarı** — biri barikat tamir ederken 3 m içindekiler hasar almaz
  *(tamir eden savunmasızdır; bu kart onu korumayı bir role dönüştürür)*
- `[G]` **Ortak Kasa** — herkesin kazandığı puanın %10'u ortak havuza gider; kapılar
  yalnızca oradan açılır *(kapı artık kimsenin cebinden çıkmaz — "kim ödeyecek"
  tartışması biter, "ne zaman açalım" tartışması başlar)*

---

## 4b. Kart olmayan şeyler — varsayılana alındı (2026-09-04)

İki kart silindi. İkisi de aynı hatayı yapıyordu: **oyunun kartsız hâlini kötü kılan bir
şeyi kart olarak satmak.** Bir kart oyuna bir şey *eklemeli*, eksikliği kapatmamalı.

| Silinen kart | Ne oldu |
|---|---|
| ~~**Telsiz**~~ | Takım arkadaşının canı, mermisi ve konumu artık **varsayılan olarak görünür** — duvar ardından da |
| ~~**Ortak Cüzdan**~~ | Silindi. Kapı **zaten** herkese açılıyor (aşağıya bak); kartın gerçek etkisi "bir sonraki kapı bedava" idi, yani salt indirim — PILLAR-01'in reddettiği türden |

### Varsayılan co-op görünürlüğü

> **Karar (geliştirici, 2026-09-04):** *"Birimin canını hepsi zaten görebilmeli, duvar
> arkasından da varsayılanda görünür olmalı. Co-op için önemli, zaten bu."*

| Bilgi | Görünürlük | Neden |
|---|---|---|
| Takım arkadaşının **canı** | Her zaman, duvar ardından | PILLAR-02'nin ön şartı: kimin yardıma ihtiyacı olduğunu görmeden yardım edemezsin |
| Takım arkadaşının **konumu** | Her zaman, duvar ardından | Sürüyü bir hatta tutmak koordinasyon ister; nerede olduğunu bilmeden koordine olunmaz |
| Takım arkadaşının **mermisi** | **Duvar ardından GÖRÜNMEZ** | *"Mermiler duvar arkasından gözükmesin, gereksiz"* (geliştirici, 2026-09-04). Doğru: mermi acil bir bilgi değil, ve her şeyi göstermek PILLAR-04'ün reddettiği ekran kalabalığını üretir. Yakındayken/görüş hattındayken görünür |
| **Düşmüş** oyuncu | Her zaman, **en belirgin** işaret | Diriltme co-op'un can damarı; bunu kaçırmak bir tasarım hatasıdır, bir zorluk değil |

**PILLAR-04 kısıtı:** bu işaretler dört oyuncu × sürekli açık demek. Kırk zombinin
ortasında ekranı doldurmamalı — mesafeyle küçülür, doğrudan görüş hattında sönükleşir
(zaten görüyorsun), duvar ardında belirginleşir. Bu bir UX işi:
`design/ux/hud.md`'ye düşer.

### Kapı zaten herkese açılıyor

Geliştirici *"kapıyı kim açarsa açsın diğerlerine de açılmış olmalı, diğer türlü saçma"*
dedi. **Öyle zaten:** `PurchasableDoor` açık durumunu `SyncVar` ile taşıyor ve geç katılan
bir istemci bile kapıyı açık buluyor (M1-09, ADR-0004). Tasarım niyeti buydu ve kod da
öyle yazıldı.

Açık kalan tek soru **kim öder** — ve ona **Ortak Kasa** kartı bir cevap veriyor
(yukarıda).

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

## 7b. Draft sıklığı — HER TUR SONU

> **Karar (geliştirici, 2026-09-04):** *"Her tur sonu kart seçimi olmalı bence."*
> Önceki tasarım her 3 turda birdi (tur 3, 6, 9…). Değişti.

**Her tur sonunda, molada.** 3 karttan 1 seçim, 4 oyuncu eşzamanlı. Boss sonrası
garanti nadir kart kuralı duruyor.

### Neyi düzeltiyor

Her 3 turda bir sabit görünüyordu ama **dakikada değildi**. Tur süreleri büyüdüğü için
draft'lar arası mesafe açılıyordu:

| Draft | Tur | Dakika | Öncekinden fark |
|---|---|---|---|
| 3. | 9 | 10:05 | +5:23 |
| 5. | 15 | 33:07 | **+14:20** |
| 6. | 18 | 56:52 | **+23:45** |

PILLAR-03 draft'ı *"ritmin zirvesi"* diye tanımlıyor. Zirveler arası 24 dakika, ritim
değil bekleyiştir. Her tur sonu bunu kökten çözüyor: **her zirve bir tur uzaklıkta.**

### Bedeli — GOAL-02 ile aritmetik gerilim

`design/00-brief.md`'deki GOAL-02: *"aynı run'da 4 oyuncunun kart yığınları arasındaki
örtüşme **< %40**"*, ölçüm M-03 sonu.

Tur 20'ye kadar (~57 dakika) her tur draft = oyuncu başına **20 kart**.

| Havuz | Beklenen ikili örtüşme | GOAL-02 |
|---|---|---|
| 45 kart | %44 | ✗ kalıyor |
| **55 kart (şu anki havuz)** | **%36** | ✓ ama payı dar |
| 70 kart | %29 | ✓ rahat |

*(Kaba tahmin: N kart / P havuz. Etiket ağırlıklandırması oyuncuları birbirinden
uzaklaştırdığı için gerçek örtüşmeyi **düşürür**; oyuncu tercihi "en iyi kart"ta
yoğunlaştığı için **yükseltir**. İkincisi genelde daha güçlüdür — yani %36 iyimser bir
tahmin.)*

**Sonuç: her tur draft, havuzu 70'e çıkarmayı zorunlu kılıyor.** Kapsam dokümanındaki
"MVP 25 → yayında 45" hedefi her 3 turda bir için yazılmıştı; her tur draft'la birlikte
o sayı yetmiyor. Şu anki havuz 55 — yayın hedefi **70+** olmalı.

### İkinci bedeli — PILLAR-03 ile gerilim

Draft artık **her turda** araya giriyor. PILLAR-03 kart ekranını *"oyunun tek beklemeli
anı"* olarak kabul ediyor, ama o kabul üç turda bir içindi. Her turda bir bekleme,
"kesintisiz tur" sözünü sıklık üzerinden aşındırabilir.

Bunu yönetmenin yolu **draft'ı hızlı tutmak**, ve bu doğrudan §7c'deki yenileme
tasarımıyla çelişiyor: yuva başına iki yenileme × üç yuva = tur başına altı ek karar.
**Bu gerilim çözülmedi;** oyun testinde ölçülecek. İlk ölçüt: draft ekranında geçen
sürenin tur süresine oranı. %15'i aşıyorsa ya yenileme kısılır ya draft seyrekleşir.

---

## 7c. Yenileme (reroll) — 2026-09-04

> **Karar (geliştirici):** *"Kartı beğenmezse yenileme hakkı olmalı, üç seçenek için
> ayrı ayrı. Beğenmezse yenilediği hâlde bu sefer puan harcayarak yenileyebilir.
> Puanlı yenileme sınırı 1 kere olmalı."*

**Yuva başına**, tüm ekranı birden değil. Üç yuvanın her biri bağımsız yenilenir.

| Sıra | Ne | Bedel | Sınır |
|---|---|---|---|
| 1 | İlk yenileme | **Ücretsiz** | Yuva başına 1 |
| 2 | İkinci yenileme | **Puan** | Yuva başına 1 |
| 3 | Üçüncü | — | **Yok** |

Yani bir draft'ta en fazla: 3 ücretsiz + 3 puanlı yenileme.

**Neden yuva başına, hepsi birden değil:** "üçü de kötü" ile "ikisi iyi, biri kötü"
farklı durumlar. Hepsini birden yenilemek, beğendiğin iki kartı da atmaya zorlar —
yani yenileme bir çözüm değil, bir kumar olur.

### Puanlı yenilemenin fiyatı TURLA ARTMALI

Sabit bir fiyat geç turlarda bedavaya döner: simülasyona göre tur 15'te oyuncunun
elinde binlerce puan birikiyor. 200 puanlık bir yenileme tur 3'te gerçek bir karar,
tur 15'te hiçbir şey.

Öneri: `yenilemeBedeli = temel × tur`. Sayılar `config/balance/cards.json`'a girecek ve
değerini `systems-designer` verecek — **burada sayı yazmıyorum**, çünkü bu bir denge
kararı ve oynanmadan verilmez.

### Sıfırlanma (karar: geliştirici, 2026-09-04)

> *"Ücretsiz yenileme yuva başına, her tur bu özellik sıfırlanır."*

Yani **her draft'ta yeniden 3 ücretsiz yenileme** (yuva başına bir tane). Biriktirilmez,
devredilmez.

Bu, her tur draft kararıyla birlikte okunmalı: draft artık her tur geldiği için ücretsiz
yenileme de her tur yenileniyor. Toplamda tur 20'lik bir run'da 60 ücretsiz yenileme
hakkı — kulağa çok geliyor ama **kullanılmak zorunda değil** ve asıl işlevi şu: kötü bir
üçlü gördüğün turda takılıp kalmıyorsun.

**İzlenecek risk:** yenileme ücretsiz ve her tur sıfırlanıyorsa, oyuncunun "beğenene
kadar çevir" alışkanlığı geliştirmesi mümkün — ki bu draft süresini uzatır ve §7b'deki
%15 eşiğini zorlar. Yuva başına **tek** ücretsiz hak bunu sınırlıyor (üç yuvayı da
çevirdiğinde elinde yine üç kart var, seçmek zorundasın), ama oyun testinde bakılacak.

---

## 7d. Puan harcama düzeni — tek tablo

> **Karar (geliştirici):** *"Puanla alınabilecek kart gibi özellikler, varsayılan
> alışveriş düzeni oluşturmak lazım — hem kart seçiminde hem de shop için."*

Puan tek para birimi. Bugün üç musluğu var (mermi, kapı, kutu) ve **hepsi erken oyun
için**. Simülasyon geç oyunda harcanacak bir şey kalmadığını gösterdi: iyi oyuncu tur
15'te 15.620 puanla oturuyor (`design/economy/curves.md`, KIRILMA 2).

Kart ekonomisi tam olarak o boşluğa oturuyor.

| Harcama | Nerede | Ne zaman | Fiyat davranışı | Amacı |
|---|---|---|---|---|
| Mermi | Duvar | Her zaman | Sabit | Erken oyun musluğu (mevcut) |
| Kapı | Harita | Her biri bir kez | Sabit bant | Harita büyümesi (mevcut) |
| Rastgele dağıtıcı | Harita | Her zaman | Sabit | Kumar (mevcut, mekaniği `<tbd>`) |
| **Kart yenileme** | Draft ekranı | Yuva başına 1 | **Turla artar** | Draft'ta ajans |
| **Temel yükseltmeler** | Tezgâh, tur arası | Her zaman | **Her alışta artar** | **Geç oyun sink'i** (§7e) |
| **Kart unutma** | Tezgâh | Her zaman | Orta | Yanlış build'i düzeltme |

### İki kural

**1. Draft ücretsiz kalır.** Tur başına bir kart, her zaman, puansız. Puan yalnızca
*daha iyi seçenek* satın alır (yenileme), *daha fazla kart* değil. Aksi hâlde iyi oynayan
oyuncu daha çok kart alır ve makas açılır — PILLAR-02'nin açıkça reddettiği şey:
*"iyi oynayanı güçlendirip zorlananı geride bırakan ödül yapıları"*.

**2. Tezgâh KART SATMAZ.** Tezgâhın işi temel yükseltmeler (§7e); kimlik kartlardan
gelir, tezgâhtan değil. Tezgâh kart satsaydı yeterince puan biriktiren herkes aynı
build'e giderdi — GOAL-02'yi doğrudan çürütür.

### Kart unutma neden var

20 kartlık bir run'da erken alınan kötü bir kart 50 dakika taşınıyor. Unutma, o kararı
geri alınabilir yapıyor — ama bedelli, yani hâlâ bir karar. Etiket bonusları (3 kart =
bonus) yüzünden ayrıca taktiksel: iki etiket arasında kalmışsan birini bırakıp diğerini
tamamlayabilirsin.

**Kapsam uyarısı:** unutma yeni bir ekran, yeni bir akış ve etiket bonuslarının yeniden
hesaplanması demek. M-03 için **isteğe bağlı** işaretli; kesilirse ilk kesilecek şey bu.

---
## 7e. Tezgâh — temel yükseltmeler (2026-09-04)

> **Karar (geliştirici):** *"Bazı temel özellikleri satın alabiliyor olacaksın. Maks can
> artımı, hasar artımı, saldırı hızı artımı, mermi kapasitesi artımı. Bunlar minör artış
> olmalı ve her alışta puan etkisi artmalı ki tamamen oraya odaklanılmasın — ama bir tık
> 'şunu da alayım' dedirtmeli."*

Tur arası açılan tezgâh. Dört hat, her biri bağımsız sayaç tutar.

| Hat | Ne artar | Neden bu dört |
|---|---|---|
| **Dayanıklılık** | Maks can | Hayatta kalma tabanı |
| **Güç** | Hasar | Zombi canı bileşik büyüyor; taban hasarın da büyümesi gerek |
| **Tetik** | Atış hızı | Kalabalık temizleme hızı |
| **Şarjör** | Mermi kapasitesi | **Mermi seferleri sorununa doğrudan dokunur** (`design/economy/curves.md`) |

### Artışlar KÜÇÜK olmalı, fiyat ARTAN

Bu, tasarımın kalbi ve senin cümlen zaten doğru kuralı söylüyor:

```
n. alımın fiyatı = temelFiyat × büyüme^n
```

**Neden artan fiyat:** sabit fiyatta oyuncu bir hattı sonuna kadar basar ve tek boyutlu
bir güç eğrisi çıkar. Artan fiyat, dördüncü Güç alımını beşinciden daha cazip yapar ve
oyuncuyu ya yaymaya ya durmaya iter — yani **her alış bir karar kalır**, alışkanlık
olmaz.

**Neden küçük artış:** tezgâh **kimlik vermemeli.** SYS-02 §1'in üç katmanı (kart /
silah alışkanlığı / drop) birbirine dik duruyor ve tezgâh dördüncü bir katman. Karışmaması
için tezgâhın rolü açıkça **taban** olmalı, tepe değil: sıkıcı, küçük, kümülatif. "Ben
yanıcı-kan build'iyim" cümlesi kartlardan gelir; tezgâh o cümleyi değiştirmez, altını
sağlamlaştırır.

**Sayılar burada yazmıyor.** Temel fiyat, büyüme çarpanı, artış adımı ve hat başına tavan
`config/balance/shop.json`'a girecek ve değerini `systems-designer` verecek. Şema
açıklamaları "aralığın dışına çıkarsan oyuncu ne hisseder" cümlesini taşıyacak.

### Tavan gerekli mi — açık soru

Artan fiyat tek başına yumuşak bir tavan üretiyor. Ama geç turlarda puan da bileşik
büyüdüğü için "pahalı" göreli bir kavram: tur 20'de 10.000 puanı olan oyuncu için
5.000'lik bir alım ucuz. **Öneri:** hat başına sert bir tavan (ör. 10 kademe) ve tavana
ulaşınca hattın kapanması. Karar `systems-designer` + oyun testi.

---

## 7f. Yıpranma ekonomisi — tur sonu kısmi yenilenme

> **Karar (geliştirici, 2026-09-04):** *"Ekonomiyi zorlu ve heyecanlı kılmak için her tur
> sonu barikatlar, mermiler ve canlar %100 değil %40–60 falan olmalı. Piyasadakilere
> kıyasla var mı böyle bir mekanik?"*

**Var, ve iyi çalışıyor.** Ayrıca şu an bizde tam tersi yapılıyor.

### Şu an ne oluyor

`WindowBarricade` her tur başında `ResetBarricade()` çağırıyor ve barikat
`boards.startingCount: 4 / perWindow: 4` ile **%100'e** dönüyor. Yani tur içinde yapılan
tamir bir sonraki tura taşınmıyor; tamir etmemenin bedeli yalnızca o turla sınırlı.

### Piyasada karşılığı

| Oyun | Mekanik | Ne öğretiyor |
|---|---|---|
| **Deep Rock Galactic** | İkmal kapsülü her kullanımda **~%50** mermi verir; kapsülün 4 hakkı var ve nitra ile çağrılır | En yakın örnek ve **co-op**: "bu ikmali kim alsın" gerçek bir takım anı üretiyor — doğrudan PILLAR-02 |
| **Darktide / Vermintide 2** | İki katman: **toughness** hızlı yenilenir, **health** yenilenmez — yalnızca kısmi iyileştirilir | Yıpranmayı hız kaybetmeden üretmenin yolu: bir katman affeder, biri affetmez |
| **Left 4 Dead** | Haplar geçici ve azalan can; çanta %80 iyileştirir, %100 değil | Tam iyileşme yoksa her hasar bir borç bırakır |
| **Killing Floor 2** | Dalga arası tüccar: mermi **satın alınır**, bedava gelmez | Bizim duvar silahımızın aynısı |
| **CoD Zombies (klon tabanı)** | Barikat otomatik onarılmaz, oyuncu tamir eder; can **tam** yenilenir | **Bizim şu anki modelimiz** — ve senin değiştirmek istediğin şey |

### Canla çelişki — çözülmesi gerekiyor

Bir sorun var: **3 Eylül'de "vurulmayınca can tam yenilenir" kararını sen verdin**
(`player.json`, gerekçe ÇK-17: kalıcı hasar birikimi 20 dakikalık oturumda ceza hissi
yaratır). Tur sonu %40–60 can, tam yenilenmeyle **bağdaşmaz** — zaten yenileniyorsa tur
sonu tavanının hiçbir etkisi olmaz.

İki karar birbirini iptal ediyor. Üç çıkış yolu:

| Yol | Ne olur | Bedeli |
|---|---|---|
| **A — İki katman** *(önerilen)* | Hızlı yenilenen bir **kalkan** + yavaş/kısmi yenilenen **can**. Kalkan kaçmayı ödüllendirmeye devam eder; can run boyunca yıpranır | Yeni bir sistem, yeni HUD. Darktide'ın kanıtlanmış modeli |
| **B — Yenilenmeyi kaldır** | Can yalnızca tur sonu kısmi dolar | 3 Eylül'ün gerekçesi geri gelir: ceza hissi, ÇK-17 riski |
| **C — Yenilenme tavanı** | Can yenilenir ama yalnızca "tur tavanına" kadar; tavan her tur düşer | Tek sistem, ama oyuncuya anlatması zor: neden dolmayı bıraktı? |

**A öneriliyor** çünkü ikisini de koruyor: kaçmanın ödülü (kalkan) ve yıpranma (can).
Ama **karar senin** ve bu bir tasarım kararı, bir uygulama detayı değil.

### Barikat ve mermi

Bunlarda çelişki yok, tasarım doğrudan uygulanabilir:

| | Öneri |
|---|---|
| **Barikat** | Tur başında **%100'e dönmez**; eksik tahtaların bir kısmı geri gelir (ör. +2/4). Oyuncunun tamir ettiği tahta **kalır** — yani tamir etmek artık bu turu değil, bütün run'ı etkiler |
| **Mermi** | Tur sonu **ücretsiz kısmi ikmal** (kapasitenin %40–60'ı). Duvar satın almasının yerine geçmez, tabanını kurar |

**Barikatta önemli nüans:** tur başında %40–60'a **düşürmek** yanlış olur — tamir eden
oyuncuyu cezalandırır. Doğrusu eksiği **kısmen kapatmak**: 0/4 ile bitiren 2/4 ile
başlar, 4/4 ile bitiren 4/4 kalır. Böylece tamir etmek ödüllendirilir, ihmal etmek
birikir.

**Mermide beklenmedik bir yan fayda:** simülasyon tur 14'te oyuncunun duvara **15 ayrı
sefer** yaptığını gösterdi (`design/economy/curves.md`, KIRILMA 1). Tur sonu kısmi ikmal
o sefer sayısını doğrudan düşürüyor — yani bu karar iki sorunu birden çözüyor.

### Kapsam uyarısı — M-01'e GİRMEZ

Bunların hiçbiri M-01'e girmemeli. Sebebi süre değil, **ölçüm**: M-01 bilerek bir
**kontrol grubu** ("klon taban"). Yukarıdaki tablonun son satırı bunun klasik modelden
ayrıldığını gösteriyor — yani bu bir **farklılaştırıcı**, tam da projenin ihtiyacı olan
şey. Ama farklılaştırıcıyı kontrol grubunun içine koyarsan, karşılaştıracak bir taban
kalmaz ve ÇK-17'nin cevabı yorumlanamaz olur.

**Hedef: M-02.** M-01 önce oynanır, ÇK-17 cevaplanır, sonra bu gelir ve "bundan daha iyi
mi?" sorusu sorulabilir hâle gelir.

---


## 8. Açık kalanlar

- **Ücretsiz yenileme yuva başına mı, draft başına mı?** (§7c) — **onayına ihtiyaç var**
- Toplam kart sayısı: havuzda **55** var, her tur draft için hedef **70+** (§7b)
- Nadirlik kademeleri ve draft'taki dağılımı — `<tbd>`
- Yenileme ve kart satın alma fiyat eğrisi — `systems-designer`, `cards.json`
- **Kart unutma** M-03'te isteğe bağlı; kesilecek ilk madde (§7d)
- **Can modeli çelişkisi (§7f)** — tur sonu kısmi can, 3 Eylül'deki "tam yenilenme"
  kararıyla bağdaşmıyor. Üç yol sunuldu (iki katman / yenilenmeyi kaldır / tavan);
  **A önerildi, karar geliştiricinin**
- Tezgâhta hat başına sert tavan olsun mu (§7e) — `systems-designer` + oyun testi
- Tezgâh fiyat eğrisi ve artış adımları — `config/balance/shop.json`, henüz yok
- Rastgele silah dağıtıcısının (mystery box) mekaniği hiç tarif edilmedi — yalnızca bir
  fiyatı var. **Kasa Hırsızı** kartı ona bağlı olduğu için o da askıda
- Draft ekranında geçen sürenin tur süresine oranı ölçülecek; %15'i aşarsa yenileme
  kısılır ya da draft seyrekleşir (§7b)
- Denge sayıları `config/balance/cards.json` içinde yaşar, C# içinde değil
- **Temizlik** (50 öldürmede nuke) sayacı oyuncu başına mı, takım toplamı mı? Öneri:
  oyuncu başına — takım toplamı olursa 4 kat sık patlar ve kişisel build olmaktan çıkar
