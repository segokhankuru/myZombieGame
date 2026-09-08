using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// <b>Tel:</b> <c>.claude/tools/balance-sim.ps1</c>, buradaki eğrileri PowerShell'de
    /// <b>ikinci kez</b> uyguluyor.
    ///
    /// <para><b>Neden ikinci bir uygulama var:</b> simülasyon Unity açmadan, saniyeler
    /// içinde koşmalı — ÇK-13 ve ÇK-14'ün cevabı ondan geliyor. Bedeli, aynı formülün
    /// iki yerde yaşaması; yani <c>config-data.md</c>'nin "kopyalamak bir kusurdur"
    /// kuralına bilinçli bir istisna.</para>
    ///
    /// <para><b>Bu testler o istisnanın bedelini ödüyor.</b> Kod incelemesi haklı olarak
    /// şunu söyledi: iki uygulamayı senkron tutan tek şey bir yorum satırıydı, ve
    /// <c>rounds.json</c> ilk ayarlandığında simülasyon <b>sessizce</b> yanlış cevap
    /// vermeye başlayacaktı — üstelik yetkili görünerek.</para>
    ///
    /// <para><b>Bu test kırıldığında yapılacak şey:</b> beklenen değerleri güncellemek
    /// <i>yeterli değildir</i> — <c>balance-sim.ps1</c>'i de güncelle ve
    /// <c>design/economy/curves.md</c>'yi yeniden üret. Testin tek amacı seni oraya
    /// bakmaya zorlamak.</para>
    /// </summary>
    public sealed class RoundScalingParityTests
    {
        /// <summary>
        /// <c>config/balance/rounds.json</c>'ın 2026-09-04 tarihli hâli. Config
        /// varlığını yüklemek yerine burada duruyor: test Unity varlığına bağlı
        /// olmamalı (ÇK-16).
        /// </summary>
        private static RoundScaling Scaling() => new RoundScaling(new RoundsConfig(
            version: 1,
            countPerPlayerAtRoundOne: 6f,
            countLinearAddPerPlayerPerRound: 1.5f,
            countLinearPhaseUntilRound: 9,
            countGrowthMultiplierAfterLinear: 1.10f,
            countMaxConcurrent: 40,
            healthAtRoundOne: 150f,
            healthLinearAddPerRound: 100f,
            healthLinearPhaseUntilRound: 9,
            healthGrowthMultiplierAfterLinear: 1.10f,
            healthCap: 25000f,
            speedWalkMetersPerSecond: 1.4f,
            speedJogMetersPerSecond: 2.9f,
            speedRunMetersPerSecond: 4.6f,
            speedWalkUntilRound: 4,
            speedJogUntilRound: 8,
            pacingBreatherSecondsEarly: 10f,
            pacingBreatherSecondsLate: 10f,
            pacingSpawnIntervalSecondsAtRoundOne: 2f,
            pacingSpawnIntervalFloorSeconds: 0.25f));

        [TestCase(1, 6)]
        [TestCase(5, 12)]
        [TestCase(9, 18)]
        [TestCase(10, 20)]
        [TestCase(12, 24)]
        [TestCase(18, 42)]
        public void ZombiSayisi_SimulasyonlaAyni(int round, int expected)
        {
            Assert.AreEqual(expected, Scaling().TotalZombiesForRound(round, 1),
                            $"Tur {round} zombi sayisi degisti. balance-sim.ps1'i ve " +
                            "design/economy/curves.md'yi de guncelle.");
        }

        [TestCase(1, 150f)]
        [TestCase(9, 950f)]
        [TestCase(10, 1045f)]
        [TestCase(15, 1683f)]
        public void ZombiCani_SimulasyonlaAyni(int round, float expected)
        {
            Assert.AreEqual(expected, Scaling().HealthForRound(round), 1f,
                            $"Tur {round} zombi cani degisti. balance-sim.ps1'i ve " +
                            "design/economy/curves.md'yi de guncelle.");
        }

        [Test]
        public void DogumAraligi_SayiylaTersOrantili()
        {
            RoundScaling scaling = Scaling();

            // Simulasyon bu iliskiyi yeniden uyguluyor: interval = atOne * countAtOne / countNow.
            // Iliskinin SEKLI degisirse (ornegin usel bir azalmaya donerse) burasi kirilir.
            float atOne = scaling.SpawnIntervalForRound(1);
            float atTwelve = scaling.SpawnIntervalForRound(12);

            int countAtOne = scaling.TotalZombiesForRound(1, 1);
            int countAtTwelve = scaling.TotalZombiesForRound(12, 1);

            Assert.AreEqual(atOne * countAtOne / countAtTwelve, atTwelve, 0.01f,
                            "Dogum araligi formulunun sekli degisti. balance-sim.ps1 " +
                            "bunu yeniden uyguluyor - orayi da guncelle.");
        }

        [Test]
        public void EszamanliTavan_KirkTaKaliyor()
        {
            // CK-15 olcumu 40 esamanli zombiye dayaniyor; tavan degisirse o kanit
            // dosyasi da gecersizlesir.
            Assert.AreEqual(40, Scaling().MaxConcurrent,
                            "Esamanli tavan degisti. docs/qa/performance/ altindaki " +
                            "CK-15 olcumu bu sayiya dayaniyor.");
        }
    }
}
