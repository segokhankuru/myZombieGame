using System;
using System.Collections.Generic;

namespace Bunker.Systems.Net
{
    /// <summary>Bir oturumun görünen durumu. Menü bunu okur.</summary>
    public enum SessionStatus
    {
        /// <summary>Menüdeyiz, oturum yok.</summary>
        Offline,
        /// <summary>Bağlanılıyor ya da sunucu ayağa kalkıyor.</summary>
        Connecting,
        /// <summary>Oyun içindeyiz.</summary>
        InSession,
        /// <summary>Son deneme başarısız. <see cref="SessionSignals.LastError"/> sebebi taşır.</summary>
        Failed
    }

    /// <summary>
    /// Menü ile ağ katmanı arasındaki <b>tek</b> kapı. M-04.
    ///
    /// <para><b>Neden olay, doğrudan çağrı değil:</b> menü <c>Bunker.UI</c>'de,
    /// <c>NetworkManager</c> <c>Bunker.Net</c>'te. Arayüzün ağ tiplerini görmesi,
    /// "hiçbir şey UI'ye bağımlı olamaz" kuralının tersini kurardı ve menüyü Mirror'a
    /// çivilerdi (netcode.md). Menü <b>niyet</b> bildirir — "oda aç", "katıl", "ayrıl" —
    /// ne olacağına ağ katmanı karar verir.</para>
    ///
    /// <para><b>Steam daveti geldiğinde burası değişmez:</b> davet de bir
    /// <see cref="RequestJoin"/>'dir, adresi Steam'den gelir. Menünün Steam'i bilmesi
    /// gerekmez (ADR-0007).</para>
    ///
    /// <para><b>Statik olmanın iki kuralı burada da geçerli</b> (RunSignals ile aynı):
    /// her abone <c>OnDisable</c>'da bırakır, <see cref="Clear"/> yalnızca açılışta.</para>
    /// </summary>
    public static class SessionSignals
    {
        /// <summary>Tek kişilik oturum istendi (teknik olarak uzak istemcisi olmayan host).</summary>
        public static event Action SoloRequested;

        /// <summary>Arkadaşlara açık oda istendi.</summary>
        public static event Action HostRequested;

        /// <summary>Bir odaya katılma istendi. Metin bir adres ya da davet kimliğidir.</summary>
        public static event Action<string> JoinRequested;

        /// <summary>Oturumdan ayrılma istendi (ana menüye dönüş).</summary>
        public static event Action LeaveRequested;

        /// <summary>Oyundan çıkılması istendi.</summary>
        public static event Action QuitRequested;

        /// <summary>
        /// Steam davet penceresi istendi.
        ///
        /// <para><b>Arayüz Steam'i bilmez</b> (ADR-0007): "davet et" bir niyettir;
        /// Steam kurulu değilse <see cref="CanInvite"/> kapalıdır ve düğme hiç
        /// çizilmez. Çalışmayan bir düğme, olmayan bir özelliği varmış gibi
        /// gösterir.</para>
        /// </summary>
        public static event Action InviteRequested;

        /// <summary>
        /// Davet edilebilir mi. <b>Yalnızca <c>Bunker.Net</c> yazar</b>: Steam
        /// başlatıldı, bir lobi açık ve bu makine host.
        /// </summary>
        public static bool CanInvite { get; private set; }

        /// <summary>
        /// Arkadaşa elle verilebilecek katılma kodu (Steam lobi kimliği).
        ///
        /// <para><b>Davetin yanında BU DA duruyor</b>, çünkü arkadaş listesinden davet
        /// yalnızca oyun Steam üzerinden başlatıldığında çalışır; kod yapıştırmak her
        /// koşulda çalışır. Tek yola bağlamak, ilk arkadaş testini Steam'in kurulum
        /// ayrıntısına rehin verirdi.</para>
        /// </summary>
        public static string JoinCode { get; private set; } = string.Empty;

        public static void RequestInvite() => InviteRequested?.Invoke();

        /// <summary>
        /// Şu an hangi taşımayla bağlanılacağı, oyuncuya gösterilecek hâliyle
        /// ("Steam" / "Yerel ag (KCP)"). 2026-09-09.
        ///
        /// <para><b>Neden ekranda:</b> taşımanın Steam'den KCP'ye düşmesi bugüne kadar
        /// yalnızca <c>Player.log</c>'a yazılıyordu. İki makineli bir testte kimse log
        /// okumaz; oyuncunun gördüğü şey "davet gitmedi, kod çalışmadı" oluyor ve teşhis
        /// oturumun tamamını yiyor. Bir satır yazı, bir saatlik aramanın yerine
        /// geçiyor.</para>
        /// </summary>
        public static string TransportLabel { get; private set; } = string.Empty;

        /// <summary>Ağ katmanı yazar, menü okur.</summary>
        public static void SetTransportLabel(string label)
        {
            if (TransportLabel == label) return;

            TransportLabel = label ?? string.Empty;
            LobbyChanged?.Invoke();
        }

