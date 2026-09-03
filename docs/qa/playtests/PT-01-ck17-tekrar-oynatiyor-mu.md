# PT-01 — ÇK-17: 20 dakika sonra tekrar oynatıyor mu?

| Alan | Değer |
|---|---|
| **Durum** | Protokol hazır — **koşulmadı** |
| **Soru** | "20 dakika oynadıktan sonra tekrar oynamak istiyor musun?" |
| **Kaynak** | M-01 ÇK-17 — milestone'un karar kriteri |
| **Sahibi** | `playtest-analyst` |
| **Build** | M-01 gri kutu, sanatsız. 13 işin 13'ü kod olarak bitti, **9'u hiç oynanmadı** |
| **Tarih** | Protokol 2026-09-03 |

---

## Önce: bu test neyi ölçemez

M-01 **bilerek** yalın bir kontrol grubu. Kart yok (PILLAR-01), dört oyuncu bağımlılığı
yok (PILLAR-02). Yani "hayır, tekrar oynamak istemiyorum" cevabının **iki farklı sebebi**
olabilir ve bunları ayırmak bu protokolün asıl işi:

| | Sebep | Ne demek |
|---|---|---|
| **(a)** | **Temel bozuk** | Silah sünger hissediyor, zombi okunmuyor, tur ritmi tıkanıyor. Tasarım/his sorunu, düzeltilebilir. |
| **(b)** | **Temel doğru ama ince** | Nişan almak, kaçmak, barikat tutmak tatmin ediyor; ama 20 dakika sonra "aynı şeyin tekrarı" hissi var, çünkü kart/build/takım katmanı henüz yok. |

M-01 ikisini de aynı şekilde durduruyor ("ÇK-17 hayır → M-02'ye geçilmez"). **Ama
söyleyeceğimiz şey farklı.** (a) ise M1-06 ve M1-13'e geri dönülür. (b) ise gerçek soru
şuna dönüşür: *temel, kart gelene kadar taşıyabilecek kadar iyi mi?*

Gözlem çizelgesi ve seans sonrası sorular bu ayrımı yapabilmek için tasarlandı. İkili bir
evet/hayır tek başına yetmez.

---

## Üç seans, bu sırayla

| Seans | Kim | Ne için | Ön koşul |
|---|---|---|---|
| **A — Shakedown** | geliştirici, editörde | **Fun kararı değil.** Engel temizliği: dokuz oynanmamış işin her biri çalışıyor mu | yok |
| **A-Doğrulama** | geliştirici, editörde, 20-25 dk | İlk dürüst ÇK-17 okuması, kapalı zarf tahminleriyle | A temiz |
| **B — Asıl test** | **en az 2 arkadaş** | ÇK-17'nin kendisi | A-Doğrulama + **standalone build** |

> **SEANS B'nin ön koşulu bir build.** Arkadaşlar senin editöründe oynayamaz ve bu
> projede **hiç build alınmadı**. `/build` SEANS A temiz çıktıktan sonra koşulmalı.

---

## SEANS A — shakedown kontrol listesi (~20-25 dk)

Geçen oturumun beş hatasının ortak dersi: **sessiz başarısızlık en pahalı hata türü.**
Beşinin dördü hiçbir hata mesajı vermedi, hiçbiri çökmedi — sadece *beklenen bir şey
olmadı*. Bu yüzden her satırda ayrı bir "sessiz arıza işareti" sütunu var: **hiçbir şey
olmadığında bunu fark etmenin yolu.**

