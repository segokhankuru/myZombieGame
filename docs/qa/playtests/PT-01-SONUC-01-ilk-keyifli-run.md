# PT-01 / Sonuç 01 — ilk "keyifliydi" run'ı

**Tarih:** 2026-09-05 · **Oyuncu:** geliştirici, solo, editörde · **Süre:** 7:49
**Yapı:** kart sistemi + tezgâh dahil (`80e9eff`)

---

## Geliştiricinin kendi cümlesi

> *"7 dk kapıştım keyifliydi tezgah falan gayet iyi çalışıyor"*

**Bu projedeki ilk olumlu oynanış verisi.** Kayda değer, çünkü M-01 boyunca on bir iş
"kod bitti, hiç oynanmadı" durumundaydı ve temelin eğlenceli olup olmadığı bilinmiyordu.

**Ama ÇK-17 DEĞİL.** ÇK-17 "20 dakika sonra tekrar oynamak istiyor musun" diye sorar; bu
7 dakika ve tek run. Ayrıca kartlar artık içeride, yani ÇK-17'nin tasarlandığı kontrol
grubu (kartsız temel) bir daha ölçülemez — bu bilinen ve kabul edilmiş bir kayıp.

---

## Telemetri

| | |
|---|---|
| Ulaşılan tur | **9** (ölümle bitti) |
| Süre | 7:49 |
| Öldürme | 103 |
| **Kafa vuruşu** | **%76** |
| Bıçak | **%0** |
| Toplam puan | 12.057 |

Tur başına süre: 29 · 33 · 33 · 37 · 42 · 46 · **69** · 62 sn

---

## BULGU 1 — simülasyonum oyuncuyu yanlış modelledi

`curves.md` iki profil varsayıyordu: "ortalama" %25 kafa, "iyi" %50 kafa.
**Gerçek oyuncu %76.** Modelin en iyi profilinden bile %50 daha isabetli.

Bu bir ayrıntı değil, **bütün ekonomi sonuçlarının girdisi**. Kafa vuruşu 2× hasar
verdiği için öldürme başına mermi sayısını doğrudan belirliyor:

| | Model (%25 kafa) | Gerçek (%76 kafa) |
|---|---|---|
| Ortalama atış hasarı | 68.8 | 96.8 |
| Tur 9'da öldürme başına isabet | 14 | 10 |

### Sonucu: KIRILMA 1 abartılıydı

`curves.md`'nin en önemli bulgusu "tur 14'te duvara 15 ayrı mermi seferi" idi ve
PILLAR-03 ihlali olarak işaretlenmişti. Gerçek isabetle yeniden koşulduğunda tur 9'a
kadar **tur başına 4 sefer**. Hâlâ çok, ama "turu parçalıyor" tanısı **kanıtlanmadı** —
ve geliştirici 7 dakikayı keyifli buldu, yani seferler o eşikte rahatsız etmiyor.

**Ders:** simülasyon oyuncu becerisi konusunda kendinden emin şekilde yanıldı, ve
`balance-check`'in kendi uyarısı ("modeller gerçek insanların olmadığı biçimlerde
yanılır") tam olarak bu şekilde gerçekleşti. **Hiçbir değeri o model yüzünden
değiştirmemiş olmak doğru karardı.**

---

## BULGU 2 — turlar hedeften HIZLI, yavaş değil

Gerçek sayılarla yeniden koşulan model tur 10'u **8.8 dakikada** veriyor; gerçek run
tur 9'a **7:49**'da ulaştı — model ile gerçek burada uyuşuyor.

**ÇK-13 hedefi 12–18 dakika.** Yani oyun hedef bandın **belirgin şekilde altında.**

Bu bir denge kararı gerektiriyor ama **henüz veri yeterli değil**: tek bir run, tek bir
oyuncu, ve o oyuncu oyunun yazarı. İkinci ve üçüncü run'dan sonra bakılmalı. Aday
düğmeler `curves.md`'de duruyor (`healthLinearAddPerRound`, `countLinearAddPerPlayerPerRound`).

---

## BULGU 3 — bıçak hiç kullanılmadı (%0)

M1-07 bir iş olarak yazıldı, kodlandı, test edildi. **103 öldürmenin sıfırı bıçakla.**

Bıçak öldürmesi en yüksek puanı veriyor (130) ve mermi harcamıyor — yani ekonomik olarak
en iyi seçenek. Oyuncu ya bunu bilmiyor, ya riskli buluyor, ya da V tuşu akışta yok.

Bu **öğretme sorunu mu, denge sorunu mu, his sorunu mu** ayrılmadan çözülemez. Bir
sonraki oturumda geliştiriciye tek soru: *"bıçağı neden hiç kullanmadın?"*

---

## Ne yapıldı

**Hiçbir denge değeri değiştirilmedi.** Tek run, tek oyuncu. `curves.md`'nin KIRILMA 1
bölümü artık yanıltıcı — düzeltilmesi gerekiyor ama yeni sayılarla, tek bir run'la değil.
