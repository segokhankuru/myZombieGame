# BUG-005 — Zombiler doğrudan içeride beliriyor ve mavi yanıp sönüyor

**Durum:** düzeltildi · **Bulan:** geliştirici (oyun testi) · **Tarih:** 2026-09-02
**Şiddet:** kritik — aktif bölgede oynanışı bozuyordu · **İş:** M1-05

---

## Belirti

Aktif bölgede, pencerelerin dışında değil **doğrudan binanın içinde** zombi beliriyor;
hareket edemiyor, mavi (sıkışmış) renkte yanıp sönüyor. Hepsi değil, **bazıları**.

## Kök sebep

Doğum noktası doğru hesaplanıyordu. Sorun onu **uygulamaktaydı**:

```csharp
zombie.transform.SetPositionAndRotation(hit.position, ...);   // yanlis
zombie.Spawn(...);                                            // ajani aciyor
```

`NavMeshAgent` açıkken `transform.position`'a yazmak işe yaramaz: ajan kendi iç
konumunu korur ve nesneyi oraya geri çeker. Havuzdan çıkan bir zombi böylece **bir
önceki hayatında öldüğü yere** — yani binanın içine — dönüyordu.

"Bazıları" olmasının sebebi buydu: ilk turun ilk zombileri yeni nesnelerdi ve doğru
yerde doğdu; **yeniden kullanılanlar** eski ölüm yerlerine ışınlandı.

Yanıp sönme de aynı zincirin devamı: içeride olan zombi henüz "binaya girmiş"
sayılmadığı için pencereye gitmeye çalışıyor, içeriden dışarıdaki noktaya yol
olmadığı için hareket edemiyor → sıkışıyor → kurtarma → tekrar sıkışıyor.

## Düzeltme

Konumu artık **tek bir sahip** yazıyor (`ZombieAgent.Spawn`), doğru sırayla:

1. `NavMeshAgent`'ı **kapat**
2. konumu yaz
3. ajanı aç
4. `Warp()` ile ajanın iç konumunu da hizala

## İkinci düzeltme: kapalı bölgede doğum yok

Yönetmen bütün pencereleri sırayla kullanıyordu — kapalı kapının ardındakiler dahil.
O zombiler barikatı söküp içeri giriyor, sonra oyuncuya yolları kapalı olduğu için
sıkışıyordu.

Artık bir pencere ancak **içerisinden oyuncuya yol varsa** doğum yapar. Bölge tanımına
gerek yok: kapalı kapı NavMesh'i kesiyor (M1-09), yani "yol var mı" sorusu bölge
sorusunun tam karşılığı. Kapı açılınca o bölgenin pencereleri kendiliğinden devreye
girer.

## Geri alınan yama

İlk denemede "sıkışan zombiyi 15 saniye sonra sahadan çek" diye bir emniyet ağı
yazılmıştı. **Geliştirici haklı olarak reddetti:** o, hatayı başka bir hatayla
kapatmaktı — turun neden kilitlendiğini gizler ve gerçek sebebi arama isteğini
öldürürdü. Kaldırıldı.

## Alınan ders

**Bir Unity bileşeni bir alanın sahibiyse, o alana başka kimse yazmamalı.** NavMeshAgent
konumun sahibidir; ona `transform` üzerinden değil `Warp` ile konuşulur. Aynı kural
`Rigidbody` için de geçerli ve aynı sessiz belirtiyi üretir.