| İş | Tek bakış | Sessiz arıza işareti |
|---|---|---|
| M1-03 harita | Koşu döngüsünde tıkanma / çıkmaz nokta var mı | Bir köşede sıkışıp NavMesh dışına çıkmış gibi duruyorsun |
| M1-04 zombi | Pencereden gerçekten içeri giriyor mu; kovalama görüş alanına girince başlıyor mu | Zombi pencerenin önünde duruyor ama **içeri geçmiyor** — bunu "yavaş" ile karıştırma |
| M1-05 doğum/havuz | Tur ilerledikçe sayı gerçekten artıyor mu. **BUG-005:** havuzdan çıkan zombi eski ölüm yerinde belirmiyor | Yeni doğan zombi hiç görünmüyor (sayıya girdi, sahnede yok) |
| M1-06 silah | **BUG-001:** üçüncü zombiden sonra da mermi hasar veriyor mu. **BUG-002:** ard arda hızlı tıklamada meşru atış reddedilmiyor mu | İsabet işareti çıkıyor ama can düşmüyor |
| M1-07 bıçak | V'ye basınca görünür bir vuruş/tepki var mı | Tuşa basılıyor, hiçbir şey olmuyor, zombi de ölmüyor |
| M1-08 barikat | **BUG-003:** tahta sökülüp tamir edildikten sonra doğum durmadan devam ediyor mu | Barikat tamir edildi ama yeni zombi hiç gelmiyor |
| M1-09 kapı | **BUG-004:** kapı görünür bir nesne mi; satın alınca gerçekten açılıyor mu | Puan düştü ama görsel/geçiş değişikliği yok |
| M1-10 duvar silahı | E ile satın alma çalışıyor, mermi gerçekten ekleniyor mu | Puan düştü ama şarjör sayısı değişmedi |
| **M1-11 ölüm/skor** | Can görünür düşüyor mu; ölünce skor ekranı geliyor mu; R ile **kapılar tekrar kilitleniyor** mu | Can barı hiç hareket etmiyor ama vuruluyorsun (yenilenme çok hızlı olabilir) |
| **M1-12 telemetri** | `telemetry/runs.jsonl` proje kökünde gerçekten oluşuyor mu; `telemetry.ps1` doğru özet veriyor mu | Dosya hiç oluşmuyor, ya da oluşuyor ama boş / bozuk satır var |
| M1-13 vuruş hissi | İsabet tepkisi (sendeleme) görünüyor mu; kafa vuruşu ayrı okunuyor mu | Mermi gidiyor, zombi ölüyor, arada hiçbir görsel tepki yok — **M1-13'ün var oluş sebebi buydu** |

**Çıktı:** bug listesi + "temiz" kararı. Temiz değilse A-Doğrulama ve B **başlamaz** —
sıradaki adım düzeltme, sonra yeniden A.

---

## SEANS A-Doğrulama — kapalı zarf

Yazarın kendi oyununa dair körlüğü ölçülecek veri türlerinin en güvenilmezi. Bunu dürüst
tutmanın tek yolu, **oynamadan önce tahmin yazmak**. Play'e basmadan önce şunları bir
dosyaya yaz:

```
1. 20 dakika sonra R'ye kendiliğimden basacak mıyım?   evet / hayır
2. Kaç gönüllü run oynayacağımı tahmin ediyorum:       ___
3. En sıkıcı olacağını tahmin ettiğim an:              ___________________
```

Seans bitince bunları ekran kaydı ve `telemetry.ps1` çıktısıyla karşılaştır.
**Fark varsa farkın kendisi bir bulgudur.** İki tipik körlük:

- **Batık maliyet** — "bu kadar emek verdim, eğlenceli olmalı"
- **Bilginin laneti** — nereye bakacağını, hangi köşenin tuttuğunu zaten biliyorsun;
  arkadaşların bilmiyor

**F7/F8 kullanma.** Debug tur atlaması ölçümü bozar; telemetri o run'ı ÇK-13 hesabından
zaten çıkarıyor ama ÇK-17 için de temiz olmalı.

---

## SEANS B — arkadaşlarla

### Ne söylenir

Kağıda yazılı, sözlü tekrar yok:

```
Sol tık = ates    R = dolum    V = bicak
E (tut) = tamir   E (bas) = satin al

"Elinden geldigince hayatta kal. Dusunurken sesli dusun."
```

### Ne söylenmez

Barikat mekaniği, kapı ekonomisi, zombi davranışı, tur ritmi, ne kadar süreceği,
"eğlenceli mi bulacaksın" beklentisi. **Hiçbir ipucu. Hiçbir "aslında şöyle yapman
lazım."** Yardım ettiğin an test biter.

F7/F8/F9'dan hiç bahsedilmez.

### Neden kontroller söyleniyor da keşif söylenmiyor

M-01'de tutorial/onboarding **bilerek** yok. Kontrol keşfi ayrı bir testin konusu; şimdi
test edersek ilk 90 saniye tuş aramayla geçer ve asıl soruyu — *çekirdek fiil eğlenceli
mi* — kirletir.

---

## Gözlem çizelgesi

**Geliştirici oynarken not tutamaz.** Çözüm seansa göre değişir:

| Seans | Kim oynuyor | Not nasıl alınır |
|---|---|---|
| A | geliştirici | Ekran kaydı (OBS) + sesli düşünme. Notlar seans **sonrasında** kayıttan çıkarılır |
| A-Doğrulama | geliştirici | Aynı + `telemetry/runs.jsonl` — öznel izlenimin çapası |
| **B** | **arkadaşlar** | Geliştirici **oynamaz, yalnızca gözler.** Kağıda canlı not, OBS zaman damgasına göre |

