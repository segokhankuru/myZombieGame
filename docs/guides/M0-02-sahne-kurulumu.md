# M0-02 — Sahne kurulumu (elle yapılacak adımlar)

**İş:** M0-02 · **Çıkış kriteri:** ÇK-10 — Mirror host modunda sahne açılıyor, tek oyuncu
spawn oluyor ve hareket ediyor.

Script'ler yazıldı ve derlendi. Bu belge onları sahneye nasıl bağlayacağını anlatıyor.
Sahne ve prefab kurulumu Unity editöründe elle yapılır — bu iş bilinçli olarak
otomatikleştirilmiyor (`.claude/rules/asset-art.md`: `.unity` ve `.prefab` dosyaları elle
düzenlenmez, ama editörde **kurulur**).

---

## Yazılan script'ler

| Dosya | Ne yapar |
|---|---|
| `Assets/_Project/Code/Net/BunkerNetworkManager.cs` | Mirror `NetworkManager` türevi. Play'e basınca solo host oturumu açar |
| `Assets/_Project/Code/Gameplay/PlayerController.cs` | Birinci şahıs hareket + fare bakışı, istemci otoritesinde |

---

## 1. Sahne

1. **File → New Scene → Basic (URP)**
2. Kaydet: `Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity`
3. Hierarchy'deki **Main Camera**'yı **sil**.
   *Neden:* oyuncu prefab'ının kendi kamerası var. İkisi birden açık kalırsa hangisinin
   ekrana çizdiği belirsizleşir ve iki `AudioListener` uyarısı alırsın.
4. Directional Light kalsın.

## 2. Zemin

1. **GameObject → 3D Object → Plane**
2. Position `0, 0, 0` · Scale `5, 1, 5`
3. Adı: `Ground`

Geçicidir. Gerçek gri kutu harita M1-03'ün işi.

## 3. NetworkManager

1. **GameObject → Create Empty**, adı `NetworkManager`, position `0,0,0`
2. **Add Component** → arama kutusuna **`Bunker`** yaz → **Bunker Network Manager**
3. **Add Component** → arama kutusuna **`Kcp`** yaz → **Kcp Transport**
   *Mirror'ın `NetworkManager`'ı transport'u otomatik eklemez — elle eklemen gerekiyor.*

> **Add Component menüsü hakkında iki not:**
>
> **Kategoriye göz atma, arama kutusunu kullan.** Bizim script'lerimiz
> `[AddComponentMenu("Bunker/...")]` taşıdığı için **Bunker** adlı kendi kategorilerinde
> görünür — "Scripts" altında **görünmezler**. Mirror'ın transport'larında ise hiç
> `AddComponentMenu` yok, dolayısıyla onlar **Scripts** altındadır. İki farklı yer;
> arama kutusu ikisini de bulur.
>
> **Hiçbiri çıkmıyorsa** editör eski derlemeyi gösteriyordur: Unity penceresine tıkla,
> **Assets → Refresh** (`Ctrl+R`). Son çare, ilgili `.cs` dosyasını Project penceresinden
> doğrudan objenin üstüne sürüklemek — bu yol menüden bağımsız çalışır.
4. `Bunker Network Manager` bileşeninde:
   - **Transport** alanına aynı objedeki **KcpTransport**'u sürükle
   - **Auto Start Solo** → ✅ işaretli kalsın
   - **Auto Create Player** → ✅ (Mirror varsayılanı)
   - **Player Prefab** → şimdilik boş, 5. adımda dolduracağız

## 4. Oyuncu prefab'ı

1. **GameObject → Create Empty**, adı `Player`
2. Bileşenleri ekle, **bu sırayla**:

   | Bileşen | Ayar |
   |---|---|
   | **Character Controller** | Height `1.8`, Radius `0.3`, Center `0, 0.9, 0` |
   | **Network Identity** | (varsayılan) |
   | **Network Transform Unreliable** | **Sync Direction → `Client To Server`** ⚠️ |
   | **Player Controller** *(Bunker)* | referanslar 4. adımda |

   ⚠️ **Sync Direction'ı `Client To Server` yapmayı unutma.** Varsayılan
   `Server To Client`'tır; öyle kalırsa oyuncu kendi karakterini hareket ettiremez
   (ADR-0004: hareket istemci otoritesinde).

