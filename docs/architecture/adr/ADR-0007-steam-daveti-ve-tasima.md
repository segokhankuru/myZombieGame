# ADR-0007 — Steam üzerinden davet ve taşıma (transport)

- **Durum:** **kabul edildi ve uygulandi** (2026-09-05 aksam; gelistirici paketleri kurdu)
- **Tarih:** 2026-09-05
- **Karar veren:** technical-director (geliştirici onayı gerekiyor)
- **İlgili:** ADR-0004 (Mirror), `.claude/rules/netcode.md`, `Systems/Net/SessionSignals.cs`

## Bağlam

Geliştirici, 2026-09-05: *"Steam'den davet etme özelliğini de ekleyelim, yavaş yavaş
arkadaşlarla test etmenin zamanı geliyor."*

Bugünkü durum: Mirror + **KcpTransport** (UDP). Bu, aynı ağdaki iki makine arasında
çalışır; internet üzerinden çalışması için oda açanın **port yönlendirmesi** yapması
gerekir. Arkadaş testinde bu, testin kendisinden daha uzun süren bir kurulum adımıdır ve
çoğu denemede oturum hiç kurulamaz.

Steam taşıması bu sorunu ortadan kaldırır: **Steam relay** üzerinden bağlantı, NAT
delme yok, port yönlendirme yok, oyuncunun IP'si karşı tarafa görünmüyor. Davet, Steam
arkadaş listesinden veya oyun içinden gönderilir; kabul eden kişi doğrudan lobiye düşer.

**Bu ADR bugün uygulanamaz**, çünkü iki adım proje dışında:

1. **Steamworks paketi projeye girmeli.** İndirme gerektirir; `CLAUDE.md`'ye göre yeni
   bir üçüncü parti paket ADR'siz eklenemez — bu dosya o ADR.
2. **Steam App ID gerekir.** Geliştirme ve arkadaş testi için Valve'ın herkese açık
   **480 (Spacewar)** App ID'si kullanılabilir; yayın için Steamworks'te kayıtlı kendi
   App ID'miz gerekir (100 USD Steam Direct ücreti, publishing-manager'ın işi).

## Seçenekler

| # | Seçenek | Artı | Eksi | Karar |
|---|---|---|---|---|
| 1 | **KCP (bugünkü hâli) ile devam** | Sıfır bağımlılık, hemen çalışır, LAN'da sorunsuz | İnternette port yönlendirme; arkadaş testi kurulumda ölür | **Ara çözüm olarak kalır** — menüdeki "adresle katıl" |
| 2 | **FizzySteamworks** (Steamworks.NET tabanlı) | Mirror ekosisteminde en çok kullanılan, en çok örnek | Steamworks.NET'in native DLL'leri; API C tarzı ve ayrıntılı | Aday |
| 3 | **FizzyFacepunch** (Facepunch.Steamworks tabanlı) | Modern C# API, async/await, lobi ve davet kodu belirgin kısa | Topluluğu Steamworks.NET'ten küçük | **Öneri** |
| 4 | Kendi relay sunucumuz (Edgegap vb.) | Steam'e bağımlı değil | Aylık maliyet, işletme yükü, solo geliştiriciye ağır | Elendi |

## Karar (önerilen)

**FizzyFacepunch + Facepunch.Steamworks**, App ID 480 ile geliştirme, kendi App ID'mizle
yayın. Taşıma **değiştirilebilir** kalır: KCP paketten çıkarılmaz.

**Neden taşıma değiştirilebilir kalmalı:** Steam çalışmıyorken (Steam kapalı, hesap
yok, ikinci makine Steam'siz) oyun hâlâ test edilebilmeli. Menüdeki "adresle katıl"
yolu bu yüzden silinmez.

### Kodun bu karara hazır olan tarafı

Bugün yazılan menü katmanı bu değişikliği **görmeyecek** şekilde kuruldu:

- `Systems/Net/SessionSignals.cs` — menü yalnızca **niyet** bildirir: `RequestHost()`,
  `RequestJoin(string)`, `RequestLeave()`. Menü ne Mirror'ı ne Steam'i bilir.
- `Net/BunkerNetworkManager.cs` — niyeti uygular. Steam geldiğinde `RequestJoin`'in
  taşıdığı metin bir IP değil bir **lobi/Steam kimliği** olur; `networkAddress` alanı
  ikisini de taşır (FizzyFacepunch adresi SteamID olarak okur).

### Steam geldiğinde yapılacak iş (tahmin: yarım gün)

1. Facepunch.Steamworks + FizzyFacepunch'ı `Assets/ThirdParty/` altına al, lisansları
   `docs/architecture/THIRD-PARTY.md`'ye yaz (`asset-art.md`: lisans **import'tan önce**).
2. `steam_appid.txt` (içinde `480`) proje köküne. Depo bunu bilerek yok sayıyor
   (`.gitignore` → "sırlar"): App ID bir sır değil, ama **makineye özel bir geliştirme
   dosyası** ve Steam üzerinden dağıtılan bir build'a girmemeli. Bu yüzden dosyayı
   `Bunker/Steam/Kurulumu Kontrol Et` aracı yoksa kendisi oluşturur.
3. Menü sahnesindeki `NetworkManager`'a `FizzyFacepunch` bileşeni; `transport` alanı
   ona bağlanır. KCP bileşeni **kalır**, ikisi arasında menüden seçim.
4. `SteamLobby` bileşeni (`Bunker.Net`): lobi oluştur, `GameLobbyJoinRequested`
   olayını dinle, kabul edilen davette `SessionSignals.RequestJoin(hostSteamId)`.
