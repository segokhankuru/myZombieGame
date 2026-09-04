using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Config;
using Bunker.Systems.Economy;
using Bunker.Systems.Rounds;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Tezgâhın oyun tarafı: satın almayı puanla ödetir ve etkiyi yayar. SYS-02 §7e.
    ///
    /// <para><b>Ödeme burada, sayaç <see cref="ShopState"/>'te.</b> Ayrımın sebebi
    /// otorite: puanı düşürmek sunucunun işi (ADR-0004), kademe hesabı ise saf C# ve
    /// Unity açmadan test edilebilir olmalı (ÇK-16).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Shop Controller")]
    public sealed class ShopController : MonoBehaviour
    {
        [Tooltip("config/balance/shop.json'dan uretilen varlik.")]
        [SerializeField] private ShopConfigAsset shopConfig;

        private ShopState _shop;

        /// <summary>Tezgâhın durumu. Arayüz fiyat ve kademe okumak için kullanır.</summary>
        public ShopState Shop => _shop;

        private void Awake()
        {
            if (shopConfig == null)
            {
                // Sessiz varsayilan yok: tezgah yoksa oyuncu "yukseltme yok" diye
                // okur ve sebebini kimse soylemez.
                Debug.LogError("[Tezgah] shop.asset atanmamis. " +
                               "'Bunker/Config/Ice Aktar' calistir, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            _shop = new ShopState(shopConfig.ToRuntime());

            // Kart yigini ve tezgah tek noktadan okunuyor (RunModifiers): tuketiciler
            // iki kaynagi kendi icinde toplasaydi ayni is kurali uce dagilirdi.
            RunModifiers.AttachShop(_shop);
        }

        private void OnEnable() => RunSignals.RunRestarted += OnRunRestarted;

        private void OnDisable() => RunSignals.RunRestarted -= OnRunRestarted;

        private void OnRunRestarted()
        {
            RunModifiers.ResetRun();

            // Yeni run'da yukseltmeler de sifirlaniyor; tuketicilerin bunu ogrenmesi
            // icin yayin sart, yoksa silah bir onceki run'in hasariyla devam eder.
            CardSignals.NotifyModifiersChanged();
        }

        /// <summary>
        /// Bir hattı satın alır.
        ///
        /// <para><b>Önce ödeme, sonra kademe.</b> Ters sırada, puan yetmediğinde
        /// kademe ilerlemiş olurdu — bedava yükseltme.</para>
        /// </summary>
        /// <returns>Alındıysa <c>true</c>.</returns>
        public bool Buy(ShopLine line, PlayerScore score)
        {
            if (_shop == null || score == null) return false;
            if (_shop.IsMaxed(line)) return false;

            int cost = _shop.CostFor(line);

            if (score.TrySpend(cost) != PurchaseResult.Success) return false;

            _shop.Buy(line);

            // Silah, can ve ekonomi bu yayini dinleyip kendini tazeler.
            CardSignals.NotifyModifiersChanged();

            return true;
        }

        /// <summary>Oyuncunun bu hattı alacak puanı var mı (arayüz için).</summary>
        public bool CanAfford(ShopLine line, PlayerScore score) =>
            _shop != null && score != null && !_shop.IsMaxed(line) &&
            score.Spendable >= _shop.CostFor(line);
    }
}
