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
    /// Ekonomi ayarları. `config/balance/economy.json` dosyasının C# karşılığı.
    /// Salt okunur; çalışma anında hiçbir sistem değiştirmez (systems-code.md).
    ///
    /// <para>Şu an elle yazılmış; config importer geldiğinde şemadan üretilecek.</para>
    /// </summary>
    public sealed class EconomyConfig
    {
        public readonly int Hit;
        public readonly int BodyKill;
        public readonly int HeadshotKill;
        public readonly int MeleeKill;
        public readonly int BarricadeBoardRepair;

        public readonly float DownedSpendableFraction;

        public readonly int DoorCheap;
        public readonly int DoorMid;
        public readonly int DoorExpensive;
        public readonly int WallWeaponCheap;
        public readonly int WallWeaponMid;
        public readonly int MysteryBox;

        /// <summary>
        /// Varsayılanlar `config/balance/economy.json` ile birebir aynıdır. Testler
        /// yalnızca ilgilendikleri alanı geçer.
        /// </summary>
        public EconomyConfig(
            int hit = 10,
            int bodyKill = 60,
            int headshotKill = 100,
            int meleeKill = 130,
            int barricadeBoardRepair = 10,
            float downedSpendableFraction = 0.5f,
            int doorCheap = 750,
            int doorMid = 1250,
            int doorExpensive = 2000,
            int wallWeaponCheap = 500,
            int wallWeaponMid = 1200,
            int mysteryBox = 950)
        {
            Hit = hit;
            BodyKill = bodyKill;
            HeadshotKill = headshotKill;
            MeleeKill = meleeKill;
            BarricadeBoardRepair = barricadeBoardRepair;

            DownedSpendableFraction = downedSpendableFraction;

            DoorCheap = doorCheap;
            DoorMid = doorMid;
            DoorExpensive = doorExpensive;
            WallWeaponCheap = wallWeaponCheap;
            WallWeaponMid = wallWeaponMid;
            MysteryBox = mysteryBox;
        }

        /// <summary>Bir olayın puan karşılığı.</summary>
        public int AwardFor(PointEvent pointEvent)
        {
            return pointEvent switch
            {
                PointEvent.Hit => Hit,
                PointEvent.BodyKill => BodyKill,
                PointEvent.HeadshotKill => HeadshotKill,
                PointEvent.MeleeKill => MeleeKill,
                PointEvent.BarricadeBoardRepair => BarricadeBoardRepair,
                _ => 0
            };
        }
    }
}
