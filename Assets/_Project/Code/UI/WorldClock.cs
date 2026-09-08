using Mirror;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// <c>Time.timeScale</c>'in <b>tek sahibi</b>. 2026-09-07.
    ///
    /// <para><b>Neden gerekti</b> (geliştirici): <i>"tezgâh açtığımızda zamanı
    /// durdur."</i> Duraklatma menüsü zamanı zaten durduruyordu ve kendi bayrağını
    /// tutuyordu. Tezgâh da aynısını yapsaydı iki sahip olurdu ve klasik hata
    /// kaçınılmazdı: tezgâhı kapatınca <c>timeScale = 1</c> yazılır, ama duraklatma
    /// menüsü hâlâ açıktır — dünya menünün arkasında yeniden akmaya başlar. İki menü,
    /// tek değişken, uzun fitilli bir hata (systems-code.md: bir geçişin tek sahibi
    /// olur).</para>
    ///
    /// <para><b>Sebep sayılır, bayrak tutulmaz:</b> her sebep kendi bitini açar ve
    /// kapatır. Dünya, <b>hiç sebep kalmayınca</b> akar. Üçüncü bir menü geldiğinde
    /// yapılacak iş bir bit eklemek.</para>
    ///
    /// <para><b>Yalnızca solo'da durur:</b> host'lu bir oturumda <c>timeScale</c>
    /// sıfır ağ paketlerini de durdurur ve bağlantı kopar. Dört kişilik bir oyunda bir
    /// oyuncunun tezgâh açması diğerlerinin oyununu donduramaz — o durumda tezgâh bir
    /// risktir, ve öyle olmalıdır.</para>
    /// </summary>
    public static class WorldClock
    {
        /// <summary>Dünyayı durduran sebepler.</summary>
        public enum Reason
        {
            /// <summary>ESC menüsü.</summary>
            PauseMenu = 1,

            /// <summary>Yükseltme tezgâhı.</summary>
            Shop = 2,

            /// <summary>Silah tezgâhı.</summary>
            WeaponShop = 4
        }

        private static int _reasons;
        private static bool _frozen;

        /// <summary>
        /// Her oyun başlangıcında ve <b>her sahne yüklemesinde</b> saat serbest
        /// bırakılır.
        ///
        /// <para><b>Neden gerekli:</b> tezgâh ya da menü açıkken ana menüye dönmek,
        /// yeni sahnenin <c>timeScale = 0</c> ile açılması demek olurdu — oyuncunun
        /// gördüğü şey "oyun açılmıyor". Statikler oyun oturumları arasında da
        /// taşınabildiği için (alan yeniden yüklemesi kapalıyken) başlangıçta
        /// sıfırlama da şart.</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _reasons = 0;
            _frozen = false;
            Time.timeScale = 1f;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            ReleaseAll();
        }

        /// <summary>Dünya şu an durmuş mu.</summary>
        public static bool IsFrozen => _frozen;

        /// <summary>Bir sebebi açar ya da kapatır ve saati buna göre ayarlar.</summary>
        public static void Set(Reason reason, bool active)
        {
            int bit = (int)reason;
            int updated = active ? _reasons | bit : _reasons & ~bit;

            if (updated == _reasons) return;

            _reasons = updated;
            Apply();
        }

        /// <summary>
        /// Her şeyi bırakır ve saati serbest bırakır. <b>Sahne değişiminde ve run
        /// bitiminde çağrılır:</b> donmuş bir dünyaya geri dönmek, oyunun açılmadığı
        /// şeklinde okunan bir hatadır.
        /// </summary>
        public static void ReleaseAll()
        {
            if (_reasons == 0 && !_frozen) return;

            _reasons = 0;
            Apply();
        }

        private static void Apply()
        {
            bool shouldFreeze = _reasons != 0 && IsSolo();

            if (shouldFreeze == _frozen) return;

            _frozen = shouldFreeze;
            Time.timeScale = shouldFreeze ? 0f : 1f;
        }

        /// <summary>
        /// Tek kişilik oturum mu. <c>NetworkServer.active</c> host olduğumuzu,
        /// bağlantı sayısı yalnız olduğumuzu söyler.
        /// </summary>
        private static bool IsSolo() =>
            NetworkServer.active && NetworkServer.connections.Count <= 1;
    }
}
