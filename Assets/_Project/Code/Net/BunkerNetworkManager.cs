using Bunker.Systems.Net;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;

namespace Bunker.Net
{
    /// <summary>
    /// Mirror NetworkManager türevi. Tek görevi, oyunun her modda aynı kod yolundan
    /// başlamasını sağlamak.
    ///
    /// ADR-0004 ve M-01: <b>solo ayrı bir oyun değildir.</b> Solo, uzak istemci sayısı
    /// sıfır olan bir host oturumudur. Bu sınıf o kararın tek uygulama noktası —
    /// başka hiçbir yerde "çevrimdışı mod" dalı olmamalı.
    ///
    /// <para><b>M-04: menü geldi.</b> Artık oyun kendiliğinden bir oturum açmaz; menü
    /// niyet bildirir (<see cref="SessionSignals"/>), burası uygular. <c>autoStartSolo</c>
    /// yalnızca <b>sandbox sahnesine doğrudan Play basıldığında</b> devrede — geliştirme
    /// kolaylığı olarak duruyor ve menü sahnesinde kapalı.</para>
    ///
    /// <para><b>Steam daveti buraya takılır</b> (ADR-0007): davet de bir "katıl"
    /// niyetidir, yalnızca adresi Steam lobisinden gelir. Menü ve bu sınıf o gün
    /// değişmez; değişen taşıma (transport) bileşenidir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Bunker Network Manager")]
    public sealed class BunkerNetworkManager : NetworkManager
    {
        [Header("Solo")]
        [Tooltip("Play'e basildiginda otomatik olarak host modunda baslar. YALNIZCA " +
                 "sandbox sahnesinde acik olmali - menu sahnesinde kapali, yoksa menu " +
                 "acilir acilmaz oyuna girer.")]
        [SerializeField] private bool autoStartSolo = true;

        [Header("Katilma")]
        [Tooltip("Son kullanilan adres. Menu buraya yazar; oyuncu bir sonraki acilista " +
                 "yeniden yazmak zorunda kalmasin diye saklanir.")]
        [SerializeField] private string lastJoinAddress = "localhost";

        [Header("Sahneler")]
        [Tooltip("Oyun sahnesi. Lobiden 'BASLAT'a basildiginda SUNUCU buraya gecer ve " +
                 "istemciler onu izler. Mirror'in onlineScene alani BOS birakilir: " +
                 "dolu olsaydi oda acar acmaz oyuna dusulurdu ve lobi hic gorunmezdi.")]
        [SerializeField] private string gameScene = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

        private const string AddressKey = "bunker.session.lastAddress";

        private LobbyController _lobby;

        // Tek kisi oynarken lobi atlanir; oda acilirken atlanmaz.
        private bool _startGameImmediately;

        // Host tamamen ayaga kalkana kadar bekleyen "oyunu baslat" istegi.
        private bool _pendingStart;

        // Oyun basladi mi. Oyuncu yaratma karari buna bakar - sahne adi
        // karsilastirmasina degil (bir yazim hatasi sessizce "oyuncu yok" olurdu).
        private bool _gameStarted;

        /// <summary>Şu an bir oturum açık mı (host, sunucu ya da istemci olarak).</summary>
        public static bool SessionActive => NetworkServer.active || NetworkClient.active;

        /// <summary>Oturum açık ve uzak istemci yok — yani fiilen tek kişilik oyun.</summary>
        public static bool IsSolo => NetworkServer.active && NetworkServer.connections.Count <= 1;

        /// <summary>Menünün gösterdiği son adres.</summary>
        public static string LastJoinAddress { get; private set; } = "localhost";

        public override void Awake()
        {
            base.Awake();

            // Menu sahnesinden gelen manager sahneler arasi yasar (Mirror
            // DontDestroyOnLoad). Oyun sahnesindeki ikinci manager kendini yok eder -
            // bu Mirror'in singleton davranisi ve bilerek kullaniliyor: sandbox
            // sahnesine dogrudan Play basmak calismaya devam etsin diye orada da bir
            // manager duruyor.
            LastJoinAddress = PlayerPrefs.GetString(AddressKey, lastJoinAddress);
            _lobby = GetComponent<LobbyController>();

            EnsureUsableTransport();
        }

