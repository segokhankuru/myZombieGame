using Bunker.Systems.Combat;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Yakın dövüş çarpanlarının <b>çözülmesi</b>. Bu sınıf, geliştiricinin
    /// 2026-09-08'de istediği üç sayının (<i>kılıç 1.5x, balta 2.5x ve 2x savuruş
    /// süresi</i>) oyunda gerçekten o sayılar olduğunu Unity açmadan kanıtlıyor.
    /// </summary>
    public sealed class MeleeDefinitionTests
    {
        // knife.json'daki taban savurus. Test bu sayilarin KENDISINI dogrulamiyor -
        // onlar denge kararı; dogrulanan sey CARPIMIN dogrulugu.
        private const float BaseDamage = 160f;
        private const float BaseRange = 2.2f;
        private const float BaseArc = 70f;
        private const float BaseCooldown = 0.85f;
        private const float BaseWindup = 0.12f;

        private static MeleeDefinition Resolve(string id, float damageMultiplier,
                                               float swingTimeMultiplier,
                                               float rangeMultiplier) =>
            MeleeDefinition.Resolve(id, id, string.Empty,
                                    BaseDamage, BaseRange, BaseArc, BaseCooldown, BaseWindup,
                                    damageMultiplier, swingTimeMultiplier, rangeMultiplier,
                                    price: 0);

        [Test]
        public void AC1_HancerTabaninAynisidir()
        {
            MeleeDefinition dagger = Resolve("melee.dagger", 1f, 1f, 1f);

            Assert.AreEqual(BaseDamage, dagger.Damage, 0.001f);
            Assert.AreEqual(BaseCooldown, dagger.CooldownSeconds, 0.001f);
            Assert.AreEqual(BaseWindup, dagger.WindupSeconds, 0.001f);
        }

        [Test]
        public void AC2_KilicHancerinBirBucukKatiHasarVerir()
        {
            MeleeDefinition dagger = Resolve("melee.dagger", 1f, 1f, 1f);
            MeleeDefinition sword = Resolve("melee.sword", 1.5f, 1f, 1.2f);

            Assert.AreEqual(dagger.Damage * 1.5f, sword.Damage, 0.001f);

            // Ritim AYNI kalmali: kilicin farki guc, hiz degil.
            Assert.AreEqual(dagger.CooldownSeconds, sword.CooldownSeconds, 0.001f);
        }

        [Test]
        public void AC3_BaltaIkiBucukKatVururAmaIkiKatUzunSavrulur()
        {
            MeleeDefinition dagger = Resolve("melee.dagger", 1f, 1f, 1f);
            MeleeDefinition axe = Resolve("melee.axe", 2.5f, 2f, 1.1f);

            Assert.AreEqual(dagger.Damage * 2.5f, axe.Damage, 0.001f);

            // SURENIN IKI PARCASI DA carpilir: yalnizca bekleme carpilsaydi balta
            // hancer hizinda savurup uzun dinlenirdi - "agir" degil "gecikmeli"
            // okunurdu.
            Assert.AreEqual(dagger.CooldownSeconds * 2f, axe.CooldownSeconds, 0.001f);
            Assert.AreEqual(dagger.WindupSeconds * 2f, axe.WindupSeconds, 0.001f);
        }

        [Test]
        public void AC4_KoniAcisiCarpilmaz()
        {
            MeleeDefinition axe = Resolve("melee.axe", 2.5f, 2f, 1.1f);

            // Genis bir koni bicagi ALAN silahina cevirir ve "bir savurus, bir hedef"
            // kuralini siler (PlayerMelee).
            Assert.AreEqual(BaseArc, axe.ArcDegrees, 0.001f);
        }

        [Test]
        public void SifirVeNegatifCarpanTabanaDuser()
        {
            // Bozuk bir katalog satiri hicbir zaman SIFIR HASARLI bir bicak
            // uretmemeli - o, V tusunun sessizce hicbir sey yapmadigi bir oyun demek.
            MeleeDefinition broken = Resolve("melee.broken", 0f, -1f, 0f);

            Assert.AreEqual(BaseDamage, broken.Damage, 0.001f);
            Assert.AreEqual(BaseCooldown, broken.CooldownSeconds, 0.001f);
            Assert.AreEqual(BaseRange, broken.RangeMeters, 0.001f);
        }

        [Test]
        public void IdBossaTanimGecersizdir()
        {
            var empty = new MeleeDefinition();

            Assert.IsFalse(empty.IsValid);
        }
    }
}
