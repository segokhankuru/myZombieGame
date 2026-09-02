# ADR-0005 — Config importer ve `Bunker.Config` assembly'si

**Durum:** kabul edildi · **Tarih:** 2026-09-02 · **Karar veren:** unity-architect
**İlgili:** `.claude/docs/config-protocol.md`, `M-01` borç kaydı, ADR-0002

---

## Bağlam

M-01'e üç config alanıyla girildi: `rounds`, `economy`, `zombie`. Üçünün de sayıları
**iki yerde** duruyordu — `config/balance/*.json` içinde ve elle yazılmış C# sınıflarının
varsayılan parametrelerinde. İkisi elle eşleniyordu.

Bu borç M-01 planında kayıtlıydı ve tetikleyicisi de yazılıydı: *"üçüncü config dosyası
ya da ilk gerçek denge turu"*. `zombie.json` üçüncü dosya oldu.

İki yerde duran bir sayının bedeli gecikmelidir: JSON'da değiştirilir, oyun eski değerle
koşar, kimse fark etmez ve haftalar sonra "denge testinde ölçtüğümüz şey neydi?" sorusu
cevapsız kalır.

## Karar

`config/` altındaki JSON tek kaynaktır. Bir **importer** ondan hem saf C# sınıfını hem de
ScriptableObject sınıfını üretir; üretilen `.asset` dosyaları çalışma anında okunur.

```
config/schema/<alan>.schema.json   sekil, aralik, tasarim gerekcesi
config/balance/<alan>.json         sayilar
        |  ConfigImporter
        v
Code/Systems/Config/Generated/<Alan>Config.g.cs     saf C#  (Bunker.Systems)
Code/Config/Generated/<Alan>ConfigAsset.g.cs        SO      (Bunker.Config)
Assets/_Project/Config/<alan>.asset                 varlik
```

Bunun için **yeni bir assembly** açıldı: `Bunker.Config`.

### Neden yeni bir assembly

`Bunker.Systems` `noEngineReferences: true` — bu projenin temel kuralı ve testlerin
Unity açmadan koşmasının tek sebebi. Bir `ScriptableObject` orada **yaşayamaz**.

Alternatifler ve neden reddedildi:

| Seçenek | Neden hayır |
|---|---|
| `noEngineReferences`'ı kaldır | Mimarinin tabanı gider; testler Unity'ye bağlanır. Bedeli en yüksek seçenek |
| SO'ları `Bunker.Gameplay`'e koy | `Bunker.AI` Gameplay'e bağımlı değil ve olmamalı; zombi ayarına ulaşamazdı |
| SO hiç olmasın, sayılar üretilen C# sabitleri olsun | Config'i değiştirmek her seferinde **yeniden derleme** demek. Denge turu böyle yapılmaz; ayrıca `config-protocol.md`'nin çalışma anı sözleşmesini çiğner |

`Bunker.Config` yalnızca `Bunker.Systems`'e bağımlıdır ve herkes ona bağımlı olabilir.
Bağımlılık oku hâlâ tek yönlü.

### Üretilen isimlendirme: her zaman grup öneki

`count.perPlayerAtRoundOne` → `CountPerPlayerAtRoundOne`.

"Çakışma varsa öneklе" kuralı daha kısa isimler verirdi, ama şemaya yeni bir anahtar
eklendiğinde **başka** bir alanın adını sessizce değiştirirdi. Üreteçte sürpriz olmaz.

### İki geçiş, ve saklanmadı

Birinci geçiş C# üretir, Unity derler, ikinci geçiş varlıkları doldurur. Yeni üretilmiş
bir tip, onu üreten alan yüklemesi (domain) içinde var olamaz. Editör içinde ikinci geçiş
derleme sonrası kendiliğinden koşar; komut satırında iki ayrı çağrıdır. Bunu bir arka
plan durum makinesiyle gizlemek, yarım kalmış bir içe aktarmayı teşhis edilemez yapardı.

### Kendi JSON ayrıştırıcımız

`JsonUtility` yalnızca önceden bilinen tipe okur; şemayı gezmek için şekli bilinmeyen bir
belge gerekiyor. Bir paket eklemek kalıcı bir bağımlılık ve bir ADR daha demekti. Yazılan
ayrıştırıcı saf C#, `Bunker.Systems` içinde ve **15 testi var** — çünkü burada sessiz bir
hata oyunun bütün denge sayılarını bozar ve hiçbir test bunu söylemez.

Ayrıştırıcı sayıları `InvariantCulture` ile okur. Geliştirme makinesi tr-TR: kültüre
duyarlı bir okuma `"1.4"` değerini sessizce **14** yapar. On kat hızlı bir zombi, hiçbir
hata mesajı vermeden.

## Sonuçlar

**İyi**
- Bir denge sayısı artık tek yerde. JSON değişir, importer koşar, oyun yeni değerle koşar
- Aralıklar Inspector'da `[Range]`, açıklamalar `[Tooltip]` olarak görünür — tasarım
  niyeti editörde okunur
- Bilinmeyen anahtar ve eksik zorunlu anahtar **hata**; sessiz varsayılan yok
- `/tune` ve `/balance-check` hâlâ Unity açmadan, düz JSON üstünde çalışabilir

**Kötü / bedel**
- Bir assembly daha: derleme grafiği büyüdü
- Üretilen dosyalar depoda duruyor (git'te görünür gürültü), ama diff'te üretim tarihi
  değil yalnızca içerik değişiyor — `WriteIfChanged` aynı içeriği yeniden yazmaz
- Şema değiştiğinde iki geçiş koşmak gerekir; unutulursa "alan bulunamadı" hatası verir
  (sessiz kalmaz)

**Bundan sonra**
- Yeni bir tunable **serialized field olarak eklenmez**: şemaya anahtar eklenir, değeri
  `systems-designer` verir, importer koşar
- `Assets/_Project/Config/*.asset` ve `*.g.cs` dosyaları **elle düzenlenmez**
- M1-05'te gelecek boot servisi config'i tek yerden çözüp aşağı verecek; şu an tezgâh
  kendi referanslarıyla enjekte ediyor