        public override void Start()
        {
            base.Start();

            // Menuden gelindiginde oyun sahnesindeki bu manager Mirror tarafindan
            // yok edilir (singleton). Yok edilmis bir manager'in Start'i yine de
            // kosarsa, menuden acilmis oturumun ustune IKINCI bir solo oturum
            // baslatmaya calisirdi.
            if (singleton != this) return;

            if (autoStartSolo && !SessionActive)
            {
                StartSolo();
            }
        }

        /// <summary>
        /// Kullanılabilir bir taşımaya düşer. ADR-0007'nin sözü: <b>Steam kapalıyken
        /// oyun test edilebilmeli.</b>
        ///
        /// <para><b>Neden gerekiyordu</b> (2026-09-06 log'u): Steam kapalıyken
        /// FizzyFacepunch <c>Awake</c>'te bir hata basıyor ama <b>bağlı taşıma olarak
        /// kalıyor</b> — yani oyun Steam olmadan hiç bağlanamıyordu. Taşımanın kendisi
        /// zaten <c>Available()</c> ile "ben çalışamam" diyor; kimse sormuyordu.</para>
        ///
        /// <para><b>Sessizce düşmez, söyler.</b> Steam'e düşmüş bir oturumu fark
        /// etmeden arkadaş davet etmeye çalışmak, teşhis edilmesi en can sıkıcı
        /// durumlardan biri olurdu.</para>
        /// </summary>
        private void EnsureUsableTransport()
        {
            if (transport != null && transport.Available()) return;

            Transport[] candidates = GetComponents<Transport>();

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == transport) continue;
                if (!candidates[i].Available()) continue;

                Debug.LogWarning(
                    $"[Bunker] '{(transport != null ? transport.GetType().Name : "yok")}' " +
                    $"kullanilamiyor (Steam kapali olabilir). Tasima " +
                    $"'{candidates[i].GetType().Name}' olarak degistirildi.\n" +
                    "Steam daveti bu oturumda YOK; adresle katilma calisiyor (ADR-0007).");

                transport = candidates[i];
                Transport.active = candidates[i];
                return;
            }

