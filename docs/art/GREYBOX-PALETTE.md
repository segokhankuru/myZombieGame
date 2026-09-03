# Gri Kutu Paleti ve Atmosfer

**Sahibi:** `art-director` · **Uygulayan:** `.claude/tools` → `Bunker.Editor.GreyboxLook`
**Son güncelleme:** 2026-09-04

---

## Bu bir sanat geçişi değildir

Gerçek görsel dil `/art-direction`'ın işi ve **stil kilidi henüz yok**. Burada yapılan
şey bir *okunabilirlik aracı*.

PILLAR-04 aynen şunu söylüyor:

> Oyuncu her an **neyin kendisini öldürdüğünü** bilmeli. Bu, sanat gelmeden önce, gri
> kutuda da doğru olmalı — okunabilirlik bir cila işi değil, bir tasarım kısıtıdır.

Ve reddettikleri arasında: *"sanat gelince okunur hale gelir" gerekçesiyle ertelenen
tasarım kararları.*

Bu palet gelmeden önce sahnedeki **her şey Unity'nin varsayılan grisiydi** — kapı,
duvar, zemin, rampa, delik kenarı ayırt edilemiyordu. Duvar silahı satın alma noktalarının
ise **hiç görseli yoktu** (boş `GameObject`), yani oyuncu mermiyi nereden alacağını
göremiyordu. Bu, BUG-004'ün ("kapı diye bir nesne yoktu") birebir aynı sınıfı.

---

## Palet

Renkler `Assets/_Project/Code/Editor/GreyboxLook.cs` içinde, tek bir tabloda.
**Denge sayısı değil** → `config/` içinde değil (config-data.md config'i *denge* için
ayırır).

| Rol | Renk | Değer | Neden bu |
|---|---|---|---|
| `Floor_Ground` | koyu sıcak gri | en koyu | Zemin. Üstünde duran her şey ondan ayrılsın |
| `Floor_C` | biraz açık | koyu-orta | Üst kat. Alt kattan **açık** — hangi kattasın, bakmadan bilinir |
| `Wall_`, `Partition_` | nötr gri | orta | Arka plan. Dikkat çekmemeli |
| `Ramp_` | mavi-gri | orta | Geçilebilir yüzey duvardan ayrılır — kaçış yolu okunur olmalı |
| `Apron_` | çok koyu, mat | en koyu | Dış zemin. Zombilerin geldiği yer; iç mekândan ayrı |
| `DropLip_` | soluk sarı | **açık** | Düşme deliğinin kenarı. Görülmeyen bir delikten düşmek PILLAR-04 ihlali |
| Kapı (kilitli) | kehribar, **emisyonlu** | parlak | Etkileşim noktası. Karanlık köşede görünmeyen bir kapı, olmayan bir kapıdır |
| Kapı (açılmış) | sönük kehribar | koyu | Durum değişikliği görünür olmalı |
| Duvar silahı | camgöbeği, **emisyonlu** | parlak | Kapıdan **farklı sınıf** bir etkileşim; renk de sınıf da ayrı |

### Bilgi renkle tek başına taşınmıyor

Her rolün rengi **ve** parlaklık değeri farklı. Ekran gri tonlamaya düşse, oyuncu renk
körü olsa ya da ekran kalitesiz olsa bile zemin / duvar / rampa / delik kenarı / etkileşim
yüzeyi ayrılır. `ui-code.md`'nin "hiçbir bilgi yalnızca renkle taşınmaz" kuralının 3B
karşılığı.

---

## Işık, gökyüzü, sis

Unity'nin varsayılanları bu oyun için yanlış: tek beyaz yönlü ışık + düz gri gökyüzü +
sissiz sahne, gri kutuyu **düz bir yüzeye** çevirir. Derinlik ipucu kalmaz, mesafe
okunamaz — ve mesafe okunamayan bir nişancı oyununda tehdit de okunamaz.

| Ayar | Değer | Neden |
|---|---|---|
| Anahtar ışık | 38° yükseklik, 145° yön | **Alçak ve yandan.** Uzun gölgeler mesafe ipucu verir. Tepeden gelen ışık her yüzeyi aynı parlaklıkta gösterir ve sahne düzleşir |
| Işık rengi | hafif sıcak beyaz | Soğuk ortam ışığıyla kontrast |
| Ortam | üç renkli (gök / ufuk / yer) | Gölgedeki yüzey tamamen siyah olmasın |
| Sis | doğrusal, 18 m → 75 m | Mesafeyi okutur ama **yakını kapatmaz**: 18 m'den önce hiçbir şey solmaz, çünkü tehdidin okunması gereken mesafe orası |

