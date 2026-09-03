# Project Context

**Game:** Bunker *(kod adı — satış ismi `<tbd>`)* — 4 kişilik co-op + solo, sonsuz tur
bazlı zombi hayatta kalma FPS'i; klasik tur döngüsünün üstüne kart draft'ıyla kurulan
build sistemi.
**Genre / reference:** Call of Duty zombi modu · Killing Floor · Megabonk / Risk of Rain 2.
Farkı: klasik zombi modunun 20. turu her run aynıdır; kart sistemi her run'ı farklı kılar.
**Target player:** Tur bazlı zombi modunu bilen, arkadaş grubuyla oynayan, roguelite build
sistemlerinden keyif alan oyuncu.
**Platform:** PC / Steam. 1080p/60, orta seviye genel donanım.
**Stage:** preproduction
**Milestone:** **M-01 Solo Çekirdek Döngü** — 13 işten 2'si kapandı, 9'u kod olarak bitti
**Review mode:** lean

## Pillars
- **PILLAR-01** Build kimliği silahtan önce gelir.
- **PILLAR-02** Dört oyuncu birbirine muhtaç olmalı.
- **PILLAR-03** Kesintisiz tur, birkaç turda bir dönüm noktası. *(Reddedilen: belirsiz
  süreli bekleme — beklemenin kendisi değil)*
- **PILLAR-04** Kaosta okunabilirlik.

## Core loop
1. Tur başlar, zombiler pencerelerden girer; öldür, barikat tamir et, puan kazan.
2. Puanla kapı aç (harita genişler), duvardan silah al, rastgele dağıtıcıyı dene.
3. Turlarda kart draft'ı (2·5·8·11...): slot makinesi döner, 3 karttan 1 seçim.
4. Kartlar birikir, etiketler eşleşir, build kimliği oluşur. Kullandığın silah paralel
   olarak alışkanlık seviyesi kazanır.
5. Turlar zorlaşır; ölene kadar devam. Ölüm = run sonu, sicil ekranı ve unvanlar.

**Modlar:** 4 kişilik co-op (kurucu = host) ve solo. **Solo ayrı bir kod yolu değildir** —
uzak istemci sayısı sıfır olan bir Mirror host oturumudur. Bu kural M-02'nin doğrulama mı
yeniden yazım mı olacağını belirler.

## Deliberately not in this game
- Host göçü yok · Yayında tek harita · PvP ve oyuncular arası skor yarışı yok
- Bot desteği yok · Hikâye kampanyası yok · Karakter kozmetik ekonomisi yok (v1)

## Geliştirme yaklaşımı
1. **Klon taban = kontrol grubu** (M-01) — klasik döngüyü sadık inşa et, ölçüm referansı üret
2. **Kendi sistemleri** (M-03) — kart draft'ı, ödül yapısı. Hâlâ gri kutuda
3. **Görsel makyaj** — asset, ışık, ses, dönem kimliği

Mekanik kopyalanır, kimlik kopyalanmaz: isimler, sesler, görsel imzalar ve **kat planı**
prototipte bile girmez.

## Technology
| Area | Choice | ADR |
|---|---|---|
| Engine | Unity 6000.3.23f1 LTS, Personal | ADR-0002 |
| Render pipeline | URP (Forward+) | ADR-0003 |
| Netcode | Mirror (MIT), host otoriteli, hareket client-authoritative | ADR-0004 |
| Transport | KCP (yerel) → FizzySteamworks (M-02) | ADR-0004 |

**ADR-0001 supersede edildi** (FishNet). Gerekçe zinciri belgede duruyor.

