namespace Bunker.Systems.Rounds
{
    /// <summary>Zombinin tur bazlı hız kademesi.</summary>
    public enum ZombieSpeedTier
    {
        Walk,
        Jog,
        Run
    }

    /// <summary>
    /// Tur ölçekleme ayarları. `config/balance/rounds.json` dosyasının C# karşılığı.
    ///
    /// <para><b>Salt okunur.</b> Çalışma anında hiçbir sistem bu değerleri değiştirmez
    /// (systems-code.md). Değer değişikliği JSON'da yapılır, oradan üretilir.</para>
    ///
    /// <para>Şu an elle yazılmış; config importer geldiğinde
    /// <c>config/schema/rounds.schema.json</c>'dan üretilecek ve bu dosya
    /// <c>// &lt;auto-generated&gt;</c> ile işaretlenecek.</para>
    /// </summary>
    public sealed class RoundConfig
    {
        // --- adet ---
        public readonly float PerPlayerAtRoundOne;
        public readonly float LinearAddPerPlayerPerRound;
        public readonly int CountLinearPhaseUntilRound;
        public readonly float CountGrowthMultiplierAfterLinear;
        public readonly int MaxConcurrent;

        // --- can ---
        public readonly float HealthAtRoundOne;
        public readonly float HealthLinearAddPerRound;
        public readonly int HealthLinearPhaseUntilRound;
        public readonly float HealthGrowthMultiplierAfterLinear;
        public readonly float HealthCap;

        // --- hiz ---
        public readonly float WalkMetersPerSecond;
        public readonly float JogMetersPerSecond;
        public readonly float RunMetersPerSecond;
        public readonly int WalkUntilRound;
        public readonly int JogUntilRound;

        // --- tempo ---
        public readonly float BreatherSeconds;
        public readonly float SpawnIntervalSecondsAtRoundOne;
        public readonly float SpawnIntervalFloorSeconds;

        /// <summary>
        /// Varsayılanlar `config/balance/rounds.json` ile birebir aynıdır. Testler
        /// yalnızca ilgilendikleri alanı geçer; geri kalan dengeli hâliyle kalır.
        /// </summary>
        public RoundConfig(
            float perPlayerAtRoundOne = 6f,
            float linearAddPerPlayerPerRound = 1.5f,
            int countLinearPhaseUntilRound = 9,
            float countGrowthMultiplierAfterLinear = 1.10f,
            int maxConcurrent = 40,
            float healthAtRoundOne = 150f,
            float healthLinearAddPerRound = 100f,
            int healthLinearPhaseUntilRound = 9,
            float healthGrowthMultiplierAfterLinear = 1.10f,
            float healthCap = 25000f,
            float walkMetersPerSecond = 1.4f,
            float jogMetersPerSecond = 2.9f,
            float runMetersPerSecond = 4.6f,
            int walkUntilRound = 4,
            int jogUntilRound = 8,
            float breatherSeconds = 10f,
            float spawnIntervalSecondsAtRoundOne = 2f,
            float spawnIntervalFloorSeconds = 0.25f)
        {
            PerPlayerAtRoundOne = perPlayerAtRoundOne;
            LinearAddPerPlayerPerRound = linearAddPerPlayerPerRound;
            CountLinearPhaseUntilRound = countLinearPhaseUntilRound;
            CountGrowthMultiplierAfterLinear = countGrowthMultiplierAfterLinear;
            MaxConcurrent = maxConcurrent;

            HealthAtRoundOne = healthAtRoundOne;
            HealthLinearAddPerRound = healthLinearAddPerRound;
            HealthLinearPhaseUntilRound = healthLinearPhaseUntilRound;
            HealthGrowthMultiplierAfterLinear = healthGrowthMultiplierAfterLinear;
            HealthCap = healthCap;

            WalkMetersPerSecond = walkMetersPerSecond;
            JogMetersPerSecond = jogMetersPerSecond;
            RunMetersPerSecond = runMetersPerSecond;
            WalkUntilRound = walkUntilRound;
            JogUntilRound = jogUntilRound;

            BreatherSeconds = breatherSeconds;
            SpawnIntervalSecondsAtRoundOne = spawnIntervalSecondsAtRoundOne;
            SpawnIntervalFloorSeconds = spawnIntervalFloorSeconds;
        }
    }
}
