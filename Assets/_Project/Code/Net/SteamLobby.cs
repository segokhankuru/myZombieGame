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

        /// <summary>Steam olaylarına abone olundu mu (bir kez).</summary>
        private bool _subscribed;

        private float _lastAvailabilityCheck = -99f;

        private void OnEnable()
        {
            SessionSignals.StatusChanged += OnStatusChanged;
            SessionSignals.InviteRequested += OnInviteRequested;
            SessionSignals.InviteFriendRequested += OnInviteFriendRequested;

            TrySubscribeToSteam();
        }

        private void OnDisable()
        {
            SessionSignals.StatusChanged -= OnStatusChanged;
            SessionSignals.InviteRequested -= OnInviteRequested;
            SessionSignals.InviteFriendRequested -= OnInviteFriendRequested;

            if (!_subscribed) return;

            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

            _subscribed = false;
        }

        /// <summary>
        /// Steam hazır olur olmaz olaylara abone olur — <b>ve hazır değilse pes
        /// etmez</b>. 2026-09-07.
        ///
        /// <para><b>Düzeltilen hata:</b> önceki sürüm <c>OnEnable</c>'da bir kez
        /// <c>SteamClient.IsValid</c> soruyor, <c>false</c> ise sessizce geri
        /// dönüyordu. Ama Steam'i <b>taşıma bileşeni</b> başlatıyor
        /// (<c>FizzyFacepunch.Awake → SteamClient.Init</c>) ve iki bileşen aynı
        /// nesnede: Unity'nin <c>Awake</c>/<c>OnEnable</c> sırası bunlar arasında
        /// <b>tanımsız</b>. Sıra ters düştüğünde lobi hiçbir Steam olayına abone
        /// olmuyor, davet penceresi hiç açılmıyor ve <i>hiçbir hata mesajı
        /// çıkmıyordu</i>. Bir kere sorup pes etmek, bu projedeki en sık hata
        /// sınıfı.</para>
        ///
        /// <para><b>Neden yoklama, neden çalışma sırası ayarı değil:</b> Unity'nin
        /// script execution order ayarı proje ayarlarında yaşayan, kodda görünmeyen
        /// ve üçüncü parti bir bileşene bağımlı bir çözüm olurdu. Saniyede bir soru,
        /// bedava ve kendini açıklıyor.</para>
        /// </summary>
        private void TrySubscribeToSteam()
        {
            if (_subscribed || !SteamAvailable) return;

            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;

            _subscribed = true;

            Debug.Log($"[Steam] Hazir - davet acik. Kullanici: {SteamClient.Name}");
        }

        private void Update()
        {
            if (_subscribed && !_wantLobby) return;

            // Saniyede bir: Steam gec acilabilir, ya da tasima bizden sonra uyanmis
            // olabilir. Kare basina sormak bedava degil, saniyede bir bedava.
            if (Time.unscaledTime - _lastAvailabilityCheck < 1f) return;
            _lastAvailabilityCheck = Time.unscaledTime;

            TrySubscribeToSteam();

            // Oda Steam hazir olmadan acildiysa lobi hic kurulmamis olabilir. Ayni
            // ders: bir kez deneyip pes etmek, sebebi soylenmeyen bir eksiklik uretir.
            if (_wantLobby && !_hasLobby && SteamAvailable) CreateLobby();
        }

        /// <summary>
        /// Steam çalışıyor mu. <b>Her çağrının önünde durur</b>: Steam kapalıyken
        /// Facepunch çağrıları exception atar ve oyunu açılışta düşürürdü.
        /// </summary>
        private static bool SteamAvailable => SteamClient.IsValid;

        // ---------------------------------------------------------------- oturum

        /// <summary>Oda açık ve host biziz — yani bir lobi olmalı.</summary>
        private bool _wantLobby;

        private void OnStatusChanged(SessionStatus status)
        {
            if (status == SessionStatus.InSession && NetworkServer.active)
            {
                // NIYET SAKLANIR (2026-09-07): Steam bu anda henuz hazir olmayabilir.
                // Sadece burada deneyip birakmak, "oda actim ama davet dugmesi yok"
                // diye yasanan ve sebebi soylenmeyen bir eksiklik uretiyordu.
                _wantLobby = true;
                CreateLobby();
                return;
            }

            if (status == SessionStatus.Offline)
            {
                _wantLobby = false;
                LeaveLobby();
            }
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
                string hostId = SteamClient.SteamId.ToString();
                _lobby.SetData(HostKey, hostId);

                // KATILMA KODU = HOST'UN SteamId'si, LOBI KIMLIGI DEGIL (2026-09-07).
                //
                // <b>Duzeltilen hata:</b> kod olarak lobi kimligi gosteriliyordu, ama
                // menuye yapistirilan sey dogruca <c>networkAddress</c>'e yaziliyor ve
                // FizzyFacepunch onu bir <i>SteamId</i> olarak cozuyor. Lobi kimligi
                // ile host kimligi farkli sayilar; yani ekranda yazan kodu yapistiran
                // arkadas HER ZAMAN "baglanilamadi" aliyordu - ve suclu adres gibi
                // gorunuyordu.
                //
                // Davet yolu bundan etkilenmiyor: Steam'in davet penceresi lobiyi
                // veriyor, lobi de host kimligini <c>HostKey</c> verisinde tasiyor.
                // Iki yol da ayni yere variyor, ama artik ikisi de calisiyor.
                SessionSignals.SetInviteState(true, hostId);

                // Arkadas listesi lobi kurulur kurulmaz hazir olsun: dugmeye basip
                // bir kare beklemek, "yine bir sey olmadi" diye okunur.
                RefreshFriends();

                Debug.Log($"[Steam] Lobi hazir (id {_lobby.Id}). Katilma kodu: {hostId}");
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
            SessionSignals.SetFriends(null);
        }

        // ---------------------------------------------------------------- davet

        /// <summary>
        /// "Arkadaş davet et"e basıldı: <b>iki yol birden</b>. 2026-09-09.
        ///
        /// <para><b>Düzeltilen hata</b> (geliştirici: <i>"Steam davet et etkisiz,
        /// herhangi bir liste gelmiyor"</i>): tek yol Steam'in davet penceresiydi ve o
        /// pencere <b>overlay</b>'dir. Overlay yalnızca <b>Steam'in kendi başlattığı</b>
        /// bir sürece enjekte edilir; doğrudan çift tıklanan bir <c>.exe</c>'de
        /// <c>OpenGameInviteOverlay</c> <b>sessizce hiçbir şey yapmaz</b> — dönüş değeri
        /// yok, hata yok, log yok. Buton basılıyor ve hiçbir şey olmuyor.</para>
        ///
        /// <para><b>Önceki gerekçe yanlış bir varsayıma dayanıyordu:</b> "Steam'in
        /// zaten yaptığı işi ikinci kez yapmayalım" ancak Steam o işi <i>yapabiliyorsa</i>
        /// doğru. Arkadaş listesi artık oyunun içinde çiziliyor ve davet doğrudan
        /// lobiden gidiyor (<c>Lobby.InviteFriend</c>) — overlay'e hiç ihtiyaç yok.</para>
        ///
        /// <para>Overlay yine de açılmaya çalışılıyor: enjekte edilmişse iki yol da
        /// çalışır ve oyuncu alışkın olduğu pencereyi görür.</para>
        /// </summary>
        private void OnInviteRequested()
        {
            if (!SteamAvailable || !_hasLobby) return;

            RefreshFriends();

            // Overlay calisiyorsa acilir; calismiyorsa sessizce duser ve yukaridaki
            // liste devreye girer. Bu cagriyi kaldirmadik cunku Steam'den baslatilan
            // bir kurulumda alisilmis olan bu.
            SteamFriends.OpenGameInviteOverlay(_lobby.Id);
        }

        /// <summary>
        /// Çevrimiçi arkadaşları menüye verir.
        ///
        /// <para><b>Oyunu oynayanlar başta:</b> App ID 480 ile bir davet, karşı taraf
        /// oyunu <i>zaten açmışsa</i> işe yarar — Steam davetin sahibi olarak Spacewar'ı
        /// görür ve oyunu kendisi başlatamaz. Yani listenin en üstündeki isimler,
        /// davetin gerçekten varacağı isimler.</para>
        ///
        /// <para><b>Çevrimdışı arkadaşlar hiç listelenmiyor:</b> davet edilemeyecek
        /// yüz kişilik bir liste, davet edilebilecek üç kişiyi gizler.</para>
        /// </summary>
        private void RefreshFriends()
        {
            if (!SteamAvailable)
            {
                SessionSignals.SetFriends(null);
                return;
            }

            _friendBuffer.Clear();

            foreach (Friend friend in SteamFriends.GetFriends())
            {
                if (!friend.IsOnline) continue;

                _friendBuffer.Add(new SessionSignals.FriendEntry(
                    friend.Id.ToString(), friend.Name, friend.IsPlayingThisGame));
            }

            // Oyunu oynayanlar basa. Sabit bir siralama sart: her yenilemede sirasi
            // degisen bir liste, tiklamak uzere olan parmagin altindan kayar.
            _friendBuffer.Sort(CompareFriends);

            SessionSignals.SetFriends(_friendBuffer);
        }

        private static int CompareFriends(SessionSignals.FriendEntry a,
                                          SessionSignals.FriendEntry b)
        {
            if (a.InGame != b.InGame) return a.InGame ? -1 : 1;

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        private readonly System.Collections.Generic.List<SessionSignals.FriendEntry>
            _friendBuffer = new System.Collections.Generic.List<SessionSignals.FriendEntry>(32);

        /// <summary>
        /// Bir arkadaşa <b>doğrudan</b> lobi daveti. Overlay gerektirmez.
        /// </summary>
        private void OnInviteFriendRequested(string steamId)
        {
            if (!SteamAvailable || !_hasLobby) return;

            if (!ulong.TryParse(steamId, out ulong id))
            {
                Debug.LogWarning($"[Steam] Gecersiz arkadas kimligi: '{steamId}'");
                return;
            }

            bool sent = _lobby.InviteFriend(id);

            // SESSIZ KALMAZ: davetin gidip gitmedigi, oyuncunun ekranindan
            // anlasilmiyor - karsi tarafta beliriyor. Log tek kanit.
            Debug.Log(sent
                ? $"[Steam] Davet gonderildi: {steamId}"
                : $"[Steam] Davet GONDERILEMEDI: {steamId}. Arkadas listesinde " +
                  "olmayabilir ya da davetleri kapali olabilir.");
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
