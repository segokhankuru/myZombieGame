using Bunker.Systems.Rounds;
using Bunker.Systems.Config;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// `RoundScaling` için EditMode testleri (M1-01).
    ///
    /// Bunlar sayıların "doğru" olduğunu test etmez — doğru sayı denge sorusudur ve
    /// arkadaş testiyle bulunur. Bunlar **eğrinin şeklinin** ve kenar durumların
    /// bozulmadığını test eder: birinin JSON'da bir değeri değiştirmesi eğriyi
    /// sessizce ters çevirmesin.
    /// </summary>
    public sealed class RoundScalingTests
    {
        private static RoundScaling Default() => new RoundScaling(new RoundsConfig());

        // ---------------------------------------------------------------- zombi sayisi

        [Test]
        public void TurBir_OyuncuBasinaYapilandirilanSayi()
        {
            var scaling = new RoundScaling(new RoundsConfig(countPerPlayerAtRoundOne: 6f));

            Assert.AreEqual(6, scaling.TotalZombiesForRound(1, 1));
            Assert.AreEqual(24, scaling.TotalZombiesForRound(1, 4));
        }

        [Test]
        public void ZombiSayisi_TurIlerledikce_HicAzalmaz()
        {
            var scaling = Default();
            int previous = 0;

            for (int round = 1; round <= 60; round++)
            {
                int current = scaling.TotalZombiesForRound(round, 4);
                Assert.GreaterOrEqual(current, previous,
                    $"Tur {round}: zombi sayisi bir onceki turdan az olamaz.");
                previous = current;
            }
        }

        [Test]
        public void DogrusalFazdanSonra_BuyumeHizlanir()
        {
            var scaling = Default();

            // Dogrusal fazin icindeki iki ardisik tur farki
            int linearStep = scaling.TotalZombiesForRound(5, 4) - scaling.TotalZombiesForRound(4, 4);
            // Carpimsal fazin icindeki iki ardisik tur farki
            int growthStep = scaling.TotalZombiesForRound(20, 4) - scaling.TotalZombiesForRound(19, 4);

            Assert.Greater(growthStep, linearStep,
                "Carpimsal fazda tur basina artis, dogrusal fazdakinden buyuk olmali.");
        }

        [Test]
        public void SifirVeNegatifTur_TurBirGibiDavranir()
        {
            var scaling = Default();
            int atOne = scaling.TotalZombiesForRound(1, 4);

            Assert.AreEqual(atOne, scaling.TotalZombiesForRound(0, 4));
            Assert.AreEqual(atOne, scaling.TotalZombiesForRound(-5, 4));
        }

        [Test]
        public void SifirOyuncu_TekOyuncuGibiDavranir()
        {
            var scaling = Default();

            Assert.AreEqual(scaling.TotalZombiesForRound(3, 1), scaling.TotalZombiesForRound(3, 0));
        }

        [Test]
        public void ToplamSayi_EszamanliTavandanBagimsizdir()
        {
            // Tavan bir performans siniri; turun toplam sayisini kisitlamaz,
            // yalnizca ayni anda kacinin canli olacagini belirler.
            var scaling = new RoundScaling(new RoundsConfig(countMaxConcurrent: 40));

            Assert.Greater(scaling.TotalZombiesForRound(30, 4), 40,
                "Gec turda toplam sayi tavandan buyuk olmali; fazlasi sirada bekler.");
            Assert.AreEqual(40, scaling.MaxConcurrent);
        }

        // ---------------------------------------------------------------- can

        [Test]
        public void Can_TurIlerledikce_HicAzalmaz()
        {
            var scaling = Default();
            float previous = 0f;

            for (int round = 1; round <= 80; round++)
            {
                float current = scaling.HealthForRound(round);
                Assert.GreaterOrEqual(current, previous, $"Tur {round}: can azalamaz.");
                previous = current;
            }
        }

        [Test]
        public void Can_TavaniAsmaz()
        {
            var scaling = new RoundScaling(new RoundsConfig(healthCap: 5000f));

            Assert.AreEqual(5000f, scaling.HealthForRound(200), 0.01f,
                "Yeterince yuksek turda can tavanda sabitlenmeli.");
        }

        [Test]
        public void Can_TavanOlmadanUstelBuyur_AmaTavanlaDurur()
        {
            var withCap = new RoundScaling(new RoundsConfig(healthCap: 2000f));

            // Tavana ulasildiktan sonra iki ardisik tur ayni degeri vermeli.
            Assert.AreEqual(withCap.HealthForRound(40), withCap.HealthForRound(41), 0.01f);
        }

        // ---------------------------------------------------------------- hiz

        [Test]
        public void HizKademeleri_YapilandirilanTurlardaDegisir()
        {
            var scaling = new RoundScaling(new RoundsConfig(speedWalkUntilRound: 4, speedJogUntilRound: 8));

            Assert.AreEqual(ZombieSpeedTier.Walk, scaling.SpeedTierForRound(1));
            Assert.AreEqual(ZombieSpeedTier.Walk, scaling.SpeedTierForRound(4));
            Assert.AreEqual(ZombieSpeedTier.Jog, scaling.SpeedTierForRound(5));
            Assert.AreEqual(ZombieSpeedTier.Jog, scaling.SpeedTierForRound(8));
            Assert.AreEqual(ZombieSpeedTier.Run, scaling.SpeedTierForRound(9));
            Assert.AreEqual(ZombieSpeedTier.Run, scaling.SpeedTierForRound(100));
        }

        [Test]
        public void Hiz_KademeIlerledikce_Artar()
        {
            var scaling = Default();

            Assert.Less(scaling.SpeedForRound(1), scaling.SpeedForRound(6));
            Assert.Less(scaling.SpeedForRound(6), scaling.SpeedForRound(12));
        }

        // ---------------------------------------------------------------- tempo

        [Test]
        public void DogumAraligi_TabaninAltinaInmez()
        {
            var scaling = new RoundScaling(new RoundsConfig(pacingSpawnIntervalFloorSeconds: 0.25f));

            Assert.AreEqual(0.25f, scaling.SpawnIntervalForRound(50), 0.0001f,
                "Gec turda aralik tabana oturmali; yoksa sürü aynı anda belirir.");
        }

        [Test]
        public void DogumAraligi_TurIlerledikce_HicUzamaz()
        {
            var scaling = Default();
            float previous = float.MaxValue;

            for (int round = 1; round <= 40; round++)
            {
                float current = scaling.SpawnIntervalForRound(round);
                Assert.LessOrEqual(current, previous, $"Tur {round}: dogum araligi uzayamaz.");
                previous = current;
            }
        }
    }
}
