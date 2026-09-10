# Asset Log

Append-only provenance for every promoted generated asset.
This is what makes an asset regenerable months later.

| Date | Asset | Workflow | Seed | Subject prompt | Approved by |
|---|---|---|---|---|---|

## 2026-09-07 — Asset Store paketleri (ADR-0008)

Bunlar üretilmiş değil, **satın alınmış** varlıklar; seed/prompt yok, kaynağı var.
Oyunun kullandığı hiçbir dosya paket klasöründen değil — hepsi
`Assets/_Project/Art/ThirdParty/` altındaki üretilmiş URP kopyaları
(`Bunker/Gorunum/Magaza Modellerini Bagla`).

| Kullanım | Kaynak prefab | Üretilen kopya |
|---|---|---|
| `weapon.pistol` | `Low Poly Weapons VOL.1/Prefabs/M1911` | `Art/ThirdParty/Models/weapon_pistol.prefab` |
| `weapon.smg` | `Low Poly Weapons VOL.1/Prefabs/Uzi` | `…/weapon_smg.prefab` |
| `weapon.shotgun` | `Low Poly Weapons VOL.1/Prefabs/Bennelli_M4` | `…/weapon_shotgun.prefab` |
| `weapon.ak74` | `Low Poly Weapons VOL.1/Prefabs/AK74` | `…/weapon_ak74.prefab` |
| `weapon.m4` | `Low Poly Weapons VOL.1/Prefabs/M4_8` | `…/weapon_m4.prefab` |
| `weapon.sniper` | `Low Poly Weapons VOL.1/Prefabs/M107` | `…/weapon_sniper.prefab` |
| `weapon.sniper` dürbünü | `Low Poly Weapons VOL.1/Prefabs/TAN_LR_Scope_01` | `…/weapon_sniper_scope.prefab` |
| `weapon.m4` dürbünü (ELCAN / "HAMR") | `Low Poly Weapons VOL.1/Prefabs/ELCAN` | `…/weapon_m4_scope.prefab` |
| Oyuncu gövdesi | `Kevin Iglesias/Human Animations/Models/HumanM_Model` | `…/player_body.prefab` + `player_locomotion.controller` |
| Zombi | `3D Characters Zombie City Streets…/Prefabs/(P) Characters_Zombie_SuitMan_1` | `…/zombie_suitman.prefab` |
| Bunker duvarı | `Tim's Substances/Bloody_Wood_graph_0/*.tga` | `Art/Materials/Surfaces/mat_bunker_wood.mat` |
| Dış dekor | `The Wasteland LITE/Prefabs/{Props Misc, Fortified_Walls}` | sahnede `Decor_Outside` (sabit tohum 20260907) |

Lisans: hepsi Asset Store Standard EULA. Oyun içi kullanım serbest, **kaynak varlığı
yeniden dağıtmak değil** — depo herkese açılırsa bu bir yayın engelidir (ADR-0008).

**2026-09-09 — silah envanteri tek pakete taşındı.** Önceki hâlde dört silah dört ayrı
paketten geliyordu ve her paket kendi kalınlığını, kendi detay yoğunluğunu getiriyordu;
silüet dili tutarsızdı. Altı silah ve dürbün artık aynı elin çiziminden. Emekliye ayrılan
kopyalar (`weapon_rifle.prefab` ve eski üç paketin kopyaları) `Art/ThirdParty/Models/`
altında **duruyor** — silmek, eski bir sahne referansını sessizce boşa düşürürdü;
temizlik sanat yönü kilitlendiğinde tek seferde yapılır.

## Ses (ADR-0009, 2026-09-09)

Ses dosyaları **kopyalanmıyor**: modelin aksine ses URP dönüşümü gerektirmiyor. Oyun
paketin kliplerine `audio.asset` kataloğu üzerinden bakıyor
(`Bunker/Gorunum/Ses Dosyalarini Bagla`). Üçüncü parti klasörlerinde değiştirilen tek şey
**içe aktarma ayarları** (yükleme tipi, mono, sıkıştırma) — dosyaların kendisi değil.

| Kullanım | Kaynak |
|---|---|
| Ana menü müziği | `Music Loops Mini Set/Mystical Music Loops/Mystical  Loop #1.wav` |
| Ayak sesi (yürüme/koşu/iniş) | `Footsteps - Essentials/Footsteps_DirtyGround/*` (10 + 10 + 3 varyant) |
| `weapon.pistol` | `FreeWeaponSounds/Handgun/*` — perde 1.00 |
| `weapon.smg` | `FreeWeaponSounds/AssaultRifle/*` — perde 1.22 |
| `weapon.shotgun` | `FreeWeaponSounds/Shotgun/*` — perde 1.00 |
| `weapon.ak74` | `FreeWeaponSounds/AssaultRifle/*` — perde 0.92 |
| `weapon.m4` | `FreeWeaponSounds/AssaultRifle/*` — perde 1.05 |
| `weapon.sniper` | `FreeWeaponSounds/AssaultRifle/*` — perde 0.62 |

Pakette dört ses ailesi var, oyunda altı silah: perde farkı bir **köprü**, çözüm değil.
Gerçek cevap silah başına kayıttır ve ses yönü kilitlendiğinde gelir. Gerekçe ADR-0009'da.

**Lisans borcu:** üç ses paketinin lisansı `docs/audio/AUDIO-BIBLE.md`'ye işlenmeli —
`audio-code.md`'nin kuralı, lisans dosyanın geldiği anda kaydedilir. Yayında bulunan
kayıtsız bir ses, o noktada kimsenin nereden geldiğini hatırlamadığı bir engeldir.

**Yayın öncesi:** bunların hiçbiri sanat yönü kilitlendikten sonra kalmak zorunda değil.
`/art-direction` çalıştığında paletle çelişen her parça yeniden değerlendirilir.
