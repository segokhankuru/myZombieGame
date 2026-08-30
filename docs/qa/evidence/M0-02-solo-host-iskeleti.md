# M0-02 — Solo host iskeleti kanıtı

**Tarih:** 2026-08-30 · **İş:** M0-02 · **Tip:** Net
**Karşılanan kriter:** **ÇK-10** — Mirror host modunda sahne açılıyor, tek oyuncu spawn
oluyor ve hareket ediyor
**İlgili:** ADR-0004, M-01 (zorunlu mimari kısıt)

---

## Ne kuruldu

| Parça | Yer |
|---|---|
| Sahne | `Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity` |
| Ağ yöneticisi | `BunkerNetworkManager` + `KcpTransport` |
| Oyuncu prefab'ı | `Assets/_Project/Prefabs/Gameplay/Player.prefab` |
| Kontrol | `PlayerController` — CharacterController tabanlı, istemci otoritesinde |
| Replikasyon | `NetworkTransformUnreliable`, `syncDirection = ClientToServer` |

---

## Kontrol listesi — hepsi geçti

- [x] Play'e basınca hiçbir butona tıklamadan oyuna giriliyor (`autoStartSolo`)
- [x] Karakter WASD ile hareket ediyor
- [x] Fare bakışı çalışıyor; dikeyde ±85° sınırlanıyor, gövde dikeyde dönmüyor
- [x] Space ile zıplanıyor, yer çekimi geri indiriyor
- [x] Karakter zeminden düşmüyor
- [x] **Console temiz** — hata yok, `AudioListener` uyarısı yok
- [x] Play'den çıkınca imleç serbest kalıyor

---

## Bu doğrulamanın asıl anlamı

ÇK-10 yüzeyde "karakter yürüyor" gibi görünüyor, ama kanıtladığı şey daha büyük:
**M-01'in zorunlu mimari kısıtı ayakta.**

Solo mod ayrı bir çevrimdışı kod yolu değil; uzak istemci sayısı sıfır olan bir Mirror
host oturumu. Oyuncu bir `NetworkBehaviour`, konumu ağ üzerinden replike ediliyor, otorite
tablosu ADR-0004'e uygun (istemci yalnızca kendi konumu ve bakış yönü).

Bu, M-02'nin bir **doğrulama** mı yoksa **yeniden yazım** mı olacağını belirleyen şeydi.
Şu an doğrulama tarafındayız.

---

## Bilinen geçici çözümler

| Konu | Durum | Nereye taşınacak |
|---|---|---|
| Hareket sayıları `[SerializeField]` içinde | `TODO(gameplay-programmer, M1-01)` | `config/balance/player.json` |
| Girdi doğrudan `Keyboard.current` / `Mouse.current` | `TODO(gameplay-programmer, M1-01)` | `InputSystem_Actions` varlığı |
| Zemin bir `Plane` | geçici | Gri kutu harita, M1-03 |
| Görsel gövde bir kapsül | geçici | M-03 sanat geçişi |

---

## Notlar

- Bu doğrulama **tek istemci** ile yapıldı. İki istemcinin gerçekten senkron olması
  M-02'nin işi; DoD'nin `Net` tipi için istediği iki istemcili test o zaman yapılacak.
- `PlayerController`'daki `lookSensitivity = 0.08` hissiyat değeridir ve Inspector'dan
  denenerek ayarlanabilir. Kalıcı değeri M1-01'de config'e girecek.