Sahnede zaten bir yönlü ışık varsa araç **yenisini yaratmaz** — iki yönlü ışık iki gölge
yönü demektir ve ışığın nereden geldiği okunmaz olur.

---

## Atmosfer katmanı — ve neden kapatılabilir

`Assets/_Project/Settings/AtmosphereProfile.asset`, sahnedeki `_Atmosphere` nesnesinde
global bir `Volume` olarak.

| Efekt | Ayar | Neden |
|---|---|---|
| Tonemapping | Neutral | HDR'yi ekrana makul indirir. Olmadan emisyonlu kapı ve levha patlar, yanındaki her şeyi yutar |
| Bloom | eşik **1.1**, şiddet 0.55 | Yalnızca emisyonlu yüzeyleri parlatır. **Eşik yüksek tutuldu** — düşük eşik bütün sahneyi sisler ve tehdidi gizler |
| Vinyet | 0.28, yumuşak | Gözü nişangâha toplar. **Hafif** — ağır vinyet çevre görüşünü keser ve arkadan gelen zombiyi gizler |
| Renk derecelendirme | +0.15 pozlama, +12 kontrast, −8 doygunluk | Kontrast okunabilirliğe **hizmet eder**: düz gri bir görüntüde siluet ayırmak zordur |

> **Hiçbiri tam ekran bulanıklık değil.** Okunabilirliği düşüren bir atmosfer, atmosfer
> değil hasardır. Her efekt bir bütçe kalemi (`shader-graphics.md`: post-processing her
> piksele, her karede uygulanır) — dördü de bilinçli seçildi.

### F10 — atmosferi kapat

`AtmosphereToggle` (`Bunker.UI`), `_CombatHud` üzerinde.

**Neden şart:** M-01'in çıkış kriteri ÇK-17 tam olarak *"gri kutuda, sanatsız bir tur
döngüsü 20 dakika sonra tekrar oynatıyor mu?"* diye soruyor. Atmosfer açıkken alınan bir
"evet", **döngünün mü görselliğin mi taşıdığını söylemez.** Bu tuş ölçümü temiz gri kutuda
tekrarlayabilmek için var.

Kapanan yalnızca *atmosfer*: post-processing ve sis. **Renk dili her iki durumda da
açıktır** — o bir cila değil, PILLAR-04'ün tasarım kısıtı. Kapalıyken ekranın sol üstünde
"ATMOSFER KAPALI (F10)" yazar; hangi modda ölçtüğünü bilmeden alınan bir ölçüm, ölçüm
değildir.

---

## Aracı çalıştırma

```
powershell -NoProfile -File .claude/tools/unity-exec.ps1 -Method Bunker.Editor.GreyboxLook.ApplyBatch
```

Editörde: **Bunker → Gorunum → Gri Kutu Gorunumunu Uygula**

**Idempotent** (`editor-tools.md`): iki kez çalıştırmak bir kez çalıştırmakla aynı sonucu
verir. İkinci koşu `0 nesne/varlik guncellendi` der ve tek dosya değiştirmez. *(Bu
projede kurulum aracının aynı kuralı çiğnediği ve 25 orphan NavMesh biriktirdiği biliniyor
— bu araç o hatayı tekrarlamıyor.)*

**Sessiz başarısızlık savunmaları** — bu projedeki beş hatanın dördü hiçbir mesaj
vermemişti:

| Durum | Ne yapar |
|---|---|
| Yanlış sahne açık / harita üretilmemiş | **Hata verir ve çıkış kodu 1** döner. Eskiden "başarılı" diyordu |
| Bir nesne hiçbir role eşleşmedi | Adlarını **listeler** — `BlockoutGenerator`'daki rol öneki değişmişse tek uyarı budur |
| Kapı kanadı ya da HUD nesnesi yok | Uyarır ve neyin çalıştırılması gerektiğini söyler |

---

## Sonra ne olacak

`/art-direction` gerçek stil kilidini kurduğunda bu palet **yerini bırakır**. Buradaki
hiçbir renk bir sanat kararı değil; hepsi "bu şey şu şeyden ayrılmalı" cümlesinin
karşılığı. Kalıcı olan şey renk değil, **ayrımın kendisi**.
