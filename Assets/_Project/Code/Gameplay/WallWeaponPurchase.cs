using Bunker.Config;
using Bunker.Systems.Config;
using Bunker.Systems.Economy;
using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Duvardaki silah alımı. M1-10.
    ///
    /// <para><b>M-01'de yalnızca mermi satar.</b> Silah çeşitliliği SYS-03'ün işi ve bu
    /// milestone'un duvarının dışında; buradaki tek görev, mermi ekonomisine bir
    /// <b>musluk</b> açmak. Musluk olmadan tur ekonomisi tek yönlüdür: puan birikir,
    /// harcanacak yer yoktur, mermi biter ve oyun bıçakla oynanan başka bir oyuna
    /// döner.</para>
    ///
    /// <para><b>Tekrar tekrar alınabilir</b> — kapıdan farkı bu. Kapı bir karardır ve
    /// kalıcıdır; duvar silahı bir kaynaktır ve her tur yeniden ihtiyaç duyulur.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Wall Weapon Purchase")]
    public sealed class WallWeaponPurchase : MonoBehaviour, IPurchasable
    {
        [Header("Ayar")]
        [Tooltip("config/balance/economy.json'dan uretilen varlik.")]
        [SerializeField] private EconomyConfigAsset economyConfig;

        [Tooltip("Ucuz mu orta mi. Sayilar economy.json'da.")]
        [SerializeField] private bool midTier;

        [SerializeField] private string displayName = "MERMI";

        private int _cost;

        /// <summary>
        /// Bir alımda verilen şarjör sayısı. <c>economy.json → ammo.magazinesPerPurchase</c>.
        ///
        /// <para><b>2026-09-04'e kadar bu bir <c>[SerializeField]</c>'di</b> ve tooltip'i
        /// "denge değeri DEĞİL" diyordu. Denge simülasyonu tersini gösterdi: bu sayı
        /// turun ritmini doğrudan belirliyor — 5 ile oyuncu tur 14'te duvara <b>on beş
        /// ayrı sefer</b> yapıyor, ki PILLAR-03'ün açıkça reddettiği şey. Ritmi belirleyen
        /// bir sayı <c>config-data.md</c>'ye göre C# içinde duramaz.</para>
        /// </summary>
        private int _magazinesPerPurchase;

        public bool IsAvailable => true;   // kaynak, karar degil: tekrar alinabilir
        public int Cost => _cost;
        public string Prompt => $"{displayName}  -  {_cost} puan";

        /// <summary>Son satın alan oyuncu; sunucu mermiyi ona yazar.</summary>
        private PlayerWeapon _lastBuyer;

        private void Awake()
        {
            if (economyConfig == null)
            {
                Debug.LogError("[Duvar silahi] economy.asset atanmamis. " +
                               "'Bunker/Config/Ice Aktar' ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            EconomyConfig config = economyConfig.ToRuntime();
            _cost = midTier ? config.PricesWallWeaponMid : config.PricesWallWeaponCheap;
            _magazinesPerPurchase = config.AmmoMagazinesPerPurchase;
        }

        /// <summary>
        /// Alıcıyı işaretler. Etkileşim tarafı çağırır; <b>kim aldıysa mermi onun</b>.
        /// </summary>
        public void SetBuyer(PlayerWeapon buyer) => _lastBuyer = buyer;

        public void OnPurchased()
        {
            if (_lastBuyer == null) return;

            _lastBuyer.ServerAddReserve(_magazinesPerPurchase * _lastBuyer.MagazineCapacity);
            _lastBuyer = null;
        }
    }
}
