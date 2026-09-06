using System.Collections.Generic;
using Bunker.Systems.Net;
using Mirror;
using Steamworks;
using UnityEngine;

namespace Bunker.Net
{
    /// <summary>İstemci katılırken kendini tanıtır. Ad <b>istemciden</b> gelir.</summary>
    public struct LobbyHelloMessage : NetworkMessage
    {
        public string DisplayName;
    }

    /// <summary>
    /// Sunucudan istemcilere lobi listesi.
    ///
    /// <para><b>Tek bir metin, liste değil:</b> Mirror mesajlarında dizi göndermek
    /// mümkün ama dört isim için ayrı bir serileştirici bakmaya değmez. Ayırıcı satır
    /// sonu; oyuncu adları satır sonu içeremez (Steam'de de içeremiyor).</para>
    /// </summary>
    public struct LobbyRosterMessage : NetworkMessage
    {
        public string Names;
    }

    /// <summary>
    /// Lobi: oda açıldıktan sonra oyun başlayana kadar geçen yer. M-04.
    ///
    /// <para><b>Neden var</b> (geliştirici, 2026-09-05): <i>"oyunu açınca direkt aksiyon
    /// başlıyor, lobi falan yok, hiç davet falan oluşturacaktık hani."</i> Doğrudan
    /// oyuna düşen bir oda, davet göndermeye zaman bırakmıyordu — arkadaşın katıldığında
    /// host çoktan üçüncü turdaydı.</para>
    ///
    /// <para><b>Otorite sunucuda</b> (ADR-0004): listeyi sunucu tutar ve yayınlar.
    /// İstemcinin kendi listesini üretmesi, iki ekranda iki farklı oda görünmesi
    /// demek olurdu.</para>
    ///
    /// <para><b>Ad bir güven sınırıdır</b> (netcode.md): istemci kendi adını söyler ve
    /// bu meşru — ama sunucu onu <b>kırpar</b> ve sınırlar. Kırpılmamış bir ad,
    /// arayüzü bozan ya da başkasının adını taklit eden bir metin olabilir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Lobby Controller")]
    public sealed class LobbyController : MonoBehaviour
    {
        /// <summary>Adın en fazla uzunluğu. Uzun ad listeyi taşırır.</summary>
        private const int MaxNameLength = 24;

        // connectionId -> ad. Sunucuda yasar.
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>(4);
        private readonly List<string> _roster = new List<string>(4);

        private void OnEnable()
        {
            SessionSignals.StartGameRequested += OnStartGameRequested;
            SessionSignals.StatusChanged += OnStatusChanged;
        }

        private void OnDisable()
        {
            SessionSignals.StartGameRequested -= OnStartGameRequested;
            SessionSignals.StatusChanged -= OnStatusChanged;
        }

        // ---------------------------------------------------------------- sunucu

        /// <summary>Sunucu tarafı hazır: tanıtma mesajını dinlemeye başla.</summary>
        public void ServerStarted()
        {
            _names.Clear();
            NetworkServer.RegisterHandler<LobbyHelloMessage>(OnServerHello);
        }

        public void ServerStopped()
        {
            _names.Clear();
            _roster.Clear();
        }

        /// <summary>Bir istemci ayrıldı: listeden düşer ve herkese yeni liste gider.</summary>
        public void ServerConnectionChanged(int connectionId, bool connected)
        {
            if (!connected) _names.Remove(connectionId);

            PublishRoster();
        }

        private void OnServerHello(NetworkConnectionToClient conn, LobbyHelloMessage message)
        {
            _names[conn.connectionId] = Sanitize(message.DisplayName, conn.connectionId);
            PublishRoster();
        }

        /// <summary>
        /// Listeyi kurar ve <b>hem yayınlar hem yerel olarak uygular</b>: host kendi
        /// mesajını almaz, ama kendi ekranında da listeyi görmeli.
        /// </summary>
        private void PublishRoster()
        {
            if (!NetworkServer.active) return;

            _roster.Clear();

            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn == null) continue;

                _roster.Add(_names.TryGetValue(conn.connectionId, out string name)
                    ? name
                    : $"Oyuncu {conn.connectionId}");
            }

            SessionSignals.SetLobbyState(SessionSignals.IsInLobby, isHost: true, _roster);

