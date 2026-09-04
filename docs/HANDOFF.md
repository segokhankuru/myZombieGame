# Handoff — 2026-09-04

> **2026-09-04 eki (geliştirici AFK'yken yapılanlar).** Aşağıdaki 09-03 özeti hâlâ
> geçerli; bunlar üstüne eklendi:
>
> | Ne | Sonuç |
> |---|---|
> | **Gri kutu görünümü** | Okunabilirlik paleti + atmosfer. F10 atmosferi kapatır (ÇK-17'yi temiz ölçmek için). `docs/art/GREYBOX-PALETTE.md` |
> | **İlk build** | `Bunker.exe` çıkıyor, açılıyor, `Player.log` temiz. `build.ps1`'de iki hata düzeltildi |
> | **NavMesh borcu** | Kapandı: 25 yetim dosya → 1, sebep düzeltildi |
> | **ÇK-15 ölçüldü** | 40 zombi, build'de, gerçek haritada: p99 bütçenin **%8.5**'i. `PerfRunner` otomatik |
> | **ÇK-13 / ÇK-14 modellendi** | `balance-sim.ps1`. İkisi de modelde karşılanıyor — ama **iki kırılma** çıktı |
>
> **Oyun testinde özellikle bunlara bak** (`design/economy/curves.md`):
> 1. **Mermi seferleri.** Model tur 14'te duvara **15 ayrı sefer** öngörüyor. "Tur 10
>    civarında sıkıldım" dersen sebebi büyük olasılıkla budur.
> 2. **Geç oyunda harcanacak şey yok.** İyi oyuncu tur 15'te 15.620 puanla oturuyor.
>
> **Hiçbir denge değeri değiştirilmedi** — simülasyon oyuncunun modeli, oyuncu değil.

---

# Handoff — 2026-09-03

Bir sonraki oturumun ilk okuyacağı özet. Çalışma ağacı temiz, 234 EditMode testi yeşil.

---

## Nerede kaldık

**M-01'in 13 işinin 13'ü de kod olarak bitti. Kalan iş kod değil — oynamak.**

| İş | Durum |
|---|---|
| M1-01 Tur ölçekleme | ✅ 12 test |
| M1-02 Ekonomi | ✅ 18 test |
| M1-03 Gri kutu harita | 🔧 üretildi |
| M1-04 Zombi | 🔧 kod + kurulum bitti |
| M1-05 Doğum, havuzlama, ağ seam'i | 🔧 kod bitti |
| M1-06 Silah | 🔧 kod bitti — `/feel-check` notu **DoD zorunlu** |
| M1-07 Bıçak | 🔧 kod bitti |
| M1-08 Barikat | 🔧 kod bitti |
| M1-09 Kapı | 🔧 kod bitti |
| M1-10 Duvar silahı (M-01'de yalnızca mermi) | 🔧 kod bitti |
| **M1-11 Ölüm / skor ekranı** | 🔧 kod bitti, 35 test |
| **M1-12 Telemetri** | 🔧 kod bitti, 22 test |
| M1-13 Vuruş hissi | 🔧 kod bitti |

**On bir iş "kod bitti, hiç oynanmadı" durumunda.**

---

## Sıradaki tek şey: PT-01

`docs/qa/playtests/PT-01-ck17-tekrar-oynatiyor-mu.md` — protokol hazır, **koşulmadı**.

Milestone'un asıl sorusu: **ÇK-17 — 20 dakika oynadıktan sonra tekrar oynamak istiyor
musun?** Üç seans, bu sırayla:

1. **SEANS A** — geliştirici solo, editörde, ~20-25 dk. **Fun kararı değil**, engel
   temizliği. 11 iş için tek bakışlık kontrol listesi, her birinde "sessiz arıza işareti"
   sütunu.
2. **SEANS A-Doğrulama** — kapalı zarf: Play'e basmadan ÜÇ tahmin yazılır, sonra kayıt ve
   telemetriyle karşılaştırılır. Yazarın kendi oyununa körlüğünün ölçüsü.
3. **SEANS B** — en az 2 arkadaş. **Standalone build gerektirir ve bu projede hiç build
   alınmadı** → önce `/build`.

**Protokolün en önemli kısmı:** M-01 bilerek bir kontrol grubu. Kart (PILLAR-01) ve dört
oyuncu bağımlılığı (PILLAR-02) **yok**. "Hayır" cevabının iki sebebi olabilir — *(a)
temel bozuk* ya da *(b) temel doğru ama kart katmanı henüz yok*. İkisini ayırmak testin
asıl işi; ayıramazsan milestone kararı verilemez.

---

## Oyun şu an ne yapıyor

Play'e bas, kurulum gerekmiyor. 10 saniyelik mola, sonra tur 1. Zombiler pencerelerin
dışında doğuyor, barikatı söküyor, içeri tırmanıyor, kovalıyor, telegraflı vuruş yapıyor.
**Vuruyorlar ve artık ölebiliyorsun.**

```
sol tik  ates        R  dolum        V  bicak
E        tamir (tut) / satin al (bas)
F7/F8    tur atla    F9  sahayi temizle     <- DEBUG, olcumu bozar
```

Ölünce skor ekranı gelir (tur, süre, öldürme, kafa %, bıçak, puan), **R** yeni run
başlatır: saha boş, can tam, puan sıfır, kapılar tekrar kilitli.

---

## Bu oturumda eklenen iki iş

### M1-11 — ölüm, run sonu, skor ekranı (`13e9c89`)

`DebugPlayerHealth` canı sessizce dolduruyordu; run'ın sonu yoktu. Yeni:

- **Yenilenen can** (`RegeneratingHealth`, saf C#): 4 sn vurulmazsan 25/sn dolar. Yeni
  hasar gecikmeyi baştan başlatır. Karar geliştiriciye ait, gerekçe ÇK-17.
- **`RunSignals`** — `RoundSignals`'ın kardeşi. Run sonu **bir kez** olur.
- **`RunRecorder`** (saf C#) — donduktan sonra sağır: havadaki mermi skoru gözün önünde
  değiştirmesin.
- Yeni config alanı `player.json`.

### M1-12 — telemetri (`3a56ca2`)

Her run `telemetry/runs.jsonl`'a bir JSONL satırı: tur, süre, öldürmeler, puan, **ölüm
yeri**, **her turun kaçıncı saniyede başladığı**.

```
powershell -NoProfile -File .claude/tools/telemetry.ps1
```

ÇK-13'ü doğrudan cevaplar ve tur başına süreyi çizer. `-Runs` ve `-Deaths` bayrakları var.

---

## Bu oturumun dersleri

| Ne | Ders |
|---|---|
| **Girdi kesme yarımdı** | M1-11'de girdiyi yalnızca `PlayerController`'da kestim; ateş, bıçak, tamir ve satın alma skor ekranının arkasında çalışmaya devam ediyordu. Kod incelemesi BLOCKER olarak yakaladı. **Bir kuralı bir yerde uygulamak, uygulamak değildir.** |
| **`.gitignore` kaynağı yuttu** | `telemetry/` satırı köke sabitlenmemişti ve `Assets/_Project/Code/Systems/Telemetry/` kaynak klasörünü de eşleştiriyordu — `RunLogWriter.cs` hiç commit edilmeyecekti, **sessizce**. `/telemetry/` ile düzeltildi. Geçen oturumun dersi bu oturumda yeni bir kılıkta geri geldi. |
| **Türkçe Windows JSON'u bozar** | `12.5f.ToString()` bu makinede `"12,5"` üretir; kültürsüz yazılan telemetri geçerli JSON olmaktan çıkar ve hata aylar sonra, veriyi okuyan araçta görünür. Her sayı `InvariantCulture` ile yazılıyor, gerçek `tr-TR` kültürüyle test ediliyor. |
| **Debug kısayolu ölçümü kirletiyordu** | F7/F8 ile atlanan turların zaman damgası uydurma. Tek bir hata ayıklama run'ı ÇK-13 ortalamasını sessizce bozacaktı. Artık `usedRoundSkip` satıra yazılıyor, özet aracı o run'ları hesaba katmıyor. |
| **Telemetri oyunu durduramaz** | Yazıcı `RunEnded`'ın **ilk** abonesi; kaçan bir istisna yayın zincirini keser ve skor ekranı hiç gelmez. `catch` bilerek geniş, gerekçesi kodda yazılı. |

---

## Bilinmesi gereken tuzaklar

1. **Config kodu tüketicisinden ÖNCE üretilir.** Sıra: şema → içe aktar → tüketici.
2. **Bir betiği taşırken `.cs` ve `.cs.meta` birlikte taşınır.**
3. **NavMesh bake'i tetikleyicileri geometri sayabiliyor.** Kurulum aracı bake sırasında
   trigger'ları ve kapı kanatlarını kapatıyor; bunu bozma.
4. **Bağlantı ölçümü kurulumun içinde**, bake'in hemen ardında yapılıyor.
5. **`Bunker.Systems` motoru göremez** (`noEngineReferences`). `Application.*`,
   `Debug.Log`, `Vector3` orada kullanılamaz — yol ve zaman dışarıdan verilir.
6. **`CharacterController` / `NavMeshAgent` açıkken `transform.position` yazmak işe
   yaramaz.** Önce kapat, yaz, aç. BUG-005 buydu; M1-11'in yeniden başlatması aynı
   tuzağa düşebilirdi.

---

## Açık borç

**Kurulum aracı idempotent değil.** `Assets/_Project/Scenes/Sandbox/M0-Sandbox/` altında
**25 orphan NavMesh varlığı** birikmiş; her `SetupTestbedBatch` bir tane daha bırakıyor.
`editor-tools.md`'nin "aynı aracı iki kez çalıştırmak bir kez çalıştırmakla aynı sonucu
verir" kuralının ihlali. Ayrı bir iş olarak önerildi.

---

## Araçlar (hepsi Unity kapalıyken koşar)

| Komut | Ne yapar |
|---|---|
| `.claude/tools/unity-test.ps1` | Derler + EditMode testlerini koşar |
| `.claude/tools/unity-exec.ps1 -Method <sinif.metot>` | Editör metodunu başsız çalıştırır |
| `.claude/tools/config-validate.ps1` | JSON'ları şemaya karşı doğrular |
| **`.claude/tools/telemetry.ps1`** | **Biriken run'ları özetler, ÇK-13'ü cevaplar** |

```
Bunker.Editor.ZombieSetup.SetupTestbedBatch                 # her sey: uret, bagla, bake, olc
Bunker.Editor.ConfigTools.ConfigImporter.GenerateCodeBatch  # config -> C#
Bunker.Editor.ConfigTools.ConfigImporter.FillAssetsBatch    # config -> .asset
```

Unity açıksa kilit yüzünden koşmaz. Sahipsiz kilidi araçlar kendileri temizliyor.

---

## Denge ayarı

Bütün sayılar `config/balance/*.json` içinde. Değiştirme adımları:
`docs/guides/config-nasil-degistirilir.md`.

Oynanarak ayarlanmayı bekleyen dördü:

- `weapon.json` → silah sünger mi hissettiriyor?
- `barricade.json` → 4 tahta × 1.2 sn sökme / 0.9 sn tamir bir yarış gibi mi?
- `zombie.json` → telegraf (0.55 sn) okunup kaçılabiliyor mu?
- **`player.json`** → 4 sn kaçınca canın dolması bir ödül mü, yoksa hasarı anlamsız mı
  kılıyor? %35 uyarı eşiği ölmeden **önce** görülüyor mu? (Hiç oynanmadı.)

---

## Geliştiriciyle çalışma biçimi

- **Unity'yi ilk kez kullanıyor, Türkçe.** "Şunu tıkla" deme — **araç yaz ve kendin
  çalıştır.** Geliştiricinin tek yapacağı Play'e basmak olsun.
- Oyun testi bulgularını ciddiye al: bu projedeki beş hatanın hepsi oradan çıktı,
  hiçbiri testlerden çıkmadı.
- Hatayı hatayla kapatma. Kök sebebi bul. (Geçmişte bir emniyet ağı yazıldı ve
  geliştirici haklı olarak reddetti — *bu tavrı koru.*)

---

## Nereye bakılır

| Ne | Nerede |
|---|---|
| Proje özeti | `docs/CONTEXT.md` ← **önce burası** |
| **Oyun testi protokolü** | **`docs/qa/playtests/PT-01-ck17-tekrar-oynatiyor-mu.md`** |
| Kararlar | `docs/DECISIONS.md` |
| Hata kayıtları | `docs/qa/bugs/` |
| Story paketleri | `design/stories/` |
| Milestone | `design/milestones/M-01.md` |
| Mimari / ADR | `docs/architecture/ARCHITECTURE.md`, `adr/` |
| Config nasıl değiştirilir | `docs/guides/config-nasil-degistirilir.md` |
