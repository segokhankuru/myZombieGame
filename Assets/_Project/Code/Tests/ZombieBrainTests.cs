using Bunker.Systems.Ai;
using Bunker.Systems.Config;
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
            float stuckSpeed = 0.1f,
            float maxHit = 1f,
            float bossMaxHit = 2f)
        {
            return new ZombieConfig(
                spawnEmergeDelaySeconds: emerge,
                windowEntryVaultSeconds: vault,
                attackRangeMeters: range,
                attackRangeToleranceMeters: tolerance,
                attackMaxHitDistanceMeters: maxHit,
                attackBossMaxHitDistanceMeters: bossMaxHit,
                attackWindupSeconds: windup,
                attackRecoverySeconds: recovery,
                navigationStuckSpeedMetersPerSecond: stuckSpeed,
                navigationStuckAfterSeconds: stuckAfter,
                navigationStuckRecoverySeconds: stuckRecovery);
        }

        private static ZombieSenses Far => new ZombieSenses(true, 20f, actualSpeedMetersPerSecond: 2f);
        private static ZombieSenses InReach => new ZombieSenses(true, 1f, actualSpeedMetersPerSecond: 2f);

        /// <summary>Beyni istenen duruma taşır; testlerin kurulum gürültüsünü siler.</summary>
        private static ZombieBrain Chasing(ZombieConfig config)
        {
            var brain = new ZombieBrain(config);
            brain.Tick(config.SpawnEmergeDelaySeconds + 0.01f, Far);
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

        // ---------------------------------------------------------------- barikat (M1-08)

        [Test]
        public void AC7_BarikatliPencerede_TirmanmazSOKER()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, true, 8f, 2f));

            brain.Tick(0.1f, new ZombieSenses(true, 20f, true, 1f, 2f, windowBlocked: true));

            Assert.AreEqual(ZombieState.Tearing, brain.State);
            Assert.IsFalse(brain.WantsMovement, "sokerken pencerede durur");
        }

        [Test]
        public void AC7_BarikatAcilinca_Tirmanisa_Gecer()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, true, 8f, 2f));
            brain.Tick(0.1f, new ZombieSenses(true, 20f, true, 1f, 2f, windowBlocked: true));

            brain.Tick(0.1f, new ZombieSenses(true, 20f, true, 1f, 0f, windowBlocked: false));

            Assert.AreEqual(ZombieState.Vaulting, brain.State);
        }

        [Test]
        public void AC7_SokerkenBarikatTamirEdilirse_SokmeyeDevamEder()
        {
            var brain = new ZombieBrain(Config(emerge: 0.5f));
            brain.Tick(0.6f, new ZombieSenses(true, 20f, true, 8f, 2f));
            brain.Tick(0.1f, new ZombieSenses(true, 20f, true, 1f, 2f, windowBlocked: true));

            brain.Tick(2f, new ZombieSenses(true, 20f, true, 1f, 0f, windowBlocked: true));

            Assert.AreEqual(ZombieState.Tearing, brain.State, "oyuncu tamir ettikce sokmeye devam");
        }

        /// <summary>
        /// Oyun testinde bulunan boşluk: bir kez sıkışıp kovalamaya geçen zombi,
        /// binaya girmesi gerektiğini bir daha hiç hatırlamıyordu.
        /// </summary>
        [Test]
        public void AC7_DisaridaKalanZombi_PencereyeGeriDoner()
        {
            var brain = Chasing(Config());

            brain.Tick(0.1f, new ZombieSenses(true, 20f, needsWindowEntry: true,
                                              distanceToWindowMeters: 8f,
                                              actualSpeedMetersPerSecond: 2f));

            Assert.AreEqual(ZombieState.ApproachingWindow, brain.State);
        }

        [Test]
        public void AC7_IcerideykenPencereyeDonmez()
        {
            var brain = Chasing(Config());

            brain.Tick(0.1f, Far);

            Assert.AreEqual(ZombieState.Chasing, brain.State,
                "iceri girmis zombi pencereye geri donmemeli");
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

        // ---------------------------------------------------------------- mutlak tavan (2026-09-10)

        /// <summary>
        /// Kol uzunluğu ne derse desin, merkezler arası 1 m'yi aşan oyuncuya telegraf
        /// başlamaz (geliştirici: "zombiler 1 m'den uzaktan vuramaz").
        /// </summary>
        [Test]
        public void AC3_MutlakTavan_BirMetreyiAsanOyuncuyaTelegrafBaslamaz()
        {
            var brain = Chasing(Config(range: 1.6f, maxHit: 1f));

            brain.Tick(0.1f, new ZombieSenses(true, 0.2f, actualSpeedMetersPerSecond: 2f,
                                              distanceToTargetMeters: 1.2f));

            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        [Test]
        public void AC3_MutlakTavan_TelegrafSirasindaTavaniAsanOyuncuVurulmaz()
        {
            var brain = Chasing(Config(windup: 0.5f, range: 1.6f, tolerance: 0.5f, maxHit: 1f));

            brain.Tick(0.1f, new ZombieSenses(true, 0.2f, actualSpeedMetersPerSecond: 2f,
                                              distanceToTargetMeters: 0.9f));
            Assert.AreEqual(ZombieState.WindingUp, brain.State, "kurulum: telegraf baslamaliydi");

            // Kol hala yetisiyor (bosluk 0.3 < 2.1) ama merkez 1.05 m: tavan kazanir.
            brain.Tick(0.6f, new ZombieSenses(true, 0.3f, actualSpeedMetersPerSecond: 2f,
                                              distanceToTargetMeters: 1.05f));

            Assert.IsFalse(brain.AttackLandedThisTick, "1 m'yi asan oyuncu hasar almaz");
            Assert.AreEqual(ZombieState.Chasing, brain.State);
        }

        [Test]
        public void AC3_MutlakTavan_BossIkiMetreyeKadarVurur()
        {
            var brain = Chasing(Config(windup: 0.5f, range: 1.6f, maxHit: 1f, bossMaxHit: 2f));
            var senses = new ZombieSenses(true, 0.2f, actualSpeedMetersPerSecond: 2f,
                                          distanceToTargetMeters: 1.8f, isBoss: true);

            brain.Tick(0.1f, senses);
            brain.Tick(0.6f, senses);

            Assert.IsTrue(brain.AttackLandedThisTick,
                          "boss 1.8 m'den vurabilmeli - normal zombi bu mesafeden vuramazdi");
        }

        [Test]
        public void AC3_MutlakTavan_BossIkiMetredenUzaktanVuramaz()
        {
            var brain = Chasing(Config(range: 1.6f, bossMaxHit: 2f));

            brain.Tick(0.1f, new ZombieSenses(true, 0.2f, actualSpeedMetersPerSecond: 2f,
                                              distanceToTargetMeters: 2.2f, isBoss: true));

            Assert.AreEqual(ZombieState.Chasing, brain.State);
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

        // ---------------------------------------------------------------- vurus hissi (M1-13)

        [Test]
        public void AC6_IsabetAlanZombi_Sendeler_VeYavaslar()
        {
            var brain = Chasing(Config());

            brain.NotifyHit(false);

            Assert.IsTrue(brain.IsFlinching, "vurusun bir karsiligi olmali");
            Assert.Less(brain.SpeedMultiplier, 1f, "sendeleyen zombi yavaslar");
        }

        [Test]
        public void AC6_SendelemeSuresiDolunca_HizGeriGelir()
        {
            var brain = Chasing(Config());
            brain.NotifyHit(false);

            brain.Tick(1f, Far);

            Assert.IsFalse(brain.IsFlinching);
            Assert.AreEqual(1f, brain.SpeedMultiplier, 0.001f);
        }

        [Test]
        public void AC6_HazirlananVurus_IsabetleKESILIR()
        {
            // Telegrafi goren oyuncunun iki secenegi olur: geri cekilmek ya da vurup
            // kesmek. Sendeleme yalnizca gorsel olsaydi bu secenek hic dogmazdi.
            var brain = Chasing(Config(windup: 0.5f));
            brain.Tick(0.1f, InReach);
            Assert.AreEqual(ZombieState.WindingUp, brain.State, "kurulum");

            brain.NotifyHit(false);

            Assert.AreEqual(ZombieState.Chasing, brain.State, "vurus kesilmeli");

            brain.Tick(0.6f, InReach);
            Assert.IsFalse(brain.AttackLandedThisTick, "kesilen vurus isabet edemez");
        }

        [Test]
        public void AC6_KafaVurusu_DahaUzunSendeletir()
        {
            var a = Chasing(Config());
            var b = Chasing(Config());

            a.NotifyHit(false);
            b.NotifyHit(true);

            // Govde vurusunun sendelemesi bitecek kadar, kafa vurusununki bitmeyecek
            // kadar zaman gecir. Config: 0.22 x 2 = 0.44 sn (varsayilanlar).
            a.Tick(0.3f, Far);
            b.Tick(0.3f, Far);

            Assert.IsFalse(a.IsFlinching, "govde vurusunun sendelemesi bitmis olmali");
            Assert.IsTrue(b.IsFlinching, "kafa vurusu daha uzun sendeletir");
        }

        [Test]
        public void AC6_ArkaArkayaIsabetler_SendelemeyiKISALTMAZ()
        {
            var brain = Chasing(Config());
            brain.NotifyHit(true);      // uzun sendeleme

            brain.NotifyHit(false);     // kisa olan uzerine yazmamali

            brain.Tick(0.3f, Far);
            Assert.IsTrue(brain.IsFlinching, "uzun olan kazanir");
        }

        [Test]
        public void AC6_OluZombi_Sendelemez()
        {
            var brain = Chasing(Config());
            brain.Kill();

            brain.NotifyHit(true);

            Assert.IsFalse(brain.IsFlinching);
            Assert.AreEqual(ZombieState.Dead, brain.State);
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
