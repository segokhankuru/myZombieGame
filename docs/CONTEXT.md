# Project Context

**Game:** Bunker *(kod adı — satış ismi `<tbd>`)* — 4 kişilik co-op + solo, sonsuz tur
bazlı zombi hayatta kalma FPS'i; klasik tur döngüsünün üstüne kart draft'ıyla kurulan
build sistemi.
**Genre / reference:** Call of Duty zombi modu · Killing Floor · Megabonk / Risk of Rain 2.
Farkı: klasik zombi modunun 20. turu her run aynıdır; kart sistemi her run'ı farklı kılar.
**Target player:** Tur bazlı zombi modunu bilen, arkadaş grubuyla oynayan, roguelite build
sistemlerinden keyif alan oyuncu.
**Platform:** PC / Steam. 1080p/60, orta seviye genel donanım.
**Stage:** production
**Milestone:** **M-01 Solo Çekirdek Döngü** — 13 işin 13ü kod olarak bitti, 2si kanıtla kapandı; kalan kanıt oyun testi
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
2. **Kendi sistemleri** (M-03) — kart draft'ı, ödül yapısı
3. **Görsel makyaj** — asset, ışık, ses, dönem kimliği

**Sıra kaydı (2026-09-07/08):** 2 ve 3 planlanandan erken ve **iç içe** ilerledi. Bunun
bedeli kayıtlı: ÇK-17 artık temiz gri kutuda ölçülemiyor (atmosfer F10 ile kapansa da
kartlar ve sanat duruyor). Kazancı da kayıtlı: farklılaştırıcı, en ucuz olduğu anda
test edilebilir hâle geldi — PILLAR'ların istediği buydu.

Mekanik kopyalanır, kimlik kopyalanmaz: isimler, sesler, görsel imzalar ve **kat planı**
prototipte bile girmez.

## Technology
| Area | Choice | ADR |
|---|---|---|
| Engine | Unity 6000.3.23f1 LTS, Personal | ADR-0002 |
| Render pipeline | URP (Forward+) | ADR-0003 |
| Netcode | Mirror (MIT), host otoriteli, hareket client-authoritative | ADR-0004 |
| Transport | FizzyFacepunch (Steam), Steam kapalıysa **KCP'ye düşer ve söyler** | ADR-0004 / ADR-0007 |
| Mağaza sanatı | Altı paket; üçüncü parti salt okunur, oyun üretilmiş URP kopyasını kullanır | ADR-0008 |
| Ses | Mağaza kayıtları (`audio.asset` kataloğu); sentez **yedek** yol olarak kalır | ADR-0006 / ADR-0009 |

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
Bunker.Audio     ses servisi; yalnizca Systems e bakar, kimse ondan bir sey beklemez (ADR-0006)
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

**Üç oyun testi koşuldu** (2026-09-05) ve bulgularının tamamı kapatıldı. Sonrasında
M-01'in dışına taşan işler de girdi ve **oyun artık gri kutu değil**: ana menü + lobi
(M-04), Steam daveti (ADR-0007), Asset Store sanatı (ADR-0008), co-op yere düşme /
kaldırılma, kart havuzu (37 kart) ve tezgâh, dört ateşli silah + üç yakın dövüş silahı,
eşya düşürme (drop), dış hava ve dış arazi. Kararların tamamı `docs/DECISIONS.md`'de bir
satır; uzun anlatımları `docs/archive/`'da.

**2026-09-09 — envanter, ses ve karakter turu.** Bıçak V tuşundan **1 numaralı slota**
taşındı (2-5 ateşli silahlar, 6-0 eşyalar); droplar artık cepte **birikiyor** ve
istenen anda harcanıyor. Tur içinde can yenilenmesi kapandı, tur sonunda **tam dolum**.
Silah envanteri tek pakete taşındı ve **altıya çıktı** (M1911, Uzi, Benelli M4, AK-74,
M4, M107 + işlevsel dürbün). Oyuncunun **gerçek bir gövdesi** var (Human Basic Motions).
Ses artık sentez değil kayıt: ayak sesi, silah sesleri ve ana menü müziği (ADR-0009).
Nuke sahayı silmiyor, en yakın 15 zombiyi öldürüyor.

**Aynı gün, ikinci tur.** Slot düzeni oturdu: **1 bıçak, 2-3 silah, 4-8 eşya** ve aynı
anda en fazla **2 ateşli silah** taşınıyor — satın alınanlar run boyunca hatırlanıyor ve
tezgâhta bedelsiz değiştiriliyor. M4'e ELCAN dürbünü geldi; iki dürbün de artık
**gerçekten dürbün görüntüsü** çiziyor (M107 çevreyi alan nişancı dürbünü, M4 çevreyi
bırakan prizmalı optik). Co-op tarafında: tur arası **sabit 30 sn** + F ile hazır,
yere düşen **10 saniyede** kanıyor, tur başı dirilişi **kendi puanından** ödeniyor ve
ölüm artık kalıcı bir bedel bırakıyor (eşyalar gider, yedek mermi yarılanır).

