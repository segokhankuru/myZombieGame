# ADR-0009 — Mağaza ses dosyaları ve ses kataloğu

- **Durum:** kabul edildi
- **Tarih:** 2026-09-09
- **Karar veren:** audio-director (geliştirici onayıyla)
- **İlgili:** ADR-0006 (kısmen supersede edilir), ADR-0008 (mağaza sanatı), `.claude/rules/audio-code.md`

## Bağlam

ADR-0006 **varlıksız ses** kararıydı: `Bunker.Audio` sesleri çalışma anında sentezliyor,
proje tek bir `.wav` taşımıyordu. Gerekçesi iki ayaklıydı ve ikisi de doğruydu:

1. Ses yönü kilitlenmemişti (`/audio-direction` çalışmamıştı); kilitlenmeden alınan ses
   sonra atılacak iş.
2. Her ses dosyası bir **lisans kaydı borcu** getirir ve kayıtsız bir ses, yayında
   bulunduğunda bir gemi engeli olur (`audio-code.md`).

2026-09-09'da birinci ayak değişti. Geliştirici üç mağaza paketi ekledi:

| Paket | Ne getiriyor |
|---|---|
| Footsteps - Essentials | On iki zemin için yürüme, koşma, iniş varyantları |
| Free Weapon Sound Effects | Tabanca, tüfek, pompalı ve bombaatar aileleri (atış + foley) |
| Music Loops Mini Set | Üç "Mystical" müzik döngüsü |

Ve üç istek geldi: menüde müzik çalsın, karakterin ayak sesi olsun, silahlara uygun ses
seti uygulansın.

Sentezlenmiş sesin ulaştığı sınır da ölçülmüştü: sentez, **bir şeyin olduğunu**
söyleyebiliyor ama *ne* olduğunu söyleyemiyor. Ayak sesi buna en açık örnek — sentezle
üretilmiş bir adım sesi, adım gibi duyulmadığı için hiç konmamıştı.

## Karar

**Mağaza ses dosyaları oyuna girer; sentezlenmiş yol silinmez, yedek olarak kalır.**

Üç parça:

1. **`AudioCatalogAsset`** (ScriptableObject) — menü müziği, ayak sesi varyantları ve
   silah başına ses ailesi. `AudioIntegration` üretir, elle düzenlenmez.
2. **`GameAudio.PlayClip`** — katalogdan gelen klipleri, `SfxId` yolunun kullandığı
   **aynı** havuz, öncelik ve mesafe eğrisiyle çalar.
3. **`AudioBootstrap`** — sahne başına katalogu bağlar ve müziğin bu sahnede çalıp
   çalmayacağını söyler.

**Katalog bir yükseltmedir, bir bağımlılık değil.** Bağlanmazsa ya da bir silahın ailesi
eksikse, o ses ADR-0006'nın bıraktığı sentez yolundan çalar. Sessizlik hiçbir durumda
kabul edilebilir bir sonuç değil: sesi olmayan bir silah, oyun testinde teşhis edilmesi
en pahalı hata türüdür.

### Neden bir katalog varlığı, `Resources.Load` değil

`Resources.Load` yeni kodda yasak (`csharp-code.md`) ve burada sebebi çok somut: yanlış
yazılmış bir yol **çalışma anında sessizce** `null` döner. Ses hiç çıkmaz, hata yoktur,
uyarı yoktur. Serileşmiş bir referans dosya taşındığında bile bozulmaz; silindiğinde ise
`AudioIntegration` içe aktarma anında yüksek sesle söyler.

### Neden perde kaydırma

Pakette **dört** ses ailesi var, oyunda **altı** silah. Uzi ile AK-74 aynı tüfek kaydını
paylaşıyor. Aynı perdeden çalarlarsa oyuncu ikisini kulağıyla ayırt edemez — oysa silahın
kimliğinin yarısı sesidir. `WeaponSounds.basePitch` bunu köprüler: Uzi yukarı (1.22),
M107 aşağı (0.62), AK ve M4 birbirinden bir tık ayrı.

**Bu bir çözüm değil, bir köprü** ve öyle olduğunu kaydediyoruz. Gerçek cevap silah
başına kayıttır; ses yönü kilitlendiğinde gelecek. O güne kadar altı silahın altı ayrı
duyulması, altısının aynı duyulmasından iyi.

### Neden yalnızca DirtyGround

Ayak sesi paketi on iki zemin getiriyor. Oyunun tek haritası var ve zemini beton/toprak.
On ikisini de bağlamak, on birinin hiç kullanılmadığı bir katalog üretirdi; zemin algılama
(raycast + materyal eşleme) gerçek bir iş ve bu sürümün kapsamında değil. Diğer zeminler
pakette duruyor — ikinci harita geldiğinde `AudioIntegration`'a bir satır yazılır.

### Müzik sahneye bağlıdır, oyuna değil

Geliştirici: *"oyuna katılınca bu ses çalmasın, sadece menü müziği olacak."*

`AudioBootstrap.playMusic` ana menüde açık, oyun sahnesinde **kapalı** — ve kapalıyken
müziği ayrıca **susturuyor**. Yani durdurma işi menüden çıkan koda değil, oyun sahnesinin
kendisine ait. Hangi yoldan girilirse girilsin (menü, doğrudan sahne, yeniden bağlanma)
sonuç aynı. Durdurmayı çağıran tarafa bırakmak, unutulan bir yolun müziği çatışmanın
üstünde bırakması demekti.

Tur içinde müzik **yok** ve bu bir eksik değil bir karar: sürünün sesi bir bilgi kaynağı
(ADR-0006'nın kurulma sebebi) ve müzik onu örter.

## Sonuçlar

**Kazanılan**

- Ayak sesi, silah sesi ve menü müziği gerçek kayıtlarla çalıyor
- Müzik seviyesi ayrı bir kaydıraçta (varsayılan %50) — erişilebilirlik, tercih değil
- Katalog üretilebilir: paket güncellendiğinde tek komut

**Ödenen**

- **Lisans borcu artık gerçek.** Üç paketin lisansı `docs/audio/AUDIO-BIBLE.md`'ye
  kaydedilmeli — `audio-code.md`'nin kuralı: lisans **geldiği anda** kaydedilir.
- Build boyutu artıyor (yaklaşık 40 klip)
- İki yollu bir ses sistemi: bir sesin nereden geldiğini anlamak için katalogda karşılığı
  olup olmadığına bakmak gerekiyor

**ADR-0006 hangi kısmında geçerli kalıyor**

Assembly kararı (`Bunker.Audio`, statik cephe, havuzlanmış kanallar, öncelik ve ses
limiti) **tamamen geçerli**. Değişen tek şey kliplerin nereden geldiği. Supersede edilen
kısım yalnızca "proje ses dosyası taşımaz" cümlesi.

## Geri dönüş

Katalog referansı sahnelerden kaldırılır; her şey sentezlenmiş yola düşer ve oyun
çalışmaya devam eder. Bu, kararın ucuz olmasının sebebi: iki yol da ayakta.