5. Menüye "ARKADAŞ DAVET ET" düğmesi: Steam overlay'in davet penceresini açar.
6. **İki makinede test** (netcode.md): host + geç katılan, biri oyunun ortasında
   katılsın; 150 ms gecikme ve %5 paket kaybı ile.

### Bu ADR'nin bugün kapattığı kapı

**Şu an "davet et" düğmesi eklenmeyecek.** Çalışmayan bir düğme, oyuncuya (ve
geliştiriciye) var olmayan bir özelliği varmış gibi gösterir; menüdeki not bunun yerine
gerçeği söylüyor: *"Steam üzerinden davet M-04'te gelecek (ADR-0007)."*

## Sonuçlar

**Olumlu:** Arkadaş testi port yönlendirmesiz çalışır; oyuncu IP'si gizlenir; Steam
yayınıyla aynı yol kullanılır, yani yayında sürpriz çıkmaz.

**Olumsuz:** Steam çalışmadan oyun çevrimiçi test edilemez hâle **gelmemeli** — bu
yüzden KCP yolu korunuyor ve bu, bakılacak iki taşıma demek.

**Risk:** Facepunch.Steamworks'ün Unity 6000.3 ile uyumu doğrulanmadı. İlk adım bir
**yarım günlük sivri uç (spike)**: iki makinede boş bir sahnede bağlantı kurup ölçmek.
Uyum çıkmazsa seçenek 2'ye (FizzySteamworks) dönülür — karar tersine çevrilebilir.

## Geliştiriciden beklenen

1. Bu ADR'nin **onayı** (FizzyFacepunch mı, FizzySteamworks mı).
2. Steamworks paketlerinin indirilmesi (internet erişimi gerektiriyor).
3. Yayın App ID'si — sadece Steam'e çıkarken; test 480 ile yürür.

---

## Uygulama notu (2026-09-05 akşamı)

Geliştirici paketleri kurdu: **Facepunch.Steamworks 2.5.2** + **FizzyFacepunch v4.4**.
Kurulan hâliyle proje **derlenmedi** — iki ayrı uyumsuzluk çıktı ve ikisi de üçüncü
parti kaynağın yamalanmasını gerektirdi.

`asset-art.md` kuralı açık: *"Üçüncü parti klasörler asla düzenlenmez. ADR ile yamala
ya da hiç yamalama."* Bu bölüm o yamanın kaydıdır. Her yama kaynakta
`// BUNKER YAMASI (2026-09-05, ADR-0007)` yorumuyla işaretli — paket güncellenirse
`grep "BUNKER YAMASI"` yamaları bulur.

### Yama 1 — Facepunch 2.5.2 iki enum üyesini sildi

`LegacyCommon.cs`: Valve `P2PSessionError.NotRunningApp` ve `DestinationNotLoggedIn`
üyelerini kullanımdan kaldırdı; Facepunch onları `..._DELETED` diye yeniden adlandırdı.
İki `case` kaldırıldı, `default` dalının mesajı **zenginleştirildi** — o iki durum en
sık görülen iki bağlantı hatasıydı ve "Unknown error" demek teşhisi tamamen
kaybetmek olurdu.

### Yama 2 — Mirror 96 taşıma geri çağrılarını değiştirdi

FizzyFacepunch v4.4 "Mirror 66 uyumlu"; bizde Mirror **96.0.1** var.

| Eski (Fizzy) | Yeni (Mirror 96) | Dosya |
|---|---|---|
| `OnServerConnected(id)` | `OnServerConnectedWithAddress(id, adres)` | `LegacyServer.cs`, `NextServer.cs` |
| `OnServerError(id, Exception)` | `OnServerTransportException(id, Exception)` | `LegacyServer.cs`, `NextServer.cs` |

Mirror 96 hata kanalını ikiye ayırmış: `OnServerError(TransportError, string)`
**beklenen** hatalar için, `OnServerTransportException(Exception)` **beklenmeyenler**
için. Fizzy bir `Exception` üretiyor, yani doğru kanal ikincisi. Steam taşımasında
"adres" bir IP değil **SteamId**'dir; `ServerGetClientAddress` zaten onu döndürüyor.

### Bugün çalışan hâli

- Menü sahnesindeki `NetworkManager`: **FizzyFacepunch bağlı** (App ID 480),
  **KcpTransport duruyor** (Steam kapalıyken adresle katılma yolu).
- `Net/SteamLobby.cs`: oda açılınca lobi kurar, host kimliğini lobi verisine yazar,
  arkadaş listesinden gelen daveti karşılar ve `SessionSignals.RequestJoin` çağırır.
- Duraklatma menüsünde **"ARKADAŞ DAVET ET (Steam)"** ve **katılma kodu + kopyala**.
  İkisi birden var: davet penceresi yalnızca oyun Steam üzerinden başlatıldığında
  güvenilir çalışır, kod her koşulda çalışır.
- `Bunker/Steam/Kurulumu Kontrol Et` aracı kurulumu denetler ve bağlar.

### Kalan risk

**Henüz iki makinede denenmedi.** `netcode.md`'ye göre bir ağ hikâyesi tek makinede
bitmiş sayılmaz: host + geç katılan, oyun ortasında katılma, kopan bağlantı ve 150 ms
gecikme / %5 kayıp altında test şart. Bu, bir sonraki oturumun işi.

**App ID 480 herkese açıktır.** Aynı anda 480 ile test eden yabancıların lobileri de
görünebilir; `SteamLobby` bu yüzden lobi verisinde `bunker.host` anahtarını arıyor ve
bulamazsa katılmıyor. Açık lobi listeleme ekranı kendi App ID'miz gelmeden yazılmamalı.
