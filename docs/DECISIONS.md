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
| 2026-08-31 | Tur eğrisi iki fazlı: doğrusal sonra çarpımsal | systems-designer | Saf doğrusal sonsuza kadar kolay kalır, saf çarpımsal oyuncuyu öğrenmeden ezer. Doğrusal faz kuralları öğretir ve puan biriktirmeye izin verir | config/schema/rounds.schema.json |
| 2026-08-31 | maxConcurrent bir denge değeri değil, PERF-BUDGET tavanı | systems-designer | 40 değeri M0-04 ölçümünden geliyor; artırmadan önce yeniden ölçüm gerekir | docs/architecture/PERF-BUDGET.md |
| 2026-08-31 | Doğum aralığı zombi sayısıyla ters orantılı, tabanı var | systems-designer | Taban olmazsa geç turlarda sürü aynı anda belirir, oyuncunun tepki süresi kalmaz | config/schema/rounds.schema.json |
| 2026-08-31 | Puan iki ayrı sayı: harcanabilir bakiye + kazanılan toplam | systems-designer | Tek sayı olsaydı kapı açan oyuncu skor kaybederdi; hiçbir şey almayan tabloda birinci olurdu. SYS-01 ve PILLAR-02 ihlali | Assets/_Project/Code/Systems/Economy/PlayerWallet.cs |
| 2026-08-31 | Düşme cezası yalnızca bakiyeyi alır, skoru almaz | systems-designer | Düşmek riskin bedeli olmalı, sicilin silinmesi değil | config/schema/economy.schema.json |
| 2026-08-31 | Ödül sırası zorunlu: bıçak > kafa > gövde > isabet | systems-designer | Bıçak en riskli ve mermi harcamayan yöntem; ucuzlarsa erken tur ekonomisi çöker. Test ile korunuyor | Assets/_Project/Code/Tests/PlayerWalletTests.cs |
| 2026-08-31 | BORÇ: config JSON'ları henüz oyuna bağlı değil, sayılar iki yerde | unity-architect | Importer yazılana kadar sapma riski. Tetikleyici: 3. config dosyası ya da ilk gerçek denge turu | design/milestones/M-01.md |
| 2026-09-02 | Zombi kararı saf C#'ta (`ZombieBrain`), motor tarafı `Bunker.AI`'da | ai-programmer | Karar mantığı sahnesiz test edilebilmeli (ÇK-16); Unity'de kalan tek şey NavMesh'i sürmek. Bir kural motora sızarsa test edilemez hâle gelir | Assets/_Project/Code/Systems/Ai/ZombieBrain.cs |
| 2026-09-02 | Her vuruşun telegrafı var; telegraf sırasında kaçan oyuncu hasar almaz | ai-programmer | Habersiz vuruş oyunu adaletsiz hissettirir. Iskalama tasarlanmış bir kayıptır — zombiyi okunabilir ve yenilebilir yapan şey bu (ai-code.md) | config/schema/zombie.schema.json |
| 2026-09-02 | `Bunker.AI` Mirror'a bağlanmadı; zombi mantığı `SetSimulated` ile kapatılır | ai-programmer | ADR-0004'ün "zombide NetworkTransform yok" kuralı burada başlar. Ağ seam'i M1-05'te tek noktadan gelir; AI ağı bilmemeye devam eder | Assets/_Project/Code/AI/ZombieAgent.cs |
| 2026-09-02 | Zombi hedefi kayıt üzerinden bulur (`ZombieTargetBeacon`), oyuncuyu aramaz | ai-programmer | `Bunker.AI` → `Bunker.Gameplay` bağımlılığı olmasın diye bağımlılık ters çevrildi; ayrıca 40 zombinin kare başına oyuncu araması yasak (csharp-code.md) | Assets/_Project/Code/AI/ZombieTargets.cs |
| 2026-09-02 | Pencereden giriş NavMeshLink ile değil, elle sürülen tırmanışla yapılıyor | ai-programmer | Tırmanış süresi telegrafın bir parçası ve config'ten gelmeli; link'in otomatik geçişi o kontrolü vermiyor. Ayrıca paket asmdef bağımlılığı eklenmedi | Assets/_Project/Code/AI/ZombieAgent.cs |
| 2026-09-02 | Gri kutu haritaya bina çevresinde yürünecek şerit (apron) eklendi | level-designer | Zombiler pencereden girer; dışarıda NavMesh olmadan pencereye yürüyemezler. Zemin plakası duvarların altında bitiyordu | Assets/_Project/Code/Editor/BlockoutSettings.cs |
| 2026-09-02 | Kurulum menü tarifi değil araç: prefab, oyuncu yaması, sahne ve bake tek komut | tools-programmer | Elle kurulum bu projede tekrar tekrar hataya düştü. `-executeMethod` ile Unity açmadan da koşar (editor-tools.md) | Assets/_Project/Code/Editor/ZombieSetup.cs |
| 2026-09-02 | Üreteç kökü yeniden kurduğu için NavMeshSurface her üretimden sonra yeniden eklenir | tools-programmer | Eski bake sessizce geçersiz kalıyordu; kurulum aracı bake'i kendi yapıyor | Assets/_Project/Code/Editor/ZombieSetup.cs |
| 2026-09-02 | TETİKLENDİ: 3. config dosyası (`zombie.json`) eklendi → config importer artık borç değil, sıradaki iş | unity-architect | Tetikleyici M-01'de tanımlıydı ve karşılandı. Üç dosyanın sayıları hâlâ C# varsayılanlarında da duruyor; sapma riski artık üç kat | design/milestones/M-01.md |
| 2026-09-02 | `DebugPlayerHealth` ve `ZombieSandbox` bilinçli olarak geçici | ai-programmer | Zombinin hissiyatına bugün bakabilmek için en küçük iskele. Gerçek doğum akışı M1-05, oyuncu canı M1-11; ikisi de bu dosyaları siler | Assets/_Project/Code/AI/ZombieSandbox.cs |
| 2026-09-02 | Rampanın üstünden geçen iç bölmeye üreteç otomatik olarak tavana kadar açıklık açar | level-designer | Rampa duvarı 3 m yükseklikte kesiyordu — kapı boşluğunun üstünde, gözle görünmeyen bir tıkaç. Zombiler orada takıldı ve üst kata çıkamadı. Ölçüler denenerek bulunduğu için bu kontrol insana bırakılamaz | Assets/_Project/Code/Editor/BlockoutGenerator.cs |
| 2026-09-02 | Çakışan kapı ve rampa açıklığı tek açıklıkta birleştirilir | level-designer | Ayrı bırakılırsa kapının lentosu tam rampanın geçtiği yükseklikte kalıyor; tıkaç yerinde duruyor ve düzeltildi sanılıyor | Assets/_Project/Code/Editor/BlockoutGenerator.cs |
| 2026-09-02 | NavMesh bağlantısı göz kararıyla değil `CalculatePath` ile doğrulanıyor | level-designer | Scene görünümünde mavi katmana bakmak rampayı kesen duvarı yakalamadı. Kopukluğun bedeli oyunda "zombiler yukarı gelmiyor" olarak ödeniyor ve orada teşhis pahalı | Assets/_Project/Code/Editor/LevelConnectivityCheck.cs |
| 2026-09-02 | M0-04 yük testi (`AgentLoadTest`) kaldırıldı | kullanıcı | M-00 kapandı, ölçüm kanıtı `docs/qa/performance/M0-04-navmesh-agent-yuku.md`'de duruyor. Gerekirse git geçmişinden geri alınır | Assets/_Project/Code/AI/ |
| 2026-09-02 | **Config borcu kapandı:** sayılar artık yalnızca `config/` içinde | unity-architect | Üç alanın sayıları JSON'da ve C# varsayılanlarında elle eşleniyordu. Importer ikisini de JSON'dan üretiyor; sapma yapısal olarak imkânsız | docs/architecture/adr/ADR-0005-config-importer-ve-bunker-config.md |
| 2026-09-02 | Yeni assembly: `Bunker.Config` (üretilen ScriptableObject'ler) | unity-architect | `Bunker.Systems` Unity'ye kapalı, SO orada yaşayamaz. Gameplay'e koymak AI'ın zombi ayarına ulaşmasını engellerdi | docs/architecture/ARCHITECTURE.md |
| 2026-09-02 | Üretilen alan adları **her zaman** grup önekli (`CountMaxConcurrent`) | tools-programmer | "Çakışırsa önekle" kuralı, şemaya yeni anahtar eklendiğinde başka bir alanın adını sessizce değiştirirdi | Assets/_Project/Code/Editor/Config/ConfigSchema.cs |
| 2026-09-02 | İçe aktarma iki geçiş ve bu gizlenmedi | tools-programmer | Yeni üretilmiş tip, onu üreten domain içinde var olamaz. Arka plan durum makinesi yarım kalmış içe aktarmayı teşhis edilemez yapardı | Assets/_Project/Code/Editor/Config/ConfigImporter.cs |
| 2026-09-02 | Kendi JSON ayrıştırıcımız yazıldı (paket eklenmedi), 15 testle | tools-programmer | `JsonUtility` şekli bilinmeyen belge okuyamaz. Sayılar InvariantCulture ile okunur: tr-TR makinede "1.4" sessizce 14 olurdu | Assets/_Project/Code/Systems/Config/Json.cs |
| 2026-09-02 | `EconomyConfig.AwardFor` uzantı metoduna taşındı | tools-programmer | Üretilen kod yalnızca veri taşır; bir olayın hangi puanı verdiği kuraldır ve kurallar üretilmez | Assets/_Project/Code/Systems/Economy/EconomyRules.cs |
| 2026-09-02 | Zombi konum seam'i KİLİTLENDİ: tek paket, `ZombieNetworkRelay` | netcode-programmer | M-01'in "M1-05'ten önce kilitlenir" sözleşmesi. 40 nesnenin her biri kendi konumunu yayınlarsa 40 mesaj ve 40 başlık olur; tek seam tek yerden ölçülebilir | Assets/_Project/Code/Net/ZombieNetworkRelay.cs |
| 2026-09-02 | Paket maliyeti: zombi başına 12 bayt, 40 zombi = 482 bayt, 10 Hz'de ~4.8 KB/sn | netcode-programmer | Konum santimetre çözünürlüğünde 3×3 bayt, yaw 1 bayt. Ham float'la aynı paket ~10 KB/sn olurdu. Sayı testle korunuyor | Assets/_Project/Code/Systems/Net/ZombieSnapshot.cs |
| 2026-09-02 | Ölen zombi ayrı mesajla değil, **pakette görünmeyerek** kaybolur | netcode-programmer | Ayrı bir ölüm mesajı kaybolabilir ve istemcide hayalet zombi bırakır; paket zaten her tick geliyor, kayıp paketi bir sonraki düzeltir | Assets/_Project/Code/Net/ZombieNetworkRelay.cs |
| 2026-09-02 | Otoriteyi `Bunker.Net` söyler, `Bunker.AI` yalnızca dinler | unity-architect | AI'ın Mirror'a bağımlı olmaması zombi mantığının ağsız test edilebilmesinin tek sebebi. Bağımlılık ters çevrildi: `SetAuthoritative` | Assets/_Project/Code/AI/ZombieDirector.cs |
| 2026-09-02 | M1-05 `Bunker.Gameplay` yerine `Bunker.AI` + `Bunker.Net`'e yazıldı | unity-architect | `gameplay-code.md` Gameplay'in AI'ya bağımlı olmasını yasaklıyor; doğum zombi nesnesine dokunuyor. Milestone tablosundaki yer, kural yazılmadan önce seçilmişti | design/milestones/M-01.md |
| 2026-09-02 | Tur akışı bütçe verir, emir vermez (`ReportSpawned`) | systems-designer | Doğum noktası kapalıysa ya da havuz doluysa istenen zombi doğmaz. Sayaç niyete göre ilerleseydi tur, hiç doğmamış zombileri bekleyerek sonsuza kadar açık kalırdı | Assets/_Project/Code/Systems/Rounds/RoundRunner.cs |
| 2026-09-02 | Havuz sınırlı ve sınırda `null` döner | systems-programmer | Sessizce büyüyen bir havuz, ertelenmiş bir kare hızı hatasıdır. Sınır `maxConcurrent`, yani PERF-BUDGET'tan gelir | Assets/_Project/Code/AI/ZombiePool.cs |
| 2026-09-02 | Araçlar sahipsiz Unity kilidini kendileri temizliyor | tools-programmer | Derleme hatasıyla ölen toplu çalıştırma kilit bırakıyor; sonraki her araç "Editor açık" diyerek yanlış yere baktırıyordu | .claude/tools/unity-lock.ps1 |
| 2026-09-02 | Silahta girdi aynı karede karşılanır; host'un onayı beklenmez | gameplay-programmer | Geri tepme, iz ve mermi sayacı yerelde hemen olur. Onay beklemek tek kişilik oyunda bile hissedilen bir gecikme üretir — oyunun ölü hissetmesinin en yaygın sebebi (gameplay-code.md, 60 ms) | Assets/_Project/Code/Gameplay/PlayerWeapon.cs |
| 2026-09-02 | Hasar yalnızca host'ta; atış hızı sunucuda AYRICA sayılır | netcode-programmer | Her RPC bir güven sınırıdır. İstemcinin `CmdFire`'ı döngüde çağırması sunucunun kendi `WeaponState`'i tarafından reddedilir; atış noktası da oyuncudan 3 m'den fazla kopamaz | Assets/_Project/Code/Gameplay/PlayerWeapon.cs |
| 2026-09-02 | `IDamageable.CountsAsHeadshot`: hedef kendi anatomisini bilir, silah bilmez | gameplay-programmer | Alternatifler katman testi ya da bileşen tipi tanımaktı; ikisi de yeni düşman tipinde silaha dokunmayı gerektirirdi. Çarpan hasardan ÖNCE bilinmeli, yoksa tek atıştan iki hasar olayı çıkar | Assets/_Project/Code/Systems/Combat/Damage.cs |
| 2026-09-02 | Girdi tamponu (`inputBufferSeconds`) silahın ayarına girdi | game-designer | "Bastım ama atmadı" hissi, bekleme süresinin bir kare öncesine denk gelen basışların yenmesinden doğar. Tampon bunu config'ten ayarlanabilir yaptı | config/schema/weapon.schema.json |
| 2026-09-02 | Geri tepme nişanın ÜSTÜNE biner, yerine geçmez | gameplay-programmer | Oyuncu geri tepmeyi aşağı çekerek bastırabilmeli. Silahın kameraya doğrudan yazması aynı sayıyı iki yerden süren iki sahip demek olurdu; nişanın "kayması" tam olarak böyle doğar | Assets/_Project/Code/Gameplay/PlayerController.cs |
| 2026-09-02 | İsabet işareti nişangâhın kendisini değiştirir, üstüne şekil binmez | game-ux-designer | Kaosun içinde iki ayrı şey okumaya zorlamak PILLAR-04'ü çiğner. Kafa vuruşu ayrı renk | Assets/_Project/Code/UI/CombatHud.cs |
| 2026-09-02 | Boş şarjörde tetiğe basmak dolum başlatır | game-designer | Ayrıca R'ye basmayı beklemek, sürünün içinde ceza gibi hissettirir | Assets/_Project/Code/Gameplay/PlayerWeapon.cs |
| 2026-09-02 | Ekonomi silaha bağlandı: öldürme puanı `PlayerScore`'da yazılır | gameplay-programmer | M1-02'nin cüzdanı ilk kez oyuna bağlandı. Puan kalıcı sonucu olan bir şeydir; otorite host'ta (ADR-0004) | Assets/_Project/Code/Gameplay/PlayerScore.cs |
| 2026-09-02 | Config kodu tüketicisinden ÖNCE üretilmeli | tools-programmer | `WeaponState` üretilmemiş `WeaponConfig`'e bağlanınca proje derlenmedi ve importer da koşamadı (Unity derleme hatasıyla açılmıyor). Sıra: şema → içe aktar → tüketici | docs/guides/config-nasil-degistirilir.md |
| 2026-09-02 | **BUG-001:** sunucunun gölge şarjörü hiç dolmuyordu (dolum bildirilmiyordu) | qa-lead | Üç zombiden sonra bütün atışlar sessizce reddediliyordu. Ders: sunucuda kaynak sayan sistem, kaynağı geri veren yolu aynı anda yazmalı | docs/qa/bugs/BUG-001-sunucu-sarjoru-hic-dolmuyor.md |
| 2026-09-02 | Reddedilen her sunucu işlemi **görünür** olmalı, sessiz `return` yasak | netcode-programmer | Sessizlik teşhisi bir oyun testine bağladı. Uyarı saniyede bir kez sınırlı — döngüde çağrılan istemci Console'u dolduramamalı | Assets/_Project/Code/Gameplay/PlayerWeapon.cs |
| 2026-09-02 | Hitscan `RaycastNonAlloc` yerine `Physics.Raycast` kullanıyor | gameplay-programmer | NonAlloc tamponu SIRASIZ doldurur; ışın üzerinde sekizden fazla çarpışan varsa gerçek en yakını atabilir. Kalabalıkta "mermi gitmedi" hatası üretir | Assets/_Project/Code/Gameplay/PlayerWeapon.cs |
| 2026-09-02 | **BUG-002:** sunucu doğrulaması eşitlik değil **makuliyet** testi oldu | netcode-programmer | Gölge `WeaponState` kare kare aynılık bekliyordu; komut bir kare geç geldiği için meşru atışlar reddediliyordu. `ServerFireGuard` aynı kuralları ağ payıyla uyguluyor | docs/qa/bugs/BUG-002-mesru-atislar-reddediliyor.md |
| 2026-09-02 | Ağ toleransı (0.12 sn) config'te değil, mühendislik sabiti olarak kodda | systems-designer | Bir denge değeri değil; oyuncunun hissedeceği bir şeyi ayarlamıyor, ağın fiziksel gecikmesini karşılıyor | Assets/_Project/Code/Systems/Combat/ServerFireGuard.cs |
| 2026-09-02 | **M1-13 vuruş hissi** M-01 kapsamına eklendi (+2 gün) | kullanıcı | Silah çalışıyor ama vurmak bir şey hissettirmiyor. ÇK-17'nin ön şartı: gri kutuda eğlenceli mi sorusu, vuruşun karşılıksız olduğu bir yapıda dürüstçe cevaplanamaz | design/milestones/M-01.md |
| 2026-09-02 | Ağ toleransı **birikmez**: bir sonraki izinli an planlanan andan ilerler | netcode-programmer | İlk sürümde tolerans her atıştan ayrı düşülüyordu ve 400 RPM'lik silah fiilen 2000 RPM atabiliyordu. Test yakaladı — tolerans bir karelik titremeyi yutmalı, sürekli hız avantajı vermemeli | Assets/_Project/Code/Systems/Combat/ServerFireGuard.cs |
| 2026-09-02 | M1-13: isabet **hazırlanan vuruşu keser** | game-designer | Telegrafı gören oyuncunun iki seçeneği olur — geri çekilmek ya da vurup kesmek. Sendeleme yalnızca görsel olsaydı bu seçenek hiç doğmazdı; ateş etmeye taktik değeri veren yer burası | Assets/_Project/Code/Systems/Ai/ZombieBrain.cs |
| 2026-09-02 | Ölüm ile havuza iade **ayrı olaylar** (`Killed` / `Despawned`) | ai-programmer | Puan ve tur sayacı ölüm anında yazılmalı, ama nesne yıkılma anı boyunca görünür kalmalı. Tek olay olsaydı ya ceset anında kaybolur ya tur cesetleri beklerdi | Assets/_Project/Code/AI/ZombieAgent.cs |
| 2026-09-02 | Barikat zombiyi **durdurmaz, geciktirir** (`beforeEntry` eşiği) | game-designer | Mutlak bir duvar olsaydı tek pencereyi tutmak yeterli olurdu ve harita anlamını yitirirdi. Barikatın oyuna kattığı şey, oyuncuya kazandırdığı zamandır | config/schema/barricade.schema.json |
| 2026-09-02 | Söküm ve tamir aynı anda ilerlemez; biri diğerinin ilerlemesini sıfırlar | game-designer | İkisi de yarım tahta biriktirip sırayla tamamlasaydı kimin kazandığı okunmaz olurdu (PILLAR-04) | Assets/_Project/Code/Systems/Combat/Barricade.cs |
| 2026-09-02 | Tamir puanı **tahtanın takıldığı anda** yazılır, tuşa basılı tutmaya değil | systems-designer | Süreye göre puan, tuşa yaslanarak puan basmak demek olurdu. Tamamlanan iş ödüllendirilir | Assets/_Project/Code/Gameplay/PlayerRepair.cs |
| 2026-09-02 | `IRepairable` arayüzü: tamir eden taraf neyi tamir ettiğini bilmez | unity-architect | Barikat `Bunker.AI`'da, tamir `Bunker.Gameplay`'de ve Gameplay AI'ya bağımlı olamaz. `IDamageable` ile aynı desen | Assets/_Project/Code/Systems/Combat/IRepairable.cs |
| 2026-09-02 | Bıçak koni içindeki **tek** hedefe vurur, hepsine değil | game-designer | Koni içindeki herkese vurmak bıçağı alan silahına çevirir ve riskini siler. Bıçak en yüksek puanı riski karşılığında alır | Assets/_Project/Code/Gameplay/PlayerMelee.cs |
| 2026-09-02 | İstemciden gelen `deltaTime` doğrulanır (tamir) | netcode-programmer | "Bir saniye tamir ettim" diyerek barikat anında doldurulabilirdi. Bir karelik makul tavanla sınırlanıyor | Assets/_Project/Code/Gameplay/PlayerRepair.cs |
| 2026-09-02 | Hız sınırı + ağ payı ortak sınıfa çıkarıldı (`ActionRateLimiter`) | netcode-programmer | BUG-002'nin dersi tek yerde yaşasın: bıçak aynı hatayı tekrar üretmesin | Assets/_Project/Code/Systems/Combat/ActionRateLimiter.cs |
| 2026-09-02 | **BUG-003:** `IsOpen` (yapısal) ile `AllowsEntry` (anlık) ayrıldı | ai-programmer | Barikat `IsOpen`'ı kapatınca doğum noktası seçimi hiçbir pencere bulamadı ve hiç zombi doğmadı. Zombi barikatlı pencerede doğar ve söker; geçilemiyor olması oranın giriş noktası olmadığı anlamına gelmez | docs/qa/bugs/BUG-003-barikat-dogumu-kilitledi.md |
| 2026-09-02 | Betik taşınırken `.cs` ve `.cs.meta` **birlikte** taşınır | tools-programmer | Config üretimi için geçici olarak taşınan `.cs` dosyalarının meta'ları geride kaldı; Unity yeni GUID üretti ve Player prefab'ındaki bileşenler koptu | docs/guides/config-nasil-degistirilir.md |
| 2026-09-02 | Doğum noktası bulunamazsa bir kez yüksek sesle hata | ai-programmer | Tur akışının dönüp sahanın boş kalması, sessiz kaldığı sürece teşhisi en zor hata türü (BUG-001 ile aynı ders) | Assets/_Project/Code/AI/ZombieDirector.cs |
| 2026-09-02 | `IPurchasable`: satın alma etkileşimi tek yerde | gameplay-programmer | Kapı, duvar silahı ve ileride dağıtıcı aynı yoldan geçiyor. Yeni bir satın alınabilir eklemek oyuncu koduna dokunmayı gerektirmiyor (`IDamageable` ile aynı desen) | Assets/_Project/Code/Systems/Economy/IPurchasable.cs |
| 2026-09-02 | Kapı kalıcı ve tek yönlü; duvar silahı tekrar alınabilir | game-designer | Kapı bir **karardır** — geri alınabilir bir karar karar değildir. Duvar silahı bir **kaynaktır** ve her tur yeniden gerekir | Assets/_Project/Code/Gameplay/PurchasableDoor.cs |
| 2026-09-02 | Kapının açık olması `SyncVar` — geç katılan doğru görür | netcode-programmer | netcode.md'nin dört durumundan biri. M-01'de yazılmazsa M-02'de aranacak olan tam da bu | Assets/_Project/Code/Gameplay/PurchasableDoor.cs |
| 2026-09-02 | M1-10 M-01'de yalnızca **mermi** satıyor | game-designer | Silah çeşitliliği SYS-03 ve bu milestone'un duvarının dışında. Buradaki tek görev mermi ekonomisine bir musluk açmak — musluk olmadan puan birikir, harcanacak yer olmaz ve oyun bıçakla oynanır | Assets/_Project/Code/Gameplay/WallWeaponPurchase.cs |
| 2026-09-02 | `E` çakışması tanımlı: basmak satın alır, tutmak tamir eder | game-ux-designer | İkisi pratikte aynı anda görüş alanında olmaz, ama "olmaz" varsaymak yerine kural yazıldı: satın alınabilir bir şeye bakarken tamir durur | Assets/_Project/Code/Gameplay/PlayerRepair.cs |
| 2026-09-02 | Barikat kuralı okunabilir hâle getirildi: **tahta varsa geçemez** (`beforeEntry` 1→0) | game-designer | Oyun testinde "barikat olduğu halde zombi geçti" diye okundu — son tahta dururken sızma serbestti ve ekranda hâlâ tahta görünüyordu. Sızma mekaniği duruyor, gerekirse geri açılır | config/balance/barricade.json |
| 2026-09-02 | Dışarıda kalan zombi pencereye **geri döner** | ai-programmer | Bir kez sıkışıp kovalamaya geçen zombi, binaya girmesi gerektiğini bir daha hatırlamıyordu: dışarıdaki NavMesh adasında oyuncuya en yakın noktaya yürüyüp orada kalıyordu | Assets/_Project/Code/Systems/Ai/ZombieBrain.cs |
| 2026-09-02 | **BUG-004:** kapı artık gerçek bir kanat; navigasyonu `NavMeshObstacle` keser | gameplay-programmer | Üreteç duvarda zaten boşluk bırakıyordu, kapanmış bir şey yoktu. Kanat bake'e girseydi satın almak geçidi açmazdı — bake edilmiş NavMesh çalışma anında değişmez | docs/qa/bugs/BUG-004-kapi-gorunmuyor-zombiler-icerde.md |
| 2026-09-02 | `SetPrivateField` her tipi açıkça ele alıyor; bilinmeyen tip **hata** | tools-programmer | Yalnızca bool ve nesne referansı biliyordu; enum, metin ve dizi alanları sessizce boş kalıyordu. Kapının fiyat bandı ve adı bu yüzden hiç yazılmadı | Assets/_Project/Code/Editor/ZombieSetup.cs |
| 2026-09-02 | Doğum noktası örnekleme yarıçapı 4 m → 1.5 m + dış taraf garantisi | ai-programmer | `SamplePosition` duvarları umursamaz; dışarıda nokta bulunamayınca içerideki zemine yapışıp zombiyi binanın içinde doğuruyordu | Assets/_Project/Code/AI/ZombieDirector.cs |
| 2026-09-02 | Bağlantı ölçümü kurulumun içine taşındı (bake'in hemen ardına) | tools-programmer | Kapalı kapılar NavMesh'i oyuyor ve oymanın geri alınması oyun döngüsüne bağlı; toplu çalıştırmada kare ilerlemediği için araç haksız yere "kopuk" diyordu. **Aracın yanlış anda ölçmesi, yanlış ölçmesinden tehlikelidir** | Assets/_Project/Code/Editor/ZombieSetup.cs |
| 2026-09-02 | Barikat 6×1.8 sn → 4×1.2 sn | game-designer | 11 saniyelik sökme, tur süresinin büyük kısmını pencerede beklemeye çeviriyordu ve "zombiler takip etmiyor" diye okundu | config/balance/barricade.json |
| 2026-09-02 | **BUG-005:** doğum konumunu `ZombieAgent.Spawn` yazar, `Warp` ile | ai-programmer | `NavMeshAgent` açıkken `transform.position` yazmak işe yaramaz; havuzdan çıkan zombi bir önceki hayatında öldüğü yere geri çekiliyordu. Bir bileşen bir alanın sahibiyse o alana başka kimse yazmamalı | docs/qa/bugs/BUG-005-havuzdan-cikan-zombi-eski-yerinde-beliriyor.md |
| 2026-09-02 | Yalnızca **oyuncunun ulaşabildiği** pencerelerde doğum olur | game-designer | Klasik tur döngüsünün temel kuralı: kapalı bölgede zombi doğmaz. Bölge tanımına gerek yok — kapalı kapı NavMesh'i kestiği için "oyuncuya yol var mı" sorusu bölge sorusunun karşılığı | Assets/_Project/Code/AI/ZombieDirector.cs |
| 2026-09-02 | REDDEDİLDİ: "sıkışan zombiyi bir süre sonra sahadan çek" emniyet ağı | kullanıcı | Hatayı başka bir hatayla kapatmak. Turun neden kilitlendiğini gizler ve gerçek sebebi arama isteğini öldürür. Kök sebep düzeltildi, yama geri alındı | Assets/_Project/Code/AI/ZombieDirector.cs |

## 2026-09-04 — Gri kutuya okunabilirlik paleti ve kapatılabilir atmosfer

**Karar:** M-01'e renk dili, ışık, gökyüzü, sis ve post-processing eklendi.
Atmosfer katmanı **F10 ile kapatılabilir**.

**Neden:** iki ayrı gerekçe, ikisi de geliştiriciden geldi.

1. **Okunabilirlik — açık bir M-01 borcuydu.** Sahnedeki her şey Unity'nin varsayılan
   grisiydi; kapı, duvar, zemin, rampa ve düşme deliği kenarı ayırt edilemiyordu, duvar
   silahı satın alma noktalarının **hiç görseli yoktu**. PILLAR-04 "okunabilirlik bir
   cila işi değil, bir tasarım kısıtıdır" diyor ve *"sanat gelince okunur hale gelir"*
   gerekçesiyle ertelenen kararları açıkça reddediyor. Bu bir asset eksikliği değil,
   tasarım eksikliğiydi.
2. **Atmosfer — geliştiricinin bilinçli tercihi.** "Gri gri oynamayalım."

**Ölçüm bedeli — kayda geçirildi.** ÇK-17 tam olarak *"gri kutuda, sanatsız bir tur
döngüsü 20 dakika sonra tekrar oynatıyor mu"* diye soruyor. Atmosfer açıkken alınan bir
"evet", döngünün mü görselliğin mi taşıdığını söylemez. Bu, geliştiriciye söylendi ve
tercih onun. **Azaltma:** atmosfer tek tuşla kapanır, böylece ÇK-17 istenirse temiz gri
kutuda ölçülebilir; renk dili her iki durumda da açık kalır (o cila değil, kısıt).

**Not:** bu bir **sanat geçişi değildir**. Stil kilidi yok; `/art-direction` gerçek görsel
dili kurduğunda bu palet yerini bırakır. Kalıcı olan renk değil, ayrımın kendisi.

Detay: `docs/art/GREYBOX-PALETTE.md`

## 2026-09-04 — `magazinesPerPurchase` config'e taşındı (economy şema v2)

**Karar:** duvardan bir alımda verilen şarjör sayısı, `WallWeaponPurchase`'daki bir
`[SerializeField]` olmaktan çıktı; `config/balance/economy.json → ammo.magazinesPerPurchase`
oldu. **Değer değişmedi (5).**

**Neden:** alanın tooltip'i *"Denge değeri DEĞİL — şarjör kapasitesinin katıdır"* diyordu.
Denge simülasyonu (`design/economy/curves.md`) tersini kanıtladı: bu sayı **turun ritmini
doğrudan belirliyor.** 5 ile oyuncu tur 14'te duvara **on beş ayrı sefer** yapıyor —
PILLAR-03'ün açıkça reddettiği "menü işi yapıyorum" modu. Ritmi belirleyen bir sayı,
`config-data.md` gereği C# içinde duramaz.

**Şema sürümü 1 → 2.** Kayıtlı bir anahtar değil, migration gerekmiyor.

**Not:** bu bir `/tune` değil. Değer aynı kaldı; yalnızca yaşadığı yer değişti — böylece
oyun testinden sonra ayarlanabilir hâle geldi. Aralık (1–40) ve şema açıklaması,
oynamadan önce hangi yönün neyi bozacağını yazıyor.

## 2026-09-04 — Tur başı otomatik yenilenme kaldırıldı (barikat + mermi)

**Karar (geliştirici):** *"Tamir eden zaten tur arası süresinde tamir etsin edebildiği
kadar; ama tur başladığında tamamen yenilenen barikat, can, mermi oyunu çok kolay
kılıyor."*

Üçünün üçü de gerçekten öyle çalışıyordu. Ama **üçü aynı şey değil:**

| | Neydi | Ne yapıldı | Neden M-01 |
|---|---|---|---|
| **Barikat** | Her tur başında `4/4`'e dönüyordu | Tur sıfırlaması kaldırıldı; yalnızca **run** başında sıfırlanıyor | **Sadakat düzeltmesi.** Klon taban barikatı otomatik onarmaz; otomatik dönüş bizim kazara eklediğimiz sapmaydı |
| **Mermi** | Her tur başında tam yedeğe dönüyordu | Aynı: yalnızca run başında | **Kalmış iskele.** Kodun kendi yorumu *"M1-10 duvar silahı gelince kaldırılacak"* diyordu; M1-10 geldi, iskele kaldı |
| **Can** | Vurulmayınca tam yenileniyor | **Değişmedi** — M-02'ye ertelendi | Bu klona **sadık**. İki katmanlı model (aşağıda) bir farklılaştırıcı, kontrol grubuna girmez |

**Mermi değişikliğinin etkisi küçük değil.** Denge simülasyonu mermiyi satın alınan bir
kaynak varsayarak koştu ve gelirin %77–98'inin mermiye gittiğini buldu
(`design/economy/curves.md`). Yani oyun bugüne kadar simülasyonun anlattığından
**belirgin şekilde kolaydı**; bu değişiklikle ikisi hizalandı ve KIRILMA 1 (mermi
seferleri) artık gerçekten hissedilecek.

**Run sıfırlaması eklendi.** Tur sıfırlaması kaldırılınca ikisini düzelten başka hiçbir
yol kalmıyordu: `R` ile başlayan ikinci run, birincinin sökük barikatları ve kalan
mermisiyle başlardı — M1-11 AC-6'nın ("sayılar sızmaz") barikat ve mermi karşılığı.

**Oyun testinde izlenecek:** barikatın eski yorumu bir uyarı taşıyordu ve hâlâ geçerli
olabilir — *"sökük pencereleri tek tek tamir etmek molayı bir dinlenme değil ev ödevi
yapar (PILLAR-03)."* 10 saniyelik mola dört pencereye yetmiyorsa bu gerilim gerçek olur.

## 2026-09-04 — Can: iki katmanlı model onaylandı (M-02)

**Karar (geliştirici):** Darktide modeli onaylandı — hızlı yenilenen bir **kalkan**
katmanı + kısmi yenilenen **can** katmanı.

Bu, 3 Eylül'deki "vurulmayınca can tam yenilenir" kararıyla arasındaki çelişkiyi çözüyor:
kaçmanın ödülü kalkanda yaşamaya devam ediyor, yıpranma ise cana yazılıyor.

**M-02'ye ertelendi.** M-01 bilerek bir kontrol grubu; iki katmanlı can bir
farklılaştırıcı ve kontrol grubunun içine konursa ÇK-17'nin cevabı yorumlanamaz olur.

**Açık kalan:** can nasıl geri gelir? Kalkan kendiliğinden dolar, ama can hiç dolmazsa
her run bir ölüm sarmalına döner. Darktide bunu medicae istasyonu ve iksirlerle çözüyor.
Bizde adayları: tezgâhtan iyileştirme, zombiden düşen nadir can, hasarsız biten turun
ödülü, `Vampir`/`Kan Nakli` kartları. **Karar verilmedi** — `SYS-02` §7f.
