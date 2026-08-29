# Kart Draft Ekranı

**Durum:** taslak · **Sahibi:** game-ux-designer
**İlgili:** SYS-02 (kart sistemi) · PILLAR-02 · PILLAR-03

---

## 1. Slot makinesi dönüşü

Üç kart yuvası slot makinesi gibi dönerek yavaşlar ve nihai kartlarda durur.

**Neden doğru bir seçim:** ödülün kendisinden çok *beklentisi* haz verir. Dönüş, sonucu
geciktirerek beklentiyi uzatır — rastgele silah dağıtıcısının aynı psikolojisi, ve bu oyun
zaten o psikolojinin üstüne kurulu.

**İki teknik zorunluluk:**

1. **Sonucu host belirler, animasyon sonuca oynanır.** Kartlar önce sunucuda seçilir,
   istemciye gönderilir, animasyon bilinen sonuca doğru yavaşlar. Animasyonun sonucu
   belirlemesine asla izin verilmez — hem hile kapısıdır hem dört istemcide farklı sonuç
   üretir.
2. **Atlanabilir olmalı.** Ellinci run'da dönüş heyecan değil gecikmedir. Basılı tut →
   anında dur. İlk run'da atlama ipucu gösterilmez, ikinciden itibaren gösterilir.

---

## 2. Dört oyuncu tek ekranda

Dört oyuncunun yuvaları yan yana görünür. Kimin ne çektiği ve ne seçtiği herkes tarafından
görülür.

Bu bir kolaylık değil, **PILLAR-02'nin gereği**: takım kartı çıktığında *"kim alacak?"*
anı ancak herkes aynı kartı görüyorsa var olur. Aynı zamanda build'lerin birbirini
tamamlaması için oyuncuların birbirinin yığınını bilmesi gerekir.

---

## 3. Zamanlayıcı yok — bir istisnayla

Normal akışta **süre sınırı yoktur.** Arkadaşlarla oynanıyor; beklemek sosyal bir andır,
ceza değil. Aceleye getirilen bir seçim, üç tur sonra pişmanlık üretir.

**AFK kaçış kapısı:**

```
0 sn      Draft açılır. Süre yok, herkes düşünür.
30 sn     Seçimini yapmış oyunculara bir buton belirir:
          "Seçimi zorla" — kim basarsa bassın, seçim yapmışların hepsi basmalı.
+5 sn     Geri sayım. Bu sırada AFK oyuncu geri dönerse iptal olur.
          Sonunda kalanlar için otomatik seçim yapılır.
```

Zorlama, seçimini yapmış oyuncuların **hepsinin** onayını ister. Tek kişi zorlayabilirse
sabırsız bir oyuncu diğerlerinin kararını çalar.

---

## 4. Otomatik seçim mantığı

Rastgele seçmek, oyuncunun build'ini bozar. Sıra:

1. **Etiket eşleşmesi** — oyuncunun en çok sahip olduğu etikete ait kart varsa onu seç.
   Bir oyuncu Yıkım build'i kuruyorsa ve seçenekler arasında Yıkım kartı varsa, o gelir.
2. **İki etikete ikinci kart** — bir etiketten tam 2 kartı varsa (bonusa bir adım kala)
   o etiket önceliklidir.
3. **Nadirlik** — eşleşme yoksa en nadir kart.
4. **Sol yuva** — hepsi eşitse.

Otomatik seçim yapıldığında oyuncuya döndüğünde ne aldığı açıkça gösterilir.

---

## 5. Gösterilmesi zorunlu bilgi

- Oyuncunun mevcut kart yığını ve **her etiketten kaç tanesi olduğu** — bonusa bir adım
  kalmışsa bu vurgulanır (yakın hedefe ilerleme, ucuz ve güçlü)
- Kartın etiketi, ilk bakışta ayırt edilecek şekilde (renk **ve** simge — PILLAR-04:
  yalnızca renk yetmez)
- Takım kartı ise bunun açıkça işaretlenmesi
- Solo modda co-op kartları hiç gösterilmez (SYS-02 §6)

---

## 6. PILLAR-03 ile çatışma ve çözümü

PILLAR-03 şunu reddediyordu: *"kart ekranında üç oyuncunun bir kişiyi beklemesi."*
Bu tasarım beklemeyi bilinçli olarak kabul ediyor. Çatışma gerçek, örtülmedi.

**Çözüm ve sütunun düzeltilmiş hâli:** kart ekranı oyunun tek beklemeli anıdır ve bu
bilinçlidir — ama **bekleme asla mecburi olmaz; her beklemenin bir çıkışı vardır.**
Reddedilen şey belirsiz süreli bekleme, beklemenin kendisi değil. §3'teki kaçış kapısı
bunu sağlıyor.

`design/PILLARS.md` bu ifadeyle güncellendi.
