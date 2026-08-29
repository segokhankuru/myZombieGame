# ADR-0001: Netcode kütüphanesi

**Status:** Superseded (by ADR-0004)

> **DÜZELTME (2026-08-29):** Bu ADR Mirror'ı "lag compensation yok" gerekçesiyle elemişti.
> Bu bilgi **yanlıştı** — Mirror'ın lag compensation'ı var (Beta, MIT lisansı altında
> ücretsiz). Karar bu düzeltmeyle yeniden verildi: **ADR-0004**. Bu belge, gerekçe zinciri
> izlenebilir kalsın diye silinmedi.

**Date:** 2026-08-29 | **Approval:** kullanıcı (technical-director rolü)
**Reversal cost:** high

## Context

Dört oyunculu co-op, host-otoriteli, adanmış sunucu yok. Aynı anda ~40 zombi, hitscan
silahlar, sonsuz tur. Geliştirici tek kişi ve netcode hata ayıklaması onun en zayıf olduğu
alan (ağ bug'ları kod okunarak bulunmaz, zamanlama ve durum senkronizasyonu sorunudur).

Karşılanması gereken üç kısıt:

1. **Hitscan isabet doğrulaması.** Host otoriteli bir FPS'te, gecikmeli istemcinin
   gördüğü zombiye ateş etmesi lazım. Bu **lag compensation** (collider rollback)
   gerektirir. Olmazsa oyuncular hedefin önüne nişan almak zorunda kalır — oynanamaz.
2. **Hareket hissi.** Host olmayan üç oyuncu için **client-side prediction** olmadan
   hareket sünger gibi hisseder.
3. **~40 zombi bant genişliği.** Bu, hiçbir kütüphanenin hazır çözmediği kısım; her
   durumda özel toplu snapshot yazılacak. Yani kütüphane seçimi bu kısıt üzerinden
   yapılamaz — 1 ve 2 üzerinden yapılır.

Referans: `design/00-brief.md` riskli varsayım #1 (M0'da doğrulanacak).

## Options considered

| Option | Pros | Cons | Why eliminated |
|---|---|---|---|
| **Netcode for GameObjects (Unity resmî)** | Birinci parti, uzun vadeli bakım garantisi, Unity dokümantasyonu ve MPPM entegrasyonu | 2026 itibarıyla **hazır client-side prediction ve lag compensation yok**. `AnticipatedNetworkVariable` / `AnticipatedNetworkTransform` yapı taşları var, tam sistem kullanıcıya bırakılmış ve Unity dokümanı bunu "yalnızca ileri düzey kullanıcılara önerilir" diye işaretliyor | **Elendi.** Solo geliştiricinin en zayıf olduğu iki sistemi sıfırdan yazmasını gerektiriyor. Bu, projeyi öldürebilecek tek teknik karar |
| **Mirror** | On yıllık olgunluk, en büyük topluluk, OSI açık kaynak, bol örnek | Hazır prediction yok, lag compensation yok. Singleton API (tek NetworkManager) | **Elendi.** NGO ile aynı temel sorun; olgunluk avantajı eksik iki sistemi telafi etmiyor |
| **Netick** | Ücretsiz ve açık kaynak, state-sync, düşük bant genişliği ve CPU iddiası, delta snapshot yerleşik | Lag compensation ve interest management **Pro** özellik. Topluluk FishNet/Mirror'dan belirgin küçük. Steam transport durumu net değil | **Elendi.** Teknik olarak güçlü, ama solo geliştirici için "bu hatayı başka kim yaşamış" aranabilirliği gerçek bir özelliktir ve burada zayıf |
| **FishNet + Pro** | **Yerleşik client-side prediction ücretsiz sürümde** · lag compensation Pro'da hazır · tick tabanlı mimari · sıfır hotpath allocation · yerleşik object pooling · Area of Interest ücretsiz · Steam transport'ları (FishySteamworks / FishyFacepunch) aynı geliştirici tarafından bakılıyor · aktif (4.7.2, Nisan 2026) | Lag compensation, Level of Detail, kod ayıklama **ücretli** (~$60 ömür boyu). Birinci parti değil — bakım tek bir ekibe bağlı | **Seçildi** |
| Do nothing / kendi netcode'unu yaz | Tam kontrol | Prediction + reconciliation + lag compensation solo olarak aylar sürer | **Elendi.** Projenin kendisi bu değil |

## Decision

**FishNet kullanacağız, Pro lisansıyla (~$60 ömür boyu), Steam taşıması için
FishySteamworks veya FishyFacepunch ile.**

## Consequences

**Positive:**
- Prediction ve lag compensation gün bir hazır — projenin en riskli iki teknik parçası
  satın alınmış oluyor
- Steam relay taşıması aynı ekosistemde bakılıyor, NAT delme yok, oyuncu IP'si gizli
- Tick tabanlı mimari ve sıfır hotpath allocation, 40 zombilik özel snapshot yazımını
  destekliyor
- Unity Multiplayer Play Mode 1 ana + 3 sanal oyuncu destekliyor — tam olarak bu oyunun
  oyuncu sayısı. Solo netcode testi editörden yapılabilir

**Cost we are accepting:**
- **Lag compensation ücretsiz değil.** ~$60 gerekiyor. Bu, kararı verirken bilinmesi
  gereken bir düzeltmedir: eski referans doküman ücretsiz sürümde olduğunu yazıyordu,
  yanlış. Bütçe içinde önemsiz, ama gerekçeyi değiştiriyor
- **Birinci parti değil.** Unity'nin resmî çözümünü bırakıyoruz; bakım tek bir ekibe
  bağlı. Karşılığında iki kritik sistemi hazır alıyoruz
- **40 zombi senkronu yine bizim işimiz.** Hiçbir kütüphane bunu çözmüyor

**Reversal cost:** **high** — netcode kütüphanesi tüm Runtime katmanına dokunur.
Değiştirmek, ağ üzerinden çalışan her sistemi yeniden yazmak demektir. Bu yüzden karar
M0'da, oyun kodu yazılmadan önce doğrulanır.

## Implementation guidance

**Required pattern:**
- Host otoritesi: zombi AI, tur mantığı, ekonomi, hasar doğrulama, tüm ödül/istatistik
  sayaçları host tarafında
- Oyuncu hareketi: FishNet Prediction
- Hitscan: FishNet lag compensation (collider rollback), "favor the shooter"
- Zombiler: **özel toplu snapshot** — düşük tick oranı, quantize pozisyon, yalnızca Y
  rotasyonu, delta sıkıştırma, istemcide interpolasyon
- Her ağ nesnesi pool'dan

**Forbidden pattern:**
- **Zombilerde `NetworkTransform` kullanmak.** 40 nesne × yüksek tick = bant genişliği
  çöker. Bu yasak tek başına M0'ın var oluş sebebidir
- Host göçü uygulamak (kapsam dışı — `design/00-brief.md`)
- İstemci tarafında hasar veya puan hesaplamak

**Lives in:** `Assets/_Project/Code/Net` (ayrı asmdef). Oyun mantığı `Code/Systems`
içinde saf C# kalır ve FishNet'e referans vermez — kütüphane değişirse mantık hayatta kalır.

**Watch out for:** Kütüphane seçimini "40 zombiyi kim daha iyi senkronize eder" sorusuyla
yapmak. Hiçbiri yapmaz; o kod bizim. Seçim prediction, lag compensation ve topluluk
büyüklüğü üzerinden yapıldı.

## Verification

- **M0 çıkış kriteri:** 4 istemci, 40 hareketli nesne, Steam relay üzerinden senkron;
  host upload bant genişliği ölçülmüş ve kaydedilmiş. Bu ölçüm yapılmadan M1'e geçilmez.
- **Derleme kuralı:** `Code/Systems` asmdef'i FishNet'e referans **vermez**. Referans
  eklendiği an bu ADR ihlal edilmiştir ve derleyici bunu yakalar.
- **Kod incelemesi:** zombi prefablarında `NetworkTransform` bileşeni aranır.
