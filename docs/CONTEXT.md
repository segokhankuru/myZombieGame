# Project Context

**Game:** `<isim tbd>` — 4 kişilik co-op, sonsuz tur bazlı zombi hayatta kalma FPS'i;
klasik tur döngüsünün üstüne kart draft'ıyla kurulan build sistemi.
**Genre / reference:** Call of Duty zombi modu · Killing Floor · Risk of Rain 2.
Farkı: klasik zombi modunun 20. turu her run aynıdır; kart sistemi her run'ı farklı kılar.
**Target player:** Tur bazlı zombi modunu bilen, arkadaş grubuyla oynayan, roguelite build
sistemlerinden keyif alan oyuncu.
**Platform:** PC / Steam. Hedef donanım `<tbd>`.
**Stage:** preproduction
**Milestone:** henüz planlanmadı
**Review mode:** lean

## Pillars
- **PILLAR-01** Build kimliği silahtan önce gelir.
- **PILLAR-02** Dört oyuncu birbirine muhtaç olmalı.
- **PILLAR-03** Kesintisiz tur, birkaç turda bir dönüm noktası.
- **PILLAR-04** Kaosta okunabilirlik.

## Core loop
1. Tur başlar, zombiler pencerelerden girer; öldür, barikat tamir et, puan kazan.
2. Puanla kapı aç (harita genişler), duvardan silah al, rastgele dağıtıcıyı dene.
3. Turlarda kart draft'ı (2·5·8·11...): slot makinesi döner, 3 karttan 1 seçim.
4. Kartlar birikir, etiketler eşleşir, build kimliği oluşur. Kullandığın silah paralel
   olarak alışkanlık seviyesi kazanır.
5. Turlar zorlaşır; ölene kadar devam. Ölüm = run sonu, sicil ekranı ve unvanlar.

**Modlar:** 4 kişilik co-op (kurucu = host) ve solo. Solo aynı host kod yolunu kullanır,
ayrı bir çevrimdışı yol değildir; kart havuzu `soloValid` alanıyla filtrelenir.

## Deliberately not in this game
- Host göçü yok — host çıkarsa oyun biter
- Yayında tek harita
- PvP ve oyuncular arası skor yarışı yok
- Bot desteği yok
- Hikâye kampanyası / anlatı yok
- Karakter kozmetik ekonomisi yok (v1)

## Technology
| Area | Choice | ADR |
|---|---|---|
| Engine | Unity 6.3 LTS (6000.3.x), Personal | ADR-0002 |
| Render pipeline | URP (Forward+) | ADR-0003 |
| Netcode | FishNet + Pro (~$60), host-otoriteli | ADR-0001 |
| Transport | FishySteamworks veya FishyFacepunch (Steam relay) | ADR-0001 |

## Performance budget
Hedef: **1080p / 60 FPS orta seviye genel donanımda**; kalite ayarları ve çözünürlük
ölçeğiyle üstü açık. Yerel geliştirme makinesi ölçüt değildir.

**Darboğaz GPU değil CPU.** Kapalı küçük harita URP'de ucuz; yükü ~40 NavMesh agent'ı,
~40 animator ve ağ senkronu üretiyor. Kare düşerse önce Profiler'da AI ve animator'a
bakılır, çözünürlüğe değil. Sayısal bütçe `<tbd>` — M0 ölçümü üretecek.

## Active roles
`<kickoff sonrası belirlenecek>`

## Current work
**Milestone:** M-00 Teknoloji Doğrulaması — planned
**In progress:** tasarım temeli yazıldı (brief, pillars, SYS-01/02/03, draft UX, ADR-0001/2/3)
**Blocked:** kapsam sayıları — silah/zombi/kart adedi bilinçli olarak ertelendi

## Known debt and risks
- En büyük risk: klon aşaması uzar, farklılaştırıcı (kartlar) hiç inşa edilmez. Uyarı
  işareti M2 tarihinin ikinci kez kayması.
- Netcode 40 zombide ölçeklenmeyebilir — M0'da, kod yazmadan önce doğrulanacak.
- IP sınırı: mekanik serbest, kimlik ve kat planı değil.
- Ödül/tanınma sistemi tasarlandı ama henüz spec'e dökülmedi.
