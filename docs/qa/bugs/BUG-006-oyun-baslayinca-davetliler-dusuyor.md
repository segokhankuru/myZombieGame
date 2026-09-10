# BUG-006 — Oyun başlayınca davetliler giremiyor, host tek başına kalıyor

**Durum:** düzeltildi, iki makineli oyun testiyle **henüz doğrulanmadı** · **Bulan:** geliştirici (Steam co-op testi) · **Tarih:** 2026-09-10
**Şiddet:** kritik — co-op hiç çalışmıyordu · **İş:** M-04 / ADR-0007

---

## Belirti

Steam'den davet gönderildi, arkadaş lobiye girdi ve listede göründü. Host "BASLAT"a
bastı: host oyuna girdi, **davetliler giremedi**. Ekranda kırmızı hatalar:

- `HandleData Unknown connectionId:1` (art arda)
- `Could be one of the following: Steam is closed ... Calling SteamClient.Init but is already initialized`

## Kanıt (`Player.log`, host makinesi)

```
Client with SteamID ...008 connected. Assigning connection id 1   <- arkadaş lobide
Multiple NetworkManagers detected ... duplicate will be destroyed <- BASLAT, oyun sahnesi
[Bunker] Tasima: KcpTransport (Yerel ag (KCP))                    <- KOPYA taşımayı değiştirdi
```

Aynı üç satır, aynı oturumda roller değişip bu makine **katılan** olunca da çıktı.
Yani iki tarafta da aynı hata.

## Kök sebep

Oyun sahnesinde (`M0-Sandbox`), sahneye doğrudan Play basılabilsin diye ikinci bir
`BunkerNetworkManager` duruyor (yalnızca KCP taşımalı). Menüden gelen manager sahneler
arasında yaşadığı için Mirror bu kopyayı `base.Awake()` içinde yok etmeye karar verip
**dönüyor**.

Ama `BunkerNetworkManager.Awake` **devam ediyordu** ve kopyanın
`EnsureUsableTransport()`'u **statik** `Transport.active`'i kendi KCP'sine yazıyordu.
Mirror bütün ağ trafiğini `Transport.active` üzerinden pompalar. Sonuç:

- **Host:** yerel istemci taşıma kullanmaz, oyuna girdi. Steam sunucusu artık hiç
  pompalanmıyordu.
- **Davetli:** sahneyi yükledi, kopya onda da taşımayı KCP'ye çevirdi. "Hazırım" mesajı
  bağlı olmayan KCP'ye gitti, oyuncusu hiç yaratılmadı.
- **`HandleData Unknown connectionId:1`:** host menüye dönünce `StopHost` Steam'i değil
  KCP'yi kapattı. Steam sunucusu **zombi** olarak açık kaldı, arkadaşın paketlerini
  artık var olmayan bağlantıya iletti.
- **`SteamClient.Init ... already initialized`:** menü yeniden yüklenince yeni
  FizzyFacepunch, açık olan Steam'i ikinci kez başlatmaya çalıştı. Yanıltıcı ama
  zararsızdı: Steam kapalı değildi.

## Düzeltme

1. `BunkerNetworkManager.Awake`: `singleton != this` ise **hemen dön**. Aynı koruma
   `OnEnable`, `Update` ve `LateUpdate`'te de var. Kopyanın `LateUpdate`'i Mirror'ın
   statik sahne yükleme durumunu da bitirebiliyordu.
2. `Use()`: oturum açıkken taşıma değişikliğini **reddeder ve hata basar**. Bu sınıf
   bir daha sessiz olamaz.
3. `FizzyFacepunch.Awake` (ADR-0007 yaması): `SteamClient.IsValid` ise `Init` çağrılmaz.

## Doğrulama (bekliyor)

İki makinede **yeni build** ile: davet → lobi → BASLAT → iki oyuncu da sahada.
Başarı kanıtı olarak `Player.log`'da `Multiple NetworkManagers` satırından sonra
`[Bunker] Tasima: KcpTransport` satırı **olmamalı**.

## Alınan ders

**Mirror'ın "yok edilecek kopya" kararı, türetilmiş sınıfın `Awake`'ini durdurmaz.**
`base.Awake()` bir karar döndürmüyor; kopyanın kalan kodu çalışır ve statik bir alana
yazıyorsa canlı oturumu bozar. Singleton kopyası olabilecek her bileşen, statik bir şeye
dokunmadan önce `singleton == this` diye sormalı.
