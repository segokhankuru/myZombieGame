using System;
using Bunker.Systems.Config;

namespace Bunker.Systems.Cards
{
    /// <summary>Tezgâhın dört hattı. SYS-02 §7e.</summary>
    public enum ShopLine
    {
        /// <summary>Dayanıklılık — maksimum can.</summary>
        Health,
        /// <summary>Güç — silah hasarı.</summary>
        Damage,
        /// <summary>Tetik — atış hızı.</summary>
        FireRate,
        /// <summary>Şarjör — mermi kapasitesi.</summary>
        Magazine
    }

    /// <summary>
    /// Tur arası tezgâh: dört temel yükseltme hattı, her alışta artan fiyat.
    ///
    /// <para><b>Tasarım niyeti fiyat eğrisinde</b> (geliştirici, 2026-09-04):
    /// <i>"Minör artış olmalı ve her alışta puan etkisi artmalı ki tamamen oraya
    /// odaklanılmasın — ama bir tık 'şunu da alayım' dedirtmeli."</i> Sabit fiyatta
    /// oyuncu bir hattı sonuna kadar basar ve tek boyutlu bir güç eğrisi çıkar; artan
    /// fiyat dördüncü alımı beşinciden cazip yapar ve <b>her alış bir karar kalır</b>.</para>
    ///
    /// <para><b>Tezgâh kimlik VERMEZ.</b> Artışlar küçük ve sıkıcı; "ben yanıcı-kan
    /// build'iyim" cümlesi kartlardan gelir (SYS-02 §1'in katman ayrımı). Tezgâh o
    /// cümlenin altını sağlamlaştırır, onu değiştirmez.</para>
    ///
    /// <para><b>Saf C#.</b> Puana dokunmaz — harcamayı çağıran taraf yapar, çünkü puan
    /// sunucunun otoritesindedir (ADR-0004) ve burada düşürülseydi cüzdanla iki ayrı
    /// yerden konuşulurdu.</para>
    /// </summary>
    public sealed class ShopState
    {
        private readonly ShopConfig _config;
        private readonly int[] _tiers = new int[4];

        public ShopState(ShopConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Bu hattan kaç kez alındı.</summary>
        public int Tier(ShopLine line) => _tiers[(int)line];

        public int MaxTier(ShopLine line) => line switch
        {
            ShopLine.Health => _config.HealthMaxTier,
            ShopLine.Damage => _config.DamageMaxTier,
            ShopLine.FireRate => _config.FirerateMaxTier,
            _ => _config.MagazineMaxTier
        };

        public bool IsMaxed(ShopLine line) => Tier(line) >= MaxTier(line);

        /// <summary>
        /// Bir sonraki alımın fiyatı: <c>temel × büyüme^kademe</c>.
        ///
        /// <para>Tavana ulaşmış bir hat <see cref="int.MaxValue"/> döner — "pahalı"
        /// değil, <b>kapalı</b>. Sıfır dönseydi bedava görünürdü.</para>
        /// </summary>
        public int CostFor(ShopLine line)
        {
            if (IsMaxed(line)) return int.MaxValue;

            (int baseCost, float growth) = line switch
            {
                ShopLine.Health => (_config.HealthBaseCost, _config.HealthCostGrowth),
                ShopLine.Damage => (_config.DamageBaseCost, _config.DamageCostGrowth),
                ShopLine.FireRate => (_config.FirerateBaseCost, _config.FirerateCostGrowth),
                _ => (_config.MagazineBaseCost, _config.MagazineCostGrowth)
            };

            double cost = baseCost * Math.Pow(growth, Tier(line));

            // Yuvarlama okunabilirlik icin: 1247 puanlik bir fiyat, 1250'den daha
            // zor okunur ve hicbir sey kazandirmaz.
            return (int)(Math.Round(cost / 10d, MidpointRounding.AwayFromZero) * 10d);
        }

        /// <summary>
        /// Bir kademe satın alındı olarak işaretler.
        /// <b>Ödemeyi çağıran taraf yapar</b>; burada yalnızca sayaç ilerler.
        /// </summary>
        /// <returns>Kademe ilerlediyse <c>true</c>; tavandaysa <c>false</c>.</returns>
        public bool Buy(ShopLine line)
        {
            if (IsMaxed(line)) return false;

            _tiers[(int)line]++;
            return true;
        }

        /// <summary>Bu hattın şu ana kadarki toplam etkisi.</summary>
        public float TotalFor(ShopLine line)
        {
            float increment = line switch
            {
                ShopLine.Health => _config.HealthIncrement,
                ShopLine.Damage => _config.DamageIncrement,
                ShopLine.FireRate => _config.FirerateIncrement,
                _ => _config.MagazineIncrement
            };

            return increment * Tier(line);
        }

        /// <summary>Bir stat'ın tezgâhtan gelen katkısı. Kart yığınıyla TOPLANIR.</summary>
        public float Total(CardStat stat) => stat switch
        {
            CardStat.MaxHealth => TotalFor(ShopLine.Health),
            CardStat.WeaponDamage => TotalFor(ShopLine.Damage),
            CardStat.FireRate => TotalFor(ShopLine.FireRate),
            CardStat.MagazineCapacity => TotalFor(ShopLine.Magazine),
            _ => 0f
        };

        /// <summary>Yeni run: bütün kademeler sıfırlanır.</summary>
        public void Reset() => Array.Clear(_tiers, 0, _tiers.Length);

        public static string DisplayName(ShopLine line) => line switch
        {
            ShopLine.Health => "DAYANIKLILIK",
            ShopLine.Damage => "GUC",
            ShopLine.FireRate => "TETIK",
            _ => "SARJOR"
        };

        public static string Description(ShopLine line) => line switch
        {
            ShopLine.Health => "Maksimum can",
            ShopLine.Damage => "Silah hasari",
            ShopLine.FireRate => "Atis hizi",
            _ => "Sarjor kapasitesi"
        };
    }
}
