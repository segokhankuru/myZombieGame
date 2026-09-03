# BUG-004 — Kapı satın alındı ama hiçbir şey olmadı; zombiler barikatsız içeride

**Durum:** düzeltildi · **Bulan:** geliştirici (oyun testi) · **Tarih:** 2026-09-02
**Şiddet:** yüksek · **İş:** M1-08, M1-09

---

## Üç ayrı bulgu

### 1. Kapı diye bir nesne yoktu

Gri kutu üreteci duvarda **zaten bir boşluk** bırakıyordu; `Door_A_to_B` yalnızca bir
işaretti. Yani "kapıyı satın aldım" dendiğinde kapanmış bir şey olmadığı için görünür
hiçbir şey değişmiyordu.

Üreteç artık gerçek bir **kapı kanadı** kuruyor. Kanat navigasyonu `NavMeshObstacle`
(oyma) ile keser, bake ile değil: bake edilmiş NavMesh çalışma anında değişmez, kanat
bake'e girseydi kapıyı satın almak geçidi **açmazdı**.

Ayrıca `SetPrivateField` yalnızca `bool` ve nesne referansı biliyordu; **enum, metin ve
dizi alanları sessizce boş kalıyordu.** Kapının fiyat bandı ve adı bu yüzden hiç
yazılmadı. Artık her tip açıkça ele alınıyor, bilinmeyen tip hata veriyor.

### 2. Zombiler barikatı yıkmadan içeride belirdi

Doğum noktası `NavMesh.SamplePosition(nokta, 4 m)` ile bulunuyordu. **O çağrı duvarları
umursamaz:** dışarıda uygun bir nokta bulunamadığında 4 metre ötedeki *içerideki* zemine
yapışıyordu. Zombi barikatı hiç görmeden binanın içinde beliriyordu.

İki önlem: yarıçap 1.5 m'ye indi, ve geometriden bağımsız bir garanti eklendi — bulunan
nokta pencerenin **dışa bakan** tarafında değilse doğum atlanır (ve bir kez uyarı basar).

Aynı hata sıkışma kurtarmasında da vardı: NavMesh dışına düşen zombi 5 m yarıçapla
örnekleniyordu. Artık kendi penceresinin dış noktası çevresinde, 2 m ile.

### 3. "Zombiler takip etmiyor"

Barikat 6 tahta × 1.8 sn = **11 saniye** sökme demekti; tur süresinin büyük kısmı
pencerede beklemekle geçiyordu ve bu, oyuncuya "takip etmiyorlar" diye okundu.
4 tahta × 1.2 sn = ~5 saniyeye çekildi.

## Bonus: bağlantı kontrolü haksız yere "kopuk" diyordu

Kapı kanatları NavMesh'i oyunca araç her seferinde "KOPUK" raporladı — bake doğru
olduğu hâlde. Oymanın geri alınması oyun döngüsüne bağlı ve **toplu çalıştırmada kare
ilerlemiyor**, yani ölçüm anında kapılar hep kapalı görünüyordu.

Ölçüm artık **kurulumun içinde, bake'in hemen ardından** yapılıyor — kanatların zaten
kapalı olduğu an. Sonuç: `zemin -> ust kat: tam (45.0 m)`, 10 pencerenin hepsi bağlı.

## Alınan ders

**Bir aracın yanlış anda ölçmesi, yanlış ölçmesinden daha tehlikelidir:** araç iki tur
boyunca gerçek bir hata varmış gibi gösterdi ve teşhis oraya yöneldi. Ölçümün doğru anı,
ölçülen şeyin kontrol edildiği andır.