            NetworkServer.SendToAll(new LobbyRosterMessage { Names = string.Join("\n", _roster) });
        }

        // ---------------------------------------------------------------- istemci

        /// <summary>
        /// İstemci bağlandı: listeyi dinlemeye başla, sonra kendini tanıt.
        ///
        /// <para><b>Handler HOST'ta da kaydedilir</b> (2026-09-06 hatası). Sunucu
        /// listeyi <c>SendToAll</c> ile yayınlıyor ve host'un <i>kendi</i> istemcisi de
        /// o yayını alıyor. Handler yalnızca uzak istemcilerde kayıtlıyken host kendi
        /// mesajını tanımıyor, Mirror <c>"Unknown message id"</c> deyip
        /// <b>bağlantıyı kesiyordu</b> — yani tek kişilik oyun, oyun sahnesine geçtiği
        /// anda oyuncusuz kalıyordu (log'da ardı ardına "no audio listeners" uyarısı
        /// tam olarak buydu: oyuncu hiç doğmamıştı).</para>
        ///
        /// <para><b>Kural:</b> yayınladığın her mesajın <b>her alıcıda</b> bir
        /// karşılığı olmalı. Host da bir alıcıdır.</para>
        /// </summary>
        public void ClientConnected()
        {
            NetworkClient.RegisterHandler<LobbyRosterMessage>(OnClientRoster);

            // Host kendini tanitmaz: sunucu tarafi zaten listeye kendi adiyla
            // yaziyor (PublishRoster) ve ikinci bir kayit ayni oyuncuyu iki kez
            // gosterirdi.
            if (NetworkServer.active) return;

            NetworkClient.Send(new LobbyHelloMessage { DisplayName = LocalDisplayName() });
        }

        private static void OnClientRoster(LobbyRosterMessage message)
        {
            // Host kendi listesini zaten yazdi; ustune yazmak zararsiz ama gereksiz.
            if (NetworkServer.active) return;

            string[] names = string.IsNullOrEmpty(message.Names)
                ? System.Array.Empty<string>()
                : message.Names.Split('\n');

            SessionSignals.SetLobbyState(SessionSignals.IsInLobby, isHost: false, names);
        }

        /// <summary>
        /// Bu makinenin oyuncu adı. Steam varsa Steam adı, yoksa Windows kullanıcı adı.
        ///
        /// <para>Steam adı <b>tanıdık</b> bir şeydir: arkadaşın listede kendi bildiği
        /// adı görür. "Oyuncu 2" yazan bir liste, dört kişilik bir odada kimin nerede
        /// olduğunu söylemez.</para>
        /// </summary>
        private static string LocalDisplayName()
        {
            try
            {
                if (SteamClient.IsValid) return SteamClient.Name;
            }
            catch
            {
                // Steam kapali: sorun degil, asagidaki yedege dusuyoruz.
            }

            string user = System.Environment.UserName;
            return string.IsNullOrWhiteSpace(user) ? "Oyuncu" : user;
        }

        private static string Sanitize(string name, int connectionId)
        {
            if (string.IsNullOrWhiteSpace(name)) return $"Oyuncu {connectionId}";

            name = name.Replace("\n", " ").Replace("\r", " ").Trim();

            return name.Length > MaxNameLength ? name.Substring(0, MaxNameLength) : name;
        }

        // ---------------------------------------------------------------- akis

        private void OnStatusChanged(SessionStatus status)
        {
            if (status != SessionStatus.Offline) return;

            ServerStopped();
        }

        /// <summary>
        /// Host "BASLAT" dedi: <b>sunucu sahneyi değiştirir</b>, istemciler onu izler.
        ///
        /// <para>Her istemcinin kendi sahnesini yüklemesi, birinin diğerinden önce
        /// başlamasına ve ilk turun bir kısmını kaçırmasına yol açardı (netcode.md:
        /// tek geçiş sahibi).</para>
        /// </summary>
        private void OnStartGameRequested()
        {
            if (!NetworkServer.active) return;

            SessionSignals.SetLobbyState(false, true, _roster);

            var manager = NetworkManager.singleton as BunkerNetworkManager;
            manager?.ServerStartGame();
        }
    }
}