        /// <summary>Davet durumunu bildirir. <b>Yalnızca <c>Bunker.Net</c> çağırır.</b></summary>
        public static void SetInviteState(bool canInvite, string joinCode)
        {
            CanInvite = canInvite;
            JoinCode = joinCode ?? string.Empty;
        }

        // ------------------------------------------------- oyun ici arkadas listesi

        /// <summary>
        /// Davet edilebilecek bir arkadaş. <b>Düz veri</b> — <c>Bunker.Systems</c>
        /// ne Unity'yi ne Steamworks'ü görür (<c>noEngineReferences</c>), o yüzden
        /// <c>SteamId</c> burada bir metin.
        /// </summary>
        public readonly struct FriendEntry
        {
            /// <summary>SteamId, metin hâlinde. Davet bunu geri gönderir.</summary>
            public readonly string Id;

            public readonly string Name;

            /// <summary>Bu oyunu <b>şu an oynuyor mu</b>. Listenin başına o gelir.</summary>
            public readonly bool InGame;

            public FriendEntry(string id, string name, bool inGame)
            {
                Id = id;
                Name = name;
                InGame = inGame;
            }
        }

        /// <summary>
        /// Davet edilebilecek çevrimiçi arkadaşlar. 2026-09-09.
        ///
        /// <para><b>Neden kendi listemizi çiziyoruz</b> (geliştirici: <i>"Steam davet
        /// et etkisiz, herhangi bir liste gelmiyor"</i>): Steam'in davet penceresi
        /// <b>overlay</b>'dir ve overlay yalnızca Steam'in kendi başlattığı bir sürece
        /// enjekte edilir. Doğrudan çift tıklanan bir <c>.exe</c>'de
        /// <c>OpenGameInviteOverlay</c> <b>sessizce hiçbir şey yapmaz</b> — hata da
        /// vermez. Önceki gerekçe ("Steam'in zaten yaptığı işi ikinci kez yapmayalım")
        /// overlay çalıştığı varsayımına dayanıyordu ve o varsayım bu kurulumda
        /// yanlış.</para>
        ///
        /// <para>Overlay hâlâ ayrıca açılmaya çalışılıyor; çalışıyorsa iki yol da
        /// var demektir. Bu liste, çalışmadığında davetin ölmemesini sağlıyor.</para>
        /// </summary>
        public static IReadOnlyList<FriendEntry> Friends => _friends;

        private static readonly List<FriendEntry> _friends = new List<FriendEntry>(16);

        /// <summary>Listeyi <c>Bunker.Net</c> doldurur, menü okur.</summary>
        public static void SetFriends(IReadOnlyList<FriendEntry> friends)
        {
            _friends.Clear();

            if (friends != null)
            {
                for (int i = 0; i < friends.Count; i++) _friends.Add(friends[i]);
            }

            LobbyChanged?.Invoke();
        }

        /// <summary>Bir arkadaşa doğrudan lobi daveti gönder.</summary>
        public static event Action<string> InviteFriendRequested;

        public static void RequestInviteFriend(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return;

            InviteFriendRequested?.Invoke(steamId);
        }

        // ---------------------------------------------------------------- lobi

        /// <summary>
        /// Oda kuruldu ama oyun <b>henüz başlamadı</b> — lobideyiz.
        ///
        /// <para><b>Neden bir lobi var</b> (geliştirici, 2026-09-05): <i>"oyunu açınca
        /// direkt aksiyon başlıyor, lobi falan yok."</i> Doğrudan oyuna düşen bir oda,
        /// davet göndermek için zaman bırakmıyordu: arkadaşın katıldığında sen zaten
        /// üçüncü turdaydın. Lobi, <b>oyunun ne zaman başlayacağına host'un karar
        /// verdiği</b> yer.</para>
        /// </summary>
        public static bool IsInLobby { get; private set; }

        /// <summary>Bu makine odayı açan mı. "BASLAT" yalnızca onda görünür.</summary>
        public static bool IsHost { get; private set; }

        /// <summary>
        /// Lobideki oyuncu adları. <b>Sunucudan gelir</b> — istemci kendi listesini
        /// uydurmaz, yoksa iki ekranda iki farklı oda görünürdü.
        /// </summary>
        public static IReadOnlyList<string> LobbyPlayers => _lobbyPlayers;

        private static readonly List<string> _lobbyPlayers = new List<string>(4);

        /// <summary>Lobi durumu değişti (biri katıldı, biri ayrıldı).</summary>
        public static event Action LobbyChanged;

        /// <summary>Host "BASLAT" dedi.</summary>
        public static event Action StartGameRequested;

        public static void RequestStartGame() => StartGameRequested?.Invoke();

