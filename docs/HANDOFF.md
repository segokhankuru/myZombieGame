# Handoff — 2026-09-02

Bir sonraki oturumun ilk okuyacağı özet. Çalışma ağacı temiz, 177 EditMode testi yeşil.

---

## Nerede kaldık

**M-01'de 13 işten 2'si kapandı, 9'u kod olarak bitti ve oyun testi bekliyor.**

| İş | Durum |
|---|---|
| M1-01 Tur ölçekleme | ✅ 12 test |
| M1-02 Ekonomi | ✅ 18 test |
| M1-03 Gri kutu harita | 🔧 üretildi, ölçü ayarı sürüyor |
| M1-04 Zombi | 🔧 kod + kurulum bitti |
| M1-05 Doğum, havuzlama, ağ seam'i | 🔧 kod bitti |
| M1-06 Silah | 🔧 kod bitti — `/feel-check` notu **DoD zorunlu** |
| M1-07 Bıçak | 🔧 kod bitti |
| M1-08 Barikat | 🔧 kod bitti |
| M1-09 Kapı | 🔧 kod bitti |
| M1-10 Duvar silahı (M-01'de yalnızca mermi) | 🔧 kod bitti |
| M1-13 Vuruş hissi | 🔧 kod bitti |
| **M1-11 Ölüm / skor ekranı** | ⬅ **sıradaki** |
| **M1-12 Telemetri** | ⬅ sıradaki |

Sonra milestone'un asıl sorusu: **ÇK-17 — 20 dakika oynadıktan sonra tekrar oynamak
istiyor musun?**

## Oyun şu an ne yapıyor

Play'e bas, kurulum gerekmiyor. 10 saniyelik mola (ekranın ortasında geri sayım), sonra
tur 1. Zombiler pencerelerin dışında doğuyor, barikatı söküyor, içeri tırmanıyor,
kovalıyor, telegraflı vuruş yapıyor.

```
sol tik  ates        R  dolum        V  bicak
E        tamir (tut) / satin al (bas)
F7/F8    tur atla    F9  sahayi temizle
```

## Bu oturumda çözülen beş hata

Hepsinin kaydı `docs/qa/bugs/` altında. **Ortak ders: sessiz başarısızlık en pahalı
hata türü.** Beşinin dördü hiçbir hata mesajı vermiyordu ve teşhis oyun testine kaldı.

| # | Neydi | Kök sebep |
|---|---|---|
| BUG-001 | 3 zombiden sonra hasar gitmiyor | Sunucunun gölge şarjörü hiç dolmuyordu (dolum bildirilmiyordu) |
| BUG-002 | Arada bir tık yeniyor | Sunucu doğrulaması **eşitlik** bekliyordu; ağ payı yoktu. Şimdi makuliyet testi |
| BUG-003 | Barikat gelince hiç zombi doğmuyor | `IsOpen` iki farklı soruya cevap veriyordu; yapısal/anlık ayrıldı |
| BUG-004 | Kapı alındı, hiçbir şey olmadı | Kapı diye bir **nesne yoktu**; ayrıca `SetPrivateField` enum/metin/dizi yazmıyordu |
| BUG-005 | Zombiler içeride beliriyor, mavi yanıp sönüyor | `NavMeshAgent` açıkken `transform.position` yazmak işe yaramaz — havuzdan çıkan zombi eski ölüm yerine dönüyordu |

## Reddedilen yaklaşım (kayda değer)

"Sıkışan zombiyi 15 saniye sonra sahadan çek" diye bir emniyet ağı yazıldı; geliştirici
**haklı olarak reddetti**: hatayı başka bir hatayla kapatmak, turun neden kilitlendiğini
gizler. Kök sebep düzeltildi, yama geri alındı. *Bu tavrı koru.*

## Bilinmesi gereken tuzaklar

1. **Config kodu tüketicisinden ÖNCE üretilir.** Üretilmemiş bir config sınıfına
   bağlanan kod projeyi derlenemez yapar ve importer da Unity içinde koştuğu için o
   noktadan sonra çalışamaz. Sıra: şema → içe aktar → tüketici.
2. **Bir betiği taşırken `.cs` ve `.cs.meta` birlikte taşınır.** Yalnızca `.cs` taşımak
   Unity'ye yeni GUID ürettirir ve o betiğe bakan her prefab/sahne referansı kopar.
3. **NavMesh bake'i tetikleyicileri geometri sayabiliyor.** Kurulum aracı bake sırasında
   bütün trigger'ları ve kapı kanatlarını kapatıyor; bunu bozma.
4. **Bağlantı ölçümü kurulumun içinde**, bake'in hemen ardında yapılıyor. Ayrı bir
   oturumda ölçmek kapalı kapıların NavMesh oymasını geri almayı gerektiriyor ve o geri
   alma toplu çalıştırmada hiç olmuyor — araç haksız yere "KOPUK" der.

## Araçlar (hepsi Unity kapalıyken koşar)

| Komut | Ne yapar |
|---|---|
| `.claude/tools/unity-test.ps1` | Derler + EditMode testlerini koşar |
| `.claude/tools/unity-exec.ps1 -Method <sinif.metot>` | Herhangi bir editör metodunu başsız çalıştırır |
| `.claude/tools/config-validate.ps1` | JSON'ları şemaya karşı doğrular |

Sık kullanılan üç metot:

```
Bunker.Editor.ZombieSetup.SetupTestbedBatch                 # her sey: uret, bagla, bake, olc
Bunker.Editor.ConfigTools.ConfigImporter.GenerateCodeBatch  # config -> C#
Bunker.Editor.ConfigTools.ConfigImporter.FillAssetsBatch    # config -> .asset
```

Unity açıksa kilit yüzünden koşmaz. **Sahipsiz kilidi araçlar kendileri temizliyor**
(derleme hatasıyla ölen çalıştırmadan kalan).

## Denge ayarı

Bütün sayılar `config/balance/*.json` içinde; her birinin şema açıklaması "aralığın
dışına çıkarsan oyuncu ne hisseder" cümlesini taşıyor. Değiştirme adımları:
`docs/guides/config-nasil-degistirilir.md`.

Şu an oynanarak ayarlanmayı bekleyen en kritik üçü:

- `weapon.json` → silah sünger mi hissettiriyor?
- `barricade.json` → 4 tahta × 1.2 sn sökme / 0.9 sn tamir bir yarış gibi mi?
- `zombie.json` → telegraf (0.55 sn) okunup kaçılabiliyor mu?

## Geliştiriciyle çalışma biçimi

- **Unity'yi ilk kez kullanıyor, Türkçe.** "Şunu tıkla" deme — **araç yaz ve kendin
  çalıştır.** Geliştiricinin tek yapacağı Play'e basmak olsun.
- Oyun testi bulgularını ciddiye al: bu oturumdaki beş hatanın hepsi oradan çıktı,
  hiçbiri testlerden çıkmadı.
- Hatayı hatayla kapatma. Kök sebebi bul.

## Nereye bakılır

| Ne | Nerede |
|---|---|
| Proje özeti | `docs/CONTEXT.md` ← **önce burası** |
| Kararlar | `docs/DECISIONS.md` |
| Hata kayıtları | `docs/qa/bugs/` |
| Milestone | `design/milestones/M-01.md` |
| Mimari / ADR | `docs/architecture/ARCHITECTURE.md`, `adr/` |
| Config nasıl değiştirilir | `docs/guides/config-nasil-degistirilir.md` |
