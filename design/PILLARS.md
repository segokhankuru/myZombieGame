# Tasarım Sütunları

**Sahibi:** creative-director · **Son güncelleme:** 2026-08-29

Bir sütunun işe yaraması için **bir şeyi reddedebilmesi** gerekir. Her sütunun altındaki
"Reddettiği" satırı sütunun kendisi kadar bağlayıcıdır — o satır olmadan sütun süstür.

Yeni bir özellik önerildiğinde sorulacak soru: *bu dördünden hangisine hizmet ediyor, ve
hangisini zayıflatıyor?* İkincisinin cevabı varsa özellik değişmeden girmez.

---

## PILLAR-01 — Build kimliği silahtan önce gelir

20. turda oyuncu "elimde pompalı var" değil, **"ben yanıcı-kan build'iyim"** demeli.
Silah bir araç; kart yığını bir kimlik. Run'ı hatırlanır kılan şey silah değil, o run'da
kim olduğun.

**Reddettiği:**
- Her run'da herkesin aldığı "doğru cevap" kartı
- Tüm build'lere eşit yarayan salt istatistik kartları (kartların en az yarısı kural
  değiştirmeli)
- Build'i gölgeleyen wonder weapon — imza silahı build'in *ifadesi* olmalı, alternatifi değil
- Silah yükseltmesinin karttan güçlü olması

---

## PILLAR-02 — Dört oyuncu birbirine muhtaç olmalı

Solo-optimal oynanış bir hata, özellik değil. Ekonomi, kartlar, diriltme ve harita
bağımlılık üretmeli. Dört nişancının paralel oynadığı bir oyun co-op değil, aynı odada
dört tek kişilik oyundur.

**Reddettiği:**
- Herkesin kendi köşesinde farm yapabildiği düzenler
- Takım kartını otomatik ve "adil" dağıtan sistem — *"kim alacak?"* anı özelliğin kendisi
- Tek başına ayakta kalabilen, kendi kendini iyileştiren kapalı devre build
- İyi oynayanı güçlendirip zorlananı geride bırakan ödül yapıları (zorlanan oyuncu tam da
  yardıma ihtiyacı olduğu anda cezalandırılmış olur)

---

## PILLAR-03 — Kesintisiz tur, birkaç turda bir dönüm noktası

Ritim: 60–90 saniye baskı, ardından kısa nefes. Kart turu bu ritmi bölen bir menü değil,
ritmin zirvesi. Oyuncu hiçbir zaman "şimdi menü işi yapıyorum" moduna geçmemeli.

**Reddettiği:**
- Envanter ve donanım yönetimi ekranları
- Tur arası uzun hazırlık fazı
- **Belirsiz süreli** bekleme — her beklemenin bir çıkışı olmalı
- Uzun animasyonlu ödül ve açılış ekranları
- Atlanamayan tekrar eden animasyonlar

> **Düzeltme (2026-08-29):** bu sütun önce "kart ekranında üç oyuncunun bir kişiyi
> beklemesi"ni reddediyordu. Draft ekranı tasarımı (`design/ux/draft-ekrani.md`) beklemeyi
> bilinçli olarak kabul ediyor — arkadaşlarla oynanıyor, beklemek sosyal bir andır ve
> aceleye getirilen seçim üç tur sonra pişmanlık üretir. **Kart ekranı oyunun tek beklemeli
> anıdır ve bu bilinçlidir; reddedilen şey belirsiz süreli beklemedir.** Bu yüzden AFK
> kaçış kapısı zorunludur, sayaç değil.

---

## PILLAR-04 — Kaosta okunabilirlik

Kırk zombi, dört oyuncu ve altı kart efekti aynı ekranda. Oyuncu her an **neyin kendisini
öldürdüğünü** bilmeli. Bu, sanat gelmeden önce, gri kutuda da doğru olmalı — okunabilirlik
bir cila işi değil, bir tasarım kısıtıdır.

**Reddettiği:**
- Ekranı dolduran, altındaki tehdidi gizleyen VFX
- Yalnızca renkle ayrılan zombi tipleri (silüet ve ses de ayırmalı)
- Sesle ayırt edilemeyen tehditler — arkadan gelen bir şey duyulmalı
- "Sanat gelince okunur hale gelir" gerekçesiyle ertelenen tasarım kararları

---

## Sütunlardan türeyen bağlayıcı kurallar

Bunlar sütunların doğrudan sonucudur, ayrı karar değildir:

| Kural | Kaynak |
|---|---|
| Kartların en az yarısı kural değiştirmeli, salt istatistik olmamalı | PILLAR-01 |
| Kart draft'ı eşzamanlı, sayaçlı, süre dolunca otomatik seçim | PILLAR-03 |
| Ödül metrikleri yalnızca "istismarı zaten istenen davranış olan" şeyleri ölçer | PILLAR-02 |
| Kill sayısı bir manşet metriği değildir | PILLAR-02 |
| Her zombi tipi bir rahat alışkanlığı iptal eder, HP varyantı değildir | PILLAR-04 |
