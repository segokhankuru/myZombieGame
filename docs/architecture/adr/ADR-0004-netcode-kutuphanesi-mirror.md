# ADR-0004: Netcode kütüphanesi — Mirror

**Status:** Accepted (ADR-0001'in yerini alır)
**Date:** 2026-08-29 | **Approval:** kullanıcı
**Reversal cost:** high

---

## Context

ADR-0001 FishNet + Pro (~$60) kararını vermişti. Karar iki gerekçeye dayanıyordu:
FishNet'in hazır client-side prediction'ı ve Pro'daki lag compensation'ı. Aynı ADR
Mirror'ı **"hazır prediction yok, lag compensation yok"** diyerek elemişti.

**Bu eleme gerekçesinin ikinci yarısı hatalıydı.** Mirror'ın lag compensation'ı var:
Unity'den bağımsız, tam test kapsamlı bir `LagCompensation.cs` algoritması ve collider
geçmişini otomatik kaydeden yüksek seviyeli bir bileşen. Durumu **Beta**, ve MIT lisansı
altında ücretsiz.

Kısıtlar değişmedi (ADR-0001 §Context): host-otoriteli, adanmış sunucu yok, ~40 zombi,
hitscan silahlar, tek geliştirici.

---

## Bu oyuna özgü belirleyici gözlem

> **Prediction atlanabilir, lag compensation atlanamaz.**

**Prediction neden atlanabilir:** Bu oyun davetle girilen, arkadaşlarla oynanan bir PvE
co-op. Oyuncuya kendi hareketi üzerinde otorite verirsek (client-authoritative movement)
karakter anında tepki verir ve prediction/reconciliation makinesine hiç ihtiyaç kalmaz.
Bedeli, bir oyuncunun teorik olarak konumunu manipüle edebilmesi. PvP'de bu kabul
edilemez; dört arkadaşın davetle girdiği bir lobide pratik bir karşılığı yok.

**Lag compensation neden atlanamaz:** Zombiler host otoritesinde ve oyuncu onları
vuruyor. Telafi olmazsa oyuncu, ekranında gördüğü zombinin **önüne** nişan almak zorunda
kalır. Yavaş zombide fark edilmez, Koşucu'da belirgin olur ve oyuncular bunu "vuruş kaydı
bozuk" diye okur.

Bu asimetri, iki kütüphanenin güçlü yanlarını doğrudan karşılaştırılabilir kılıyor:
FishNet prediction'ı bedava verip lag compensation'ı ücrete bağlıyor, Mirror tam tersini
yapıyor. Bu oyunun ihtiyacı Mirror'ın verdiği tarafta.

---

## Options considered

| Option | Pros | Cons | Why eliminated |
|---|---|---|---|
| **FishNet + Pro** (ADR-0001'in kararı) | Hazır prediction, hazır lag compensation, Level of Detail | Lag compensation ücretli (~$60). Prediction'ın karşılığı bu oyunda düşük, çünkü client-authoritative hareket zaten kabul edilebilir | **Elendi.** Ödenen şey (prediction) bu oyunda gerekli değil; gerekli olan şey (lag compensation) Mirror'da ücretsiz |
| FishNet ücretsiz | Prediction hazır, $0 | Lag compensation **yok** ve bu atlanamaz | Elendi |
| Netcode for GameObjects | Birinci parti | Ne prediction ne lag compensation hazır | Elendi (ADR-0001 ile aynı gerekçe) |
| Netick | Düşük bant genişliği iddiası | Lag compensation Pro; topluluk küçük | Elendi |
| **Mirror** | **MIT — hiçbir özellik kilitli değil.** Lag compensation (Beta) hazır, üstelik Unity'den bağımsız ve test kapsamlı bir algoritma olarak. Interest Management ve Snapshot Interpolation stable. On yıllık olgunluk ve **en büyük topluluk** — solo geliştirici için "bu hatayı başka kim yaşamış" aranabilirliği gerçek bir özellik. Steam taşımaları mevcut (FizzySteamworks / FizzyFacepunch) | Client-side prediction "Researching" durumunda; `PredictedRigidbody` yalnızca fizik nesneleri için. Lag compensation **Beta**. Belgelenen Unity desteği **6000.1**'e kadar yazıyor — 6000.3 LTS test edilmeli | **Seçildi** |

---

## Decision

**Mirror kullanacağız (MIT).** Oyuncu hareketi client-authoritative, zombiler ve oyun
durumu host-otoriteli. Hitscan isabeti Mirror'ın lag compensation bileşeniyle doğrulanır.
Steam taşıması FizzySteamworks veya FizzyFacepunch.

---

## Consequences

**Positive:**
- Kütüphane maliyeti **$0**, ve hiçbir özellik ücretli katmanın arkasında değil —
  bütçeden $60 çıkıyor, ama daha önemlisi ileride "bunu almadık diye yapamıyoruz"
  durumu oluşmuyor
- Lag compensation algoritması Unity'den bağımsız yazılmış; `Bunker.Systems`'in saf C#
  felsefesiyle aynı çizgide ve gerekirse okunup anlaşılabilir
- En büyük Unity netcode topluluğu — solo geliştiricinin en çok ihtiyaç duyacağı kaynak
- Interest Management ve Snapshot Interpolation stable olarak geliyor; 40 zombilik özel
  snapshot işinde (M0-07) işe yarayacak

**Cost we are accepting:**
- **Güven modeli gevşiyor.** Client-authoritative hareket ve isabet, hile yapmayı teknik
  olarak mümkün kılar. Davetle girilen arkadaş co-op'unda kabul edilebilir. **Sonuç:**
  herkese açık liderlik tabloları anlamlı olmaz; meta ilerleme (kart havuzu açma,
  SYS-01) istemciden bildirildiği için manipüle edilebilir. Bu bilinçli bir taviz
- **Lag compensation Beta.** M0-09'da doğrulanacak; tutmazsa mermi tabanlı silahlar
  değerlendirilir
- **Prediction yok.** İleride PvP veya rekabetçi bir mod istenirse bu karar yeniden açılır
- **Unity 6.3 desteği belgelenmemiş.** Mirror'ın kendi belgesi 6000.1'e kadar yazıyor.
  M0-01'in ilk işi bunu doğrulamak

**Reversal cost:** **high** — netcode kütüphanesi tüm `Bunker.Net` ve `Bunker.Gameplay`
katmanına dokunur. Ama `Bunker.Systems` her iki durumda da etkilenmez (saf C#), bu yüzden
oyun mantığı bir kütüphane değişiminden sağ çıkar. Mimarinin bu ayrımı tam olarak bunun
içindi.

---

## Implementation guidance

**Required pattern:**
- **Host otoritesi:** zombi AI, tur mantığı, ekonomi, hasar hesabı, kart draft sonucu,
  tüm ödül/istatistik sayaçları
- **Client otoritesi:** yalnızca oyuncunun kendi konumu ve bakış yönü
- Hitscan: Mirror `LagCompensation` — host, zombi collider geçmişini geri sarıp isabeti
  atıcının gördüğü anda kontrol eder
- Zombiler: **özel toplu snapshot** — düşük tick oranı, quantize pozisyon, yalnızca Y
  rotasyonu, delta sıkıştırma, istemcide interpolasyon
- Her ağ nesnesi pool'dan

**Forbidden pattern:**
- **Zombilerde `NetworkTransform` kullanmak.** 40 nesne için bant genişliği çöker.
  Bu yasak M0'ın var oluş sebebidir (M0-06 bunu ölçerek kanıtlayacak)
- İstemci tarafında hasar, puan veya kart sonucu hesaplamak — hareket dışında istemciye
  otorite verilmez
- Host göçü uygulamak (kapsam dışı, `design/00-brief.md`)

**Lives in:** `Assets/_Project/Code/Net` (Bunker.Net asmdef). Oyun mantığı
`Bunker.Systems` içinde saf C# kalır ve Mirror'a **referans vermez** —
`noEngineReferences: true` bunu derleyiciye zorlatır.

**Watch out for:** Client-authoritative hareketin kapsamının sessizce genişlemesi. Bugün
"sadece konum", yarın "bir de canım", ertesi gün "puanım da". Sınır: **oyuncu yalnızca
nerede olduğunu söyler, başka hiçbir şeyi.**

---

## Verification

- **M0-01 çıkış şartı:** Mirror, Unity 6000.3.23f1'de derleniyor ve çalışıyor
- **M0-09 çıkış şartı:** 150 ms yapay gecikmeli istemci, hareket eden hedefe 20 atışta
  20 isabet (ÇK-7)
- **Derleme kuralı:** `Bunker.Systems` asmdef'i Mirror'a referans vermez; referans
  eklendiği an derleyici hata verir
- **Kod incelemesi:** zombi prefablarında `NetworkTransform` aranır; istemciden gelen
  konum dışı hiçbir otoriter veri kabul edilmediği doğrulanır
