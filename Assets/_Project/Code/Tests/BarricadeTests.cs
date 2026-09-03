using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// M1-08 barikat. Test edilen şey <b>oyuncuya kazandırılan zaman</b>: barikat
    /// zombiyi durdurmaz, geciktirir.
    /// </summary>
    public sealed class BarricadeTests
    {
        private static Barricade Make(
            int perWindow = 6,
            int startingCount = 6,
            float zombieSecondsPerBoard = 1.8f,
            int beforeEntry = 1,
            float repairSecondsPerBoard = 0.9f)
        {
            return new Barricade(new BarricadeConfig(
                boardsPerWindow: perWindow,
                boardsStartingCount: startingCount,
                boardsZombieSecondsPerBoard: zombieSecondsPerBoard,
                boardsBeforeEntry: beforeEntry,
                repairSecondsPerBoard: repairSecondsPerBoard));
        }

        // ---------------------------------------------------------------- sokme

        [Test]
        public void AC1_SureDolmadan_TahtaDusmez()
        {
            Barricade b = Make(zombieSecondsPerBoard: 1.8f);

            Assert.IsFalse(b.Tear(1f));
            Assert.AreEqual(6, b.Boards);
        }

        [Test]
        public void AC1_SureDolunca_BirTahtaDuser()
        {
            Barricade b = Make(zombieSecondsPerBoard: 1.8f);

            b.Tear(1f);

            Assert.IsTrue(b.Tear(1f), "tahtanin dustugu an bildirilmeli");
            Assert.AreEqual(5, b.Boards);
        }

        [Test]
        public void AC1_UzunKare_BarikatiTekSeferdeSupurmez()
        {
            // Yukleme ya da takilma yuzunden uzun bir kare, barikatin tamamini bir
            // anda goturmemeli.
            Barricade b = Make(perWindow: 6, zombieSecondsPerBoard: 1f);

            b.Tear(100f);

            Assert.AreEqual(5, b.Boards, "tek cagride en fazla bir tahta duser");
        }

        [Test]
        public void AC1_BosBarikat_DahaFazlaSokulemez()
        {
            Barricade b = Make(perWindow: 2, startingCount: 0);

            Assert.IsFalse(b.Tear(100f));
            Assert.AreEqual(0, b.Boards);
        }

        // ---------------------------------------------------------------- giris esigi

        [Test]
        public void AC2_TamBarikat_ZombiyiIceriAlmaz()
        {
            Barricade b = Make(startingCount: 6, beforeEntry: 1);

            Assert.IsFalse(b.AllowsEntry);
        }

        [Test]
        public void AC2_EsigeInince_ZombiSizabilir()
        {
            // beforeEntry = 1: bir tahta kalmisken zombi aradan gecer. Barikatin
            // tamamen bosalmasini beklemek onu mutlak bir duvar yapardi.
            Barricade b = Make(perWindow: 6, startingCount: 2, zombieSecondsPerBoard: 1f,
                               beforeEntry: 1);

            b.Tear(1f);

            Assert.AreEqual(1, b.Boards);
            Assert.IsTrue(b.AllowsEntry);
        }

        [Test]
        public void AC2_SifirEsik_MutlakDuvarDemek()
        {
            Barricade b = Make(perWindow: 3, startingCount: 1, zombieSecondsPerBoard: 1f,
                               beforeEntry: 0);

            Assert.IsFalse(b.AllowsEntry, "bir tahta kaldiysa hala giremez");

            b.Tear(1f);

            Assert.IsTrue(b.AllowsEntry);
        }

        // ---------------------------------------------------------------- tamir

        [Test]
        public void AC3_TamirSuresiDolunca_TahtaEklenir()
        {
            Barricade b = Make(perWindow: 6, startingCount: 2, repairSecondsPerBoard: 0.9f);

            Assert.IsFalse(b.Repair(0.5f));
            Assert.IsTrue(b.Repair(0.5f), "tahtanin takildigi an bildirilmeli - puan oraya yazilir");
            Assert.AreEqual(3, b.Boards);
        }

        [Test]
        public void AC3_TamBarikat_TamirEdilemez()
        {
            Barricade b = Make(startingCount: 6, perWindow: 6);

            Assert.IsFalse(b.Repair(10f));
            Assert.AreEqual(6, b.Boards);
        }

        [Test]
        public void AC3_UzunKare_BarikatiTekSeferdeDoldurmaz()
        {
            Barricade b = Make(perWindow: 6, startingCount: 0, repairSecondsPerBoard: 0.5f);

            b.Repair(100f);

            Assert.AreEqual(1, b.Boards);
        }

        [Test]
        public void AC3_YarimTamir_AraVerilince_SilinmezDurur()
        {
            // Oyuncu tamire ara verip geri dondugunde bastan baslamamali.
            Barricade b = Make(perWindow: 6, startingCount: 3, repairSecondsPerBoard: 1f);
            b.Repair(0.7f);

            b.Idle();

            Assert.IsTrue(b.Repair(0.35f), "yarim kalan is korunmali");
        }

        // ---------------------------------------------------------------- cakisma

        [Test]
        public void AC4_ZombiSokerkenTamirEdilirse_SokumIlerlemesiSifirlanir()
        {
            // Iki taraf da yarim tahta biriktirip sirayla tamamlasaydi kimin kazandigi
            // okunmaz olurdu.
            Barricade b = Make(perWindow: 6, startingCount: 3,
                               zombieSecondsPerBoard: 1f, repairSecondsPerBoard: 1f);

            b.Tear(0.9f);       // neredeyse sokuldu
            b.Repair(0.1f);     // oyuncu araya girdi

            Assert.IsFalse(b.Tear(0.5f), "sokum bastan baslamali");
            Assert.AreEqual(3, b.Boards);
        }

        [Test]
        public void AC4_OyuncuTamirEderkenZombiSokerse_TamirIlerlemesiSifirlanir()
        {
            Barricade b = Make(perWindow: 6, startingCount: 3,
                               zombieSecondsPerBoard: 1f, repairSecondsPerBoard: 1f);

            b.Repair(0.9f);
            b.Tear(0.1f);

            Assert.IsFalse(b.Repair(0.5f));
            Assert.AreEqual(3, b.Boards);
        }

        // ---------------------------------------------------------------- kenar durumlar

        [Test]
        public void SifirVeNegatifZaman_HicbirSeyDegistirmez()
        {
            Barricade b = Make(startingCount: 3);

            Assert.IsFalse(b.Tear(0f));
            Assert.IsFalse(b.Tear(-5f));
            Assert.IsFalse(b.Repair(0f));
            Assert.AreEqual(3, b.Boards);
        }

        [Test]
        public void BaslangicTahtasi_KapasiteyiAsamaz()
        {
            Barricade b = Make(perWindow: 4, startingCount: 99);

            Assert.AreEqual(4, b.Boards);
        }

        [Test]
        public void Reset_BarikatiBaslangicHalineDondurur()
        {
            Barricade b = Make(perWindow: 6, startingCount: 6, zombieSecondsPerBoard: 0.1f);
            for (int i = 0; i < 6; i++) b.Tear(0.2f);
            Assert.AreEqual(0, b.Boards);

            b.Reset();

            Assert.AreEqual(6, b.Boards);
            Assert.IsFalse(b.AllowsEntry);
        }

        [Test]
        public void IlerlemeOranlari_SifirdanBire_Yurur()
        {
            Barricade b = Make(zombieSecondsPerBoard: 2f, repairSecondsPerBoard: 2f,
                               perWindow: 6, startingCount: 3);

            b.Tear(1f);
            Assert.AreEqual(0.5f, b.TearProgress01, 0.02f);

            b.Repair(0.5f);
            Assert.AreEqual(0.25f, b.RepairProgress01, 0.02f);
            Assert.AreEqual(0f, b.TearProgress01, 0.001f, "tamir sokumu sifirladi");
        }
    }
}
