# SYS-03 — Silah Çeşitliliği ve Alışkanlık

**Durum:** taslak · **Sahibi:** game-designer
**Sütunlar:** PILLAR-01 (build kimliği) · PILLAR-03 (kesintisiz ritim)

---

## 1. Çözülen problem

Tur bazlı zombi oyunlarının bilinen çürümesi: **20. turdan sonra herkes aynı iki silahı
kullanır.** Sebebi basit — silahlar yalnızca DPS ve mermi kapasitesiyle farklılaşırsa,
turlar zorlaştıkça en yüksek DPS matematiksel olarak kazanır ve geri kalan on silah
dekor olur.

İki mekanizmayla kırıyoruz:

1. **Silahlara istatistik değil, *iş* veriyoruz.** Her sınıfın tur yapısı içinde
   yeri var.
2. **Silah kullandıkça büyüyor.** Bir silahı elinde tutmanın, daha iyi bir silaha
   geçmemenin bir bedeli ve bir ödülü oluyor.

---

## 2. Alışkanlık sistemi

Her oyuncu için her **silah tipi** ayrı bir öldürme sayacı tutar. Sayaç run boyunca birikir,
run bitince sıfırlanır. Silahı bırakıp aynı tipi tekrar satın alırsan **alışkanlığın
korunur** — yoksa sistem mermi ekonomisini cezalandırır.

| Seviye | Eşik (öldürme) | Ne verir | Neden bu sırayla |
|---|---|---|---|
| 1 | ~25 | **Nicel** — +%15 hasar, +%10 şarjör | Anında hissedilir, sistemi öğretir |
| 2 | ~75 | **Ergonomi** — hızlı dolum, azalan geri tepme, hızlı nişan alma | Silahın *hissi* değişir. Bir silahı "seninki" yapan şey budur |
| 3 | ~175 | **Nitelik** — silah sınıfına özgü bir kural kazanır (§3) | Silah artık farklı bir şey yapıyor, daha iyi bir şey değil |
| 4 | ~350 | **İmza** — görsel değişim + benzersiz efekt | Nadiren görülür. Görüldüğünde olay olur |

Eşikler `config/balance/weapons.json` içinde yaşar. Yukarıdakiler oran fikri, karar değil.

**Görünürlük zorunlu:** silah HUD'unda ince bir ilerleme çubuğu. "Dolmak üzere olan çubuk"
en ucuz dopamin kaynaklarından biridir ve burada bedava geliyor.

---

## 3. Seviye 3 nitelikleri — sınıf kimliği

Nitelik silahı güçlendirmez, **farklılaştırır**. Sınıfın işini keskinleştirir.

| Sınıf | İşi (tur yapısı içindeki yeri) | Seviye 3 niteliği |
|---|---|---|
| Tabanca | Erken tur ekonomisi — ucuz mermi, yüksek puan/mermi verimi | Çekme hızı iki katı; bıçaktan sonra anında ateş |
| SMG | Ucuz kalabalık temizliği | Ateş ettikçe atış hızı artar (yükselme), bırakınca düşer |
| Pompalı | Kapı ve darboğaz reddi | Vuruşlar zombileri geri iter |
| Tüfek | Hassasiyet, zırhlı hedef | Kafa vuruşları zırhı deler |
| Keskin nişancı | Uzun görüş hatları | Tek atış bir hizadaki 3 zombiyi deler |
| Ağır | Sürekli sürü baskısı | Yürürken ateş etme cezası kalkar |
| Bıçak | Mermi harcamayan puan aracı | Öldürme, ikincil silahın şarjörünü doldurur |

---

## 4. Kartlarla ilişkisi — dik durmalı

| | Kartlar | Alışkanlık |
|---|---|---|
| Cevapladığı soru | Ben kimim? | Bu silahla ne kadar vakit geçirdim? |
| Nasıl kazanılır | Seçim | Kullanım |
| Nasıl kaybedilir | Kaybedilmez | Silahı bırakırsan durur (sıfırlanmaz) |

Yıkım build'in her silahla çalışır; seviye 3 pompalın her build'le çalışır. Çarpışırlar,
kesişmezler — çeşitlilik oradan çıkar.

**"Silaha özel nitelik" kartları (senin fikrin) bu ikisini birbirine bağlayan köprü.**
Öneri: bu kartlar draft havuzunda **o an en yüksek alışkanlığa sahip olduğun silaha doğru
ağırlıklandırılsın.** Oyun ne kullandığını fark eder ve ona göre kart önerir. Böylece
"sıradan silahı önemli kılmak" fikri kendiliğinden çalışır — sıradan silahı taşıyan
oyuncuya, tam o silahı özel yapacak kart gelir.

---

## 5. Bunun ürettiği asıl karar

Rastgele silah dağıtıcısı klasik modda saf yükseltmedir: çıkan şey daha iyiyse alırsın.
Alışkanlık sistemi bunu **gerçek bir karara** çeviriyor:

> Elinde seviye 3 bir SMG var. Dağıtıcıdan seviye 0 bir wonder weapon çıktı.
> Hangisi daha güçlü — ve daha önemlisi, 40 öldürme sonra hangisi daha güçlü olacak?

Klasik modda böyle bir soru yok. Bu, tek başına oyunun farklılaştırıcılarından biri ve
senin fikrinden çıktı.

---

## 6. Yayında kaç silah olacağı

`<tbd>` — kapsam kararı ertelendi. Ama bu sistemin bir sonucu var: **silah sayısı
düşük tutulabilir.** Yedi sınıf × dört seviye × kart etkileşimi, on iki jenerik silahtan
daha fazla çeşitlilik üretir. Az silah + derin silah, çok silah + sığ silahtan iyidir ve
solo geliştirici için ucuzdur (her silah 9+ animasyon demek).

---

## 7. Açık kalanlar

- Eşik eğrisi doğrusal mı üstel mi — M2 telemetrisi karar verecek
- Wonder weapon alışkanlık kazanır mı? Öneri: **hayır** — zaten imza silahı, seviye
  atlaması onu build'in önüne geçirir ve PILLAR-01'i çiğner
- Seviye 4 "İmza" görsel değişimi sanat işi; M3'e kadar yer tutucu
- Alışkanlık host tarafında tutulur (SYS-01 ile aynı sayaç hattı)
