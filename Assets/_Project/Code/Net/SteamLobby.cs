using System;
using Bunker.Systems.Net;
using Mirror;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Bunker.Net
{
    /// <summary>
    /// Steam lobisi: oda açıldığında bir lobi kurar, davetleri karşılar. ADR-0007.
    ///
    /// <para><b>Lobi neden gerekli:</b> taşıma (FizzyFacepunch) yalnızca "şu SteamId'ye
    /// bağlan" der. Arkadaşının o SteamId'yi <i>öğrenmesi</i> ayrı bir iş — lobi tam
    /// olarak bunun için var: Steam'in davet penceresi, kabul edildiğinde karşı tarafa
    /// lobiyi verir, lobi de host'un kimliğini taşır.</para>
    ///
    /// <para><b>İki yol birden</b> (bilinçli): Steam arkadaş listesinden davet, <i>ve</i>
    /// elle yapıştırılabilen bir kod. Davet penceresi yalnızca oyun Steam üzerinden
    /// başlatıldığında güvenilir çalışır; kod her koşulda çalışır. Tek yola bağlamak,
    /// ilk arkadaş testini bir kurulum ayrıntısına rehin verirdi.</para>
    ///
    /// <para><b>Steam yoksa bu bileşen sessizce kenara çekilir.</b> Steam kapalıyken
    /// oyun çalışmaya devam etmeli: menüdeki "adresle katıl" yolu (KCP) duruyor ve
    /// davet düğmesi hiç çizilmiyor (<see cref="SessionSignals.CanInvite"/>).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Steam Lobby")]
    public sealed class SteamLobby : MonoBehaviour
    {
        /// <summary>Lobi verisinde host'un kimliğini taşıyan anahtar.</summary>
        private const string HostKey = "bunker.host";

        [Tooltip("Lobideki en fazla oyuncu. GDD: dort kisi.")]
        [SerializeField] private int maxMembers = 4;

        private Lobby _lobby;
        private bool _hasLobby;

        private void OnEnable()
        {
            SessionSignals.StatusChanged += OnStatusChanged;
            SessionSignals.InviteRequested += OnInviteRequested;

            if (!SteamAvailable) return;

            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        private void OnDisable()
        {
            SessionSignals.StatusChanged -= OnStatusChanged;
            SessionSignals.InviteRequested -= OnInviteRequested;

            if (!SteamAvailable) return;

            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
        }

        /// <summary>
        /// Steam çalışıyor mu. <b>Her çağrının önünde durur</b>: Steam kapalıyken
        /// Facepunch çağrıları exception atar ve oyunu açılışta düşürürdü.
        /// </summary>
        private static bool SteamAvailable => SteamClient.IsValid;

        // ---------------------------------------------------------------- oturum

        private void OnStatusChanged(SessionStatus status)
        {
            if (status == SessionStatus.InSession && NetworkServer.active)
            {
                CreateLobby();
                return;
            }

            if (status == SessionStatus.Offline) LeaveLobby();
        }

        /// <summary>
        /// Oda açıldı: lobi kur.
        ///
        /// <para><b>Yalnızca host.</b> İstemci de lobi kursaydı, davet eden kişi ile
        /// oyunu yürüten makine farklı olabilirdi — davet kabul eden oyuncu boş bir
        /// odaya düşerdi.</para>
        ///
        /// <para>Solo oturumda da kuruluyor: tek kişi başlayıp sonra arkadaş davet
        /// etmek, en sık kullanılan akış. Lobi bedava, oyuncu davet etmezse kimse
        /// görmez.</para>
        /// </summary>
        private async void CreateLobby()
        {
            if (!SteamAvailable || _hasLobby) return;

            try
            {
                Lobby? created = await SteamMatchmaking.CreateLobbyAsync(maxMembers);

                if (created == null)
                {
                    // Sessiz basarisizlik yok: davet dugmesi cizilmezse oyuncu
                    // "ozellik yok" diye okur, sebebini kimse soylemez.
                    Debug.LogWarning("[Steam] Lobi olusturulamadi. Davet kapali; " +
                                     "adresle katilma yolu calisiyor.");
                    return;
                }

                _lobby = created.Value;
                _hasLobby = true;

                _lobby.SetPublic();
                _lobby.SetJoinable(true);

                // Host'un kimligi lobi verisinde tasiniyor: katilan taraf tasimaya
                // "su SteamId'ye baglan" diyebilmeli ve bunu lobiden ogrenir.
                _lobby.SetData(HostKey, SteamClient.SteamId.ToString());

                SessionSignals.SetInviteState(true, _lobby.Id.ToString());

                Debug.Log($"[Steam] Lobi hazir. Katilma kodu: {_lobby.Id}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Steam] Lobi olusturulamadi: {e.Message}");
            }
        }

        private void LeaveLobby()
        {
            if (_hasLobby)
            {
                _lobby.Leave();
                _hasLobby = false;
            }

            SessionSignals.SetInviteState(false, string.Empty);
        }

        // ---------------------------------------------------------------- davet

        private void OnInviteRequested()
        {
            if (!SteamAvailable || !_hasLobby) return;

            // Steam'in KENDI davet penceresi. Kendi arkadas listemizi cizmek, Steam'in
            // zaten yaptigi isi ikinci kez ve daha kotu yapmak olurdu.
            SteamFriends.OpenGameInviteOverlay(_lobby.Id);
        }

        /// <summary>Lobi kuruldu (Steam'in onayı).</summary>
        private static void OnLobbyCreated(Result result, Lobby lobby)
        {
            if (result == Result.OK) return;

            Debug.LogWarning($"[Steam] Lobi olusturma sonucu: {result}");
        }

        /// <summary>
        /// Bir lobiye girildi — davet kabul edildiğinde ya da kodla katılırken.
        ///
        /// <para><b>Host kendi lobisine de girer</b>; o durumda bağlanacak bir şey yok,
        /// zaten sunucu biziz.</para>
        /// </summary>
        private void OnLobbyEntered(Lobby lobby)
        {
            if (NetworkServer.active) return;

            string host = lobby.GetData(HostKey);

            if (string.IsNullOrEmpty(host))
            {
                Debug.LogWarning("[Steam] Lobide host kimligi yok - bu lobi baska bir " +
                                 "oyuna ait olabilir (App ID 480 herkese acik).");
                return;
            }

            _lobby = lobby;
            _hasLobby = true;

            // Buradan sonrasi normal katilma yolu: davet de bir "katil" niyetidir
            // (ADR-0007). Ag katmani hangi tasimayla baglandigini bilir, menu bilmez.
            SessionSignals.RequestJoin(host);
        }

        /// <summary>Arkadaş listesinden "katıl" denildi.</summary>
        private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
        {
            RoomEnter result = await lobby.Join();

            if (result == RoomEnter.Success) return;

            Debug.LogWarning($"[Steam] Lobiye girilemedi: {result}");
        }
    }
}
