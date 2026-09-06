using System;
using Bunker.Systems.Combat;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Oyuncunun yenilenen canı (M1-11). Buradaki asıl iş <b>gecikmenin gerçekten
    /// bekletmesi</b>: gecikmesiz bir yenilenme, hasarı bir sonuç olmaktan çıkarır ve
    /// sürünün içinde durmanın bedelini sıfırlar.
    ///
    /// <para>Değerler <c>player.json</c>'ın şu anki ayarıdır: 100 can, 4 sn gecikme,
    /// 25/sn hız. Testler sayıyı değil <b>davranışı</b> doğrular — denge değişince
    /// bu testlerin kırılmaması gerekir.</para>
    /// </summary>
    public sealed class RegeneratingHealthTests
    {
        private const float Max = 100f;
        private const float Delay = 4f;
        private const float Rate = 25f;
        private const float LowFraction = 0.35f;

        private static RegeneratingHealth Make() =>
            new RegeneratingHealth(Max, Delay, Rate, LowFraction);

        [Test]
        public void YeniCan_DoluBaslar()
        {
            RegeneratingHealth health = Make();

            Assert.AreEqual(Max, health.Current, 0.001f);
            Assert.IsTrue(health.IsAlive);
            Assert.IsFalse(health.IsLow);
        }

        [Test]
        public void GecersizAyar_SessizceDuzeltilmez()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegeneratingHealth(Max, -1f, Rate, LowFraction));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegeneratingHealth(Max, Delay, 0f, LowFraction));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegeneratingHealth(0f, Delay, Rate, LowFraction));
        }

        [Test]
        public void AC1_Hasar_CaniDusurur()
        {
            RegeneratingHealth health = Make();

            health.ApplyDamage(new DamageInfo(30f));

            Assert.AreEqual(70f, health.Current, 0.001f);
        }

        [Test]
        public void AC1_GecikmeBoyunca_CanSabitKalir()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(30f));

            // Gecikmenin hemen altina kadar ilerlet.
            for (int i = 0; i < 39; i++) health.Tick(0.1f);

            Assert.AreEqual(70f, health.Current, 0.001f);
            Assert.IsFalse(health.IsRegenerating);
        }

        [Test]
        public void AC1_GecikmeDolunca_CanYenilenir()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(30f));

            for (int i = 0; i < 40; i++) health.Tick(0.1f);   // gecikme doldu
            health.Tick(1f);                                  // 25 can

            Assert.AreEqual(95f, health.Current, 0.5f);
            Assert.IsTrue(health.IsRegenerating);
        }

        [Test]
        public void AC1_Yenilenme_TavaniAsmaz()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(30f));

            for (int i = 0; i < 200; i++) health.Tick(0.1f);

            Assert.AreEqual(Max, health.Current, 0.001f);
            Assert.IsFalse(health.IsRegenerating, "Tam canda yenilenme bitmis olmali.");
        }

        [Test]
        public void AC2_YenilenirkenGelenHasar_GecikmeyiBastanBaslatir()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(30f));

            for (int i = 0; i < 45; i++) health.Tick(0.1f);   // yenilenme basladi
            float beforeSecondHit = health.Current;
            Assert.Greater(beforeSecondHit, 70f);

            health.ApplyDamage(new DamageInfo(30f));
            float afterSecondHit = health.Current;

            // Ikinci vurusun hemen ardindan gecikme yeniden dolmali.
            Assert.AreEqual(Delay, health.RegenDelayRemainingSeconds, 0.001f);

            for (int i = 0; i < 39; i++) health.Tick(0.1f);
            Assert.AreEqual(afterSecondHit, health.Current, 0.001f,
                            "Yeni hasar gecikmeyi sifirlamali.");
        }

        [Test]
        public void SurekliVurulanOyuncu_HicYenilenmez()
        {
            RegeneratingHealth health = Make();

            // Her saniye bir vurus: gecikme hic dolmaz.
            health.ApplyDamage(new DamageInfo(10f));
            for (int i = 0; i < 3; i++)
            {
                for (int t = 0; t < 10; t++) health.Tick(0.1f);
                health.ApplyDamage(new DamageInfo(10f));
            }

            Assert.AreEqual(60f, health.Current, 0.001f);
        }

        [Test]
        public void AC3_SifirCan_BirKezOldurur()
        {
            RegeneratingHealth health = Make();

            DamageResult first = health.ApplyDamage(new DamageInfo(150f));
            DamageResult second = health.ApplyDamage(new DamageInfo(30f));

            Assert.IsTrue(first.Killed);
            Assert.IsFalse(second.Killed, "Olum bir kez olur.");
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void AC3_OluOyuncu_Yenilenmez()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(150f));

            for (int i = 0; i < 200; i++) health.Tick(0.1f);

            Assert.AreEqual(0f, health.Current, 0.001f);
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void AC7_EsiginAltindaUyariYanar_UstundeYanmaz()
        {
            RegeneratingHealth health = Make();

            health.ApplyDamage(new DamageInfo(60f));   // 0.40
            Assert.IsFalse(health.IsLow);

            health.ApplyDamage(new DamageInfo(10f));   // 0.30
            Assert.IsTrue(health.IsLow);
        }

        [Test]
        public void AC7_OluOyuncuda_UyariYanmaz()
        {
            RegeneratingHealth health = Make();

            health.ApplyDamage(new DamageInfo(150f));

            Assert.IsFalse(health.IsLow, "Olum ekrani acikken kenar uyarisi yanmamali.");
        }

        [Test]
        public void AC5_ResetFull_TamCanaDonerVeGecikmeyiSifirlar()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(80f));

            health.ResetFull();

            Assert.AreEqual(Max, health.Current, 0.001f);
            Assert.IsTrue(health.IsAlive);
            Assert.IsFalse(health.IsLow);
            Assert.AreEqual(0f, health.RegenDelayRemainingSeconds, 0.001f,
                            "Yeni run vurulmus bir oyuncuyla baslamamali.");
        }

        [Test]
        public void SifirHasar_GecikmeyiSifirlamaz()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(30f));

            for (int i = 0; i < 40; i++) health.Tick(0.1f);
            health.ApplyDamage(new DamageInfo(0f));

            Assert.AreEqual(0f, health.RegenDelayRemainingSeconds, 0.001f);
        }

        [Test]
        public void HicVurulmamisOyuncu_YenilenmeyiBeklemez()
        {
            RegeneratingHealth health = Make();

            Assert.AreEqual(0f, health.RegenDelayRemainingSeconds, 0.001f);
        }

        [Test]
        public void NegatifVeSifirDelta_CaniDegistirmez()
        {
            RegeneratingHealth health = Make();
            health.ApplyDamage(new DamageInfo(30f));
            for (int i = 0; i < 40; i++) health.Tick(0.1f);

            float before = health.Current;
            health.Tick(-1f);
            health.Tick(0f);

            Assert.AreEqual(before, health.Current, 0.001f);
        }
    }
}
