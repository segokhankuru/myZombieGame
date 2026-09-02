using Bunker.Systems.Ai;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// M1-04 zombi karar mantığı. <b>Sahne yok, Unity yok</b> — hepsi milisaniyede
    /// koşar (ÇK-16). Test edilen şey davranış, iç yapı değil.
    /// </summary>
    public sealed class ZombieBrainTests
    {
        private static ZombieConfig Config(
            float emerge = 0.5f,
            float windup = 0.5f,
            float recovery = 0.5f,
            float vault = 1f,
            float range = 1.6f,
            float tolerance = 0.5f,
            float stuckAfter = 1f,
            float stuckRecovery = 0.5f,
            float stuckSpeed = 0.1f)
        {
            return new ZombieConfig(
                emergeDelaySeconds: emerge,
                vaultSeconds: vault,
                attackRangeMeters: range,
                attackRangeToleranceMeters: tolerance,
                windupSeconds: windup,
                recoverySeconds: recovery,
                stuckSpeedMetersPerSecond: stuckSpeed,
                stuckAfterSeconds: stuckAfter,
                stuckRecoverySeconds: stuckRecovery);
        }

        private static ZombieSenses Far => new ZombieSenses(true, 20f, actualSpeedMetersPerSecond: 2f);
        private static ZombieSenses InReach => new ZombieSenses(true, 1f, actualSpeedMetersPerSecond: 2f);

        /// <summary>Beyni istenen duruma taşır; testlerin kurulum gürültüsünü siler.</summary>
        private static ZombieBrain Chasing(ZombieConfig config)
        {
            var brain = new ZombieBrain(config);
            brain.Tick(config.EmergeDelaySeconds + 0.01f, Far);
            Assert.AreEqual(ZombieState.Chasing, brain.State, "kurulum: kovalamaya gecmeliydi");
            return brain;
        }

        // ---------------------------------------------------------------- dogum

        [Test]
        public void AC1_ZombiDogarDogmazKosmaz()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));

            brain.Tick(0.2f, Far);

            Assert.AreEqual(ZombieState.Emerging, brain.State);
            Assert.IsFalse(brain.WantsMovement, "belirme aninda hareket etmemeli");
        }

        [Test]
        public void AC1_BelirmeSuresiDolunca_IceridekiZombiKovalamayaBaslar()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));

            brain.Tick(0.6f, Far);

            Assert.AreEqual(ZombieState.Chasing, brain.State);
            Assert.AreEqual(ZombieMoveIntent.Player, brain.MoveIntent);
        }

        // ---------------------------------------------------------------- pencereden giris

        [Test]
        public void AC2_DisaridakiZombi_OncePencereyeYonelir()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));

            brain.Tick(0.6f, new ZombieSenses(true, 20f, needsWindowEntry: true,
                                              distanceToWindowMeters: 8f,
                                              actualSpeedMetersPerSecond: 2f));

            Assert.AreEqual(ZombieState.ApproachingWindow, brain.State);
            Assert.AreEqual(ZombieMoveIntent.Window, brain.MoveIntent);
        }

        [Test]
        public void AC2_PencereyeYaklasinca_Tirmanisa_Gecer()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f, vault: 1f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, true, 8f, 2f));

            brain.Tick(0.1f, new ZombieSenses(true, 20f, true, 1f, 2f));

            Assert.AreEqual(ZombieState.Vaulting, brain.State);
            Assert.IsFalse(brain.WantsMovement, "tirmanirken kendi ayagiyla yurumez");
        }

        [Test]
        public void AC2_TirmanisSuresiDolunca_IcerideKovalamayaGecer()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f, vault: 1f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, true, 8f, 2f));
            brain.Tick(0.1f, new ZombieSenses(true, 20f, true, 1f, 2f));

            brain.Tick(1.1f, new ZombieSenses(true, 20f, true, 0f, 0f));

            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        [Test]
        public void AC2_PencereKapanirsa_ZombiKilitliKalmaz()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, true, 8f, 2f));

            // Barikat yikildi / delik acildi: artik pencereye ihtiyac yok.
            brain.Tick(0.2f, Far);

            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        // ---------------------------------------------------------------- saldiri

        [Test]
        public void AC3_MenzileGirince_OnceTelegrafVar_VurusYok()
        {
            var brain = Chasing(Config(windup: 0.5f));

            brain.Tick(0.1f, InReach);

            Assert.AreEqual(ZombieState.WindingUp, brain.State);
            Assert.IsFalse(brain.AttackLandedThisTick, "hazirlik bitmeden vurus olmaz");
        }

        [Test]
        public void AC3_TelegrafBitince_VurusBirKezIsabetEder()
        {
            var brain = Chasing(Config(windup: 0.5f));
            brain.Tick(0.1f, InReach);

            brain.Tick(0.6f, InReach);
            Assert.IsTrue(brain.AttackLandedThisTick, "hazirlik bitti, vurus isabet etmeliydi");
            Assert.AreEqual(ZombieState.Striking, brain.State);

            brain.Tick(0.1f, InReach);
            Assert.IsFalse(brain.AttackLandedThisTick, "ayni vurus ikinci kez hasar yazamaz");
        }

        [Test]
        public void AC3_TelegrafSirasindaGeriCekilenOyuncu_VurusuIskalatir()
        {
            var brain = Chasing(Config(windup: 0.5f, range: 1.6f, tolerance: 0.5f));
            brain.Tick(0.1f, InReach);

            // Oyuncu menzil + tolerans disina cikti.
            brain.Tick(0.6f, new ZombieSenses(true, 5f, actualSpeedMetersPerSecond: 2f));

            Assert.IsFalse(brain.AttackLandedThisTick, "telegrafi okuyup kacan oyuncu hasar almaz");
            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        [Test]
        public void AC3_ToleransIcindeKalanOyuncu_HalaVurulur()
        {
            var brain = Chasing(Config(windup: 0.5f, range: 1.6f, tolerance: 0.5f));
            brain.Tick(0.1f, InReach);

            // 1.6 + 0.5 = 2.1 sinirinin icinde
            brain.Tick(0.6f, new ZombieSenses(true, 2.0f, actualSpeedMetersPerSecond: 2f));

            Assert.IsTrue(brain.AttackLandedThisTick);
        }

        [Test]
        public void AC3_VurustanSonra_AciklikVar_ArkaArkayaVuramaz()
        {
            var brain = Chasing(Config(windup: 0.5f, recovery: 0.5f));
            brain.Tick(0.1f, InReach);
            brain.Tick(0.6f, InReach);   // vurus
            brain.Tick(0.1f, InReach);   // Striking -> Recovering

            Assert.AreEqual(ZombieState.Recovering, brain.State);

            brain.Tick(0.2f, InReach);
            Assert.AreEqual(ZombieState.Recovering, brain.State, "aciklik bitmeden yeni vurus baslamaz");
            Assert.IsFalse(brain.AttackLandedThisTick);
        }

        // ---------------------------------------------------------------- sikisma

        [Test]
        public void AC4_HareketEtmekIsteyipEdemeyen_SikismisSayilir()
        {
            var brain = Chasing(Config(stuckAfter: 1f, stuckSpeed: 0.1f));

            brain.Tick(0.6f, new ZombieSenses(true, 20f, actualSpeedMetersPerSecond: 0f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, actualSpeedMetersPerSecond: 0f));

            Assert.AreEqual(ZombieState.Stuck, brain.State);
        }

        [Test]
        public void AC4_YuruyenZombi_AsalSikismisSayilmaz()
        {
            var brain = Chasing(Config(stuckAfter: 1f, stuckSpeed: 0.1f));

            for (int i = 0; i < 20; i++) brain.Tick(0.2f, Far);

            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        [Test]
        public void AC4_SikismaKurtarmasiBitince_KovalamayaDoner()
        {
            var brain = Chasing(Config(stuckAfter: 1f, stuckRecovery: 0.5f, stuckSpeed: 0.1f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, actualSpeedMetersPerSecond: 0f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, actualSpeedMetersPerSecond: 0f));

            brain.Tick(0.6f, new ZombieSenses(true, 20f, actualSpeedMetersPerSecond: 0f));

            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        // ---------------------------------------------------------------- olum ve kenar durumlar

        [Test]
        public void AC5_OlumHerDurumdanGecerliVe_GeriDonusuYok()
        {
            var brain = Chasing(Config());
            brain.Tick(0.1f, InReach);   // telegraf ortasinda

            brain.Kill();
            brain.Tick(5f, InReach);

            Assert.AreEqual(ZombieState.Dead, brain.State);
            Assert.IsFalse(brain.IsAlive);
            Assert.IsFalse(brain.AttackLandedThisTick, "olu zombi vurmaz");
            Assert.IsFalse(brain.WantsMovement);
        }

        [Test]
        public void AC5_HedefsizZombi_MenzildeSayilmaz_VeCokmez()
        {
            var brain = Chasing(Config());

            brain.Tick(0.5f, new ZombieSenses(false, 0f, actualSpeedMetersPerSecond: 2f));

            Assert.AreEqual(ZombieState.Chasing, brain.State,
                "hedef yoksa mesafe 0 gorunse bile saldiriya gecmemeli");
        }

        [Test]
        public void AC5_TelegrafSirasindaHedefYokOlursa_Iskalanir()
        {
            var brain = Chasing(Config(windup: 0.5f));
            brain.Tick(0.1f, InReach);

            brain.Tick(0.6f, new ZombieSenses(false, 0f, actualSpeedMetersPerSecond: 0f));

            Assert.IsFalse(brain.AttackLandedThisTick);
            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        [Test]
        public void AC5_SifirVeNegatifZaman_DurumuIlerletmez()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));

            brain.Tick(0f, Far);
            brain.Tick(-5f, Far);

            Assert.AreEqual(ZombieState.Emerging, brain.State);
        }

        [Test]
        public void AC5_ResetSonrasi_ZombiOncekiHayatindanDurumTasimaz()
        {
            var brain = Chasing(Config());
            brain.Tick(0.1f, InReach);
            brain.Kill();

            brain.Reset();

            Assert.AreEqual(ZombieState.Emerging, brain.State);
            Assert.IsTrue(brain.IsAlive);
            Assert.AreEqual(0f, brain.StateTimeSeconds, 0.0001f);
        }

        [Test]
        public void TelegrafIlerlemesi_SifirdanBire_Yurur()
        {
            var brain = Chasing(Config(windup: 1f));
            brain.Tick(0.1f, InReach);   // menzile girdi: telegraf simdi basliyor

            Assert.AreEqual(0f, brain.WindupProgress01, 0.001f, "telegraf sifirdan baslar");

            brain.Tick(0.1f, InReach);
            Assert.AreEqual(0.1f, brain.WindupProgress01, 0.02f);

            brain.Tick(0.4f, InReach);
            Assert.AreEqual(0.5f, brain.WindupProgress01, 0.02f);
        }
    }
}