| Zaman | Ne yaptı | Ne dedi | Bence anlamı ne |
|---|---|---|---|
| 0:07 | | | |
| | | | |

**Kural:** "Ne yaptı" her satırda dolu olmalı. "Ne dedi" boş kalabilir — sessizlik de
veri. **"Bence anlamı ne" oyun sırasında değil, sonradan** doldurulur.

---

## Beş kontrol noktası

| # | Kontrol noktası | Süre | Neye bakılır |
|---|---|---|---|
| 1 | İlk girdi | ≤15 sn | Tereddüt varsa neden — kağıt mı okunmadı, yoksa ekranda ne yapacağı mı belirsiz |
| 2 | Çekirdek fiili anlama | ≤90 sn | **PILLAR-04 burada test edilir:** tehdidi görüp okuyabiliyor mu, yoksa kalabalıkta kayboluyor mu |
| 3 | Döngüyü yardımsız tamamlama | seans içinde | Takılma varsa: sessiz arıza mı, anlama sorunu mu |
| 4 | **Tekrarlamayı SEÇME** | ölümden ≤10 sn | **Asıl ÇK-17 anı.** Kendiliğinden R'ye basar mı. Duraksama, iç çekme, "bir dakika" deyip kalkma — hepsi kaydedilir |
| 5 | Ne yaptığını ifade edebilme | seans sonu | Kafasındaki model, tasarımın `FEELS LIKE` niyetiyle örtüşüyor mu |

**Kontrol noktası 4 belirleyicidir.** Döngüyü tamamlayıp tekrarlamak istemeyen bir
oyuncu, oyunun *bittiğini ama eğlenceli olmadığını* söylemiştir.

Tek başına ölçülmez: hem gözlemle (kendiliğinden R) hem telemetriyle (seans içindeki
gönüllü, F7/F8'siz run sayısı) çapraz kontrol edilir.

---

## Seans sonrası üç soru

Son olan şey hakkında, oyunun tamamı hakkında **asla**:

1. **"Az önce, öldüğün anda ne olduğunu anladın mı?"**
   → PILLAR-04'ün doğrudan testi. Ölüm sürpriz miydi?
2. **"Skor ekranını gördüğünde ilk elin nereye gitti?"**
   → Kontrol noktası 4'ün sözlü teyidi. Davranış zaten kayıtta.
3. **"O son koşuda neyi değiştirirdin?"**
   → *Az önce biteni*, bir sonrakini değil. "Oyunu nasıl geliştirirdin" sorulmaz — o soru
   oynamadıkları hâlde tasarımcı gibi cevap vermeye iter.

Hiçbir soru "eğlendin mi" / "beğendin mi" biçiminde değil.

---

## Neyi yanlışlardı

Çürütülemeyen bir test demodur.

**Varsayım:** çekirdek döngü (ateş et → tur geç → barikat/kapı yönet → öl → tekrar başla)
kendi başına, kart olmadan bile, **gönüllü tekrara** yol açar.

**Çürüten gözlemler:**

- Bir oyuncu kontrol noktası 3'ü tamamlar ama 4'te **hiç duraksamadan kalkar**, R'ye
  basmaz. *(En güçlü çürütme — kararsız kalmadan "hayır".)*
- Telemetri seans boyunca gönüllü **tek bir run** gösterir; sözlü "istekli görünme" bununla
  çelişir.
- Bir oyuncu 90 saniyede kontrol noktası 2'ye ulaşamaz ve seansın geri kalanında hâlâ
  **neyin kendisini öldürdüğünü** söyleyemez. *(PILLAR-04'ün doğrudan reddi.)*
- Geliştiricinin kendi kaydında sıkılma/rutine dönme ifadeleri var ama öz değerlendirmede
  "eğlenceliydi" yazıyor. **Kayıt ile öz-rapor çelişirse kayıt kazanır.**

**Doğrulayan gözlem:** en az 2/3 arkadaş kontrol noktası 4'te duraksamadan R'ye basar
**ve** aynı seansta 3+ gönüllü run oynanır.

---

## Sonuçlar

*(Seanslar koşulduktan sonra doldurulacak. `/playtest` bulgu analizini çalıştırır.)*

| # | Gözlenen | Yorum | Güven | Tasarım mı hata mı |
|---|---|---|---|---|
| | | | | |

**PT-FUN:** *(karar verilmedi)*
