using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Tura göre artan zombi hasarı ve boss sayısı (2026-09-11).
    ///
    /// <para>Sayıların "doğru" olduğunu değil <b>kuralın şeklini</b> test eder:
    /// geliştiricinin cümlesi — co-op 1-2-3, tek oyuncu iki boss turunda bir, hasar her
    /// tur biraz — JSON'daki bir değişiklikle sessizce tersine dönmesin.</para>
    /// </summary>
    public sealed class RoundScalingDamageAndBossTests
    {
        private static RoundScaling Scaling(
            float growth = 0.03f, float maxMultiplier = 2.5f, int every = 5,
            int coopStep = 1, int soloStep = 2, int maxBosses = 8, float bossDamage = 2f) =>
            new RoundScaling(new RoundsConfig(
                damageGrowthPerRound01: growth,
                damageMaxMultiplier: maxMultiplier,
                bossEveryRounds: every,
                bossExtraEveryBossRoundsCoop: coopStep,
                bossExtraEveryBossRoundsSolo: soloStep,
                bossMaxPerRound: maxBosses,
                bossDamageMultiplier: bossDamage));

        // ---------------------------------------------------------------- hasar

        [Test]
        public void Hasar_TurBirde_TabanHasardir()
        {
            Assert.AreEqual(1f, Scaling().ZombieDamageMultiplierForRound(1), 1e-5f);
        }

        [Test]
        public void Hasar_HerTurDogrusalArtar()
        {
            RoundScaling scaling = Scaling(growth: 0.03f);

            Assert.AreEqual(1.03f, scaling.ZombieDamageMultiplierForRound(2), 1e-5f);
            Assert.AreEqual(1.30f, scaling.ZombieDamageMultiplierForRound(11), 1e-5f);
        }

        [Test]
        public void Hasar_TurIlerledikce_HicAzalmaz()
        {
            RoundScaling scaling = Scaling();
            float previous = scaling.ZombieDamageMultiplierForRound(1);

            for (int round = 2; round <= 200; round++)
            {
                float now = scaling.ZombieDamageMultiplierForRound(round);
                Assert.GreaterOrEqual(now, previous, $"tur {round}");
                previous = now;
            }
        }

        [Test]
        public void Hasar_TavaniAsmaz()
        {
            Assert.AreEqual(2.5f, Scaling(maxMultiplier: 2.5f).ZombieDamageMultiplierForRound(1000), 1e-5f);
        }

        [Test]
        public void Hasar_SifirVeNegatifTur_TurBirGibiDavranir()
        {
            RoundScaling scaling = Scaling();

            Assert.AreEqual(1f, scaling.ZombieDamageMultiplierForRound(0), 1e-5f);
            Assert.AreEqual(1f, scaling.ZombieDamageMultiplierForRound(-4), 1e-5f);
        }

        [Test]
        public void Hasar_ArtisSifirsa_SabitKalir()
        {
            Assert.AreEqual(1f, Scaling(growth: 0f).ZombieDamageMultiplierForRound(50), 1e-5f);
        }

        [Test]
        public void BossHasari_TurCarpaniIleBossCarpaniCarpimidir()
        {
            RoundScaling scaling = Scaling(growth: 0.03f, bossDamage: 2f);

            Assert.AreEqual(1.27f * 2f, scaling.BossDamageMultiplierForRound(10), 1e-4f);
        }

        // ---------------------------------------------------------------- boss sayisi

        [Test]
        public void BossSayisi_BossTuruDegilse_Sifir()
        {
            RoundScaling scaling = Scaling(every: 5);

            Assert.AreEqual(0, scaling.BossCountForRound(4, coop: true));
            Assert.AreEqual(0, scaling.BossCountForRound(4, coop: false));
        }

        [Test]
        public void BossSayisi_Coop_HerBossTurundaBirArtar()
        {
            RoundScaling scaling = Scaling(every: 5, coopStep: 1);

            Assert.AreEqual(1, scaling.BossCountForRound(5, coop: true));
            Assert.AreEqual(2, scaling.BossCountForRound(10, coop: true));
            Assert.AreEqual(3, scaling.BossCountForRound(15, coop: true));
            Assert.AreEqual(4, scaling.BossCountForRound(20, coop: true));
        }

        [Test]
        public void BossSayisi_TekOyuncu_IkiBossTurundaBirArtar()
        {
            RoundScaling scaling = Scaling(every: 5, soloStep: 2);

            Assert.AreEqual(1, scaling.BossCountForRound(5, coop: false));
            Assert.AreEqual(1, scaling.BossCountForRound(10, coop: false));
            Assert.AreEqual(2, scaling.BossCountForRound(15, coop: false));
            Assert.AreEqual(2, scaling.BossCountForRound(20, coop: false));
            Assert.AreEqual(3, scaling.BossCountForRound(25, coop: false));
        }

        [Test]
        public void BossSayisi_TavaniAsmaz()
        {
            Assert.AreEqual(8, Scaling(every: 5, maxBosses: 8).BossCountForRound(100, coop: true));
        }

        // ---------------------------------------------------------------- boss dogumu

        [Test]
        public void BossDogumu_IlkBoss_TurunIlkZombisidir()
        {
            Assert.AreEqual(0, Scaling().BossSpawnIndex(0, 3, 30));
            Assert.AreEqual(0, Scaling().BossSpawnIndex(0, 1, 30));
        }

        [Test]
        public void BossDogumu_BirdenFazlaBoss_TuraEsitAralikliYayilir()
        {
            RoundScaling scaling = Scaling();

            Assert.AreEqual(10, scaling.BossSpawnIndex(1, 3, 30));
            Assert.AreEqual(20, scaling.BossSpawnIndex(2, 3, 30));
        }

        [Test]
        public void BossDogumu_ZombiSayisiBosstanAzsa_ArkaArkayaGelir()
        {
            RoundScaling scaling = Scaling();

            Assert.AreEqual(1, scaling.BossSpawnIndex(1, 3, 2));
            Assert.AreEqual(2, scaling.BossSpawnIndex(2, 3, 2));
        }
    }
}
