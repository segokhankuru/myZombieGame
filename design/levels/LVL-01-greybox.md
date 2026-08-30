# LVL-01 — Gri Kutu Harita

**Durum:** taslak · **Sahibi:** level-designer · **İş:** M1-03
**Sütunlar:** PILLAR-02 (takım muhtaçlığı) · PILLAR-03 (kesintisiz ritim) · PILLAR-04 (okunabilirlik)

---

## Karar hatırlatması

Yapı klasikten alınıyor: **iki katlı tek bina, başlangıç odası + kapıyla açılan iki
genişleme = üç bölge.** Kat planı bizim — mekanik iskelet ve tempo aynı, geometri özgün
(`docs/DECISIONS.md`, 2026-08-29).

Sıfır sanat. Gri kutular. Doku yok, ışık yok, prop yok.

---

## Bu türde haritayı öldüren tek şey

**Kapalı bir koşu döngüsü yoksa harita 20. turda ölür.**

Yüksek turlarda zombiler oyuncudan hızlı olur ve tek hayatta kalma yolu sürüyü peşine
takıp dönmektir. Çıkmaz sokak = ölüm. Bu yüzden aşağıdaki her şey ikinci plandadır;
**önce döngünün kapandığından emin ol.**

```
        [C] UST KAT
         |      \
     merdiven   delik
         |         \
        [A] -------- [B]
      BASLANGIC     YAN KANAT
```

A → B → C → A. Üç bağlantı, üç bölge, kapalı çember. Bir bağlantı koparsa döngü ölür.

---

## Bölgeler

### A — Başlangıç odası (zemin kat) · **ücretsiz**

| | |
|---|---|
| Ölçü | ~12 × 10 m, tavan 3 m |
| Pencere | **4** (barikatlı) |
| Duvar silahı | 1 adet, ucuz (tabanca sınıfı) |
| Çıkışlar | Kapı → B · Merdiven kapısı → C |

Tur 1–5 burada geçer. Küçük ve kapalı olmalı: oyuncu dört pencereyi tek başına
tutabilmeli ama zorlanmalı. Çok büyük olursa erken turlar sıkıcı, çok küçük olursa
tur 3'te boğulur.

### B — Yan kanat (zemin kat) · **kapı ile açılır**

| | |
|---|---|
| Ölçü | ~14 × 8 m, **L şeklinde** |
| Pencere | 3 |
| Duvar silahı | 1 adet, orta (pompalı sınıfı) |
| Çıkışlar | A'ya kapı · A'ya **ikinci bağlantı** (döngüyü kapatır) · C'ye delik |

L şekli önemli: köşe, oyuncuya sürüden görsel kopuş sağlar — kaçarken nefes alma anı.
Düz dikdörtgen bunu vermez.

### C — Üst kat · **kapı ile açılır**

| | |
|---|---|
| Ölçü | ~12 × 10 m |
| Pencere | 3 |
| Özel | **Rastgele silah dağıtıcısı burada** |
| Çıkışlar | Merdiven → A · Delik/atlama → B (tek yön, aşağı) |

Dağıtıcı bilerek en uzak bölgede: oyuncuyu ödül için döngünün tamamını dolaşmaya
zorlar. Aşağı inen tek yönlü delik, kaçış hattı — yukarıda sıkışan oyuncunun çıkışı var.

---

## Ölçü kuralları

| Şey | Ölçü | Neden |
|---|---|---|
| Oyuncu boyu | 1.8 m | `Player.prefab` CharacterController |
| Koridor genişliği | **2–3 m** | İki oyuncu yan yana geçer, sürü tek hatta sıkışır |
| Darboğaz | **1.5 m** | Tek oyuncunun sürüyü tutabileceği nokta |
| Tavan | 3 m | Kapalı hissi, ama sıkışık değil |
| Kapı boşluğu | 1.5 × 2.5 m | — |
| Pencere | 1.5 × 1.2 m, zeminden 1 m | Zombi tırmanır, oyuncu tamir eder |

**Her bölgede en az bir darboğaz olsun.** Oyuncunun "burayı tutabilirim" diyeceği yer,
tur bazlı oyunun temel taktiğidir.

---

## Yapım sırası

ProBuilder yok, ölçekli küpler yeterli. Her bölgeyi ayrı boş GameObject altında topla:
`Zone_A`, `Zone_B`, `Zone_C`.

1. **Zemin** — mevcut `Ground` plane'i sil, yerine her bölge için ayrı kalın küp koy
   (kalınlık 0.5 m, üst yüzey y=0)
2. **Duvarlar** — küpleri 0.3 m kalınlığında ölçekle. Önce dış duvarlar, sonra iç
3. **Pencere boşlukları** — duvarı üçe böl: alt parça (0–1 m), üst parça (2.2–3 m),
   yanlar. Ortada boşluk kalsın
4. **Kapı boşlukları** — aynı mantık, zeminden 2.5 m'ye kadar boşluk
5. **Üst kat** — y = 3.5 m'de zemin küpü, merdiven yerine **rampa** (küpü döndür,
   ~30°) — merdiven basamağı NavMesh'te sorun çıkarır, gri kutuda rampa yeterli
6. **NavMesh bake** — `NavMesh Surface` bileşeni sahnenin köküne, Bake

**Bake sonrası döngüyü doğrula:** NavMesh mavi katmanı A'dan B'ye, B'den C'ye ve C'den
A'ya kesintisiz gitmeli. Kopukluk varsa rampa eğimi fazladır (NavMesh varsayılan max
eğim 45°) ya da bir boşluk 2 m'den dardır.

---

## Yerleştirme işaretleri

Şimdilik boş GameObject'ler yeterli, isimlendirme önemli — M1-05'ten itibaren kod
bunları arayacak:

```
Zone_A/Windows/Window_A1 .. A4      pencere + barikat noktalari
Zone_A/WallBuy_01                   duvar silahi
Zone_A/Doors/Door_A_to_B            kapi (puanla acilir)
Zone_A/Doors/Door_A_to_C
Zone_A/SpawnPoints/Spawn_A1 ..      zombi spawn (pencere disi)
Zone_C/MysteryBox                   rastgele dagitici
PlayerSpawn                         oyuncu baslangici (Zone_A ortasi)
```

Zombi spawn noktaları **pencerelerin dışında**, oyuncunun göremeyeceği yerde olmalı.
Zombinin görünür şekilde "hiçlikten belirmesi" PILLAR-04'ü çiğner.

---

## Bu haritada olmayanlar

- Tuzaklar (M-01 kapsamı dışı)
- FORGE-9 yükseltme istasyonu
- Sanat, doku, ışık, prop
- İkinci kat dışında dikey çeşitlilik
- Gizli alanlar, easter egg

---

## Açık kalanlar

- Kapı fiyatları — ekonomi (M1-02) kalibre edildikten sonra
- Pencere başına barikat tahtası sayısı — `config/balance/` içine girecek
- Tur 1 zombi spawn oranının bölgelere dağılımı — M1-05
