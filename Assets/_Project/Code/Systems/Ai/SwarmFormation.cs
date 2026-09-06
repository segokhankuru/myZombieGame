namespace Bunker.Systems.Ai
{
    /// <summary>
    /// Sürünün <b>cephesini</b> dağıtan hakem. 2026-09-05 oyun testi:
    /// <i>"zombiler arka arkaya fazla ip gibi diziliyorlar, hedef alması kolay
    /// oluyor."</i>
    ///
    /// <para><b>Sebep bir hata değil, NavMesh'in doğası:</b> aynı hedefe giden bütün
    /// ajanlar aynı en kısa yolu bulur, aynı köşeleri aynı noktadan döner ve birbirlerini
    /// itmemek için sıraya girer. Sonuç tek sıra bir konvoy — nişan almak için ideal,
    /// kalabalık hissi için felaket.</para>
    ///
    /// <para><b>Çözüm bir arbiter</b> (ai-code.md: <i>"beş ajan bağımsız olarak en iyi
    /// kuşatma noktasını seçerse hepsi aynı noktayı seçer — atama gerekir, bağımsız
    /// optimizasyon değil"</i>). Her zombiye bir <b>şerit</b> atanır; şeritler
    /// birbirinden farklı ve <b>dağıtılmış</b>, rastgele değil. Rastgele olsaydı üç
    /// zombinin üçü de aynı tarafa düşebilirdi.</para>
    ///
    /// <para><b>Saf C#.</b> Unity'yi bilmez, sahne gerektirmez; şeritlerin gerçekten
    /// dağıldığı Unity açmadan test edilir (ÇK-16).</para>
    /// </summary>
    public static class SwarmFormation
    {
        /// <summary>Kaç ayrı şerit var. Tek sayı: ortada bir şerit kalsın diye.</summary>
        public const int LaneCount = 5;

        /// <summary>Hız sapmasının kaç kademesi var.</summary>
        private const int JitterSteps = 7;

        /// <summary>
        /// Bu zombinin yanal şeridi, metre.
        ///
        /// <para>Şeritler <c>-spread</c> ile <c>+spread</c> arasında <b>eşit
        /// aralıklarla</b> dağıtılır ve sıra <c>0, +1, -1, +½, -½</c> biçiminde
        /// serpiştirilir: arka arkaya doğan iki zombi <b>zıt</b> taraflara gider, yani
        /// dizilim daha ilk iki zombide bozulur.</para>
        /// </summary>
        /// <param name="index">Zombinin sırası. Doğum sayacından gelir.</param>
        /// <param name="spreadMeters">Şeridin en geniş hâli (config).</param>
        public static float LateralOffsetMeters(int index, float spreadMeters)
        {
            if (spreadMeters <= 0f) return 0f;

            // Negatif index (asla olmamali) mod'da negatif verir; guvenli tarafta kal.
            int lane = ((index % LaneCount) + LaneCount) % LaneCount;

            // 0 -> 0, 1 -> +1, 2 -> -1, 3 -> +0.5, 4 -> -0.5
            float unit = lane switch
            {
                0 => 0f,
                1 => 1f,
                2 => -1f,
                3 => 0.5f,
                _ => -0.5f
            };

            return unit * spreadMeters;
        }

        /// <summary>
        /// Bu zombinin hız çarpanı: <c>1 ± jitter</c> aralığında, kademeli.
        ///
        /// <para>Aynı hızda giden zombiler konvoy hâlinde kalır — biri öne geçemez,
        /// aradaki mesafe hiç değişmez. Küçük bir sapma dizilimi zamanla kendiliğinden
        /// bozar; şerit atamasının yaptığını <i>zaman içinde</i> tekrar eder.</para>
        /// </summary>
        public static float SpeedMultiplier(int index, float jitter01)
        {
            if (jitter01 <= 0f) return 1f;

            int step = ((index % JitterSteps) + JitterSteps) % JitterSteps;

            // 0..6 -> -1..+1 arasi kademeler.
            float unit = step / (JitterSteps - 1f) * 2f - 1f;

            return 1f + unit * jitter01;
        }

        /// <summary>
        /// Şeridin ne kadarının uygulanacağı (0..1).
        ///
        /// <para><b>Yakında şerit söner.</b> Sönmeseydi zombi oyuncunun iki metre
        /// yanına gidip orada dururdu — saldıramayan bir zombi, tehdit değil dekordur.
        /// Uzakta tam genişlik, <paramref name="fadeDistanceMeters"/> içinde sıfır.</para>
        /// </summary>
        public static float SpreadWeight01(float distanceToTargetMeters, float fadeDistanceMeters)
        {
            if (fadeDistanceMeters <= 0f) return 1f;
            if (distanceToTargetMeters <= fadeDistanceMeters) return 0f;

            // Sonme YUMUSAK: bir esikte aniden sifirlanan serit, zombinin son anda
            // yana sicramasi olarak gorunurdu.
            float t = (distanceToTargetMeters - fadeDistanceMeters) / fadeDistanceMeters;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        /// <summary>
        /// NavMesh kaçınma önceliği (Unity: küçük sayı = yüksek öncelik).
        ///
        /// <para><b>Eşit öncelikli ajanlar birbirini itmez, sıraya girer.</b> Unity'nin
        /// yerel kaçınması aynı önceliğe sahip iki ajanı karşılıklı yavaşlatır; farklı
        /// öncelikler birinin geçip diğerinin dolaşmasını sağlar. Tek sıra diziliminin
        /// üçüncü sebebi buydu.</para>
        /// </summary>
        public static int AvoidancePriority(int index)
        {
            // 30..69: uc degerlerden (0 ve 99) uzak durulur, cunku 0 "hic kacinma"
            // gibi davranir ve 99 ajani tamamen ezilebilir yapar.
            return 30 + (((index % 40) + 40) % 40);
        }
    }
}
