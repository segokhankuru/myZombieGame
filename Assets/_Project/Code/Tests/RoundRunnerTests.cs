using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// M1-05 tur akışı. Burada test edilen şey <b>oyunun temposu</b>: turun ne zaman
    /// başladığı, ne zaman bittiği, doğumun ne zaman durduğu.
    /// </summary>
    public sealed class RoundRunnerTests
    {
        private static RoundRunner Runner(
            float breather = 10f,
            int maxConcurrent = 40,
            float perPlayerAtRoundOne = 6f,
            float spawnIntervalAtRoundOne = 2f,
            int aliveCapAtRoundOne = 60,
            float aliveCapAddPerRound = 0f)
        {
            // Anlik tavan (2026-09-05) VARSAYILAN OLARAK KAPALI tutuluyor: bu sinifin
            // eski testleri PERF-BUDGET tavanini (maxConcurrent) olcuyor ve iki tavan
            // ayni anda calisirsa hangisinin dogurdugunu olctugu okunmaz olurdu.
            // Anlik tavanin kendi testi asagida.
            var scaling = new RoundScaling(new RoundsConfig(
                countPerPlayerAtRoundOne: perPlayerAtRoundOne,
                countMaxConcurrent: maxConcurrent,
                countAliveCapAtRoundOne: aliveCapAtRoundOne,
                countAliveCapAddPerRound: aliveCapAddPerRound,
                pacingBreatherSecondsEarly: breather,
                pacingBreatherSecondsLate: breather,
                pacingSpawnIntervalSecondsAtRoundOne: spawnIntervalAtRoundOne));

            return new RoundRunner(scaling);
        }

        /// <summary>Molayı geçip 1. turu açar.</summary>
        private static RoundRunner Started(RoundRunner runner, float breather = 10f)
        {
            runner.Tick(breather + 0.01f, 0);
            Assert.AreEqual(RoundPhase.Active, runner.Phase, "kurulum: tur acilmaliydi");
            return runner;
        }

        // ---------------------------------------------------------------- mola ve baslangic

        /// <summary>
        /// Molanın uzunluğu <b>gelecek</b> tura göredir (2026-09-07): ilk turlar kısa,
        /// sonrası uzun. Biten tura bakılsaydı, sınırın tam üstünde mola bir tur geç
        /// uzardı ve oyuncunun hazırlık zamanı tam ihtiyaç duyduğu turda gelmezdi.
        /// </summary>
        [Test]
        public void Mola_IlkTurlarda_KISA_SonraUZUN()
        {
            var scaling = new RoundScaling(new RoundsConfig(
                pacingBreatherSecondsEarly: 10f,
                pacingBreatherSecondsLate: 20f,
                pacingBreatherEarlyUntilRound: 5));

            // Tur 1..5'e girilen molalar kisa.
            Assert.AreEqual(10f, scaling.BreatherSecondsForRound(1), 0.001f);
            Assert.AreEqual(10f, scaling.BreatherSecondsForRound(5), 0.001f);

            // Tur 6 ve sonrasi uzun.
            Assert.AreEqual(20f, scaling.BreatherSecondsForRound(6), 0.001f);
            Assert.AreEqual(20f, scaling.BreatherSecondsForRound(30), 0.001f);
        }

        [Test]
        public void Mola_BesinciTurdanSonra_YirmiSaniyeSurer()
        {
            var scaling = new RoundScaling(new RoundsConfig(
                countPerPlayerAtRoundOne: 1f,
                countAliveCapAtRoundOne: 60,
                pacingBreatherSecondsEarly: 10f,
                pacingBreatherSecondsLate: 20f,
                pacingBreatherEarlyUntilRound: 5));

            var runner = new RoundRunner(scaling);
            runner.JumpToRound(5);

            // Tur 5 temizlendi -> mola. Siradaki tur 6, yani UZUN mola.
            runner.ReportSpawned(runner.TotalForRound);
            runner.Tick(0.01f, 0);

            Assert.AreEqual(RoundPhase.Breather, runner.Phase, "kurulum: tur temizlenmeliydi");
            Assert.AreEqual(20f, runner.BreatherSeconds, 0.001f);

            // On saniye YETMEZ: kisa mola artik gecerli degil.
            runner.Tick(10.5f, 0);
            Assert.AreEqual(RoundPhase.Breather, runner.Phase, "gec turda mola 10 sn degil");

            runner.Tick(10f, 0);
            Assert.AreEqual(RoundPhase.Active, runner.Phase);
            Assert.AreEqual(6, runner.Round);
        }

        [Test]
        public void AC1_RunMolaIleBaslar_HemenZombiDogmaz()
        {
            RoundRunner runner = Runner(breather: 10f);

            int budget = runner.Tick(1f, 0);

            Assert.AreEqual(RoundPhase.Breather, runner.Phase);
            Assert.AreEqual(0, budget, "molada zombi dogmaz");
            Assert.AreEqual(0, runner.Round, "mola bitmeden tur numarasi verilmez");
        }

        [Test]
        public void AC1_MolaBitince_Tur1Acilir()
        {
            RoundRunner runner = Runner(breather: 10f);

            runner.Tick(10.5f, 0);

            Assert.AreEqual(RoundPhase.Active, runner.Phase);
            Assert.AreEqual(1, runner.Round);
            Assert.IsTrue(runner.RoundStartedThisTick);
            Assert.AreEqual(6, runner.TotalForRound, "tur 1'de tek oyuncuya 6 zombi");
        }

        [Test]
        public void AC1_TurunIlkZombisi_MolaBitiminde_BeklemedenDogar()
        {
            // Her turun basina bir dogum araligi kadar sessizlik eklemek,
            // PILLAR-03'un "kesintisiz tur" sozunu her turda bir kez cigner.
            RoundRunner runner = Started(Runner(breather: 10f));

            int budget = runner.Tick(0.01f, 0);

            Assert.GreaterOrEqual(budget, 1, "tur acildiktan hemen sonra ilk zombi gelmeli");
        }

        [Test]
        public void AC1_MolaGeriSayimi_Okunabilir()
        {
            RoundRunner runner = Runner(breather: 10f);

            runner.Tick(4f, 0);

            Assert.AreEqual(6f, runner.BreatherRemainingSeconds, 0.01f);
        }

        // ---------------------------------------------------------------- dogum temposu

        [Test]
        public void AC2_DogumAraligiBeklenir_HerTickteZombiCikmaz()
        {
            RoundRunner runner = Started(Runner(spawnIntervalAtRoundOne: 2f));
            runner.ReportSpawned(runner.Tick(0.01f, 0));   // ilk zombi

            int budget = runner.Tick(0.5f, 1);

            Assert.AreEqual(0, budget, "aralik dolmadan ikinci zombi dogmaz");
        }

        [Test]
        public void AC2_UzunKare_KacirilanDogumlariTelafiEder()
        {
            // Yukleme ya da takilma yuzunden uzun bir kare, turu yavaslatmamali.
            RoundRunner runner = Started(Runner(spawnIntervalAtRoundOne: 2f));
            runner.ReportSpawned(runner.Tick(0.01f, 0));

            int budget = runner.Tick(6.5f, 1);

            Assert.AreEqual(3, budget, "6.5 saniyede 2 saniyelik araliktan 3 dogum gecti");
        }

        [Test]
        public void AC2_ToplamSayiyaUlasilinca_DogumDurur()
        {
            RoundRunner runner = Started(Runner(perPlayerAtRoundOne: 3f, spawnIntervalAtRoundOne: 1f));

            for (int i = 0; i < 20; i++) runner.ReportSpawned(runner.Tick(1f, 1));

            Assert.AreEqual(3, runner.SpawnedThisRound);
            Assert.AreEqual(0, runner.RemainingToSpawn);
            Assert.AreEqual(0, runner.Tick(5f, 1), "tur kotasi dolduysa daha fazla dogmaz");
        }

        [Test]
        public void AC2_EsZamanliTavan_Asilmaz()
        {
            // maxConcurrent bir denge degeri degil, PERF-BUDGET tavani.
            RoundRunner runner = Started(Runner(maxConcurrent: 10, perPlayerAtRoundOne: 15f,
                                                spawnIntervalAtRoundOne: 0.1f));

            int budget = runner.Tick(5f, 10);

            Assert.AreEqual(0, budget, "tavan doluyken hic dogmaz");
        }

        [Test]
        public void AC2_TavanaYerAcilinca_YalnizcaOKadarDogar()
        {
            RoundRunner runner = Started(Runner(maxConcurrent: 10, perPlayerAtRoundOne: 15f,
                                                spawnIntervalAtRoundOne: 0.1f));

            int budget = runner.Tick(5f, 7);

            Assert.AreEqual(3, budget, "tavana 3 kisilik yer var, 3 dogar");
        }

        [Test]
        public void AnlikTavan_DolduysaDogumDurur()
        {
            // 2026-09-05 oyun testi: turun butun zombileri kisa araliklarla arka
            // arkaya doguyordu; sahada yigilinca barikata donup tamir etmek imkansiz
            // hale geliyordu. Anlik tavan, baskiyi SABIT tutar.
            RoundRunner runner = Started(Runner(perPlayerAtRoundOne: 15f,
                                                spawnIntervalAtRoundOne: 0.1f,
                                                aliveCapAtRoundOne: 5));

            Assert.AreEqual(0, runner.Tick(5f, 5), "anlik tavan doluyken hic dogmaz");
            Assert.AreEqual(2, runner.Tick(5f, 3), "tavanda iki kisilik yer varsa iki dogar");
        }

        [Test]
        public void AnlikTavan_TurlaBuyur()
        {
            var scaling = new RoundScaling(new RoundsConfig(
                countAliveCapAtRoundOne: 5,
                countAliveCapAddPerRound: 1,
                countMaxConcurrent: 40));

            Assert.AreEqual(5, scaling.AliveCapForRound(1));
            Assert.AreEqual(9, scaling.AliveCapForRound(5));
            Assert.AreEqual(24, scaling.AliveCapForRound(20));
        }

        [Test]
        public void AnlikTavan_PerformansTavaniniASAMAZ()
        {
            // maxConcurrent bir denge degeri degil, PERF-BUDGET tavani: anlik tavan
            // onu asarsa olculmemis bir yuke girilmis olur.
            var scaling = new RoundScaling(new RoundsConfig(
                countAliveCapAtRoundOne: 30,
                countAliveCapAddPerRound: 4,
                countMaxConcurrent: 40));

            Assert.AreEqual(40, scaling.AliveCapForRound(50));
        }

        // ---------------------------------------------------------------- turun bitmesi

        [Test]
        public void AC3_HepsiDogduAmaCanliVar_TurBitmez()
        {
            RoundRunner runner = Started(Runner(perPlayerAtRoundOne: 2f, spawnIntervalAtRoundOne: 0.5f));
            for (int i = 0; i < 10; i++) runner.ReportSpawned(runner.Tick(0.5f, 1));

            runner.Tick(5f, 1);

            Assert.AreEqual(RoundPhase.Active, runner.Phase,
                "sahayi temizlemek turun kendisidir - 'hepsi dogdu' yetmez");
        }

        [Test]
        public void AC3_SonZombiOlunce_TurBiterVeMolaBaslar()
        {
            RoundRunner runner = Started(Runner(perPlayerAtRoundOne: 2f, spawnIntervalAtRoundOne: 0.5f));
            for (int i = 0; i < 10; i++) runner.ReportSpawned(runner.Tick(0.5f, 1));

            runner.Tick(0.1f, 0);

            Assert.IsTrue(runner.RoundClearedThisTick);
            Assert.AreEqual(RoundPhase.Breather, runner.Phase);
        }

        [Test]
        public void AC3_MolaSonrasi_SonrakiTurAcilir_VeDahaKalabalik()
        {
            RoundRunner runner = Started(Runner(breather: 10f, perPlayerAtRoundOne: 2f,
                                                spawnIntervalAtRoundOne: 0.5f));
            int firstTotal = runner.TotalForRound;

            for (int i = 0; i < 10; i++) runner.ReportSpawned(runner.Tick(0.5f, 1));
            runner.Tick(0.1f, 0);         // tur temizlendi
            runner.Tick(10.5f, 0);        // mola bitti

            Assert.AreEqual(2, runner.Round);
            Assert.Greater(runner.TotalForRound, firstTotal, "her tur bir oncekinden kalabalik");
        }

        [Test]
        public void AC3_TurBittiIsareti_TekTickDogrudur()
        {
            RoundRunner runner = Started(Runner(perPlayerAtRoundOne: 2f, spawnIntervalAtRoundOne: 0.5f));
            for (int i = 0; i < 10; i++) runner.ReportSpawned(runner.Tick(0.5f, 1));

            runner.Tick(0.1f, 0);
            Assert.IsTrue(runner.RoundClearedThisTick);

            runner.Tick(0.1f, 0);
            Assert.IsFalse(runner.RoundClearedThisTick, "ayni tur iki kez bitemez");
        }

        // ---------------------------------------------------------------- gerceklesen vs niyet

        [Test]
        public void AC4_DogurulamayanZombi_SayilmazVeTurKilitlenmez()
        {
            // Dogum noktasi kapaliysa butcenin tamami kullanilamaz. Sayac niyete gore
            // ilerleseydi tur, hic dogmamis zombileri bekleyerek sonsuza kadar acik
            // kalirdi.
            RoundRunner runner = Started(Runner(perPlayerAtRoundOne: 3f, spawnIntervalAtRoundOne: 1f));

            int budget = runner.Tick(0.01f, 0);
            Assert.GreaterOrEqual(budget, 1);
            runner.ReportSpawned(0);      // hicbiri dogurulamadi

            Assert.AreEqual(0, runner.SpawnedThisRound);
            Assert.AreEqual(3, runner.RemainingToSpawn);
        }

        [Test]
        public void AC4_FazlaBildirim_ToplamiAsmaz()
        {
            RoundRunner runner = Started(Runner(perPlayerAtRoundOne: 3f));

            runner.ReportSpawned(99);

            Assert.AreEqual(3, runner.SpawnedThisRound);
            Assert.AreEqual(0, runner.RemainingToSpawn);
        }

        // ---------------------------------------------------------------- kenar durumlar

        [Test]
        public void NegatifSureVeNegatifCanliSayisi_Cokmez()
        {
            RoundRunner runner = Started(Runner());

            Assert.DoesNotThrow(() => runner.Tick(-5f, -3));
        }

        [Test]
        public void JumpToRound_DogrudanOTuruAcar()
        {
            RoundRunner runner = Runner();

            runner.JumpToRound(12);

            Assert.AreEqual(12, runner.Round);
            Assert.AreEqual(RoundPhase.Active, runner.Phase);
            Assert.AreEqual(0, runner.SpawnedThisRound);
            Assert.Greater(runner.TotalForRound, 6);
        }

        [Test]
        public void Reset_RunuBastanBaslatir()
        {
            RoundRunner runner = Started(Runner());
            runner.ReportSpawned(3);

            runner.Reset();

            Assert.AreEqual(0, runner.Round);
            Assert.AreEqual(RoundPhase.Breather, runner.Phase);
            Assert.AreEqual(0, runner.SpawnedThisRound);
        }

        // ------------------------------------------------------ hazir (2026-09-09)

        /// <summary>
        /// Herkes hazir verdiginde mola <b>suresini beklemeden</b> biter.
        ///
        /// <para>Gelistirici: "herkes ready (F tusu) verirse zaman direk bitsin".
        /// Mola 30 saniye ama bu bir TAVAN, bir sure degil.</para>
        /// </summary>
        [Test]
        public void HazirVerilince_MolaSureyiBeklemedenBiter()
        {
            var runner = Runner(breatherSeconds: 30f);

            runner.Tick(1f, 0);
            Assert.AreEqual(RoundPhase.Breather, runner.Phase, "Bir saniyede bitmemeli.");

            runner.SkipBreather();
            runner.Tick(0.016f, 0);

            Assert.AreEqual(RoundPhase.Active, runner.Phase);
            Assert.AreEqual(1, runner.Round);
            Assert.IsTrue(runner.RoundStartedThisTick,
                          "Tur basladi bayragi kurulmali - yoksa yonetmen turu hic gormez.");
        }

        /// <summary>
        /// Hazir bayragi tur baslayinca <b>temizlenir</b>: kalsaydi bir sonraki molayi
        /// da aninda bitirir ve oyuncu hazirlik yapamadan tur acilirdi.
        /// </summary>
        [Test]
        public void HazirBayragi_SonrakiMolayaTasinmaz()
        {
            var runner = Runner(breatherSeconds: 30f);

            runner.SkipBreather();
            runner.Tick(0.016f, 0);
            Assert.AreEqual(RoundPhase.Active, runner.Phase);

            // Turu bitir: hepsi dogsun ve sahada kimse kalmasin.
            runner.ReportSpawned(runner.TotalForRound);
            runner.Tick(0.016f, 0);
            Assert.AreEqual(RoundPhase.Breather, runner.Phase);

            // Yeni mola: bayrak tasinmadiysa sure beklenmeli.
            runner.Tick(1f, 0);
            Assert.AreEqual(RoundPhase.Breather, runner.Phase,
                            "Onceki turun hazir bayragi bu molayi bitirmemeli.");
        }

        /// <summary>Tur aktifken hazir vermek hicbir sey yapmaz.</summary>
        [Test]
        public void AktifTurda_HazirVermek_EtkisizdIr()
        {
            var runner = Runner(breatherSeconds: 30f);

            runner.SkipBreather();
            runner.Tick(0.016f, 0);
            Assert.AreEqual(RoundPhase.Active, runner.Phase);

            int round = runner.Round;
            runner.SkipBreather();
            runner.Tick(0.016f, 0);

            Assert.AreEqual(round, runner.Round, "Aktif turda hazir, tur atlatmamali.");
        }

        private static RoundRunner Runner(float breatherSeconds)
        {
            return new RoundRunner(new RoundScaling(new RoundsConfig(
                countPerPlayerAtRoundOne: 1f,
                countAliveCapAtRoundOne: 60,
                pacingBreatherSecondsEarly: breatherSeconds,
                pacingBreatherSecondsLate: breatherSeconds,
                pacingBreatherEarlyUntilRound: 5)));
        }
    }
}
