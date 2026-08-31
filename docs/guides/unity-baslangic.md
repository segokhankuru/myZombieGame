# Unity'ye Başlangıç — bu projede işine yarayacak kadarı

Bu bir Unity kursu değil. Bu projede yapacağın işler için gereken minimum: pencereler,
gezinme, seçme, taşıma ve tuzaklar.

---

## 1. Pencereler — ekranda ne var

Unity açıldığında beş ana pencere görürsün.

| Pencere | Ne işe yarar | Nerede |
|---|---|---|
| **Hierarchy** | Sahnedeki **her şeyin listesi**. Ağaç yapısı — bir objenin altındaki objeler onun çocuğudur | Sol |
| **Scene** | Haritayı **düzenlediğin** 3B görünüm. Oyunu oynamıyorsun, inşa ediyorsun | Orta |
| **Game** | Oyunun **oyuncu gözünden** hâli. Play'e basınca burası çalışır | Scene'in yanındaki sekme |
| **Inspector** | Seçili objenin **bütün ayarları**. Konum, boyut, bileşenler | Sağ |
| **Project** | Diskteki **dosyalar** — script'ler, prefab'lar, sahneler | Alt |
| **Console** | **Mesajlar ve hatalar**. Kırmızı = hata, sarı = uyarı | Project'in yanındaki sekme |

En çok Hierarchy, Scene ve Inspector arasında gidip geleceksin. Üçü birlikte şu soruyu
cevaplar: *"sahnede ne var, nerede duruyor, nasıl ayarlanmış."*

Pencere kaybolursa: **Window → General →** ilgili pencere.

---

## 2. Scene görünümünde gezinme

Bu, Unity'de öğrenilmesi gereken **ilk şey**. Gezmeyi bilmezsen hiçbir şey yapamazsın.

| Ne yapmak istiyorsun | Nasıl |
|---|---|
| **Uçarak gezinmek** (FPS gibi) | **Sağ tuşu basılı tut** + `WASD`. Fare bakışı çevirir |
| Hızlı/yavaş uçmak | Sağ tuş basılıyken `Shift` (hızlı), fare tekerleği (hız ayarı) |
| Yakınlaş / uzaklaş | Fare **tekerleği** |
| Kaydırmak (pan) | **Orta tuşu** basılı tutup sürükle |
| Bir objenin etrafında dönmek | `Alt` + **sol tuş** sürükle |
| **Seçili objeye odaklan** | Objeyi seç, **`F`** tuşu |

**En çok kullanacağın: `F`.** Hierarchy'den bir obje seç, fareyi Scene penceresine götür,
`F`'ye bas — kamera o objeye uçar. Kaybolduğunda kurtarıcın budur.

---

## 3. Obje seçmek ve taşımak

Bir objeyi **Hierarchy'den** ya da **Scene'de üstüne tıklayarak** seçersin. Seçili obje
Inspector'da görünür.

Sol üstteki araç çubuğunda beş araç var, klavye kısayolları:

| Tuş | Araç | Ne yapar |
|---|---|---|
| `Q` | El | Görünümü kaydırır (obje taşımaz) |
| **`W`** | **Taşı** | Oklardan tutup sürükle |
| `E` | Döndür | Çemberlerden tutup çevir |
| `R` | Ölçekle | Küpleri çekerek boyutlandır |
| `T` | Dikdörtgen | Çoğunlukla UI için |

Oklar renkli: **kırmızı = X**, **yeşil = Y (yukarı)**, **mavi = Z**. Bir oku tutup
sürüklersen yalnızca o eksende hareket eder — bu, işleri düz tutmanın yolu.

**Daha kesin yol: Inspector'a sayı yaz.** Seçili objenin Inspector'ında en üstte
`Transform` var: `Position`, `Rotation`, `Scale`. Sürüklemek yerine buraya doğrudan sayı
yazmak her zaman daha kesindir. Bu projede ölçüler önemli, o yüzden sayı yazmayı tercih et.

---

## 4. Play modu ve **en sık yapılan hata**

Üstteki ▶ düğmesi oyunu başlatır. Tekrar basmak durdurur.

> ⚠️ **Play modundayken yaptığın hiçbir değişiklik kaydedilmez.**
>
> Play'e bastın, bir objeyi taşıdın, "güzel oldu" dedin, Play'i durdurdun — **taşıma
> geri alındı.** Unity Play modunda sahnenin bir kopyasını çalıştırır ve durdurunca
> kopyayı atar.
>
> Bu, her Unity başlangıcının bir kez yaşadığı şeydir. Bir kez yaşa, bir daha unutma.

Değer denemek için Play modu **mükemmeldir** (örneğin fare hassasiyeti). Ama beğendiğin
değeri not al, Play'i durdur, sonra tekrar gir.

**Kaydetmek:** `Ctrl + S` sahneyi kaydeder. Sık sık bas.

---

## 5. Bileşen (Component) nedir

Bir GameObject tek başına boş bir kutudur. Ona **bileşen** ekleyerek özellik verirsin.

- `Transform` — nerede, ne kadar döndürülmüş, ne kadar büyük (her objede vardır)
- `Mesh Renderer` — görünür yapar
- `Character Controller` — yürüyebilir yapar
- `Player Controller` — bizim yazdığımız script

**Bileşen eklemek:** objeyi seç → Inspector'ın en altındaki **Add Component** → arama
kutusuna yaz.

**Arama kutusunu kullan, kategorilere göz atma.** Bileşenler farklı kategorilerde durur
ve gözden kaçarlar.

---

## 6. Prefab nedir

Bir GameObject'i Project penceresine sürüklersen **prefab** olur — yani şablon. Sahnede
değil diskte yaşar, istendiği kadar kopyası üretilebilir.

Oyuncu karakteri prefab: sahnede durmuyor, oyun başlayınca Mirror onu üretiyor.

Hierarchy'de bir objenin adı **mavi** ise o bir prefab kopyasıdır.

---

## 7. Bu projeye özel notlar

- **Sahnemiz:** `Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity`. Project penceresinden
  çift tıklayarak açılır.
- **`_Project` klasörü bizim.** `Mirror` klasörü üçüncü parti, elleme.
- **Console'u açık tut.** Kırmızı bir satır gördüğün an bana yapıştır.
- **`.unity` ve `.prefab` dosyalarını metin olarak açma.** Makine çıktısıdırlar.

---

## 8. Kaybolduğunda

| Sorun | Çözüm |
|---|---|
| Scene'de nerede olduğumu bilmiyorum | Hierarchy'den bir obje seç, `F` |
| Pencere kayboldu | `Window → General →` pencere adı |
| Her şeyi bozdum | `Ctrl + Z` (Undo) çoğu şeyi geri alır |
| Haritayı bozdum | `Bunker → Level → LVL-01 Gri Kutuyu Sil`, sonra tekrar üret |
| Oyun Play'de garip davranıyor | Play'i durdur, Console'a bak |
| Değişikliğim kayboldu | Play modunda mıydın? (bkz. §4) |
