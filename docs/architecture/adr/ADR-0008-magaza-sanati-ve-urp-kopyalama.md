# ADR-0008 — Asset Store sanatı: dört paket, kopyalanarak URP'ye çevrilir

- **Durum:** **kabul edildi ve uygulandı** (2026-09-07; geliştirici paketleri kütüphaneye ekledi ve içe aktardı)
- **Tarih:** 2026-09-07
- **Karar veren:** technical-director + art-director (geliştirici onayı verdi)
- **İlgili:** ADR-0003 (URP), `.claude/rules/asset-art.md`, `Editor/ArtIntegration.cs`,
  `Config/ArtCatalogAsset.cs`, `docs/art/ASSET-LOG.md`

## Bağlam

Geliştirici, 2026-09-07, beş paket ekledi ve şunu istedi: *"silahları anladığın üzere
tabanca, pompalı, mp-kısa ve tüfek olarak ayırmıştın, bunları üstteki üç assetle
halledersin… zombi için bu asseti uygularsın… bunu bunkerın yüzeyine uygula… bunu da
dışarıya uygula."*

Bugüne kadar hem silahlar hem zombi **koddan ilkel şekillerle** kuruluyordu
(`WeaponShape`, `ZombieSetup`). Bu doğru bir karardı: sanat yönü kilitlenmeden
(`/art-direction` hâlâ çalışmadı) yapılan model işi atılacak iştir. Ama artık gerçek
modeller elimizde ve iki teknik sorun var:

1. **Paketlerin hepsi Built-in shader'la geliyor.** Proje URP (ADR-0003). Dönüştürülmemiş
   bir materyal oyunda **pembe** görünür.
2. **Dört silah dört ayrı pakette**, farklı ölçek, farklı eksen, farklı merkezle. Elde
   ve duvarda aynı dünyada durmaları gerekiyor.

`CLAUDE.md`: üçüncü parti asset ADR'siz eklenemez. Bu dosya o ADR.

## Paketler ve lisans

| Paket | Kullanım | Lisans |
|---|---|---|
| 3D Characters Zombie City Streets Lowpoly Pack — Lite | Zombi görseli | Asset Store Standard EULA (ücretsiz katman) |
| Low Poly Pistol Weapon Pack 2 | `weapon.pistol` | Asset Store Standard EULA |
| Low Poly ShotGun Weapon Pack 1 | `weapon.shotgun` | Asset Store Standard EULA |
| PolyOne — Free Pack Gun | `weapon.smg` (MP5), `weapon.rifle` (M16A1) | Asset Store Standard EULA |
| Tim's Substances — Old Bloody Wood Planks | Bunker duvar malzemesi | Asset Store Standard EULA |
| The Wasteland LITE | Dış alan dekoru | Asset Store Standard EULA |

Standard EULA oyun içinde kullanıma izin verir, **yeniden dağıtıma** (kaynak varlığı
paylaşmak) izin vermez. Depoya varlıklar giriyor; depo **özel** kaldığı sürece sorun
yok. Depo herhangi bir gün herkese açılırsa bu satır bir yayın engelidir.

## Seçenekler

| # | Seçenek | Artı | Eksi | Karar |
|---|---|---|---|---|
| 1 | Render Pipeline Converter ile **yerinde** dönüştür | Tek tıklama | Paket klasörünü düzenler; paket güncellenince kaybolur ve kimse fark etmez (`asset-art.md`: üçüncü parti klasörleri asla düzenlenmez) | Elendi |
| 2 | Modelleri elle sürükle, elle ölçekle | Hızlı görünür | Dört silah için dört ayarlama, beşincisi gelince yeniden; `.unity`/`.prefab` YAML'ına elle yazma demek | Elendi |
| 3 | **Editör aracı: kaynağa dokunmadan URP kopyası üret, ölçüyü hesapla, katalog yaz** | Idempotent, paket güncellemesine dayanıklı, yeni silahta tek satır | Bir kereye mahsus araç yazma maliyeti | **Seçildi** |

## Karar

