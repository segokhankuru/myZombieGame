using Bunker.Systems.Pickups;
using NUnit.Framework;

namespace Bunker.Tests
{
    /// <summary>
    /// Süreli eşya etkileri (2026-09-07). Statik durum: her test kendi başlangıcını
    /// kurar (<see cref="Setup"/>), yoksa bir testin bıraktığı dondurma sonrakini
    /// zehirler — test-code.md'nin izolasyon kuralı.
    /// </summary>
    public sealed class PowerupStateTests
    {
        [SetUp]
        public void Setup() => PowerupState.Clear();

        [TearDown]
        public void TearDown() => PowerupState.Clear();

        [Test]
        public void HicbirEtkiYokken_CarpanBirdir()
        {
            Assert.AreEqual(1f, PowerupState.ZombieSpeedMultiplier, 0.001f);
            Assert.IsNull(PowerupState.ActiveLabel());
        }

        [Test]
        public void Yavaslatma_HiziDusurur_SureBitince_GeriGelir()
        {
            PowerupState.ActivateSlow(0.10f, 10f);

            Assert.AreEqual(0.90f, PowerupState.ZombieSpeedMultiplier, 0.001f);

            PowerupState.Tick(9.9f);
            Assert.IsTrue(PowerupState.IsSlowActive, "sure dolmadan bitmemeli");

            PowerupState.Tick(0.2f);
            Assert.IsFalse(PowerupState.IsSlowActive);
            Assert.AreEqual(1f, PowerupState.ZombieSpeedMultiplier, 0.001f);
        }

        [Test]
        public void Dondurma_HiziSifirlar()
        {
            PowerupState.ActivateFreeze(5f);

            Assert.AreEqual(0f, PowerupState.ZombieSpeedMultiplier, 0.001f);
        }

        /// <summary>
        /// Dondurma yavaşlatmayı EZER, onunla çarpılmaz. Çarpılsalardı iki eşya üst
        /// üste geldiğinde zombiler dururdu ama oyuncu <i>neden</i> durduklarını
        /// göremezdi.
        /// </summary>
        [Test]
        public void Dondurma_Yavaslatmayi_EZER()
        {
            PowerupState.ActivateSlow(0.5f, 10f);
            PowerupState.ActivateFreeze(2f);

            Assert.AreEqual(0f, PowerupState.ZombieSpeedMultiplier, 0.001f);
            Assert.AreEqual("DONDU", PowerupState.ActiveLabel());

            // Dondurma bitince yavaslatma HALA surer: sureler bagimsiz.
            PowerupState.Tick(2.1f);

            Assert.AreEqual(0.5f, PowerupState.ZombieSpeedMultiplier, 0.001f);
            Assert.AreEqual("YAVAS", PowerupState.ActiveLabel());
        }

        [Test]
        public void IkinciEsya_SureyiTAZELER_Toplamaz()
        {
            PowerupState.ActivateSlow(0.10f, 10f);
            PowerupState.Tick(6f);

            // Kalan 4 sn iken ikinci esya: sure 10'a doner, 14 olmaz.
            PowerupState.ActivateSlow(0.10f, 10f);

            Assert.AreEqual(10f, PowerupState.SlowRemainingSeconds, 0.001f);
        }

        [Test]
        public void KisaSure_UzunSureyiKISALTMAZ()
        {
            PowerupState.ActivateFreeze(5f);
            PowerupState.ActivateFreeze(1f);

            Assert.AreEqual(5f, PowerupState.FreezeRemainingSeconds, 0.001f,
                            "daha kisa bir esya, suren etkiyi kisaltmamali");
        }

        [Test]
        public void Yavaslatma_TamamenDurduramaz()
        {
            // 1.0 kabul edilseydi yavaslatma sessizce dondurmaya donusur ve iki esya
            // arasindaki fark kaybolurdu.
            PowerupState.ActivateSlow(1f, 5f);

            Assert.Greater(PowerupState.ZombieSpeedMultiplier, 0f);
        }

        [Test]
        public void YeniRun_SayaclariSifirlar()
        {
            PowerupState.ActivateSlow(0.3f, 10f);
            PowerupState.ActivateFreeze(5f);

            PowerupState.Clear();

            Assert.AreEqual(1f, PowerupState.ZombieSpeedMultiplier, 0.001f);
            Assert.IsFalse(PowerupState.IsSlowActive);
            Assert.IsFalse(PowerupState.IsFreezeActive);
        }
    }
}
