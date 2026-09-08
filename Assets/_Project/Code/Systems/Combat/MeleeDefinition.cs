namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Bir yakın dövüş silahının <b>çözülmüş</b> sayıları. 2026-09-08.
    ///
    /// <para><b>Çarpanlar burada bitmiştir.</b> <c>melee.json</c> çarpan tutar
    /// (<c>config-data.md</c>: hesaplanan değer saklanmaz), taban <c>knife.json</c>'da
    /// durur; ikisi <see cref="Resolve"/>'da bir kez çarpılır ve oyun bundan sonra
    /// yalnızca mutlak sayıları görür. Çarpımı çalışma anına bırakmak, her savuruşta
    /// "hangi taban, hangi çarpan" sorusunu yeniden sormak olurdu.</para>
    ///
    /// <para><b>Saf C#</b> — Unity açmadan test edilir (ÇK-16).</para>
    /// </summary>
    public readonly struct MeleeDefinition
    {
        /// <summary>Kalıcı katalog id'si (<c>melee.dagger</c>).</summary>
        public readonly string Id;

        /// <summary>Ekranda görünen ad. Değişebilir; id değişemez.</summary>
        public readonly string DisplayName;

        /// <summary>Tezgâhtaki satırın açıklaması.</summary>
        public readonly string Text;

        public readonly float Damage;
        public readonly float RangeMeters;
        public readonly float ArcDegrees;
        public readonly float CooldownSeconds;
        public readonly float WindupSeconds;

        /// <summary>Puan bedeli. <c>0</c> = başlangıç silahı, satılmaz.</summary>
        public readonly int Price;

        public bool IsValid => !string.IsNullOrEmpty(Id);

        public MeleeDefinition(string id, string displayName, string text,
                               float damage, float rangeMeters, float arcDegrees,
                               float cooldownSeconds, float windupSeconds, int price)
        {
            Id = id;
            DisplayName = displayName;
            Text = text;
            Damage = damage;
            RangeMeters = rangeMeters;
            ArcDegrees = arcDegrees;
            CooldownSeconds = cooldownSeconds;
            WindupSeconds = windupSeconds;
            Price = price;
        }

        /// <summary>
        /// Tabanı ve çarpanları birleştirir.
        ///
        /// <para><b>Koni açısı çarpılmaz.</b> Kılıcın daha geniş bir koni taraması
        /// bıçağı alan silahına çevirirdi (<c>PlayerMelee</c>'nin "bir savuruş, bir
        /// hedef" kuralı); üç silah da aynı koniyi kullanır ve birbirlerinden hasar,
        /// ritim ve erişimle ayrılır.</para>
        /// </summary>
        public static MeleeDefinition Resolve(string id, string displayName, string text,
                                              float baseDamage, float baseRangeMeters,
                                              float baseArcDegrees, float baseCooldownSeconds,
                                              float baseWindupSeconds,
                                              float damageMultiplier, float swingTimeMultiplier,
                                              float rangeMultiplier, int price)
        {
            if (damageMultiplier <= 0f) damageMultiplier = 1f;
            if (swingTimeMultiplier <= 0f) swingTimeMultiplier = 1f;
            if (rangeMultiplier <= 0f) rangeMultiplier = 1f;

            return new MeleeDefinition(
                id, displayName, text,
                baseDamage * damageMultiplier,
                baseRangeMeters * rangeMultiplier,
                baseArcDegrees,
                baseCooldownSeconds * swingTimeMultiplier,
                baseWindupSeconds * swingTimeMultiplier,
                price);
        }
    }
}
