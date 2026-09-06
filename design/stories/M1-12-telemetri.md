# M1-12 — Telemetri: ulaşılan tur, ölüm yeri, oturum süresi

| Alan | Değer |
|---|---|
| **Durum** | In review |
| **Milestone** | M-01 |
| **Tip** | Infra |
| **Sahip** | `systems-programmer` |
| **Assembly** | `Bunker.Systems` (+ `Bunker.Gameplay`'de ince bir köprü) |
| **Tahmin** | 1 gün |
| **Bağımlılık** | M1-11 ✅ (kod bitti) — `RunRecorder` bu işin veri kaynağı |
| **Sistem** | SYS-01 |
| **Pillar** | — (ölçüm altyapısı) |

---

## Tasarım niyeti

M-01'in çıkış kriterlerinden ikisi **ölçüm** istiyor, kanaat değil:

- **ÇK-13** — "Tur 10'a ulaşmak mümkün ve ~15 dakika sürüyor" → *Ölçüm: telemetri*
- **ÇK-17** — "20 dakika oynadıktan sonra tekrar oynamak istiyorsun" → *dürüst öz
  değerlendirme + en az 2 arkadaş denemesi*

ÇK-17 bir histir ve sayı ile ölçülmez. Ama **kaç run oynandığı**, **ne kadar sürdüğü** ve
**nerede bittiği** ölçülür — ve "tekrar oynamak istedim" cümlesinin arkasında gerçekten
oynanmış on run mu, yoksa iki run mu var, onu yalnızca bu dosya söyler.

`SYS-01`'in mimari notu bu işi baştan istiyordu: *"Bu metrikler sürekli ölçülür → baştan
bir olay/telemetri hattı gerekir. Sonradan eklemek her sisteme tek tek dokunmak
demektir."* M1-11 sayaçları zaten kurdu; burada yapılacak tek şey onları **diske
yazmak**.

**Kapsam bilerek dar.** Kapsam dokümanı beş metrik sayıyor (ulaşılan tur, kart seçimleri,
ölüm yeri, alınmayan silahlar, oturum süresi ve terk noktası). Üçü M-01'de var olan
şeyler; kart ve silah çeşitliliği M-02/M-03'ün konusu ve o sistemler henüz yok. **Var
olmayan bir sistemin telemetrisi yazılmaz.**

---

## Kabul kriterleri

**AC-1 — Her run bir satır yazar**
> **Given** bir run bitti
> **When** skor ekranı geldi
> **Then** telemetri dosyasına **tam bir JSON satırı** eklenir; önceki run'ların
> satırları olduğu gibi durur.

**AC-2 — Satır run'ın gerçeğini taşır**
> **Given** yazılmış bir satır
> **When** okunur
> **Then** ulaşılan tur, süre, öldürme sayıları ve kazanılan puan skor ekranında
> gösterilenle birebir aynıdır. **Ölüm yeri ekranda gösterilmez, yalnızca
> kaydedilir** — koordinat gri kutu ekranında gürültüdür; veri ısı haritası için
> tutulur ve doğruluğu birim testiyle kanıtlanır.

**AC-3 — ÇK-13 bu dosyadan cevaplanabilir**
> **Given** tur 10'a ulaşılmış bir run
> **When** satır okunur
> **Then** her turun **kaçıncı saniyede başladığı** yazılıdır, yani "tur 10'a kaç
> dakikada ulaşıldı" sorusu run'ı tekrar oynamadan cevaplanır.

**AC-4 — Yazma oyunu bozmaz**
> **Given** telemetri yazılamıyor (disk dolu, klasör salt okunur, yol geçersiz)
> **When** run biter
> **Then** oyun çalışmaya devam eder, skor ekranı gelir, R çalışır; hata **bir kez**
> loglanır ve her run'da tekrar tekrar bağırmaz.

**AC-5 — Dosya bozulmaz**
> **Given** yazma sırasında oyun çöker ya da kapatılır
> **When** dosya sonradan okunur
> **Then** önceki satırlar okunabilir durumdadır. Yarım bir satır, dosyanın tamamını
> okunmaz yapmaz.

**AC-6 — Türkçe Windows'ta da geçerli JSON**
> **Given** sistem yerel ayarı `tr-TR` (ondalık ayırıcı **virgül**)
> **When** bir satır yazılır
> **Then** sayılar `123.45` biçimindedir, `123,45` değil — yoksa üretilen dosya JSON
> olmaktan çıkar ve bunu okumaya çalışana kadar kimse fark etmez.

---

## Mimari yönlendirme

**Kısıt:** `Bunker.Systems` asmdef'i `noEngineReferences: true` taşır — Unity tiplerini
**göremez**. Yani `Application.persistentDataPath`, `Debug.Log` ve `Vector3` burada
kullanılamaz. Bu kısıt bir engel değil, işin doğru şeklini dayatıyor: yazma mantığı saf
.NET olur ve **gerçek dosya sistemine karşı, Unity açmadan test edilir** (ÇK-16).

| Tip | Ne | Nerede |
|---|---|---|
| `RunSummary` | Bir run'ın diske yazılan hâli. M1-11'den geliyor; ölüm yeri ve tur zamanları burada eklendi — **ayrı bir kayıt tipi yaratılmadı**, iki tipin er geç ayrışması demek olurdu. | `Systems/Rounds/RunRecorder.cs` |
| `RunLogWriter` | Saf C#. Bir klasör yolu alır, JSONL satırı ekler. Hatayı bir kez bildirir. | `Systems/Telemetry/RunLogWriter.cs` |
| `TelemetryBootstrap` | Unity köprüsü: yolu verir, `RunSignals.RunEnded`'a abone olur. | `Gameplay/TelemetryBootstrap.cs` |

**Neden JSONL (satır başına bir JSON nesnesi), tek bir JSON dizisi değil:**
bir diziye eklemek dosyanın sonunu okuyup yeniden yazmayı gerektirir; çöken bir oyun o
sırada dosyanın **tamamını** bozar. Satır eklemek atomiktir — çöken oyun en fazla son
satırı yarım bırakır ve öncekiler okunur kalır (AC-5).

**Nereye yazılır:**

| Nerede çalışıyor | Yol | Neden |
|---|---|---|
| Editör | `<proje kökü>/telemetry/runs.jsonl` | Geliştirici dosyayı bulabilsin. `Assets/` **dışında** — içinde olsa Unity onu her yazmada içe aktarır ve editör takılır. |
| Player build | `<persistentDataPath>/telemetry/runs.jsonl` | Kurulu bir oyun kendi klasörüne yazamaz. |

`telemetry/` git'e girmez (`.gitignore`): bu ölçüm verisi, kaynak değil.

**Ölüm yeri neden üç float:** `Systems` `Vector3`'ü göremez. Köprü ayrıştırır. Yan
faydası, satırın hangi motorla üretildiğinden bağımsız okunabilir olması.

---

## Config anahtarları

**Yok.** Telemetride ayarlanacak bir denge sayısı yoktur. Dosya yolu bir mühendislik
sabiti, tur eşiği diye bir şey de yok — her run yazılır.

---

## Dokunulacak dosyalar

**Yeni**
- `Assets/_Project/Code/Systems/Telemetry/RunLogWriter.cs` (`Bunker.Systems`)
- `Assets/_Project/Code/Gameplay/TelemetryBootstrap.cs` (`Bunker.Gameplay`)
- `Assets/_Project/Code/Tests/RunLogWriterTests.cs`
- `.claude/tools/telemetry.ps1` — biriken satırları özetler

**Değişen**
- `Assets/_Project/Code/Systems/Rounds/RunRecorder.cs` — ölüm yeri + tur başlangıç zamanları
- `Assets/_Project/Code/Gameplay/PlayerHealth.cs` — öldüğü yeri bildirir
- `.gitignore` — `/telemetry/` (**köke sabitli**; sabitlenmezse `Systems/Telemetry/` kaynak klasörünü de yutar)

---

## Kapsam dışı

- Kart seçimi ve silah satın alma telemetrisi — o sistemler M-02/M-03'te
- Uzak sunucuya gönderme, analitik servisi, oyuncu kimliği — **hiçbiri M-01'de yok ve
  hiçbiri gizlilik onayı olmadan yazılmaz**
- Isı haritası görselleştirmesi — ölüm yeri **kaydedilir**, çizilmez
- Kare süresi / performans telemetrisi — `FrameTimeRecorder` ve `/perf-check`'in işi
- Terk noktası (oyunu kapatma anı) — çıkışta yazmak güvenilmez; M-02'de oturum
  kavramıyla birlikte

---

## Test senaryoları

`RunLogWriter` (EditMode, saf C#, geçici klasöre gerçek dosya yazarak):
- AC-1: iki run yazılır, dosyada iki satır olur, birincisi bozulmaz
- AC-2: yazılan satır ayrıştırıldığında sayılar birebir geri gelir (`Json.Parse` ile)
- AC-3: tur zamanları sırayla ve artan yazılır
- AC-4: yazılamayan yol oyunu bozmaz, hata **bir kez** bildirilir
- AC-6: `tr-TR` kültürü kurulup ondalık noktanın nokta kaldığı doğrulanır
- Kenar: sıfır turluk run, ölüm yeri hiç bildirilmemiş run, çok uzun run

`RunRecorder` (mevcut testlere ek):
- Ölüm yeri bir kez yazılır, dondurulduktan sonra değişmez
- `Reset()` ölüm yerini ve tur zamanlarını da temizler

---

## Gereken kanıt

| Kanıt | Nasıl |
|---|---|
| EditMode testleri yeşil | `.claude/tools/unity-test.ps1` |
| Derleme temiz | `.claude/tools/unity-log.ps1 -Errors` |
| **Gerçek dosya** | Play → öl → `telemetry/runs.jsonl` gerçekten yazıldı mı, içindeki sayılar skor ekranıyla aynı mı |
| Özet aracı | `.claude/tools/telemetry.ps1` biriken run'ları okuyup özetliyor |

---

## Sonuç (2026-09-03)

**Durum:** In review — kod bitti, **oyun testi bekliyor**.

| Kriter | Durum | Nerede kanıtlandı |
|---|---|---|
| AC-1 her run bir satır yazar | ✅ | `RunLogWriterTests.AC1_*` (2 test) |
| AC-2 satır run'ın gerçeğini taşır | ✅ | `AC2_*` (3 test) |
| AC-3 ÇK-13 dosyadan cevaplanır | ✅ | `AC3_*` (2 test) + `RunRecorderTests.AC3_*` (4 test) |
| AC-4 yazma oyunu bozmaz | ✅ | `AC4_*` (2 test) |
| AC-5 dosya bozulmaz | ✅ | `HerSatir_TekSatirdirVeSatirSonuylaBiter` — JSONL'in tasarım gerekçesi |
| AC-6 Türkçe Windows'ta geçerli JSON | ✅ | `AC6_TurkceKulturde_OndalikAyiriciNoktaKalir` (gerçek `tr-TR` kültürü kurulur) |

**Testler:** 234/234 yeşil (212 → +22). **Derleme:** temiz.

**AC-2 düzeltmesi:** ölüm yeri **skor ekranında gösterilmez**, yalnızca kaydedilir.
Oyuncuya koordinat göstermek gri kutu ekranında gürültüdür; veri ısı haritası için
tutuluyor. Doğruluğu birim testiyle kanıtlanıyor.

**CR-CODE: CONDITIONAL → düzeltildi.**

| Ağırlık | Bulgu | Düzeltme |
|---|---|---|
| MAJOR | `Append` yalnızca dört IO istisnası yakalıyordu. Bu yazıcı `RunEnded`'ın **ilk** abonesi; kaçan bir istisna yayın zincirini keser, **skor ekranı hiç gelmez** ve imleç kilitli kalır | `catch (Exception)` — gerekçesi kodda yazılı |
| MINOR | AC-2 "skor ekranıyla aynı" diyordu ama ölüm yeri ekranda yok | AC yeniden yazıldı (yukarıda) |
| MINOR | F7/F8 ile atlanan turlar uydurma zaman damgası alıyor ve ÇK-13 ortalamasını sessizce bozuyordu | `usedRoundSkip` satıra yazılıyor, özet aracı bu run'ları ÇK-13 hesabına katmıyor |
| NOTE | Proje kökü bulunamazsa sessizce `persistentDataPath`'e kayıyordu | Yüksek sesle loglanıyor |
| NOTE | Gereksiz `-=` hakkındaki yorum yanıltıcıydı | Yorum düzeltildi |

**Ayrıca yakalandı (incelemenin dışında):** `.gitignore`'a eklenen `telemetry/` satırı
kök'e sabitlenmemişti ve `Assets/_Project/Code/Systems/Telemetry/` **kaynak klasörünü
de yutuyordu** — `RunLogWriter.cs` hiç commit edilmeyecekti, sessizce. `/telemetry/`
olarak düzeltildi.

**Oyun testinde bakılacaklar:**

1. Play → öl → proje kökünde `telemetry/runs.jsonl` gerçekten oluştu mu?
2. İçindeki sayılar skor ekranındakiyle aynı mı?
3. Birkaç run sonra `.claude/tools/telemetry.ps1` ne diyor?
