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
| 2026-08-29 | Kapsam sayıları ve içerik planı ertelendi | kullanıcı | Mimariyi kısıtlamıyorlar; zamanı gelince planlanacak | `<tbd>` |
| 2026-08-29 | ADR-0001: FishNet + Pro (~$60) kabul edildi — **SUPERSEDE EDİLDİ**, bkz. 2026-08-29 Mirror satırı | kullanıcı | Gerekçe zinciri (NGO/Mirror/Netick elemesi, "lag compensation Pro özelliği" düzeltmesi) ADR'de duruyor | docs/architecture/adr/ADR-0001-netcode-kutuphanesi.md |
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
| 2026-08-29 | Kurulu Unity 6.5 reddedildi, 6000.3 LTS kurulacak | ADR-0002 (veriyle güncellendi) | LTS olmayan sürümler bir sonraki sürüm çıkınca yama almayı bırakıyor: 6.1 ve 6.2 dörder ay, 6.4 üç ay yaşadı. 6.5 için 6.6 Eylül'de bekleniyor | docs/architecture/adr/ADR-0002-unity-surumu.md |
| 2026-08-29 | M-00 planı: kaybedilebilir ölçüm milestone'u; yük gerçekçi olmalı (40 küp NavMesh ile, naif `NetworkTransform` karşılaştırma tabanıyla) | producer + analiz | Host aynı zamanda oyuncu; CPU rekabeti ancak gerçek AI yüküyle ölçülür. Plan detayı milestone dosyasında | design/milestones/M-00.md |
| 2026-08-29 | DÜZELTME: Mirror'ın lag compensation'ı VAR (Beta, MIT) | araştırma | ADR-0001 Mirror'ı "lag compensation yok" diye elemişti, bilgi yanlıştı | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Netcode: Mirror (MIT). ADR-0001 supersede edildi | kullanıcı | Bu oyunda prediction atlanabilir (co-op'ta client-authoritative hareket), lag compensation atlanamaz. FishNet tersini ücretsiz veriyor | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Oyuncu hareketi client-authoritative olacak | ADR-0004 | Davetle girilen arkadaş co-op'unda hile toleransı yüksek; prediction makinesi gereksiz | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Kabul edilen taviz: liderlik tablosu ve meta ilerleme manipüle edilebilir | ADR-0004 | Client otoritesinin bedeli; arkadaş co-op'unda kabul edilebilir | docs/architecture/adr/ADR-0004-netcode-kutuphanesi-mirror.md |
| 2026-08-29 | Mirror üçüncü parti olarak depoya commit edildi (30 MB) | analiz | Klonlayanın aynı sürümü alması ve .meta GUID'lerinin sabit kalması için | Assets/Mirror/ |
| 2026-08-29 | Sıralama değişti: önce solo çekirdek döngü, sonra multiplayer | kullanıcı | Gri kutuda küp senkronlayarak oyunun eğlenceli olduğu öğrenilemez; motivasyon solo geliştiricide gerçek kaynak | design/milestones/M-01.md |
| 2026-08-29 | M-00 küçültüldü; netcode doğrulaması M-02'ye taşındı | producer | M-00'da yalnızca solo'da bedavaya ölçülebilen kısım kaldı (40 NavMesh agent + host CPU) | design/milestones/M-00.md |
| 2026-08-29 | ZORUNLU: solo, Mirror host modunda kurulur (ağsız prototip DEĞİL) | kullanıcı | Ağsız yazılırsa multiplayer geldiğinde oyuncu/silah/zombi/spawn yeniden yazılır — Unity'nin klasik pahalı hatası | design/milestones/M-01.md |
| 2026-08-29 | ZORUNLU: zombi konum senkronu tek seam'den geçer, NetworkTransform yasak | ADR-0004 | Ölçüm ertelendiği için, kötü çıkarsa sadece o parça değişsin | design/milestones/M-00.md |
| 2026-08-29 | Kabul edilen risk: PILLAR-02 M-01'de hiç sınanamaz | analiz | Solo build "dört oyuncu birbirine muhtaç" sütununu test edemez; ÇK-17 gerekli ama yeterli değil, ÇK-17b M-02'de | design/milestones/M-02.md |
| 2026-08-29 | Oyuncuda NetworkTransformUnreliable kullanılıyor (ClientToServer) | ADR-0004 | NetworkTransform yasağı zombiler içindir (40 nesne); 4 oyuncu için doğru araç | Assets/_Project/Code/Gameplay/PlayerController.cs |
| 2026-08-29 | M0-02'de girdi doğrudan cihazdan okunuyor (Keyboard/Mouse.current) | gameplay-programmer | Proje yalnızca yeni Input System'de (activeInputHandler=1); iskelette sıfır bağlama adımı. M1-01'de InputSystem_Actions'a taşınacak | Assets/_Project/Code/Gameplay/PlayerController.cs |
| 2026-08-30 | Ölçüm aracı ölçümü bozmaz: sıfır tahsis, otomatik tarama, yüzdelik hesabı `Bunker.Systems`'te (`FrameTimeRecorder`) | performance-engineer | Elle koordine edilen dört ölçüm ilk denemede veri üretmedi. Yetkili kanıt Unity Profiler'dır, HUD canlı geri bildirim içindir | Assets/_Project/Code/Systems/Diagnostics/FrameTimeRecorder.cs |
| 2026-08-31 | BULGU: NavMesh darboğaz değil, risk animator ve ağ serileştirmesine kaydı | performance-engineer | Ölçüm 40 agent'ın sıfırında animator içeriyordu; kalabalık oyunlarında animator sık sık NavMesh'ten pahalıdır | docs/architecture/PERF-BUDGET.md |
| 2026-08-31 | **M-00 KAPANDI** (4/4 iş, 4/4 kriter): Mirror Unity 6.3ta temiz derleniyor (ÇK-9), solo host iskeleti çalışıyor (ÇK-10), 40 NavMesh agent ~0.2 ms = bütçenin %1.2i (ÇK-5) | producer | Kanıtlar `docs/qa/evidence/` ve `docs/qa/performance/` altında; yük testi aracı sonra kaldırıldı | design/milestones/M-00.md |
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
| 2026-09-02 | Rampanın üstünden geçen iç bölmeye üreteç tavana kadar açıklık açar; çakışan kapı ve rampa açıklığı TEK açıklıkta birleşir | level-designer | Rampa duvarı 3 m yükseklikte kesiyordu - kapı boşluğunun üstünde, gözle görünmeyen bir tıkaç; zombiler orada takıldı. Ayrı bırakılan iki açıklıkta kapının lentosu tam rampanın geçtiği yükseklikte kalıyor ve tıkaç düzeltildi sanılarak yerinde duruyor |
| 2026-09-02 | NavMesh bağlantısı göz kararıyla değil `CalculatePath` ile doğrulanıyor | level-designer | Scene görünümünde mavi katmana bakmak rampayı kesen duvarı yakalamadı. Kopukluğun bedeli oyunda "zombiler yukarı gelmiyor" olarak ödeniyor ve orada teşhis pahalı | Assets/_Project/Code/Editor/LevelConnectivityCheck.cs |
| 2026-09-02 | M0-04 yük testi (`AgentLoadTest`) kaldırıldı | kullanıcı | M-00 kapandı, ölçüm kanıtı `docs/qa/performance/M0-04-navmesh-agent-yuku.md`'de duruyor. Gerekirse git geçmişinden geri alınır | Assets/_Project/Code/AI/ |
| 2026-09-02 | **Config borcu kapandı:** sayılar artık yalnızca `config/` içinde | unity-architect | Üç alanın sayıları JSON'da ve C# varsayılanlarında elle eşleniyordu. Importer ikisini de JSON'dan üretiyor; sapma yapısal olarak imkânsız | docs/architecture/adr/ADR-0005-config-importer-ve-bunker-config.md |
| 2026-09-02 | Yeni assembly: `Bunker.Config` (üretilen ScriptableObject'ler) | unity-architect | `Bunker.Systems` Unity'ye kapalı, SO orada yaşayamaz. Gameplay'e koymak AI'ın zombi ayarına ulaşmasını engellerdi | docs/architecture/ARCHITECTURE.md |
| 2026-09-02 | Üretilen alan adları **her zaman** grup önekli; içe aktarma iki geçiş ve bu gizlenmedi | tools-programmer | "Çakışırsa önekle" kuralı yeni anahtarda başka bir alanın adını sessizce değiştirirdi. Yeni üretilmiş tip kendi domaininde var olamaz; arka plan durum makinesi yarım içe aktarmayı teşhis edilemez yapardı |
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
| 2026-09-02 | **BUG-001:** sunucunun gölge şarjörü hiç dolmuyordu (dolum bildirilmiyordu) | qa-lead | Üç zombiden sonra bütün atışlar sessizce reddediliyordu. Ders: sunucuda kaynak sayan sistem, kaynağı geri veren yolu aynı anda yazmalı | docs/qa/bugs/BUG-001-sunucu-sarjoru-hic-dolmuyor.md |
| 2026-09-02 | Reddedilen her sunucu işlemi **görünür** olmalı, sessiz `return` yasak | netcode-programmer | Sessizlik teşhisi bir oyun testine bağladı. Uyarı saniyede bir kez sınırlı — döngüde çağrılan istemci Console'u dolduramamalı | Assets/_Project/Code/Gameplay/PlayerWeapon.cs |
| 2026-09-02 | Hitscan `RaycastNonAlloc` yerine `Physics.Raycast` kullanıyor | gameplay-programmer | NonAlloc tamponu SIRASIZ doldurur; ışın üzerinde sekizden fazla çarpışan varsa gerçek en yakını atabilir. Kalabalıkta "mermi gitmedi" hatası üretir | Assets/_Project/Code/Gameplay/PlayerWeapon.cs |
| 2026-09-02 | **BUG-002:** sunucu doğrulaması eşitlik değil **makuliyet** testi oldu | netcode-programmer | Gölge `WeaponState` kare kare aynılık bekliyordu; komut bir kare geç geldiği için meşru atışlar reddediliyordu. `ServerFireGuard` aynı kuralları ağ payıyla uyguluyor | docs/qa/bugs/BUG-002-mesru-atislar-reddediliyor.md |
| 2026-09-02 | Ağ toleransı (0.12 sn) bir denge değeri değil, mühendislik sabiti - ve **birikmez** | systems-designer + netcode-programmer | Oyuncunun hissedeceği bir şeyi ayarlamıyor, ağın gecikmesini karşılıyor. İlk sürümde tolerans her atıştan ayrı düşülüyordu ve 400 RPMlik silah fiilen 2000 RPM atabiliyordu; test yakaladı |
| 2026-09-02 | **M1-13 vuruş hissi** M-01 kapsamına eklendi (+2 gün) | kullanıcı | Silah çalışıyor ama vurmak bir şey hissettirmiyor. ÇK-17'nin ön şartı: gri kutuda eğlenceli mi sorusu, vuruşun karşılıksız olduğu bir yapıda dürüstçe cevaplanamaz | design/milestones/M-01.md |
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
| 2026-09-02 | Config değiştirme sırası kurala bağlandı: şema → içe aktar → tüketici, ve `.cs` ile `.cs.meta` **birlikte** taşınır | tools-programmer | Üretilmemiş tipe bağlanan kod projeyi derletmedi ve importer da koşamadı (Unity derleme hatasıyla açılmıyor). Geride kalan meta yeni GUID ürettirdi ve Player prefabındaki bileşenler koptu |
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

---

## 2026-09-05 — Tezgâh kart ekranından ayrıldı, duvara taşındı

Tezgâh önce kart draft ekranının bir paneliydi. Geliştirici oynayınca istedi:
*"tezgah bence kartlardan bağımsız mermi doldurma yeri gibi duvarda olmalı."*

**Gerekçe:** kart seçimi turun **zorunlu ödülü** ve durdurulmuş bir andır; tezgâh ise
oyuncunun **gitmeyi seçtiği** bir harcama noktasıdır. Aynı ekranda olmaları ikincisini
birincisinin eklentisi gibi gösteriyordu. Duvara taşınınca gitmek bir **bedel** oldu:
molandan yiyor ve seni haritanın belirli bir noktasına bağlıyor — yani tezgâh bir karar
hâline geldi. Tezgâh turu **durdurmaz**; duvar silahından farklı renkte (mor) ve farklı
duvarda, çünkü ikisi de E ile açılıyor ve "E ne yapacak" belirsizliği haritanın kendisinde
çözülmeli.

## 2026-09-05 — İlk oyun testi simülasyonun oyuncu profilini yalanladı

Gerçek oyuncu **%76 kafa vuruşu** yaptı; `balance-sim.ps1`'in "iyi" profili %50, "ortalama"
profili %25 varsayıyordu. Kafa vuruşu 2× hasar verdiği için bu, öldürme başına mermi
sayısının ve dolayısıyla bütün ekonomi sonuçlarının girdisi.

**Sonuç:** `curves.md`'nin en önemli bulgusu olan KIRILMA 1 ("mermi seferleri turu
parçalıyor, PILLAR-03 ihlali") **abartılmıştır** — gerçek isabetle tur başına 4 sefer
çıkıyor, 15 değil. ÇK-13 cevabı da 12.5 dk'dan **8.8 dk**'ya düştü, yani oyun hedef
bandın altında.

**Hiçbir denge değeri değiştirilmedi** — tek run, tek oyuncu, ve o oyuncu oyunun yazarı.
Belgenin başına geçersizlik uyarısı kondu; iki-üç run daha biriktikten sonra yeniden
üretilecek. Kanıt: `docs/qa/playtests/PT-01-SONUC-01-ilk-keyifli-run.md`.

## 2026-09-05 — İkinci oyun testinin sekiz bulgusu

Tek bir oturumun çıktısı; hepsi geliştiricinin doğrudan gözlemi.

**1. Silah ve bıçak görünmüyordu.** Elde bir şey olduğuna dair tek kanıt HUD'daki mermi
sayacıydı. `PlayerViewmodel` eklendi: kameranın altında, çalışma anında ilkel şekillerden
kurulan bir el modeli — ateşte geri tepme ve namlu alevi, dolumda aşağı dönen silah, V ile
sağdan sola savrulan bıçak. Prefab'a yazılmadı, çünkü sanat yönü kilitlenince değişecek
tek yer bir dosya olsun.

**2. Üst kat kapısı atlanabiliyordu.** Kanat rampanın ağzında serbest duran bir bloktu;
yanından dolaşıp çıkmak mümkündü — yani 1250 puanlık kapı hiçbir şeyi kapatmıyordu.
Geliştiricinin önerdiği çözüm uygulandı: rampa iki yan duvarla **kapalı bir merdiven
boşluğuna** çevrildi, tavanı zaten üst katın kendi döşemesi. Kanat artık koridor genişliği
kadar geniş ve döşemeye kadar yüksek. *Bir kapı ancak tek geçit olduğunda kapıdır.*

İlk deneme kapıyı rampa ağzına, güney duvarının **35 cm** önüne koydu ve bağlantı kontrolü
"zemin -> ust kat: KOPUK" dedi: NavMesh ajanı 70 cm çapında, o şeride sığmıyor — kapının
önünde durulacak yer kalmamıştı. Kapı bir metre içeri alındı, 1.85 m'lik yaklaşma alanı
kaldı, ölçüm `tam (45.2 m)`. **Aracın bu sefer doğru anda ölçmesi bir oyun testini
kurtardı** (BUG-004'ün dersinin karşılığı).

**3. Tezgâh üst kata taşındı.** Zemin katta, başlangıç odasından çıkmadan ulaşılabiliyordu;
yükseltme almak için haritayı açmak gerekmiyordu. Artık kapının **arkasında**: önce üst
katı aç, sonra yükselt. Bu, kapının fiyatına bir sebep verir.

**4. Mermi fiyatı 250 puan.** `economy.json` v3: `ammo.refillCost` eklendi. Mermi artık
duvar silahı bandından (500/1200) okumuyor — o bant ileride gerçek silahlar için duruyor.
İkisi tek sayıdan okununca mermiyi ucuzlatmak silah fiyatlarını da oynatmak demekti.

**5. Zombi hızlanması yavaşlatıldı.** Kademeler tur 4/8 → **6/12**, tavan hız 4.6 → **3.6**
m/s (şema alt sınırı 3.5). Tur 8'de zombi oyuncunun 5 m/s hızına yapışıyordu ve kaçmak
imkânsızdı. Şemanın "geç turda oyuncudan hızlı olmalı" gerekçesi bu değerde artık geçerli
değil; **bilinçli bir tasarım değişikliği**, aralık genişletilmedi.

**6. Zombiye gövde ve kopan bacak.** Silindir gitti: kalça hizasından kurulan bir gövde
(gövde, kafa, öne uzanan kollar, iki bacak). Bacaklar ayrı vuruş kutusu; tur canının
%30'unu emen bacak **kopar**, zombi ölmeden sürünmeye düşer ve tur hızının %45'iyle gelir
(`zombie.json` v2, `crawl` bölümü). Nişan almanın ikinci ödülü: kafa bitirir, bacak
yavaşlatır — üçüncü bir zombi tipi yazmadan sürüye ritim farkı girer.

Gövde çarpıştırıcısı 1.8 m'den 0.78 m'ye indirildi: tek büyük kapsül ışının önünde
duruyordu ve içine konan her uzuv kutusu **hiçbir zaman vurulamazdı**.

**7. Ses geldi.** Bkz. ADR-0006. Bulgu tek cümleydi: *"arkamdan gelen zombiyi hiç
duymayınca heyecanını test edemiyorum."* Yeni `Bunker.Audio` assembly'si, çalışma anında
sentezlenen 23 ses, hiçbir varlık dosyası yok. Zombi homurtusu, saldırı hazırlığı, ölüm,
bacak kırılması ve barikat 3B çalar — yön ve mesafe taşır.

**8. Oyuncu kamerasına `AudioListener`.** Kökte olsaydı 3B sesin yönü oyuncu döndüğünde
değişmezdi — yani ses hiçbir şey söylemezdi.

**Kanıt durumu:** 1, 2, 3, 8 doğrulanmış hatalar. 4, 5, 6 **hipotez** — tek run, tek
oyuncu. Oynanarak kalibre edilecek.

---

## 2026-09-05 — Ucuncu oyun testi: on bulgu kapatildi

**1. Tezgah ve mermi panosu gorunmuyordu.** Isaretler bos `GameObject`'ti: levhalari
yalnizca `GreyboxLook` uretiyordu ve harita yeniden uretilince kayboluyordu; ustelik
levhanin ince yuzu duvarin icine bakiyordu. Levha artik `BlockoutGenerator`'in isi
(isaret odaya donuk uretiliyor) ve `GreyboxLook` levhayi haritanin merkezine cevirip
rengini veriyor. BUG-004'un ("kapi diye bir nesne yoktu") ucuncu tekrari.

**2. Yenileme ayni karti geri getirebiliyordu.** `CardPool.Draw` yenilenen yuvayi
disarida BIRAKMIYORDU. Harcanan bir hakkin sonucu gorunur sekilde degismeli.

**3. Yenilemenin fiyati yaziyordu ama alinmiyordu.** `CardDraftController` icinde bir
TODO duruyordu: puanli yenileme fiilen bedavaydi. Fiyat artik
`economy.json v4 -> prices.cardRerollBase + cardRerollAddPerRound * (tur-1)` ve
dugmenin uzerinde YAZIYOR; puan yetmiyorsa dugme kapali.

**4. Alinan kart bir daha cikmiyordu.** 21 kartlik havuz yirmi turda tukeniyor ve draft
bos aciliyordu. Artik kartlarin cogu **tekrar cikabilir ve etkileri toplanir**; yalnizca
`unique: true` olanlar (kafa carpani, tamir puani, dolum, yenilenme gecikmesi) bir kez
alinir. Havuz ayni gun 21 -> 31 karta cikarildi.

**5. Isabet parlamasi hep govdedeydi.** Sendeleme beyazi `bodyRenderer`'a yaziliyordu:
vurus kutulari vardi ama oyuncu NEREYE vurdugunu goremiyordu. Parlama artik vurulan
bolgenin kendi gorseline iniyor (120 ms, `MaterialPropertyBlock`).

**6. Bicak nereye bakarsan baksin govdeye iniyordu.** Koni taramasi her zaman merkeze
EN YAKIN kutuyu seciyordu. Artik once nisan yolu (kure taramasi) bakiliyor, koni yalnizca
yedek.

**7. Kosu (sprint) eklendi.** Shift, `player.json v2 -> sprint` (4,5 sn, 1,5x, saniyede
0,5 sn dolum). Sinirsiz kosu haritayi kucultur ve zombi hiz kademelerini anlamsiz kilardi.
HUD'a ince bir gosterge geldi: gostergesiz sinirli bir kaynak, tam kacarken suprize doner.

**8. Tur sonu kismi yenilenme.** `rounds.json v2 -> roundEnd`: yedek mermi tavaninin
%40'i ve tam barikatin %40'i geri gelir. Tam yenilenme oyunu kolaylastiriyordu (2026-09-04
kaldirilmisti), hic yenilenmemesi ise molanin tamamini tamire baglayip tezgaha gitmeyi
imkansiz kiliyordu.

**9. Anlik canli zombi tavani.** `rounds.json v2 -> count.aliveCap*`: sahada ayni anda en
fazla `5 + 1*(tur-1)` zombi durur (her zaman `maxConcurrent`=40 ile sinirli). Tavan
doluyken dogum durur, biri olunce yenisi gelir. Hem oynanabilirlik (barikat tamiri icin
bosluk) hem performans.

**Kanit durumu:** 1, 2, 3, 5, 6 dogrulanmis hatalar. 4, 7, 8, 9 tasarim kararlari ve
**hipotez** — oynanarak kalibre edilecek. Ozellikle %40 mermi yenilenmesi, mermi
ekonomisini (curves.md) belirgin gevsetebilir; ilk olculecek sey bu.

---

## 2026-09-05 (aksam) — Menu, olum hatasi ve surunun cephesi

**1. "14. turda hasar yemeden game over oldum" — bulundu.** Hata olumde degil
TEZGAHTAYDI: menu acikken oyuncunun girdisi kesiliyor ama **dunya donmeye devam
ediyordu**. Tur ortasinda tezgahi acmak, surunun ortasinda heykel olmak demekti; dort
vurus iki saniyede iner ve oyuncu menuye bakiyordur. Telemetri de soyluyor: son iki
olum de y=4.58'de, yani tezgahin bulundugu UST KATTA.

Iki savunma birden konuldu:
- **Tezgah yalnizca molada acilir** (`ShopStation.CanOpen`), tur baslayinca kapanir.
  Tezgah zaten tur arasinin harcamasi; tur ortasinda acilabilmesi bir ozellik degil,
  kimsenin karar vermedigi bir yan etkiydi.
- **Hasar alinca acik menu kapanir** (`PlayerHealth`). Emniyet kemeri: kuralin
  atlandigi bir yol kalirsa bedeli bir run.

**2. Zombiler tek sira diziliyordu.** Sebep bir hata degil NavMesh'in dogasi: ayni
hedefe giden butun ajanlar ayni en kisa yolu bulur, ayni koseyi ayni noktadan doner ve
birbirini itmemek icin sıraya girer. Uc mudahale birden (`SwarmFormation`, saf C#):
her zombiye **yanal bir serit** (hedefe yaklasinca sonen), **hiza kucuk bir sapma**
(konvoyu zamanla bozar) ve **zombi basina farkli NavMesh kacinma onceligi** (esit
oncelikli ajanlar kuyruga girer). Sayilar `zombie.json v3 -> swarm`.

**3. Ana menu geldi** (M-04). Oyun artik acilir acilmaz oturuma girmiyor:
`Scenes/Menu/Menu.unity` (arac uretiyor: `Bunker/Menu/Ana Menuyu Kur`), derleme sahne
listesinde ILK sirada. Tek kisi oyna / oda ac / odaya katil (adresle) / ayarlar / cikis.
ESC ile oyun ici duraklatma: **solo'da dunya durur, ortakli oturumda durmaz** ve ekran
bunu soyler - aksi halde 1. maddedeki hatanin aynisi menuyle tekrarlanirdi.

**4. Ayarlar oyuncunun, config oyunun.** Fare hassasiyeti, ses ve ters dikey bakis
`PlayerPrefs`'te (`GameSettings` + `ISettingsStore`); `config/` altina girmediler cunku
orasi **dengedir ve herkeste aynidir**. `Bunker.Systems` motor referansi tasimadigi icin
depo ters cevrildi: kural Systems'te, `PlayerPrefs` Gameplay'de.

**5. Steam daveti: ADR-0007 yazildi, uygulanmadi.** Iki adim proje disinda (paket
indirme ve App ID). Menu ve ag katmani o gun **degismeyecek** sekilde kuruldu:
`SessionSignals` yalnizca niyet tasir, davet de bir "katil" niyetidir. Su an calisan
yol: **adresle katil** (ayni agda port yonlendirmesi gerekmez).

**Kanit durumu:** 1 ve 2 dogrulanmis bulgular. 3, 4 uygulandi ve testleri var (294
EditMode testi gecti); menunun kendisi **oynanarak** dogrulanmali - bir menuyu test
suiti dogrulayamaz.

---

## 2026-09-05 (gece) — Hasar gorunurlugu, derin ayarlar, eksik kartlar, gercek lobi

**1. "Tek yiyorum sanirim" — artik tahmin degil, ekranda yaziyor.** Zombi hasari
config'de SABIT 30 ve turla artmiyor; yani tek vurusta olum yok, dort vurus var ve
hepsi bir buçuk saniyeye sigiyor. Sorun hasarda degil GORUNURLUKTE'ydi.
`CombatFeedback` + `DamageNumbersHud`: vurdugun hasar zombinin ustunde (kafa vurusu
sari ve unlemli, olduren vurus buyuk), yedigin hasar nisangahin ustunde ve **geldigi
yon** ekranin ortasindan disa bir cizgi olarak. Yon bilgisi `DamageInfo`'ya iki duz
sayi (SourceX/SourceZ) olarak eklendi - `Vector3` degil, cunku o katman motor tipi
tanimaz ve tanirsa butun EditMode testleri Unity'ye baglanir.

**2. Ayarlar dort sekme oldu ve hepsi calisiyor.** Goruntu (cozunurluk, ekran bicimi,
VSync, kare siniri, kalite, FOV), Ses (ana, efekt, arka planda sustur), Kontrol
(hassasiyet, ters bakis, kosu tusu hold/toggle), Arayuz (hasar sayilari, hasar yonu,
isabet isareti, nisangah). `SettingsApplier` tek uygulayici: uygulamayi ekranlara
dagitmak, birinin digerini unutmasiyla biterdi.

**Muzik ve ekran sarsintisi ayari EKLENMEDI** - o sistemler yok. Hicbir sey yapmayan
bir ayar, oyuncuya oyunun bozuk oldugunu ogretir. Tus degistirme de yok: girdi haritasi
tasinmadan yapilamaz ve yalan bir ekran olurdu.

**3. Kart havuzu 31 -> 41 ve EKSIK MEKANIKLER YAZILDI.**
- **Yikim etiketi artik bos degil**: olen zombi patliyor (`ZombieAgent.TryExplode`),
  zincirleme patlama serbest - kartin vaat ettigi an o. Yaricap `zombie.json v4 ->
  cards.explosionRadiusMeters` (3.5 m), gucu kartin degerinde.
- **Delici mermi**: `PlayerWeapon.FirePenetrating` - isin siralanmis coklu isabete
  cevrilir. Duvar mermiyi DURDURUR (yoksa harita anlamsizlasirdi), ayni zombiye iki
  kez vurmaz.
- **Kan Icici** (oldurunce iyilesme) ve **Toplayici** (oldurunce mermi): ikisi de
  `PlayerScore.ApplyKillRewards`'ta, cunku "oldurme" olayinin tek sahibi orasi -
  silaha ve bicaga ayri ayri eklemek birinin unutulmasi demekti.
- **Don**: yavaslatma tabani 0.25 -> 0.15. Eski taban, kartlarin toplami ne olursa
  olsun zombiyi yuruyebilir birakiyordu ve ust kademe kart hissedilmiyordu.
- Hala BEKLEYEN: itme/savurma, yerden esya, diriltme. Ucu de olmayan sistemlere
  dokunuyor; uydurma kart eklenmedi.

**4. Lobi gercekten yoktu - simdi var.** Sebep gorunmezdi: Mirror'in `onlineScene`
alani doluydu, yani host olur olmaz oyun sahnesine geciliyordu. "Oda ac" demek
DOGRUDAN oyuna dusmek demekti ve lobi ekrani hic gorunmuyordu. Artik `onlineScene`
BOS, `autoCreatePlayer` KAPALI; sahneyi lobi "BASLAT" dedigi an sunucu degistiriyor
(`ServerStartGame`) ve oyuncular oyun sahnesinde `OnServerReady`'de yaratiliyor.
Lobide: 4 kisilik oyuncu listesi (Steam adlariyla, `LobbyController` mesajlariyla),
davet, katilma kodu, BASLAT (yalnizca host), ayril.

**Ayrica Play artik HEP menuden basliyor** (`playModeStartScene`). Menu yazilmisti ama
hangi sahne acikise Play ona basiyordu - genellikle sandbox. Sandbox'ta hizli test icin
`Bunker/Menu/Play'i Menuden Baslat` ile kapatilabiliyor.

**Kanit durumu:** 294 EditMode testi geciyor. 1 ve 4 dogrulanmis eksiklerin
kapatilmasi. 2 ve 3 oynanarak kalibre edilecek - ozellikle patlama gucu (0.35/0.55/0.80)
ve delici merminin gec turlarda ne kadar guclu oldugu HIPOTEZ.

---

## 2026-09-06 — Uc kart hatasi: biri oyuncuyu olduruyordu

Oyun testi uc cumleyle geldi: *"direk oluyorum"*, *"delici mermi delmiyor"*,
*"patlama hissedilmiyor"*. Ucu de kendi yazdigimiz kodun hatasiydi ve ikisi ayni
kok sebebi paylasiyordu.

**1. PATLAMA OYUNCUYU OLDURUYORDU.** `TryExplode` yaricaptaki her `IDamageable`'a
hasar veriyordu - ve oyuncu da bir `IDamageable`. Sarapnel karti alan oyuncu, yanindaki
zombiyi oldurdugu anda olen zombinin canina orantili hasari KENDI yiyordu: tur 5'te
192, tur 10'da 366 hasar. 100 canla bu aninda olum. `explosionSelfDamageFraction01`
ayari yazilmisti ama KULLANILMAMISTI - ayarin var olmasi, uygulandigi anlamina gelmiyor.

Artik patlama yalnizca zombileri vurur; oyuncuya hasar ancak o oran sifirdan buyukse ve
onunla olceklenerek gider. Varsayilan sifir: patlamanin oyuncuyu yakip yakmayacagi bir
denge karari, koda gomulecek bir sey degil.

**2. DELICI MERMI HIC DELMIYORDU** - ve sebebi kartta degil, "ayni yaratiga iki kez
vurma" kuralindaydi. Kural kimligi `transform.root`'tan okuyordu. Zombiler havuzdan
`_ZombieDirector`'un ALTINA doguyor, yani **hepsinin root'u ayni nesne**: mermi ilk
zombiden sonra sahnedeki herkesi "zaten vurdum" diye eliyordu.

**Ders: kimlik sahne hiyerarsisinden turetilemez.** `IDamageable` artik `DamageRoot`
tasiyor - vurus kutusu sahibini soyler, yaratik kendisini. Ayni kural patlamaya da
uygulandi; iki yerde de hiyerarsi sorgusu kalmadi. Hiyerarsiden turetilen kimlik,
hiyerarsi degistigi gun SESSIZCE bozulur ve bu tam olarak oyle bozulmustu.

**3. PATLAMA GORUNMUYORDU.** Hasar veriyordu ama ekranda hicbir sey yoktu, sesi de
zombi olum sesiydi. Gorunmeyen bir etki, oyuncu icin OLMAYAN bir etkidir (PILLAR-04).
Havuzlu bir `ExplosionFlash` (hizla buyuyup sonen kure) ve kendi sesi eklendi.

**4. Artik tahmin yok: olum ekrani OLDURENI yaziyor.** "Tek mi yedim, dort mu" sorusu
iki oturumdur tahmine dayaniyordu. `CombatFeedback.NoteLethalHit` + skor ekraninda
"son vurus: N hasar (tur)". 30 yaziyorsa normal bir zombi vurusu, 366 yaziyorsa
bambaska bir sey - ve hangisi oldugu artik ilk bakista okunuyor.

**Kanit durumu:** 294 EditMode testi geciyor. 1, 2 ve 3 dogrulanmis hatalarin
duzeltilmesi; oynanarak teyit edilecek. Bu uc hatanin ortak dersi kayda deger:
**bir ozelligi "yazmis olmak" ile "calisir gormek" arasindaki mesafe, bu projede
gorunurluk eksikligi yuzunden buyuyor.** Hasar sayilari ve olum sebebi tam da o
mesafeyi kapatmak icin var.

---

## 2026-09-06 (aksam) — Sarapnel, silahlar, boss ve kesin sayilar

**1. Play menuden baslamiyordu - sebep gorunmezdi.** `EditorSceneManager.playModeStartScene`
projenin degil **editor OTURUMUNUN** ayari; bassiz bir toplu calistirmada yazilan deger
gelistiricinin actigi editore hic gecmiyordu. Menu kuruldu, ayar yazildi, oyun yine
sandbox'tan basladi. Artik `PlayModeStartScene` sinifi `[InitializeOnLoad]` ile her
editor acilisinda ayari kuruyor; tercih `EditorPrefs`'te (makineye ozel, dogru yer).

Ayrica menu kamerasi artik **MainCamera etiketli**: oyundan menuye donuldugunde oyuncu
ve kamerasi yok oluyor, etiketsiz bir menu kamerasi `Camera.main`'i null birakir ve ona
guvenen her sey sessizce calismaz.

**2. Patlama SARAPNEL oldu.** v4 bir kure sorgusuydu: yaricaptaki herkese TAM hasar,
duvarin arkasina geciyor ve oyunu kolaylastiriyordu. Gelistirici dogru mekanigi tarif
etti - el bombasi sarapnelleri belli alana firlar, **isabet alan hasar alir**.

Artik patlama `explosionShrapnelCount` (14) parca firlatir; her parca bir ISINDIR,
ilk carptigi seye toplam hasarin 1/N'ini verir ve **duvar onu durdurur**. Bu tek
degisiklik uc sorunu birden cozuyor: gucu dusuruyor (yakindaki zombi birkac parca yer,
hepsini degil), duvar arkasini kapatiyor ve patlamayi KONUMA bagli hale getiriyor.

Parcalar rastgele degil **kureye esit dagitilmis** (Fibonacci): rastgele on dort parca
kumelenir ve ayni mesafedeki iki zombiden biri bes parca yerken digeri hic almayabilir -
oyuncu bunu "patlama bazen calisiyor" diye okur.

**3. Dort silah** (`config/content/weapons.json` + `WeaponCatalogAsset`). Gelistirici:
*"atis hizini test edemiyorum."* Tabanca referans; MP-KISA hizli ve affedici, POMPALI
sekiz sacmayla yakin mesafenin cevabi, TUFEK yavas ve agir. **DPS'leri bilerek
birbirine yakin**: silahi secen sey guc degil RITIM ve MENZIL olmali.

- Her silahin KENDI mermisi var (ortak sayac, pompaliyla tabancanin ayni mermiyi
  paylasmasi demek olurdu).
- 500 RPM ustu silahlar **otomatik** ates eder; yari otomatik his tabancanin karakteri,
  motorun kisiti degil.
- Duvar noktalari artik silah satiyor: ilk alista SILAH, sonrakilerde MERMI. Ucuz duvar
  MP, pahali duvar pompali - fiyat farki silahin yerini de soyluyor.
- Pompalinin sacma dagilimini SUNUCU uretir: istemci gonderseydi hepsini tek noktaya
  toplayan bir istemci pompaliyi keskin nisanci tufegine cevirirdi (netcode.md).

**4. Boss geldi** (`rounds.json v3 -> boss`). Her **besinci** turda, turun ILK zombisi
boss olur: 14 kat can, 2 kat hasar, 1.7 kat buyukluk, **0.75 kat hiz** ve 6 kat puan.

- **Yavas olmasi sart**: cok canli VE hizli bir dusman oyuncuya kacmaktan baska secenek
  birakmaz ve o da isleyen bir plan degildir. Yavas boss, bir KOSU DONGUSU problemi olur.
- **Duzenli aralik, rastgele degil**: oyuncu hazirlanabilmeli. Besinci turun boss turu
  oldugunu bilmek, dorduncu turun molasinda tezgaha gitmeyi bir PLAN yapar.
- **Ayri prefab degil**, turun zombisinin carpanlari: zorluk egrisi tek yerde kaliyor.
- Turun ILK zombisi olmasi bilincli: sonda gelseydi tur boyunca "acaba simdi mi" diye
  oynanirdi; basta gelmesi turun geri kalanini onunla birlikte hayatta kalma problemine
  cevirir.

**5. TAB durum paneli.** Gelistirici: *"mevcut canimin hasarimin net bilgisini
bilmeliyim."* Can (ham sayi / tavan), silahin hasari ve atis hizi TABANIYLA birlikte
("71 (taban 55)"), kafa carpani, dolum, sarjor, delici sayisi, hiz carpani, envanter ve
kart sayisi. Basili tutulur - bir mod degil bir bakis.

Kesin can icin `PlayerHealth` artik ham can ve tavani da senkronluyor: oran tek basina
yetmiyordu, cunku kart alan oyuncunun tavani degisiyor ve "%60 can" her turda baska bir
sayi demek.

**Kanit durumu:** 294 EditMode testi geciyor. 1 ve 2 dogrulanmis hatalarin duzeltilmesi.
3, 4, 5 yeni sistemler ve **hepsi hipotez** - ozellikle silah DPS dengesi ve boss'un 14
kat cani oynanarak kalibre edilecek.

---

## 2026-09-06 (gece) — Log'un ele verdigi iki hata

Oyun testinin Console cikti bir hatanin degil IKI hatanin izini tasiyordu; ikisi de
benim onceki oturumda yazdigim kodda.

**1. LOBI MESAJI SOLO OYUNU KIRIYORDU.** Zincir soyleydi:

```
Unknown message id: 32899  ->  NetworkClient: failed to unpack -> Disconnecting
->  oyuncu hic dogmadi  ->  "There are no audio listeners" x sonsuz
```

Sunucu lobi listesini `SendToAll` ile yayinliyor ve **host'un kendi istemcisi de** o
yayini aliyor. Handler yalnizca uzak istemcilerde kayitliydi (`OnClientConnect` host
icin erken donuyordu), host kendi mesajini tanimadi, Mirror baglantiyi KESTI. Sonuc:
sunucu tur akisini yurutmeye devam ediyor ama oyuncu yok - log'daki bitmeyen "audio
listener" uyarisi da bu, cunku kamera oyuncunun uzerinde.

**Kural cikti:** yayinladigin her mesajin **her alicida** bir karsiligi olmali. Host da
bir alicidir. Bu, "solo ayri bir oyun degildir" ilkesinin (ADR-0004) unutulmus bir
kosesiydi: host'u istemci saymayan her satir, tek kisilik oyunu bozmaya adaydir.

**2. STEAM KAPALIYKEN OYUN BAGLANAMIYORDU.** FizzyFacepunch `Awake`'te "Steam is
probably not running" hatasi basiyor ama **bagli tasima olarak kaliyordu**. Tasimanin
kendisi zaten `Available()` ile "ben calisamam" diyor; kimse sormuyordu.

`EnsureUsableTransport` artik aciliste soruyor ve kullanilamayan tasimadan ayni
nesnedeki calisan bir tasimaya (KCP) **duserek** devam ediyor - ADR-0007'nin acik
sozu buydu: *"Steam calismadan oyun cevrimici test edilemez hale GELMEMELI."*

**Sessizce dusmuyor, soyluyor:** Console'a "Steam kapali, KCP'ye gecildi, davet bu
oturumda yok" yaziyor. Steam'e dustugunu fark etmeden arkadas davet etmeye calismak,
teshis edilmesi en can sikici durumlardan biri olurdu.

**Kanit durumu:** 294 EditMode testi geciyor. Ikisi de dogrulanmis hata; solo oyunun
tekrar oynanabilir oldugu OYNANARAK teyit edilmeli.

---

## 2026-09-06 (gec) — "No cameras rendering": ekrandaki menu bir HAYALETTI

Ekran goruntusu ilk bakista "iki sahne birden yuklu" gibi duruyordu: menu dugmeleri ve
oyun HUD'u ust uste. Menu sahnesi denetlendi - icinde yalnizca uc nesne var, temiz.

**Gercek:** kamera olmadigi icin ekran hic TEMIZLENMIYOR. Menunun goruntusu son cizilen
karenin kalintisi; canli olan tek sey IMGUI cizen oyun HUD'u. Yani ortada iki sahne
degil, **kamerasi olmayan bir sahne** vardi - ve kamera oyuncunun uzerinde oldugu icin
asil sorun yine oyuncunun DOGMAMASIYDI.

**Kok sebep, Mirror'in kendi yorumunda yaziyordu:**

> *"scene change needed? then change scene and spawn afterwards. => BEFORE host client
> connects."*

`ServerChangeScene`'i `OnStartHost` icinden cagiriyordum. `OnStartHost` ise
`FinishStartHost`'un ICINDE, yani host istemcisi **baglandiktan sonra** kosuyor - tam
tersi sira. Hazir olma (ready) akisi yarida kaliyor ve `OnServerReady` hic gelmiyor,
dolayisiyla oyuncu yaratilmiyordu.

Uc noktadan saglamlastirildi:

1. **Sahne degisimi bir kare sonraya birakildi** (`_pendingStart` + `Update`): host
   tamamen ayaga kalkip istemci baglanana kadar beklenir.
2. **Oyuncu yaratma iki yere baglandi**: `OnServerReady` VE `OnServerSceneChanged`.
   Hazir olma bildirimi sahne degisiminden once de sonra da gelebilir; tek birine
   baglamak "siralamaya gore bazen calisan" bir doğum demekti.
3. **Karar sahne adi karsilastirmasindan cikarildi**: `_gameStarted` bayragi. Sahne
   yolunu string olarak karsilastirmak, bir yazim hatasinda sessizce "oyuncu yok"
   uretirdi - ve hata yine kamera hatasi gibi gorunurdu.

**Ders:** "no cameras rendering" bir kamera hatasi degil, bir **oyuncu doğumu** hatasinin
gorunusuydu. Bir hatanin gorundugu yer, oldugu yer degildir.

---

## 2026-09-06 (gece yarisi) — Uzaktan yenen vurus ve silahlarin siluetleri

**1. "MESAFE VARKEN HIT YIYORUM" — bulundu, gercek bir hataydi.** Beyin mesafeyi
DUSUNME ADIMINDA olcuyordu (saniyede 8 kez = 125 ms'de bir) ve vurus o eski olcume gore
iniyordu. Kosan oyuncu 125 ms'de 0,94 m gidiyor; menzil toleransiyla (0,6 m) birlikte bu
**uc metre uzaktan yenen bir vurus** demekti.

Iki duzeltme:
- **Vurus inerken mesafe BIR KEZ DAHA olculuyor** (`LandAttack`). Beyindeki kontrol
  NIYETI belirler (vurusa baslamaya deger mi), buradaki kontrol SONUCU belirler (hala
  menzilde mi). Telegrafin bedeli budur: oyuncu hazirligi gorup cekilirse vurus
  ISKALAMALI (ai-code.md).
- **Menzil govde olcegiyle buyuyor.** Boss 1,7 kat buyuktu ama menzili normal zombiyle
  ayniydi; govdesi cok uzaktayken merkezler arasi mesafe hala menzil icinde kaliyordu.
  Buyuk yaratigin uzun kolu olmasi OKUNABILIR, "gorunmeyen bir menzil" degil.

**2. Silahlarin siluetleri ayrildi** (`WeaponShape`). Gelistirici: *"taramalida tabanca
gibi gozukmesin."* Dort silah ayni modeli tasiyorsa oyuncu elindekini yalnizca yazidan
bilir - ve savasin ortasinda kimse yazi okumaz. Tabanca kisa ve yalin; taramalida
**sarkan sarjor** ve katlanir dipcik; pompalida **kalin namlu ve pompa kolu**; tufekte
**en uzun namlu ve durbun**.

**Tek uretici, iki tuketici:** ayni geometri hem elde hem duvarda. Iki ayri yerde
cizilseydi duvardaki pompali ile eldeki pompali zamanla birbirine benzemez olurdu ve
oyuncu duvarda gordugu seyi eline aldiginda tanimazdi. Namlu ucu de sekilden geliyor -
sabit bir konum, pompalida alevin govde icinde patlamasi demekti.

**3. Silahlar duvarda MODELIYLE duruyor** ve ucu de bir yere kondu: MP-KISA baslangic
odasinda (ucuz duvar), POMPALI kapinin arkasinda, **TUFEK ust katta** (yeni ucuncu
nokta). Yeri olmayan silah, oyuncunun varligindan haberi olmayan silahtir - pompaliyi
bulamamasinin sebebi buydu, tufegin ise hic yeri yoktu.

Duvar artik sattigi silahi **oyuncudan bagimsiz** biliyor: onceki surum tanimi alicinin
katalogundan okuyordu, yani kimse yaklasmadan once ne sattigini bilmiyor ve modelini
cizemiyordu.

**4. Olum ekrani bir cikis kapisi oldu.** Yalnizca "R" yazan bir ekran oyuncuyu ya
yeniden baslamaya ya da Alt+F4'e zorluyordu. Olum, oturumu bitirmenin en dogal ani;
menuye donus orada olmazsa hicbir yerde yok demektir. Uc dugme: yeniden basla, ana menu,
cikis.

**Kanit durumu:** 294 EditMode testi geciyor. 1 dogrulanmis bir hatanin duzeltilmesi;
2, 3, 4 oynanarak dogrulanacak. Ozellikle bakilacak: bossun menzili artik adil mi
(1,7 kat erisim FAZLA gelebilir - o zaman olcek carpanini erisimden ayirmak gerekir).
