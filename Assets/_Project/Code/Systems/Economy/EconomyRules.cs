using Bunker.Systems.Config;

namespace Bunker.Systems.Economy
{
    /// <summary>Puan kazandıran olaylar.</summary>
    public enum PointEvent
    {
        /// <summary>Öldürmeyen isabet.</summary>
        Hit,
        /// <summary>Gövdeye öldürme.</summary>
        BodyKill,
        /// <summary>Kafadan öldürme.</summary>
        HeadshotKill,
        /// <summary>Bıçakla öldürme.</summary>
        MeleeKill,
        /// <summary>Tamir edilen bir barikat tahtası.</summary>
        BarricadeBoardRepair
    }

    /// <summary>Satın alma denemesinin sonucu.</summary>
    public enum PurchaseResult
    {
        Success,
        /// <summary>Yeterli harcanabilir puan yok.</summary>
        InsufficientPoints,
        /// <summary>Fiyat sıfır ya da negatif — çağıran taraf hatalı.</summary>
        InvalidCost
    }

    /// <summary>
    /// Ekonomi ayarlarının üstündeki kurallar.
    ///
    /// <para><b>Neden ayrı bir sınıf:</b> <c>EconomyConfig</c> artık
    /// <c>config/schema/economy.schema.json</c>'dan üretiliyor ve üretilen kod yalnızca
    /// <i>veri</i> taşır. Bir olayın hangi puanı verdiği ise bir kuraldır ve kurallar
    /// üretilmez — burada, elle, gözle görünür şekilde yazılır.</para>
    /// </summary>
    public static class EconomyRules
    {
        /// <summary>Bir olayın puan karşılığı.</summary>
        public static int AwardFor(this EconomyConfig config, PointEvent pointEvent)
        {
            return pointEvent switch
            {
                PointEvent.Hit => config.AwardsHit,
                PointEvent.BodyKill => config.AwardsBodyKill,
                PointEvent.HeadshotKill => config.AwardsHeadshotKill,
                PointEvent.MeleeKill => config.AwardsMeleeKill,
                PointEvent.BarricadeBoardRepair => config.AwardsBarricadeBoardRepair,
                _ => 0
            };
        }
    }
}
