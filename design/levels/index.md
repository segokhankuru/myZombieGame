# Seviye dizini

| Seviye | Konu | Durum | İş |
|---|---|---|---|
| [LVL-01](LVL-01-greybox.md) | Gri kutu harita: 3 bölge, iki kat, rampa + düşme deliği | taslak, oynanıyor | M1-03 |

Harita **elle değil üreteçle** kuruluyor: ölçüler
`Assets/_Project/Settings/BlockoutSettings.asset`, üretici
`Assets/_Project/Code/Editor/BlockoutGenerator.cs`.

**Hangi menü ne yapar** (2026-09-08'de ayrıldı — kurulum aracı artık haritaya
dokunmuyor, çünkü elle yapılmış düzeni siliyordu):

| Menü | Yapar | Yıkıcı mı |
|---|---|---|
| `Bunker/Zombi/Test Alanini Kur` | Prefab, HUD, barikat, satın alma, NavMesh bake | Hayır — harita varsa dokunmaz |
| `Bunker/Level/LVL-01 Gri Kutu Uret` | Yalnızca haritayı sıfırdan üretir | **Evet** |
| `Bunker/Level/DUNYAYI SIFIRDAN KUR` | Harita + yüzeyler + kurulum + bake, doğru sırayla | **Evet** |

Başsız karşılığı: `.claude/tools/unity-exec.ps1 -Method
Bunker.Editor.BlockoutGenerator.RebuildWorldBatch`.

**Dışarısı da üreteçten geliyor** (2026-09-08): çepere tahkimat duvarları
(`ArtIntegration.DecorateOutside`), geçitlerden barikatlara toprak yollar + bitki örtüsü
+ bulut tavanı (`OutdoorScenery.Decorate`). Yollar gerçek pencere konumlarından türüyor;
ayrı hesaplanmış bir yol, ayak izi değiştiği gün barikatın yanından geçerdi.
