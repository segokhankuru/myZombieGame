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
    /// </summary>
    [AddComponentMenu("Bunker/Bunker Network Manager")]
    public sealed class BunkerNetworkManager : NetworkManager
    {
        [Header("Solo")]
        [Tooltip("Play'e basıldığında otomatik olarak host modunda başlar. " +
                 "Geliştirme kolaylığı içindir; menü geldiğinde kapatılacak.")]
        [SerializeField] private bool autoStartSolo = true;

        /// <summary>Şu an bir oturum açık mı (host, sunucu ya da istemci olarak).</summary>
        public static bool SessionActive => NetworkServer.active || NetworkClient.active;

        /// <summary>Oturum açık ve uzak istemci yok — yani fiilen tek kişilik oyun.</summary>
        public static bool IsSolo => NetworkServer.active && NetworkServer.connections.Count <= 1;

        public override void Start()
        {
            base.Start();

            if (autoStartSolo && !SessionActive)
            {
                StartSolo();
            }
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

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[Bunker] İstemci bağlandı: {conn.connectionId} " +
                      $"(toplam {NetworkServer.connections.Count})");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Debug.Log($"[Bunker] İstemci ayrıldı: {conn.connectionId}");
            base.OnServerDisconnect(conn);
        }
    }
}
