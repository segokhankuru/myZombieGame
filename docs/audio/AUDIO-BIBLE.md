# Ses kılavuzu — Bunker

Bu belge **henüz bir ses yönü değil.** `/audio-direction` çalışmadı; burada duran şey,
2026-09-09'da oyuna giren gerçek ses dosyalarının **lisans kaydı** ve bugünkü çalışma
kurallarının özeti. Ses yönü kilitlendiğinde bu dosya yeniden yazılır.

Kural (`audio-code.md`): **her ses dosyasının lisansı geldiği anda kaydedilir.** Yayında
bulunan kayıtsız bir ses bir gemi engelidir ve o noktada nereden geldiğini kimse
hatırlamaz. Bu dosyanın bugün var olma sebebi tam olarak budur.

---

## Lisanslar

| Paket | Kullanım | Lisans | Eklendi |
|---|---|---|---|
| Footsteps - Essentials | Oyuncu ayak sesleri (yürüme, koşu, iniş) | Asset Store Standard EULA | 2026-09-09 |
| Free Weapon Sound Effects | Altı ateşli silahın atış ve foley sesleri | Asset Store Standard EULA | 2026-09-09 |
| Music Loops Mini Set | Ana menü müziği (`Mystical Loop #1`) | Asset Store Standard EULA | 2026-09-09 |

**Standard EULA'nın anlamı:** oyun içinde kullanım serbest; **kaynak dosyayı yeniden
dağıtmak değil.** Depo herkese açılırsa bu bir yayın engeli olur — mağaza sanatıyla aynı
kısıt (ADR-0008).

Kullanılan tam dosya listesi ve hangi silahın hangi aileyi kullandığı
`docs/art/ASSET-LOG.md` içinde.

---

## İki yollu ses sistemi (ADR-0009)

| Yol | Ne çalar | Ne zaman |
|---|---|---|
| Katalog (`audio.asset`) | Mağaza kayıtları | Karşılığı varsa **önce bu** |
| Sentez (`SfxBank`) | Çalışma anında üretilen sesler | Katalogda karşılık yoksa |

**Sessizlik hiçbir durumda kabul edilebilir bir sonuç değil.** Katalog bağlanmazsa ya da
bir silahın ailesi eksikse, ses sentez yolundan çalar. Sesi olmayan bir silah, oyun
testinde teşhis edilmesi en pahalı hata türüdür — hata yoktur, uyarı yoktur, sadece bir
şey eksiktir.

Bugün sentezle çalan olaylar: zombi sesleri, barikat, patlama, satın alma, tur
başlangıcı/sonu, isabet işareti, bıçak.

---

## Seviyeler ve ayarlar

| Kaydırak | Varsayılan | Neden |
|---|---|---|
| Ana ses | %100 | — |
| Efektler | %100 | Oyunun geri bildirimi; kısmak oyunu okunmaz yapar |
| Müzik | **%50** | Müzik bir atmosfer katmanı, bir geri bildirim değil. Tam sesteki bir döngü menü seslerinin üstüne biner ve oyuncunun ilk yaptığı şey onu kısmak olur — yani varsayılan yanlış demektir |

Müziğin ayrı bir kaydırağı olması bir tercih değil **erişilebilirlik**: ikisi aynı
kaydıraçta olsaydı, müziği kısmak için silah seslerini de kısmak gerekirdi.

---

## Müzik nerede çalar

**Yalnızca ana menüde.** Tur içinde müzik yok ve bu bir eksik değil bir karar: sürünün
sesi bir bilgi kaynağı (`Bunker.Audio`'nun kurulma sebebi — *"arkamdan gelen zombiyi hiç
duymayınca heyecanını test edemiyorum"*) ve müzik onu örter.

Müzik **sahneye** bağlı, oyuna değil: `AudioBootstrap.playMusic` menüde açık, oyun
sahnesinde kapalı — ve kapalıyken müziği ayrıca **susturuyor**. Durdurma işi menüden çıkan
koda değil, oyun sahnesinin kendisine ait; hangi yoldan girilirse girilsin sonuç aynı.

---

## Bugün eksik olanlar

Ses yönü kilitlenene kadar duran borçlar:

- **Bus haritası yok.** Tek bir ana seviye var; efekt/müzik/UI ayrımı bir mixer'a
  taşınmadı. Ducking de yok.
- **Silah başına gerçek kayıt yok.** Altı silah dört aileyi perde farkıyla paylaşıyor
  (`basePitch`). Köprü olduğu ADR-0009'da yazılı.
- **Tek zemin.** Ayak sesi yalnızca DirtyGround; zemin algılama (raycast + materyal
  eşleme) yapılmadı. Paket on iki zemin taşıyor, ikinci harita geldiğinde açılır.
- **Uzak oyuncunun ayak sesi yok** (M-02): bugün konumu ağ üzerinden gelmiyor, sesi
  yanlış yerden duyulurdu.
- **Karışım (mix) hiç yapılmadı.** Hedef ses seviyesi ve tepe tavanı ölçülmedi.
