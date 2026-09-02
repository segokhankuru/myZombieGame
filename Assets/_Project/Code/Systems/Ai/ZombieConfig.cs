namespace Bunker.Systems.Ai
{
    /// <summary>
    /// Zombi davranış ayarları. `config/balance/zombie.json` dosyasının C# karşılığı.
    ///
    /// <para><b>Salt okunur</b> (systems-code.md). Değer değişikliği JSON'da yapılır.
    /// Şu an elle eşleniyor; config importer geldiğinde bu dosya şemadan üretilecek ve
    /// <c>// &lt;auto-generated&gt;</c> ile işaretlenecek.</para>
    ///
    /// <para><b>Neden bu sayılar burada, hızın ve canın orada:</b> hız ve can
    /// <i>tura göre</i> değişir, o yüzden <c>RoundConfig</c>'te. Buradakiler tur
    /// numarasından bağımsız, zombinin sabit karakteridir — ne kadar önceden
    /// telefonu belli eder, pencereyi kaç saniyede aşar, kaç vurur.</para>
    /// </summary>
    public sealed class ZombieConfig
    {
        // --- dogus ---
        public readonly float EmergeDelaySeconds;

        // --- pencereden giris ---
        public readonly float WindowTriggerDistanceMeters;
        public readonly float VaultSeconds;

        // --- saldiri ---
        public readonly float AttackRangeMeters;
        public readonly float AttackRangeToleranceMeters;
        public readonly float WindupSeconds;
        public readonly float RecoverySeconds;
        public readonly float AttackDamage;

        // --- navigasyon ---
        public readonly float RepathIntervalSeconds;
        public readonly float StuckSpeedMetersPerSecond;
        public readonly float StuckAfterSeconds;
        public readonly float StuckRecoverySeconds;

        // --- butce ---
        public readonly float ThinkHz;
        public readonly float AnimatorCullDistanceMeters;

        /// <summary>
        /// Varsayılanlar `config/balance/zombie.json` ile birebir aynıdır. Testler
        /// yalnızca ilgilendikleri alanı geçer.
        /// </summary>
        public ZombieConfig(
            float emergeDelaySeconds = 0.6f,
            float windowTriggerDistanceMeters = 1.8f,
            float vaultSeconds = 1.4f,
            float attackRangeMeters = 1.6f,
            float attackRangeToleranceMeters = 0.6f,
            float windupSeconds = 0.55f,
            float recoverySeconds = 0.9f,
            float attackDamage = 30f,
            float repathIntervalSeconds = 0.35f,
            float stuckSpeedMetersPerSecond = 0.15f,
            float stuckAfterSeconds = 1.5f,
            float stuckRecoverySeconds = 0.6f,
            float thinkHz = 8f,
            float animatorCullDistanceMeters = 15f)
        {
            EmergeDelaySeconds = emergeDelaySeconds;
            WindowTriggerDistanceMeters = windowTriggerDistanceMeters;
            VaultSeconds = vaultSeconds;

            AttackRangeMeters = attackRangeMeters;
            AttackRangeToleranceMeters = attackRangeToleranceMeters;
            WindupSeconds = windupSeconds;
            RecoverySeconds = recoverySeconds;
            AttackDamage = attackDamage;

            RepathIntervalSeconds = repathIntervalSeconds;
            StuckSpeedMetersPerSecond = stuckSpeedMetersPerSecond;
            StuckAfterSeconds = stuckAfterSeconds;
            StuckRecoverySeconds = stuckRecoverySeconds;

            ThinkHz = thinkHz;
            AnimatorCullDistanceMeters = animatorCullDistanceMeters;
        }
    }
}