        /// <summary>Lobi durumunu bildirir. <b>Yalnızca <c>Bunker.Net</c> çağırır.</b></summary>
        public static void SetLobbyState(bool inLobby, bool isHost, IReadOnlyList<string> players)
        {
            IsInLobby = inLobby;
            IsHost = isHost;

            // LISTENIN KENDISI geri verilebiliyor (2026-09-11):
            // BunkerNetworkManager.ServerStartGame bu metodu LobbyPlayers ile cagiriyor.
            // Once Clear edilince kaynak da bosaliyor, dongu hicbir sey kopyalamiyor ve
            // oyun basladigi anda oyuncu listesi SESSIZCE sifira iniyordu. Co-op boss
            // sayisi bu listeden okunuyor; bos liste her oturumu "tek oyuncu" yapardi.
            if (players == null)
            {
                _lobbyPlayers.Clear();
            }
            else if (!ReferenceEquals(players, _lobbyPlayers))
            {
                _lobbyPlayers.Clear();
                for (int i = 0; i < players.Count; i++) _lobbyPlayers.Add(players[i]);
            }

            LobbyChanged?.Invoke();
        }

        /// <summary>Durum değişti — menü ekranını buna göre çizer.</summary>
        public static event Action<SessionStatus> StatusChanged;

        public static SessionStatus Status { get; private set; } = SessionStatus.Offline;

        /// <summary>
        /// Son hatanın oyuncuya gösterilecek hâli.
        ///
        /// <para><b>Boş bırakılmaz.</b> "Bağlanılamadı" deyip sebebini söylememek,
        /// oyuncuyu kendi ağını suçlamaya iter (ui-code.md: her ekranın hata durumu
        /// olmalı).</para>
        /// </summary>
        public static string LastError { get; private set; } = string.Empty;

        public static void RequestSolo() => SoloRequested?.Invoke();

        public static void RequestHost() => HostRequested?.Invoke();

        public static void RequestJoin(string address) => JoinRequested?.Invoke(address);

        public static void RequestLeave() => LeaveRequested?.Invoke();

        /// <summary>
        /// Oturumu kapatma <b>niyeti</b>. Dinleyiciler temizliğini yapar.
        ///
        /// <para><b>Uygulamadan çıkmayı burası YAPMAZ</b> ve yapamaz: bu derleme
        /// motoru görmez (<c>noEngineReferences</c>) — kasıtlı, çünkü kuralları Unity
        /// açmadan test edebilmek buna bağlı. Çıkışın kendisi
        /// <c>Bunker.UI.AppExit</c>'te ve oraya bakmanın sebebi
        /// <see cref="AppExitNote"/>'ta yazılı.</para>
        /// </summary>
        public static void RequestQuit() => QuitRequested?.Invoke();

        /// <summary>
        /// <b>Çıkış neden burada değil</b> (2026-09-06, oyun testi: "Q ya da çıkışa
        /// basınca yine çıkamadı").
        ///
        /// <para>Önceki hâlde <c>Application.Quit</c>'i <c>BunkerNetworkManager</c>
        /// çağırıyordu, yani çıkabilmek şuna bağlıydı: o nesnenin var olması,
        /// <c>OnEnable</c>'ının koşmuş olması ve aboneliğinin hâlâ duruyor olması.
        /// Sahnede ikinci bir <c>NetworkManager</c> var (log: <i>"Multiple
        /// NetworkManagers detected"</i>) ve kopya yok edilirken <c>OnDisable</c>'ı
        /// koşuyor; <b>statik</b> bir yöntemi bırakan <c>-=</c> hangi örneğin
        /// bıraktığını ayırt edemez.</para>
        ///
        /// <para>Ders: <b>uygulamadan çıkmak bir ağ kararı değil</b> ve bir
        /// dinleyicinin varlığına bağlanmamalı. Artık düğme doğrudan
        /// <c>AppExit.Quit()</c> çağırıyor; o da önce bu olayı tetikleyip (temizlik)
        /// sonra koşulsuz çıkıyor.</para>
        /// </summary>
        private const string AppExitNote = "bkz. Bunker.UI.AppExit";

        /// <summary>Ağ katmanı durumu bildirir. <b>Yalnızca <c>Bunker.Net</c> çağırır.</b></summary>
        public static void SetStatus(SessionStatus status, string error = null)
        {
            LastError = error ?? string.Empty;

            if (Status == status) return;

            Status = status;
            StatusChanged?.Invoke(status);
        }

        /// <summary>Bütün abonelikleri siler. Yalnızca açılışta.</summary>
        public static void Clear()
        {
            SoloRequested = null;
            HostRequested = null;
            JoinRequested = null;
            LeaveRequested = null;
            QuitRequested = null;
            StatusChanged = null;
            InviteRequested = null;
            InviteFriendRequested = null;
            LobbyChanged = null;
            StartGameRequested = null;

            IsInLobby = false;
            IsHost = false;
            _lobbyPlayers.Clear();
            _friends.Clear();

            Status = SessionStatus.Offline;
            LastError = string.Empty;
            CanInvite = false;
            JoinCode = string.Empty;
            TransportLabel = string.Empty;
        }
    }
}
