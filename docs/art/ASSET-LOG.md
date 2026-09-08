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
| `weapon.pistol` | `Low Poly Pistol Weapon Pack 2/Prefabs/Weapons/Pistol_F` | `Art/ThirdParty/Models/weapon_pistol.prefab` |
| `weapon.smg` | `PolyOne/Free Gun/Prefabs/SM_HK_MP5` | `…/weapon_smg.prefab` |
| `weapon.shotgun` | `Low Poly ShotGun Weapon Pack 1/Prefabs/Weapons/ShotGun_A` | `…/weapon_shotgun.prefab` |
| `weapon.rifle` | `PolyOne/Free Gun/Prefabs/SM_M16A1` | `…/weapon_rifle.prefab` |
| Zombi | `3D Characters Zombie City Streets…/Prefabs/(P) Characters_Zombie_SuitMan_1` | `…/zombie_suitman.prefab` |
| Bunker duvarı | `Tim's Substances/Bloody_Wood_graph_0/*.tga` | `Art/Materials/Surfaces/mat_bunker_wood.mat` |
| Dış dekor | `The Wasteland LITE/Prefabs/{Props Misc, Fortified_Walls}` | sahnede `Decor_Outside` (sabit tohum 20260907) |

Lisans: hepsi Asset Store Standard EULA. Oyun içi kullanım serbest, **kaynak varlığı
yeniden dağıtmak değil** — depo herkese açılırsa bu bir yayın engelidir (ADR-0008).

**Yayın öncesi:** bunların hiçbiri sanat yönü kilitlendikten sonra kalmak zorunda değil.
`/art-direction` çalıştığında paletle çelişen her parça yeniden değerlendirilir.
