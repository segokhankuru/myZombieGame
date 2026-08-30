# Decision Log

Append-only. One line per decision. Limit 300 lines - run /context-compact past that.

| Date | Decision | By | Rationale | Source |
|---|---|---|---|---|
| 2026-08-29 | Format: 4 kişilik co-op, sonsuz tur, FPS | kullanıcı | Klasik tur bazlı zombi modu yapısı | design/00-brief.md |
| 2026-08-29 | Ton: biraz karanlık + biraz arcade | kullanıcı | Erken CoD zombi modunun havası; dar mekân ve ışık kontrastı solo bütçe için ucuz | design/00-brief.md |
| 2026-08-29 | Dönem kararı M3'e ertelendi | kullanıcı | Gri kutu dönemsizdir; karar ancak asset alımında bağlayıcı olur | design/00-brief.md |
| 2026-08-29 | Hedef: Steam'de satılacak ticari ürün | kullanıcı | Kapsam disiplinini ve bitirme baskısını belirler | design/00-brief.md |
| 2026-08-29 | Geliştirme sırası: klon taban → kendi sistemleri → görsel makyaj | kullanıcı | Klon bir ürün değil kontrol grubu; denge tahminini ortadan kaldırır | design/00-brief.md |
| 2026-08-29 | Farklılaştırıcı: kart/build sistemi | kullanıcı | Klasik modda 20. tur her run aynı; kartlar bunu kırar | design/PILLARS.md |
| 2026-08-29 | Kart draft'ı görsel makyajdan ÖNCE inşa edilir | kullanıcı | Oyunu satan parça en ucuz olduğu anda test edilmeli | design/00-brief.md |
| 2026-08-29 | Draft turları 2·5·8·11 (3·6·9 değil) | kullanıcı | Farklılaştırıcı ilk 4 dakikada kendini göstermeli; 20 dakika testi | design/PILLARS.md |
| 2026-08-29 | Etiket havuzu ağırlıklandırılır | kullanıcı | Build 'denk gelmek' yerine 'kurulmak' hissi vermeli, yoksa PILLAR-01 şansa kalır | design/PILLARS.md |
| 2026-08-29 | Oyuncular arası skor yarışı REDDEDİLDİ | kullanıcı | Hedef takımca hayatta kalmak; yarış PILLAR-02'yi kırar | design/PILLARS.md |
| 2026-08-29 | Ödül metrikleri: istismarı zaten istenen davranış olanlar | kullanıcı + analiz | Ölçtüğün şey oynanan şey olur; kill sayısı takım aleyhine farmlanabilir, barikat tamiri değil | design/PILLARS.md |
| 2026-08-29 | Ödül sistemi: 4 katman + kusursuz tur ödülü onaylandı | kullanıcı | Yarış üretmeden tanıma üretmek; meta para havuz açar, güç vermez | design/systems/SYS-01-odul-ve-taninma.md |
| 2026-08-29 | Harita: 3 bölgeli iki katlı tek bina yapısı, geometri özgün | kullanıcı | Yapı tür konvansiyonu ve serbest; kat planı korunan parça ve sonradan düzeltilemez | design/00-brief.md |
| 2026-08-29 | Karakter kozmetik ekonomisi kapsam dışı (v1) | analiz | FPS'te kendini görmezsin; kostüm satın alınamaz, rig'e uymalı | design/00-brief.md |
| 2026-08-29 | Eski kapsam dokümanı referansa taşındı, bağlayıcı değil | kullanıcı | "Sadece fikir vermesi amaçlıydı" | docs/reference/ |
| 2026-08-29 | Kapsam sayıları ve içerik planı ertelendi | kullanıcı | Mimariyi kısıtlamıyorlar; zamanı gelince planlanacak | `<tbd>` |
| 2026-08-29 | Netcode: FishNet + Pro (~$60) + Steam transport | ADR-0001, kullanıcı onayı bekliyor | NGO ve Mirror'da hazır prediction/lag compensation yok; solo geliştirici bunları yazamaz. Netick küçük topluluk | docs/architecture/adr/ADR-0001-netcode-kutuphanesi.md |
| 2026-08-29 | DÜZELTME: FishNet lag compensation ücretsiz DEĞİL, Pro özelliği | araştırma | Eski referans doküman aksini yazıyordu; gerekçeyi değiştirir, kararı değiştirmez | docs/architecture/adr/ADR-0001-netcode-kutuphanesi.md |
| 2026-08-29 | ADR-0001 kabul edildi: FishNet + Pro | kullanıcı | — | docs/architecture/adr/ADR-0001-netcode-kutuphanesi.md |
| 2026-08-29 | Unity 6.3 LTS | ADR-0002 | Aralık 2027'ye kadar destekli, projenin tamamını kapsıyor; 6.0 Ekim 2026'da bitiyor, 6.4 zaten EOL | docs/architecture/adr/ADR-0002-unity-surumu.md |
| 2026-08-29 | URP (Forward+) | ADR-0003 | Unity 2026'da HDRP bakım modunda, URP hızlandırılıyor; geniş donanım tabanı | docs/architecture/adr/ADR-0003-render-pipeline.md |
| 2026-08-29 | Performans hedefi: 1080p/60 orta donanım, üstü açık; yerel makine ölçüt değil | kullanıcı | Darboğaz CPU (AI + animator), GPU değil | docs/CONTEXT.md |
| 2026-08-29 | Solo mod olacak; aynı host kod yolu, ayrı çevrimdışı yol değil | kullanıcı | İki oyun bakmamak için; kart havuzu soloValid ile filtrelenir | design/systems/SYS-02-kart-sistemi.md |
| 2026-08-29 | Kurucu host olur; simülasyon otoritesi hostta (render değil) | kullanıcı | "Sunucuda render" bulut oyunculuktur; kastedilen simülasyon otoritesi | docs/architecture/adr/ADR-0001-netcode-kutuphanesi.md |
| 2026-08-29 | Etiketler: Balistik, Yıkım, Kan, Tempo, Ganimet | analiz, kullanıcı kart fikirlerinden türetildi | Eski dokümanın element etiketleri yerine gerçek kart fikirlerine oturan küme | design/systems/SYS-02-kart-sistemi.md |
| 2026-08-29 | Hasar: kartlar toplamsal, katmanlar çarpımsal | analiz | Çarpımsal kartlar 5 kartta 2.49x'e kaçar ve dengelenemez | design/systems/SYS-02-kart-sistemi.md |
| 2026-08-29 | Hasar azaltma efektif can olarak uygulanır (hasar / (1+bonus)) | analiz | Yüzdeyle istiflenirse %100'de ölümsüzlük ve artan getiri üretir | design/systems/SYS-02-kart-sistemi.md |
| 2026-08-29 | Görünmez şans kartları reddedildi, görünür alternatif kondu | analiz, kullanıcı onayı bekliyor | Hissedilmeyen kart unutulur; gelecek kartları iyileştiren kart baskın ilk seçim tuzağı üretir | design/systems/SYS-02-kart-sistemi.md |
| 2026-08-29 | Silah alışkanlık sistemi: kullandıkça 4 seviye, sınıfa özel nitelik | kullanıcı fikri, tasarım eklendi | Silah yakınsamasını kırar; dağıtıcıyı gerçek karara çevirir | design/systems/SYS-03-silah-cesitliligi-ve-aliskanlik.md |
| 2026-08-29 | Draft ekranı: slot makinesi, süre yok, AFK kaçış kapısı | kullanıcı | Beklemek co-op'ta sosyal an; reddedilen belirsiz süreli bekleme | design/ux/draft-ekrani.md |
| 2026-08-29 | PILLAR-03 düzeltildi: belirsiz süreli bekleme reddedilir, bekleme değil | analiz | Draft ekranı tasarımıyla çatıştı, çatışma örtülmeden çözüldü | design/PILLARS.md |
| 2026-08-29 | Proje kod adı: Bunker (assembly ve namespace öneki) | kullanıcı | Satış ismi ayrı; kod adı kodda kalır | docs/architecture/ARCHITECTURE.md |
| 2026-08-29 | Bunker.Systems assembly'si noEngineReferences ile Unity'ye kapatıldı | ADR-0001 sonucu | Saf C# kuralını disiplin değil derleyici zorlasın | docs/architecture/ARCHITECTURE.md |
| 2026-08-29 | Git deposu kuruldu, LFS açıldı, ilk commit atıldı | kullanıcı | — | .gitattributes |
| 2026-08-29 | Kurulu Unity 6.5 reddedildi, 6000.3 LTS kurulacak | ADR-0002 (veriyle güncellendi) | LTS olmayan sürümler bir sonraki sürüm çıkınca yama almayı bırakıyor: 6.1 ve 6.2 dörder ay, 6.4 üç ay yaşadı. 6.5 için 6.6 Eylül'de bekleniyor | docs/architecture/adr/ADR-0002-unity-surumu.md |
| 2026-08-29 | M-00 planlandı: 10 iş, 8 sayısal çıkış kriteri, 4 başarısızlık koşulu | producer | Netcode oyun kodundan önce doğrulanır; M0 kaybedebilmeli | design/milestones/M-00.md |
| 2026-08-29 | M0'da naif NetworkTransform tabanı ölçülecek | analiz | Özel snapshot'ın işe yaradığını iddia edebilmek için karşılaştırma noktası şart | design/milestones/M-00.md |
| 2026-08-29 | 40 küp NavMesh ile hareket edecek, basit hareketle değil | analiz | Host aynı zamanda oyuncu; CPU rekabeti ancak gerçekçi AI yüküyle ölçülür | design/milestones/M-00.md |
| 2026-08-29 | DÜZELTME: Mirror'ın lag compensation'ı VAR (Beta, MIT) | araştırma | ADR-0001 Mirror'ı "lag compensation yok" diye elemişti, bilgi yanlıştı | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Netcode: Mirror (MIT). ADR-0001 supersede edildi | kullanıcı | Bu oyunda prediction atlanabilir (co-op'ta client-authoritative hareket), lag compensation atlanamaz. FishNet tersini ücretsiz veriyor | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Oyuncu hareketi client-authoritative olacak | ADR-0004 | Davetle girilen arkadaş co-op'unda hile toleransı yüksek; prediction makinesi gereksiz | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Kabul edilen taviz: liderlik tablosu ve meta ilerleme manipüle edilebilir | ADR-0004 | Client otoritesinin bedeli; arkadaş co-op'unda kabul edilebilir | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Bütçeden $60 FishNet Pro kalemi çıktı | ADR-0004 | Mirror MIT, hiçbir özellik ücretli katmanda değil | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | ÇK-9 karşılandı: Mirror, Unity 6000.3.23f1'de temiz derleniyor | M0-01 | ADR-0004'ün açık riski kapandı; Mirror Weaver çalıştı, LagCompensationSettings çözüldü | docs/qa/evidence/M0-01-mirror-unity63-uyumluluk.md |
| 2026-08-29 | Mirror üçüncü parti olarak depoya commit edildi (30 MB) | analiz | Klonlayanın aynı sürümü alması ve .meta GUID'lerinin sabit kalması için | Assets/Mirror/ |
| 2026-08-29 | Sıralama değişti: önce solo çekirdek döngü, sonra multiplayer | kullanıcı | Gri kutuda küp senkronlayarak oyunun eğlenceli olduğu öğrenilemez; motivasyon solo geliştiricide gerçek kaynak | design/milestones/M-01.md |
| 2026-08-29 | M-00 küçültüldü; netcode doğrulaması M-02'ye taşındı | producer | M-00'da yalnızca solo'da bedavaya ölçülebilen kısım kaldı (40 NavMesh agent + host CPU) | design/milestones/M-00.md |
| 2026-08-29 | ZORUNLU: solo, Mirror host modunda kurulur (ağsız prototip DEĞİL) | kullanıcı | Ağsız yazılırsa multiplayer geldiğinde oyuncu/silah/zombi/spawn yeniden yazılır — Unity'nin klasik pahalı hatası | design/milestones/M-01.md |
| 2026-08-29 | ZORUNLU: zombi konum senkronu tek seam'den geçer, NetworkTransform yasak | ADR-0004 | Ölçüm ertelendiği için, kötü çıkarsa sadece o parça değişsin | design/milestones/M-00.md |
| 2026-08-29 | Kabul edilen risk: PILLAR-02 M-01'de hiç sınanamaz | analiz | Solo build "dört oyuncu birbirine muhtaç" sütununu test edemez; ÇK-17 gerekli ama yeterli değil, ÇK-17b M-02'de | design/milestones/M-02.md |
| 2026-08-29 | Oyuncuda NetworkTransformUnreliable kullanılıyor (ClientToServer) | ADR-0004 | NetworkTransform yasağı zombiler içindir (40 nesne); 4 oyuncu için doğru araç | Assets/_Project/Code/Gameplay/PlayerController.cs |
| 2026-08-29 | M0-02'de girdi doğrudan cihazdan okunuyor (Keyboard/Mouse.current) | gameplay-programmer | Proje yalnızca yeni Input System'de (activeInputHandler=1); iskelette sıfır bağlama adımı. M1-01'de InputSystem_Actions'a taşınacak | Assets/_Project/Code/Gameplay/PlayerController.cs |
| 2026-08-29 | Bunker.Gameplay'e Unity.InputSystem referansı eklendi | gameplay-programmer | asmdef'li assembly'lere otomatik referans verilmiyor; derleme hatasıyla yakalandı | Assets/_Project/Code/Gameplay/Bunker.Gameplay.asmdef |
| 2026-08-30 | ÇK-10 karşılandı: solo host iskeleti çalışıyor | M0-02 | M-01'in zorunlu mimari kısıtı ayakta — solo ayrı kod yolu değil, uzak istemcisiz host oturumu | docs/qa/evidence/M0-02-solo-host-iskeleti.md |
| 2026-08-30 | PerfHud kare başına tahsis yapmaz; tamponlar bir kez ayrılır | performance-engineer | Ölçüm aracının kendisi ölçümü bozmamalı — halka tamponu, sıralama tamponu ve StringBuilder tek sefer ayrılır, ekran metni 4 Hz yenilenir | Assets/_Project/Code/UI/PerfHud.cs |
| 2026-08-30 | ÇK-5'in yetkili kanıtı Unity Profiler, PerfHud değil | performance-engineer | IMGUI'nin kendi maliyeti küçük ama sıfır değil; HUD canlı geri bildirim içindir | design/milestones/M-00.md |
| 2026-08-30 | Yüzdelik hesabı Bunker.Systems'e taşındı (FrameTimeRecorder) | performance-engineer | Saf mantık Unity'siz test edilmeli; ayrıca PerfHud ve AgentLoadTest aynı kodu iki kez içeriyordu | Assets/_Project/Code/Systems/Diagnostics/FrameTimeRecorder.cs |
| 2026-08-30 | Ölçüm protokolü elle değil, tek tuşla otomatik tarama (F5) | performance-engineer | Dört ayrı ölçümü elle koordine etmek hataya açık; ilk denemede oturum bitirilmediği için veri üretilmedi | Assets/_Project/Code/AI/AgentLoadTest.cs |
| 2026-08-31 | ÇK-5 karşılandı: 40 NavMesh agent = ~0.2 ms (bütçenin %1.2'si) | M0-04 | 200 agent'a kadar diz yok; marjinal maliyet düşüyor | docs/qa/performance/M0-04-navmesh-agent-yuku.md |
| 2026-08-31 | BULGU: NavMesh darboğaz değil, risk animator ve ağ serileştirmesine kaydı | performance-engineer | Ölçüm 40 agent'ın sıfırında animator içeriyordu; kalabalık oyunlarında animator sık sık NavMesh'ten pahalıdır | docs/architecture/PERF-BUDGET.md |
| 2026-08-31 | AgentLoadTest yol isteme hızı kare hızına bağlıydı, düzeltildi | performance-engineer | 600 FPS'te hedeflenenin 10 katı yol isteği; ölçümü muhafazakâr yaptı, sonucu geçersiz kılmadı | Assets/_Project/Code/AI/AgentLoadTest.cs |
| 2026-08-31 | **M-00 KAPANDI** (4/4 iş, 4/4 çıkış kriteri) | producer | Sıradaki: M-01 Solo Çekirdek Döngü | design/milestones/M-00.md |
