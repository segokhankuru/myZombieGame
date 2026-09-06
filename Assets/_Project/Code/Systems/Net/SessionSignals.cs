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

        /// <summary>Davet durumunu bildirir. <b>Yalnızca <c>Bunker.Net</c> çağırır.</b></summary>
        public static void SetInviteState(bool canInvite, string joinCode)
        {
            CanInvite = canInvite;
            JoinCode = joinCode ?? string.Empty;
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

            _lobbyPlayers.Clear();

            if (players != null)
            {
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

        public static void RequestQuit() => QuitRequested?.Invoke();

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
            LobbyChanged = null;
            StartGameRequested = null;

            IsInLobby = false;
            IsHost = false;
            _lobbyPlayers.Clear();

            Status = SessionStatus.Offline;
            LastError = string.Empty;
            CanInvite = false;
            JoinCode = string.Empty;
        }
    }
}
