using Bunker.Systems.Pickups;
using NUnit.Framework;

namespace Bunker.Tests
{
    /// <summary>
    /// Eşya tablosu (2026-09-07). <b>EditMode</b>: sahne yok, saniyeler yok — yüz bin
    /// ölümün dağılımı milisaniyelerde ölçülür. Drop'un dengeli olup olmadığını
    /// oynayarak anlamak, on tur ve tek bir anekdot demekti.
    /// </summary>
    public sealed class DropTableTests
    {
        private static int[] Weights(int health = 25, int ammo = 40, int slow = 18,
                                     int freeze = 12, int nuke = 5) =>
            new[] { health, ammo, slow, freeze, nuke };

        [Test]
        public void SifirSans_HicDusurmez()
        {
            var table = new DropTable(0f, Weights(), seed: 1);

            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(table.TryRoll(1f, out _), "sans sifirken esya dusmemeli");
            }
        }

        [Test]
        public void TamSans_HerOlumdeDusurur()
        {
            var table = new DropTable(1f, Weights(), seed: 2);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(table.TryRoll(1f, out _));
            }
        }

        /// <summary>
        /// Ganimet kartı yalnızca ŞANSI çarpar. Bu testin koruduğu şey bir sayı değil,
        /// bir <i>kural</i>: ikinci bir zar atılsaydı kartın etkisi çarpandan bambaşka
        /// olurdu ve oyuncu sıklığı hiç öğrenemezdi.
        /// </summary>
        [Test]
        public void KartCarpani_SansiArtirir()
        {
            const int rolls = 20000;

            int withoutCard = CountDrops(new DropTable(0.10f, Weights(), seed: 7), 1f, rolls);
            int withCard = CountDrops(new DropTable(0.10f, Weights(), seed: 7), 1.6f, rolls);

            Assert.Greater(withCard, withoutCard, "kart drop sansini artirmali");

            // Beklenen ~%10 ve ~%16. Genis bant: bu test dagilimin YONUNU korur,
            // rastgele sayi uretecinin tam degerlerini degil (test-code.md: kirilgan
            // olmayan iddia).
            Assert.That(withoutCard / (float)rolls, Is.EqualTo(0.10f).Within(0.02f));
            Assert.That(withCard / (float)rolls, Is.EqualTo(0.16f).Within(0.02f));
        }

        [Test]
        public void AgirliksizTur_HicCikmaz()
        {
            // Nuke kapali (agirlik 0): kirk bin zarda bir kez bile cikmamali.
            var table = new DropTable(1f, Weights(nuke: 0), seed: 11);

            for (int i = 0; i < 40000; i++)
            {
                table.TryRoll(1f, out PowerupKind kind);
                Assert.AreNotEqual(PowerupKind.Nuke, kind);
            }
        }

        [Test]
        public void NukeEnNadirdir_MermiEnSiktir()
        {
            var table = new DropTable(1f, Weights(), seed: 13);

            var counts = new int[5];

            for (int i = 0; i < 40000; i++)
            {
                table.TryRoll(1f, out PowerupKind kind);
                counts[(int)kind]++;
            }

            Assert.Greater(counts[(int)PowerupKind.Ammo], counts[(int)PowerupKind.Health],
                           "mermi en sik esya olmali");
            Assert.Less(counts[(int)PowerupKind.Nuke], counts[(int)PowerupKind.Freeze],
                        "nuke en nadir esya olmali");
        }

        [Test]
        public void AyniTohum_AyniDizi()
        {
            var a = new DropTable(0.5f, Weights(), seed: 42);
            var b = new DropTable(0.5f, Weights(), seed: 42);

            for (int i = 0; i < 500; i++)
            {
                bool dropA = a.TryRoll(1f, out PowerupKind kindA);
                bool dropB = b.TryRoll(1f, out PowerupKind kindB);

                Assert.AreEqual(dropA, dropB);
                Assert.AreEqual(kindA, kindB);
            }
        }

        [Test]
        public void BosTablo_HicDusurmez()
        {
            var table = new DropTable(1f, Weights(0, 0, 0, 0, 0), seed: 3);

            Assert.IsTrue(table.IsEmpty);
            Assert.IsFalse(table.TryRoll(1f, out _));
        }

        private static int CountDrops(DropTable table, float multiplier, int rolls)
        {
            int drops = 0;

            for (int i = 0; i < rolls; i++)
            {
                if (table.TryRoll(multiplier, out _)) drops++;
            }

            return drops;
        }
    }
}