**Üçüncü parti klasörleri salt okunur.** Oyunun kullandığı her şey
`Assets/_Project/Art/ThirdParty/` altında *üretilmiş bir kopyadır*:

```
Assets/<paket>/…                    ← kaynak, hiç düzenlenmez
  └─ ArtIntegration.LinkStoreArt()
       ├─ Art/ThirdParty/Materials/*.mat    URP/Lit kopyaları
       ├─ Art/ThirdParty/Models/*.prefab    çarpıştırıcısız, URP materyalli
       └─ Config/art.asset                  ArtCatalogAsset — çalışma anının tek adresi
```

`Resources.Load` yasak (`csharp-code.md`); model referansları serileşmiş bir
ScriptableObject üzerinden gidiyor ve prefab'lara editör aracı bağlıyor.

### Yerleşim ölçülür, elle ayarlanmaz

Silah modellerinin ölçeği, ekseni ve namlu ucu `ArtIntegration.Measure` tarafından mesh
köşelerinden hesaplanır ve **katalogda saklanır** — çalışma anında değil, çünkü köşe
taraması kare içinde yapılacak iş değil.

- **Uzun eksen** = namlu ekseni.
- **Namlu hangi uç:** ince olan. Model uzun eksen boyunca ikiye bölünür, hangi yarının
  köşeleri eksene daha yakınsa o yarı namludur. (Dipçik ve kabza bir kütle, namlu bir
  boru.)
- **Kabza hangi yön:** kütle merkezinin sınırlar kutusunun merkezinden kaydığı yön.
- **Boy:** oyunun kararı, paketin değil — gri kutu siluetlerinin boyu korunur
  (tabanca 0.26 m, taramalı 0.52 m, pompalı 0.78 m, tüfek 0.92 m).

Sonuçlar Inspector'dan düzeltilebilir; araç yeniden çalıştığında üzerine yazar.

### Gri kutuya geri düşüş korunur

Katalog boş ya da bir model eksikse `WeaponShape` eski ilkel şekilleri kurar ve zombi
gri kutu kalır. **Elin boş kalması sessiz bir hata olurdu**; gri kutu çirkin ama bir
sorunun olduğunu söyler.

### Vuruş kutuları modele geçmedi

Zombinin ilkel uzuvları (kafa, bacaklar) **çarpıştırıcı olarak duruyor**, yalnızca
renderer'ları kapatıldı. Modelin kendi mesh'ine çarpıştırıcı vermek 40 ajanda bir mesh
collider ordusu demekti; üstelik uzuv bazlı isabet ve bacak koparma zaten ayarlı.

**Bilinen gerileme:** bacak koptuğunda görünür bir değişiklik yok (kopan şey artık
görünmeyen bir kapsül). Sürünme duruşu çalışıyor, çünkü model `visualRig`'in altında.

## Sonuçlar

- Yeni bir silah paketi geldiğinde iş, `ArtIntegration.Weapons` dizisine bir satır.
- Paket güncellenirse: aracı yeniden çalıştır. Kopyalar yeniden üretilir.
- **Bilinen sınır:** bunker duvarları paylaşılan bir küp mesh'inden geliyor ve küpün
  UV'si yüz başına 0..1. Ahşap dokusu 3×3 kiremitleniyor; uzun duvarda yatay geriliyor.
  Düzgün çözüm triplanar bir shader ya da duvar başına UV — ikisi de ayrı bir iş ve
  ayrı bir ADR (`shader-graphics.md`: ölç, tahmin etme).
- **Bilinen sınır:** zombi paketinin Lite sürümünde animasyon klibi yok. Model T
  duruşunda; yürüme hâlâ `NavMeshAgent`'in hareketi. Animasyon ayrı bir iş.
- **Temizlenmedi:** zombi paketi bir `Editor/RateAssetPopup.cs` (Unity açılışında
  "puan ver" penceresi) ve dört demo sahnesi taşıyor. `asset-art.md` bunların gelir
  gelmez silinmesini istiyor; silme izni verilmedi, geliştiricinin kararına bırakıldı.