**2026-09-10 — oyun testinin beş bulgusu.** Yükseltme tezgâhı E ile kapanıyor. Yedek
mermi dört kurala bağlandı (`ReserveAmmo`): tur sonu tavanın yarısı, tavanı aşmaz, tavan
kartla büyür (artık cepteki silahta da), tavanın üstü **yalnızca satın alma**; HUD tavanı
yazıyor. **"Zombi uzaktan vuruyor" hissinin asıl sebebi oyuncunun kendi mermisiydi**
(build günlüklerinde `Oyuncu[M4] -> Oyuncu 112.93`): silah ve bıçak artık hiçbir oyuncu
gövdesine hasar yazmıyor; zombiye ayrıca mutlak tavan geldi (1 m, boss 2 m — `zombie.json`
v11) ve vuruş satırı mesafeyi yazıyor. Dış sis bir **yer** oldu, kamera durumu değil:
içeriden pencereden bakınca dışarısı sisli ve yağmurlu. **Build 673 MB → 155 MB** (mağaza
dokularına tavan + sıkıştırma, editör rafı build dışı, LZ4HC). 369 test yeşil; hepsi oyun
testi bekliyor. Açık riskler: co-op'ta host istemci oyuncuyu ~100 ms geriden görüyor; dış
sis tam ekran bir geçiş ekledi ve kare bütçesinde ölçülmedi.

**Teşhis hattı iki dosyaya çıktı** (2026-09-08): `telemetry/runs.jsonl` run özetini,
`telemetry/session-<zaman>.log` her hasar olayını satır satır tutuyor — ölüm anında son
16 vuruş aralarındaki süreyle dökülüyor. "Tek mi yiyorum" sorusu artık tahminle
cevaplanmıyor.

**Sıradaki yine kod değil, oynamak.** Protokol:
`docs/qa/playtests/PT-01-ck17-tekrar-oynatiyor-mu.md` — kalan iki seans (kapalı zarflı öz
test → arkadaşlar). **Build hazır:** `Bunker.exe` çıkıyor ve açılıyor.

**Çıkış kriterleri (2026-09-04):**

| | Durum |
|---|---|
| ÇK-12 kesintisiz oynanıyor | oyun testi bekliyor |
| ÇK-13 tur 10'a ~15 dk | ❌ **ölçüldü: ~8.8 dk — hedefin ALTINDA** (PT-01/Sonuç-01) |
| ÇK-14 üç bölge açılıyor | 🔶 modelde karşılanıyor, **iki kırılma var** |
| ÇK-15 40 zombi bütçede | ✅ **ölçüldü** — p99 bütçenin %8.5'i |
| ÇK-16 Unity'siz test | ✅ 335 test yeşil |
| **ÇK-17 tekrar oynatıyor mu** | 🔶 **ilk sinyal olumlu** (7 dk, "keyifliydi") ama 20 dk değil; kartlar girdiği için temiz kontrol grubu artık ölçülemez |

**Oyun testinde aranacak bulgu** (`design/economy/curves.md`): geç oyunda harcanacak bir
şey kalmıyor. "Mermi seferleri turu parçalıyor" bulgusu ilk oyun testinde ABARTILI çıktı
(gerçek oyuncu %76 kafa vuruşu yapıyor, model %25 varsayıyordu).

**Not:** M1-06 bir `Feel` işi; DoD'si `/feel-check` notunu zorunlu kılıyor
**Altyapı:** config importer (ADR-0005) — denge sayıları tek kaynakta ·
zombi konum seam'i kilitlendi: tek paket, 12 bayt/zombi, 10 Hz ·
**telemetri hattı** (M1-12): her run `telemetry/runs.jsonl`'a bir satır,
`.claude/tools/telemetry.ps1` özetler ve ÇK-13'ü cevaplar ·
**savaş günlüğü**: her oturum `telemetry/session-*.log`, hasar olayları satır satır
**Blocked:** kapsam sayıları (silah/zombi/kart adedi) bilinçli olarak ertelendi

**Toplam 335 EditMode testi yeşil.** Derleme ve testler Unity açmadan koşuyor:
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
- **Yayın öncesi kaldırılacak geliştirme araçları** (PILLAR-04): zombi can barları ve
  üstündeki sayılar (`ZombieSetup` → `showInBuild`, `ZombieHealthLabels`). Şu an build'de
  bilerek açık — oyun testleri build üzerinde yapılıyor.
- **Ölçülmemiş yeni yük** (2026-09-08): dış arazi 222 parça (ağaç/çalı/taş/çim) ve iki
  büyük saydam bulut katmanı. Yukarı bakınca overdraw var. Sıradaki `/perf-check`'in ilk
  kalemi bu; frame bütçesi hâlâ eski ölçüme dayanıyor.
- **Zombi vuruş hasarı turla ARTMIYOR** ve bu bir hata değil: `zombie.json →
  attack.damage` sabit 30, tur ölçeklemesi yalnızca can/hız/adet üretiyor (tek çarpan
  boss ×2). "Tek yiyorum" hissinin kaynağı aynı karede vuran zombi sayısı; savaş günlüğü
  bunu ölçmek için var. Eğri değişecekse bu bilinçli bir `/tune` kararı olmalı.
