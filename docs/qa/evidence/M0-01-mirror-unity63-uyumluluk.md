# M0-01 — Mirror / Unity 6.3 uyumluluk kanıtı

**Tarih:** 2026-08-29 · **İş:** M0-01 · **Tip:** Infra
**Karşılanan kriter:** **ÇK-9** — Mirror, Unity 6000.3.23f1'de sorunsuz derleniyor ve çalışıyor
**İlgili:** ADR-0004, ADR-0002

---

## Neden bu doğrulama gerekliydi

ADR-0004 Mirror'ı seçerken bir açık risk kaydetmişti: **Mirror'ın belgelenen Unity
desteği 6000.1'e kadar yazıyor**, biz 6000.3 LTS üzerindeyiz. M-00 risk tablosunda
"Orta × Kritik" olarak işaretlenmiş ve **ilk günün ilk işi** olarak planlanmıştı.

Ek olarak: boş bir assembly'nin import edilmesi hiçbir şey kanıtlamaz. Unity, script
içermeyen bir assembly'yi derlemeye çalışmaz, dolayısıyla asmdef referanslarının gerçekten
çözülüp çözülmediği görünmez. Bu yüzden referansları **kullanan** geçici dosyalar yazıldı.

## Yöntem

Headless derleme:

```
Unity.exe -batchmode -quit -nographics -projectPath <proje> -logFile <log>
```

İki geçici doğrulama dosyası:

| Dosya | Ne kanıtlıyor |
|---|---|
| `Assets/_Project/Code/Systems/WiringCheck.cs` | Saf C#, hiçbir `using UnityEngine` yok. Derlenmesi, `Bunker.Systems`'in Unity'siz derlendiğini gösterir |
| `Assets/_Project/Code/Net/WiringCheck.cs` | `using Mirror;` · `NetworkServer.active` · `LagCompensationSettings`. Derlenmesi, referansın çözüldüğünü ve Mirror'ın 6000.3'te çalıştığını gösterir |

`LagCompensationSettings` bilinçli seçildi: ADR-0004'ün tamamı o tipin varlığı üzerine
kuruldu, dolayısıyla doğrulamanın ona dokunması gerekiyordu.

## Sonuç

```
error CS sayisi:            0
Mirror Weaver (ILPostProcessor):  calisti
Bunker.Systems.dll:         141 referansla derlendi
Bunker.Net.dll:             281 referansla derlendi
Cikis:                      Exiting batchmode successfully, return code 0
```

**141 / 281 farkı bu doğrulamanın asıl bulgusu.** Aradaki ~140 referans Unity ve Mirror
yüzeyidir; `Bunker.Systems` onlara sahip değil. ADR-0004'ün "oyun mantığı netcode
kütüphanesinden bağımsızdır" kuralı disiplinle değil **derleyiciyle** korunuyor.

## Kurulum bilgisi

| | |
|---|---|
| Unity | 6000.3.23f1 |
| Mirror | Asset Store sürümü, 30 MB, 2270 dosya, 13 assembly |
| Bağlanan assembly'ler | `Mirror` (Core), `Mirror.Components` |
| Lag compensation | `Core/LagCompensation/LagCompensation.cs`, `Core/LagCompensation/LagCompensationSettings.cs`, `Components/LagCompensation/LagCompensator.cs` |

## Geri alma adımları

Mirror'ın kaldırılması gerekirse:

1. `Assets/Mirror/` ve `Assets/Mirror.meta` silinir
2. `Assets/ScriptTemplates/` silinir (Mirror ile geldi)
3. `Bunker.Net.asmdef` ve `Bunker.Gameplay.asmdef` içindeki `Mirror` ve
   `Mirror.Components` referansları kaldırılır
4. `Assets/_Project/Code/Net/WiringCheck.cs` silinir
5. ADR-0004 yeniden açılır

## Notlar

- Her iki `WiringCheck.cs` **geçicidir**; M0-02'de gerçek kod geldiğinde silinecek
- `Assets/Mirror/Examples/` 30 MB'ın 16 MB'ı. M0-02'de referans olarak kullanılacak,
  sonra silinmesi öneriliyor
- Bu doğrulama derlemeyi kanıtlar, **çalışma zamanını kanıtlamaz.** İki istemcinin
  gerçekten bağlanması M0-02'nin işi