3. **Görsel gövde:** `Player` altına **3D Object → Capsule** ekle.
   - Position `0, 0.9, 0` · Scale `0.5, 0.9, 0.5`
   - Capsule'ün **Capsule Collider** bileşenini **sil** (CharacterController zaten çarpışmayı
     yönetiyor; ikisi birden çakışır)

4. **Kamera:** `Player` altına **Camera** ekle.
   - Adı `PlayerCamera` · Position `0, 1.6, 0` · Rotation `0,0,0`
   - Camera bileşeninin yanında **Audio Listener** de olsun (yoksa Add Component ile ekle)
   - **Camera** bileşeninin işaretini **kaldır** (disabled)
   - **Audio Listener** işaretini de **kaldır**

   *Neden kapalı başlıyor:* `PlayerController` yalnızca yerel oyuncuda ikisini açar. Açık
   başlarlarsa co-op'ta dört kamera birden çizim yapar.

5. `Player` objesindeki **Player Controller** bileşeninde:
   - **Player Camera** → `PlayerCamera` objesini sürükle
   - **Player Audio Listener** → aynı objeyi sürükle

6. `Player` objesini **`Assets/_Project/Prefabs/Gameplay/`** klasörüne sürükleyip prefab yap
7. Prefab olduktan sonra **sahnedeki `Player` objesini sil**
   *Oyuncuyu Mirror spawn edecek; sahnede duran bir kopya ikinci bir oyuncu gibi davranır*

## 5. Prefab'ı kaydet

1. `NetworkManager` objesini seç
2. **Player Prefab** alanına `Assets/_Project/Prefabs/Gameplay/Player.prefab`'ı sürükle

## 6. Çalıştır

**Play**'e bas. Beklenen:

- Console'da `[Bunker] Solo oturum başlıyor (host, uzak istemci yok).`
- Zeminde bir kapsül belirir, kamera onun gözünden bakar
- İmleç kilitlenir ve gizlenir
- **WASD** ile hareket, **fare** ile bakış, **Space** ile zıplama

---

## ÇK-10 kontrol listesi

- [ ] Play'e basınca hiçbir butona tıklamadan oyuna giriliyor
- [ ] Karakter WASD ile hareket ediyor
- [ ] Fare bakışı yumuşak, dikeyde ±85° sınırlanıyor, gövde dikeyde dönmüyor
- [ ] Space ile zıplanıyor, yer çekimi karakteri geri indiriyor
- [ ] Zeminden düşmüyor
- [ ] Console'da hata yok, `AudioListener` uyarısı yok
- [ ] Play'den çıkınca imleç geri geliyor

---

## Sık karşılaşılan sorunlar

| Belirti | Sebep |
|---|---|
| Karakter hiç hareket etmiyor | `NetworkTransformUnreliable` → Sync Direction hâlâ `Server To Client` |
| Ekran siyah / hiçbir şey görünmüyor | Prefab'daki Camera bileşeni açılmamış — `PlayerController`'ın **Player Camera** alanı boş olabilir |
| İki `AudioListener` uyarısı | Sahnedeki Main Camera silinmemiş |
| Karakter zeminden düşüyor | Plane'in collider'ı yok ya da CharacterController Center değeri yanlış |
| `Player Prefab is not assigned` hatası | 5. adım atlanmış |
| Karakter yerde titriyor / zıplıyor | Capsule'ün `Capsule Collider`'ı silinmemiş, CharacterController ile çakışıyor |

---

## Bu adımdan sonra

M0-02 bittiğinde sırada **M0-03** var: ölçüm aracı (kare süresi, host CPU dağılımı).
Onun ardından **M0-04** — 40 NavMesh agent ile ÇK-5 ölçümü.

Script'lerde iki `TODO(gameplay-programmer, M1-01)` var: hareket sayıları
`config/balance/player.json`'a, girdi okuma da `InputSystem_Actions` varlığına taşınacak.
İkisi de M-01'in işi; M0-02 bir iskelet doğrulamasıdır.
