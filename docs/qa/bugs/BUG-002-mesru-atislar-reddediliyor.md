# BUG-002 — Arada bir tık yeniyor (sunucu meşru atışları reddediyor)

**Durum:** düzeltildi · **Bulan:** geliştirici (oyun testi logu) · **Tarih:** 2026-09-02
**Şiddet:** yüksek — doğrudan silah hissiyatını bozuyor · **İş:** M1-06

---

## Belirti

Console'da, oynanış sırasında düzenli olarak:

```
[Silah] Sunucu atisi reddetti: Cycling
[Silah] Sunucu atisi reddetti: Reloading
```

Oyuncu tarafında: mermi gidiyor, iz çiziliyor, ama arada bir atış hiçbir şey yapmıyor.
BUG-001'in düzeltmesinde eklenen uyarı sayesinde **bu sefer sessiz değildi.**

## Kök sebep

Sunucu, istemcinin `WeaponState`'inin birebir kopyasını tutuyor ve **kare kare aynı
olmasını** bekliyordu. Olamaz: `Command` ağdan bir kare sonra ulaşır, dolayısıyla
sunucunun bekleme sayacı hep bir kare geridedir. Sınırın tam üstünde gelen meşru bir
atış reddedilir.

Aynı şey dolumda daha belirgin: istemci dolumu T anında bitirir ve ateş eder; sunucunun
dolumu T + bir kare sonra biter, o atışı yer.

## Düzeltme

Sunucudaki gölge `WeaponState` yerine `ServerFireGuard`.

**Sunucunun işi hile önlemektir, simülasyonu birebir tekrarlamak değil.** Aynı kurallar
bir **ağ payıyla** uygulanıyor: bir kare (30 FPS'te 33 ms) artı makul bir gidiş-dönüş
için 0.12 sn tolerans. Toleransın bedeli, hile yapan bir istemcinin atış hızını en
fazla bu kadar aşabilmesidir — pratikte hiçbir şey.

Tolerans bir **denge değeri değil**, mühendislik sabitidir; o yüzden `config/`'te değil
sınıfın içinde, gerekçesiyle birlikte yaşıyor.

## Alınan ders

Otorite doğrulaması **eşitlik** değil **makuliyet** testidir. "Sunucu istemcinin
simülasyonunu tekrarlasın" yaklaşımı, ağın doğası gereği dürüst oyuncuyu cezalandırır;
ve bu ceza en çok hissedilen yerde ortaya çıkar — tetikte.