            if (transport == null)
            {
                Debug.LogError("[Bunker] Hicbir tasima bileseni yok. " +
                               "'Bunker/Menu/Ana Menuyu Kur' calistir.", this);
            }
        }

        /// <summary>
        /// Menu niyetlerine abone olur.
        ///
        /// <para>Mirror'in NetworkManager'i OnEnable/OnDisable tanimlamaz; bunlar
        /// Unity mesajlari olarak dogrudan cagrilir - base cagrisi YOK.</para>
        /// </summary>
        private void OnEnable()
        {
            SessionSignals.SoloRequested += OnSoloRequested;
            SessionSignals.HostRequested += OnHostRequested;
            SessionSignals.JoinRequested += OnJoinRequested;
            SessionSignals.LeaveRequested += OnLeaveRequested;
            SessionSignals.QuitRequested += OnQuitRequested;
        }

        private void OnDisable()
        {
            SessionSignals.SoloRequested -= OnSoloRequested;
            SessionSignals.HostRequested -= OnHostRequested;
            SessionSignals.JoinRequested -= OnJoinRequested;
            SessionSignals.LeaveRequested -= OnLeaveRequested;
            SessionSignals.QuitRequested -= OnQuitRequested;
        }

        // ---------------------------------------------------------------- niyetler

        /// <summary>
        /// Tek kişi: <b>lobi atlanır</b>, doğrudan oyuna girilir.
        ///
        /// <para>Kimseyi beklemeyen bir oyuncuyu boş bir lobide "BASLAT"a basmaya
        /// zorlamak, her tek kişilik oturuma anlamsız bir tıklama eklerdi.</para>
        /// </summary>
        private void OnSoloRequested()
        {
            if (SessionActive) return;

            _startGameImmediately = true;
            SessionSignals.SetStatus(SessionStatus.Connecting);
            StartSolo();
        }

        /// <summary>
        /// Oda <b>lobide</b> açılır: oyunun ne zaman başlayacağına host karar verir.
        ///
        /// <para>Önceki hâlde oda açmak doğrudan oyunu başlatıyordu ve davet
        /// göndermeye zaman kalmıyordu — arkadaş katıldığında host üçüncü turdaydı.</para>
        /// </summary>
        private void OnHostRequested()
        {
            if (SessionActive) return;

            _startGameImmediately = false;
            SessionSignals.SetStatus(SessionStatus.Connecting);
            Debug.Log("[Bunker] Oda aciliyor (host) - lobi.");
            StartHost();
        }

        private void OnJoinRequested(string address)
        {
            if (SessionActive) return;

            if (string.IsNullOrWhiteSpace(address))
            {
                // Sessizce hicbir sey yapmamak, oyuncuya dugmenin bozuk oldugunu
                // soyler (ui-code.md: her ekranin hata durumu olmali).
                SessionSignals.SetStatus(SessionStatus.Failed, "Adres bos.");
                return;
            }

            networkAddress = address.Trim();
            LastJoinAddress = networkAddress;
            PlayerPrefs.SetString(AddressKey, networkAddress);
            PlayerPrefs.Save();

            SessionSignals.SetStatus(SessionStatus.Connecting);
            Debug.Log($"[Bunker] Katilma denemesi: {networkAddress}");
            StartClient();
        }

        private void OnLeaveRequested()
        {
            // Oturumdan ayrilirken run durumu SIFIRLANIR: ayni oturumda ikinci kez
            // oynayan oyuncu, oncekinin bitmis run'iyla baslamamali (M1-11 AC-6'nin
            // oturum seviyesindeki karsiligi).
            RunSignals.RequestRestart();

            SessionSignals.SetLobbyState(false, false, null);

            // Menuye donuldugunde oyun BITTI: bir sonraki oturum lobide baslar ve
            // oyuncu yaratma karari yeniden verilir.
            _gameStarted = false;
            _pendingStart = false;

            if (NetworkServer.active && NetworkClient.isConnected) StopHost();
            else if (NetworkClient.isConnected) StopClient();
            else if (NetworkServer.active) StopServer();

            SessionSignals.SetStatus(SessionStatus.Offline);
        }

        private static void OnQuitRequested()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Tek kişilik oturum açar. Teknik olarak bu bir host oturumudur; fark yalnızca
        /// kimsenin katılmamış olmasıdır.
        /// </summary>
        public void StartSolo()
        {
            if (SessionActive)
            {
                Debug.LogWarning("[Bunker] Oturum zaten açık, StartSolo yok sayıldı.");
                return;
            }

            Debug.Log("[Bunker] Solo oturum başlıyor (host, uzak istemci yok).");
            StartHost();
        }

        // ---------------------------------------------------------------- durum

        public override void OnStartHost()
        {
            base.OnStartHost();

            _lobby = GetComponent<LobbyController>();
            _lobby?.ServerStarted();

            SessionSignals.SetStatus(SessionStatus.InSession);

            if (_startGameImmediately)
            {
                // SAHNE DEGISIMI BIR KARE SONRAYA BIRAKILIR (2026-09-06 hatasi).
                //
                // Mirror sahne degisimini host istemcisi baglanmadan ONCE bekler
                // (StartHost'un yorumu bunu acikca yaziyor: "load on server first").
                // OnStartHost ise FinishStartHost'un ICINDE, yani istemci baglandiktan
                // SONRA kosuyor. Oradan sahne degistirmek, hazir olma (ready) akisini
                // yarida birakiyor ve oyuncu HIC YARATILMIYORDU - ekranda kamera yok,
                // "no cameras rendering", menunun donmus karesi ustte kaliyordu.
                _pendingStart = true;
                return;
            }

            // Lobide kaliyoruz: sahne degismez, menu "lobi" sayfasina gecer.
            SessionSignals.SetLobbyState(true, isHost: true, null);
            _lobby?.ServerConnectionChanged(0, true);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            SessionSignals.SetStatus(SessionStatus.InSession);

            _lobby = GetComponent<LobbyController>();

            // HOST ICIN DE cagrilir (2026-09-06 hatasi): sunucunun yayinladigi lobi
            // listesini host'un kendi istemcisi de aliyor ve handler kayitli degilse
            // Mirror baglantiyi KESIYOR. Tek kisilik oyun bu yuzden oyuncusuz
            // kaliyordu.
            _lobby?.ClientConnected();

            if (NetworkServer.active) return;   // host kendi istemcisi

            // Katilan taraf once LOBIYE duser. Oyun basladiginda sunucu sahneyi
            // degistirir ve istemci onu izler.
            SessionSignals.SetLobbyState(true, isHost: false, null);
        }

        /// <summary>
        /// Oyunu başlatır: <b>sunucu sahneyi değiştirir</b>, istemciler onu izler.
        ///
        /// <para>Tek geçiş sahibi kuralı (systems-code.md): her istemcinin kendi
        /// sahnesini yüklemesi, birinin diğerinden önce başlamasına ve ilk turun bir
        /// kısmını kaçırmasına yol açardı.</para>
        /// </summary>
        public void ServerStartGame()
        {
            if (!NetworkServer.active) return;

            _pendingStart = false;
            _gameStarted = true;

            SessionSignals.SetLobbyState(false, true, SessionSignals.LobbyPlayers);
            ServerChangeScene(gameScene);
        }

        /// <summary>
        /// Bekleyen "oyunu başlat" isteğini <b>host tamamen ayağa kalktıktan sonra</b>
        /// yürütür.
        ///
        /// <para><c>NetworkClient.isConnected</c> beklenir: Mirror'ın host istemcisi
        /// bağlanmadan sahne değiştirmek, hazır olma akışını yarıda bırakıyor ve oyuncu
        /// hiç yaratılmıyordu.</para>
        /// </summary>
        public override void Update()
        {
            base.Update();

            if (!_pendingStart) return;
            if (!NetworkServer.active || !NetworkClient.isConnected) return;

            ServerStartGame();
        }

        /// <summary>
        /// Oyun sahnesine geçildi: oyuncular <b>şimdi</b> yaratılır.
        ///
        /// <para><c>autoCreatePlayer</c> kapalı, çünkü lobideyken oyuncu yaratmak
        /// menü sahnesinin ortasına birinci şahıs karakterler koymak olurdu.</para>
        /// </summary>
        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            base.OnServerReady(conn);
            TrySpawnPlayer(conn);
        }

        /// <summary>
        /// Sunucu oyun sahnesine geçti: <b>o sırada hazır olan herkese</b> karakter ver.
        ///
        /// <para><b>Neden iki yerde birden</b> (<see cref="OnServerReady"/> ve burası):
        /// hazır olma bildirimi sahne değişiminden önce de sonra da gelebilir. Yalnızca
        /// birine bağlamak, sıralamaya göre bazen çalışan bir oyuncu doğumu demek —
        /// ve "bazen çalışan" bir doğum, teşhisi en pahalı hata türü.</para>
        /// </summary>
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.isReady) TrySpawnPlayer(conn);
            }
        }

        /// <summary>
        /// Karakteri yaratır — <b>oyun başladıysa ve bu bağlantının karakteri yoksa</b>.
        ///
        /// <para><c>autoCreatePlayer</c> kapalı, çünkü lobideyken oyuncu yaratmak menü
        /// sahnesinin ortasına birinci şahıs karakterler koymak olurdu. Ne zaman
        /// yaratılacağına <b>oyunun başlamış olması</b> karar veriyor — sahne adı
        /// karşılaştırması değil: sahne yolu bir yazım hatasıyla sessizce eşleşmez
        /// olurdu ve hata yine "oyuncu yok" diye görünürdü.</para>
        /// </summary>
        private void TrySpawnPlayer(NetworkConnectionToClient conn)
        {
            if (!_gameStarted || conn == null) return;
            if (conn.identity != null) return;   // zaten bir karakteri var

            OnServerAddPlayer(conn);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            // Host kendi istemcisini de kapatir; oturum kapanirken "baglanti koptu"
            // demek yanlis olurdu.
            if (NetworkServer.active) return;

            SessionSignals.SetStatus(
                SessionSignals.Status == SessionStatus.Connecting
                    ? SessionStatus.Failed
                    : SessionStatus.Offline,
                SessionSignals.Status == SessionStatus.Connecting
                    ? $"'{networkAddress}' adresine baglanilamadi. Adresi ve odanin acik " +
                      "oldugunu kontrol et."
                    : "Baglanti koptu.");
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            SessionSignals.SetStatus(SessionStatus.Offline);
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[Bunker] İstemci bağlandı: {conn.connectionId} " +
                      $"(toplam {NetworkServer.connections.Count})");

            _lobby?.ServerConnectionChanged(conn.connectionId, true);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Bunker] İstemci ayrıldı: {conn.connectionId}");
            base.OnServerDisconnect(conn);

            _lobby?.ServerConnectionChanged(conn.connectionId, false);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _lobby?.ServerStopped();
        }
    }
}
