using System;
using Bunker.Systems.Combat;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Can havuzu. Buradaki asıl iş <b>ölümün bir kez olması</b> — ekonomi öldürme
    /// puanını bu olaya yazacak, iki kez tetiklenirse oyuncu bedava puan kazanır.
    /// </summary>
    public sealed class HealthPoolTests
    {
        [Test]
        public void YeniHavuz_DoluBaslar()
        {
            var pool = new HealthPool(150f);

            Assert.AreEqual(150f, pool.Current, 0.001f);
            Assert.IsTrue(pool.IsAlive);
            Assert.AreEqual(1f, pool.Fraction01, 0.001f);
        }

        [Test]
        public void SifirVeyaNegatifCan_KurulamazSessizceDuzeltilmez()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(-10f));
        }

        [Test]
        public void OldurmeyenHasar_CaniDusurur_OldurmezOlarakBildirir()
        {
            var pool = new HealthPool(150f);

            DamageResult result = pool.ApplyDamage(new DamageInfo(50f));

            Assert.IsFalse(result.Killed);
            Assert.AreEqual(50f, result.Absorbed, 0.001f);
            Assert.AreEqual(100f, pool.Current, 0.001f);
        }

        [Test]
        public void OlumYalnizcaBirKezBildirilir()
        {
            var pool = new HealthPool(100f);

            DamageResult first = pool.ApplyDamage(new DamageInfo(120f));
            DamageResult second = pool.ApplyDamage(new DamageInfo(120f));

            Assert.IsTrue(first.Killed, "ilk vurus oldurmeliydi");
            Assert.IsFalse(second.Killed, "ayni zombi iki kez oldurulup iki kez puan yazamaz");
            Assert.AreEqual(0f, second.Absorbed, 0.001f, "olu hedef hasar emmez");
        }

        [Test]
        public void AsiriHasar_EmilenVeTasanOlarakAyrilir()
        {
            var pool = new HealthPool(100f);

            DamageResult result = pool.ApplyDamage(new DamageInfo(160f));

            Assert.AreEqual(100f, result.Absorbed, 0.001f, "emilen, kalan candan fazla olamaz");
            Assert.AreEqual(60f, result.Overkill, 0.001f);
        }

        [Test]
        public void TamOlumcul_Hasar_Oldurur()
        {
            var pool = new HealthPool(100f);

            DamageResult result = pool.ApplyDamage(new DamageInfo(100f));

            Assert.IsTrue(result.Killed);
            Assert.IsFalse(pool.IsAlive);
            Assert.AreEqual(0f, pool.Current, 0.001f);
        }

        [Test]
        public void SifirHasar_Oldurmez()
        {
            var pool = new HealthPool(100f);

            DamageResult result = pool.ApplyDamage(new DamageInfo(0f));

            Assert.IsFalse(result.Killed);
            Assert.AreEqual(100f, pool.Current, 0.001f);
        }

        [Test]
        public void NegatifHasar_CanVermez()
        {
            var pool = new HealthPool(100f);
            pool.ApplyDamage(new DamageInfo(40f));

            pool.ApplyDamage(new DamageInfo(-50f));

            Assert.AreEqual(60f, pool.Current, 0.001f, "negatif hasar iyilestirme kapisi olamaz");
        }

        [Test]
        public void ResetTo_HavuzuYeniTuraHazirlar()
        {
            var pool = new HealthPool(100f);
            pool.ApplyDamage(new DamageInfo(100f));

            pool.ResetTo(400f);

            Assert.IsTrue(pool.IsAlive, "havuzdan cikan zombi olu gelmemeli");
            Assert.AreEqual(400f, pool.Current, 0.001f);
            Assert.AreEqual(400f, pool.Max, 0.001f);
        }

        [Test]
        public void Kill_AnindaOldurur_VeIkinciKezOldurmez()
        {
            var pool = new HealthPool(9000f);

            DamageResult first = pool.Kill();
            DamageResult second = pool.Kill();

            Assert.IsTrue(first.Killed);
            Assert.AreEqual(9000f, first.Absorbed, 0.001f);
            Assert.IsFalse(second.Killed);
        }

        [Test]
        public void KafaVurusuBilgisi_HasarlaBirlikteTasinir()
        {
            var info = new DamageInfo(100f, DamageKind.Bullet, headshot: true);

            Assert.IsTrue(info.Headshot);
            Assert.AreEqual(DamageKind.Bullet, info.Kind);
        }
    }
}