## Performance budget
Hedef **1080p / 60 FPS orta seviye donanımda** — yerel geliştirme makinesi ölçüt değildir.
Ölçülen: 40 NavMesh agent ~0.2 ms (bütçenin %1.2'si), 200'e kadar diz yok.
**Bulgu: darboğaz NavMesh değil; risk animator ve ağ serileştirmesinde.**
Detay: `docs/architecture/PERF-BUDGET.md`

## Assembly haritası
```
Bunker.Systems   saf C#, Unity'ye ve Mirror'a KAPALI (noEngineReferences)
Bunker.Config    config/*.json dosyalarindan URETILEN ayar siniflari (ADR-0005)
Bunker.Gameplay  Bunker.Net  Bunker.AI  Bunker.UI  Bunker.Editor
Bunker.Systems.Tests
```
Bağımlılık tek yönlü. Hiçbir şey `Bunker.UI`'ye bağımlı olamaz.
Detay: `docs/architecture/ARCHITECTURE.md`

## Current work
**Milestone:** M-01 Solo Çekirdek Döngü — **13 işin 13'ü de kod olarak bitti**
**Bitti:** M1-01 tur ölçekleme (12 test) · M1-02 ekonomi (18 test)
**Kodu bitti, oyun testi bekliyor (11):** M1-03 harita · M1-04 zombi · M1-05 doğum ve
tur akışı · M1-06 silah · M1-07 bıçak · M1-08 barikat · M1-09 kapı · M1-10 duvar
silahı · M1-11 ölüm/skor ekranı · M1-12 telemetri · M1-13 vuruş hissi

**Sıradaki: kod değil, oynamak.** Protokol hazır:
`docs/qa/playtests/PT-01-ck17-tekrar-oynatiyor-mu.md` — üç seans (shakedown → kapalı
zarflı öz test → arkadaşlar). **SEANS B bir build gerektiriyor ve bu projede hiç build
alınmadı.**

**Not:** M1-06 bir `Feel` işi; DoD'si `/feel-check` notunu zorunlu kılıyor
**Altyapı:** config importer (ADR-0005) — denge sayıları tek kaynakta ·
zombi konum seam'i kilitlendi: tek paket, 12 bayt/zombi, 10 Hz ·
**telemetri hattı** (M1-12): her run `telemetry/runs.jsonl`'a bir satır,
`.claude/tools/telemetry.ps1` özetler ve ÇK-13'ü cevaplar
**Blocked:** kapsam sayıları (silah/zombi/kart adedi) bilinçli olarak ertelendi
**Borç:** kurulum aracı her çalıştırmada bir NavMesh varlığı bırakıyor — 25 orphan
birikti (`editor-tools.md` idempotency ihlali)

**Toplam 234 EditMode testi yeşil.** Derleme ve testler Unity açmadan koşuyor:
`.claude/tools/unity-test.ps1`, `.claude/tools/unity-exec.ps1`.

**Oyun testinde bulunan 5 hata düzeltildi** (BUG-001…005, `docs/qa/bugs/`). Ortak ders:
**sessiz başarısızlık en pahalı hata türü** — beşinin dördü hiçbir mesaj vermiyordu ve
teşhis oyun testine kaldı.

## Known debt and risks
- **En büyük risk:** klon aşaması uzar, farklılaştırıcı (kartlar) hiç inşa edilmez. Uyarı
  işareti **M-03 tarihinin ikinci kez kayması**. Sıralama değişikliği bu riski artırdı.
- ~~Config borcu~~ **KAPANDI (2026-09-02):** importer yazıldı. Sayılar yalnızca
  `config/` içinde; C# sınıfları ve `.asset` dosyaları oradan üretiliyor (ADR-0005).
  Yeni tunable **serialized field olarak eklenmez** — şemaya anahtar eklenir.
- Netcode doğrulaması M-02'ye ertelendi. İki korkuluk zorunlu: solo Mirror host modunda,
  zombi konum senkronu tek seam'den (`NetworkTransform` zombide yasak).
- **PILLAR-02 M-01'de hiç sınanamaz** — solo build takım muhtaçlığını test edemez.
- IP sınırı: mekanik serbest, kimlik ve kat planı değil.
