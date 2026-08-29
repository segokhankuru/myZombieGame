# Proje Brief

**Durum:** taslak — kapsam sayıları (silah/zombi/kart adedi) henüz belirlenmedi
**Sahibi:** studio-head · **Son güncelleme:** 2026-08-29

---

## Oyun

4 kişilik co-op, **sonsuz tur bazlı** zombi hayatta kalma **FPS**'i. Klasik tur bazlı
zombi modunun iskeleti — puanla kapı açma, duvarda silah, rastgele silah dağıtıcı,
barikat tamiri, yerde düşen geçici güçlendirmeler — üzerine, birkaç turda bir yapılan
**kart draft'ıyla kurulan kalıcı build sistemi**.

**Modlar:** 4 kişilik co-op (lobiyi kuran host olur) ve **solo**. Solo ayrı bir oyun
değil, aynı host kod yolunun tek oyunculu hâlidir; kart havuzu bağlam filtresiyle
daraltılır (diriltme ve takım kartları çıkmaz).

**Ton:** Biraz karanlık, biraz arcade. Loş ve dar mekân, tahta barikat, üstünde uçuşan
puan sayıları. Korku hedefi yok, atmosfer hedefi var.

**Dönem:** Henüz seçilmedi ve seçilmesi gerekmiyor. Gri kutu dönemsizdir; karar asset
satın alımıyla birlikte bağlayıcı hale gelir (M3).

---

## Hedef oyuncu

Tur bazlı zombi modunu bilen, arkadaş grubuyla akşam oynayacak bir şey arayan oyuncu.
Roguelite build sistemlerine aşina; "bu run'da ne olacağım" sorusundan keyif alıyor.

**Bu akşam onun yerine ne oynuyor:** Call of Duty zombi modları ve custom map'leri ·
Killing Floor · Deep Rock Galactic · Risk of Rain 2 · Steam'deki ucuz zombi co-op'ları.

**İddiamız:** Klasik zombi modunda 20. tur her run aynıdır. Kart sistemi bunu kırar —
aynı harita, her seferinde farklı dört oyuncu.

---

## Geliştirme yaklaşımı

Bu bir karar, gözlem değil. Üç aşama:

1. **Klon taban = kontrol grubu.** Klasik döngüyü sadık şekilde inşa et. Amaç ürün değil,
   **ölçüm referansı**: sonradan eklenen her şey "tabandan daha iyi mi?" sorusuna cevap
   verebilsin. Solo geliştiricinin en çok zaman kaybettiği yer denge tahminidir; bu adım
   onu atlar.
2. **Kendi sistemleri.** Kart draft'ı, ödül/tanınma yapısı, tur modifikatörleri. Hâlâ
   gri kutuda, sanata para harcamadan.
3. **Görsel makyaj.** Asset, ışık, ses, dönem kimliği.

**Kopyalanan şey mekanik, kimlik değil.** Tür konvansiyonları (tur yapısı, kapı ekonomisi,
duvar silahı, barikat) serbesttir. İsimler, ses efektleri ve jingle'lar, görsel imzalar ve
**kat planı** kopyalanmaz — prototipte bile. Bkz. `docs/DECISIONS.md`.

---

## Hedefler

| ID | Hedef | Nasıl ölçülür | Ne zaman |
|---|---|---|---|
| GOAL-01 | Gri kutu tabanı 20 dakika sonra tekrar oynatır | 4 kişilik arkadaş testi: seans bitiminde "bir tur daha" talebi kendiliğinden geliyor mu | M1 sonu |
| GOAL-02 | Kart sistemi gerçek build farklılaşması üretir | Telemetri: aynı run'da 4 oyuncunun kart yığınları arasındaki örtüşme < %40 | M2 sonu |
| GOAL-03 | Ticari hedefler (istek listesi, satış, Steam puanı) | `<tbd>` | kapsam kararından sonra |

---

## Bu oyunda olmayacaklar

- **Host göçü yok.** Host çıkarsa oyun biter, skor kaydedilir, UI bunu açıkça söyler.
  *Acıtır* — ama adanmış sunucu maliyeti ve host göçü karmaşıklığı solo ölçeğin dışında.
- **Yayında tek harita.** İkinci harita ücretsiz güncelleme, yayın kapsamı değil.
  *Acıtır* — tek haritalı bir oyun ilk bakışta ince görünür.
- **PvP ve skor yarışı yok.** Hedef takımca hayatta kalmak; oyuncular arası yarış
  bilinçli olarak reddedildi.
- **Bot desteği yok.** 4 kişi bulunamazsa daha az kişiyle oynanır.
- **Hikâye kampanyası, anlatı, diyalog yok.** Atmosfer var, senaryo yok.
- **Karakter kozmetik ekonomisi yok (v1).** FPS'te kendi kıyafetini görmezsin ve kostüm
  sanatı satın alınamaz — karakter rig'ine uymak zorundadır. Kozmetik varsa yalnızca
  FPS'te görünen küçük parçalar (silah tılsımı, eldiven, kart masası).

---

## En riskli üç varsayım

| Varsayım | Nasıl test edilir | Ne zaman |
|---|---|---|
| 40 zombi + 4 oyuncu host-otoriteli ağda çökmüyor | Boş sahne, 40 hareketli küp, 4 istemci, bant genişliği ölçümü | **M0 — oyun kodu yazmadan önce** |
| Klon taban tek başına eğlenceli | 4 kişilik arkadaş testi, gri kutu, 20 dakika | M1 sonu |
| Kart sistemi klasik döngüyü gerçekten farklılaştırıyor | Aynı grup, kartlı ve kartsız iki seans, karşılaştırmalı | **M2 — sanata para harcamadan önce** |

---

## Bu proje büyük ihtimalle nasıl ölür

Klon aşaması tahmin edilenden uzun sürer. "Yeterli gördüğüm nokta" hiç gelmez — çünkü
klasik döngü zaten bitmiş bir tasarımdır ve cilalaması sonsuza kadar sürebilir; her hafta
düzeltilecek bir his kalır. Kart sistemi hep bir sonraki aya ertelenir. On iki ay sonra
elde iyi çalışan ama satılamaz bir kopya kalır, çünkü oyunu satacak olan parça hiç inşa
edilmemiştir.

**Erken uyarı işareti:** M2'nin başlama tarihi ikinci kez kaydıysa, klon aşamasını olduğu
yerde dondur ve kartlara geç. Klon zaten "kontrol grubu"dur; mükemmel olması gerekmez,
karşılaştırılabilir olması gerekir.
