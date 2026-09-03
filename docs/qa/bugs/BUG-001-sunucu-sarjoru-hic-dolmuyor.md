# BUG-001 — Üç zombiden sonra mermiler hasar vermiyor

**Durum:** düzeltildi · **Bulan:** geliştirici (oyun testi) · **Tarih:** 2026-09-02
**Şiddet:** kritik — çekirdek döngü oynanamaz hâle geliyordu · **İş:** M1-06

---

## Belirti

> "Ateş ediyor, ilk 2-3 zombiden sonra hasar yemiyorlar ya da ölemiyorlar."

Silah ateş etmeye devam ediyor: iz çiziliyor, geri tepme oluyor, mermi sayacı düşüyor,
dolum çalışıyor. Ama zombiler hasar almıyor. **Console'da hiçbir hata yok.**

## Kök sebep

Silahın iki `WeaponState`'i var: biri istemcide (anında geri bildirim için), biri
sunucuda (hasarın uygulandığı ve hız sınırının sayıldığı yer). Sunucudaki kopya
kasıtlıdır — istemciye güvenilmez (netcode.md).

Dolum yalnızca **istemcinin** kopyasında başlatılıyordu. Sunucunun gölge şarjörü ilk
12 mermiden sonra boş kaldı ve bir daha hiç dolmadı; `CmdFire` her çağrıda sessizce
`return` etti.

12 mermi × 55 hasar = 660 hasar. Tur 1 zombisi 150 can → **tam olarak 3–4 zombi.**

## Neden bir oyun testi sürdü

Reddedilen atış **sessizdi**. `if (_serverState.TryFire(true) != FireResult.Fired) return;`
satırı hiçbir iz bırakmıyordu. Testler yeşildi ve haklıydılar: `WeaponState`'in kendisi
doğru çalışıyor, hata iki kopyanın eşlenmemesindeydi — yani birim testinin göremeyeceği
bir yerde.

## Düzeltme

1. `CmdReload` eklendi: istemci dolumu başlattığında sunucu da kendi kopyasını doldurur.
   Süreye **sunucu kendi ayarından** karar verir, yani dolum kısaltılarak avantaj
   alınamaz.
2. Reddedilen atış artık **görünür**: saniyede en fazla bir uyarı, sebebiyle birlikte.
3. Aynı taramada bulunan ikinci hata: sunucu ışını `RaycastNonAlloc` + elle en yakını
   bulma kullanıyordu. O çağrı sekiz slotluk tamponu **sırasız** doldurur; ışın üzerinde
   sekizden fazla çarpışan varsa gerçek en yakını atabilir — kalabalık bir sürünün içinde
   "mermi gitmedi" olarak görünür. `Physics.Raycast`'e geçildi (en yakını garanti eder,
   tahsis yapmaz).
4. `ZombieSandbox`'taki geçici hata ayıklama ışını kaldırıldı: gerçek silahla **aynı
   tuşta** iki ateş kaynağı vardı, hem çift hasar hem teşhis gürültüsü üretiyordu.

## Alınan ders

**Sunucuda bir kaynağı sayan sistem, o kaynağı geri veren yolu aynı anda yazmak
zorundadır.** Yarısı yazılmış bir otorite, otorite değil sessiz bir duvardır.

Kalıcı korkuluk: reddedilen her sunucu işlemi görünür olmalı. Sessiz `return`, teşhisi
bir oyun testine bağlayan şeydir.
