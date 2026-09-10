using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Yedek mermi dört kuralı (2026-09-10, geliştirici). Kural
    /// <see cref="ReserveAmmo"/>'da tek yerde; test adları kural numarasıyla başlar.
    ///
    /// <para><b>K1</b> tur sonu tavanın yarısı · <b>K2</b> ikmal tavanı aşmaz ·
    /// <b>K3</b> tavan kartla büyür · <b>K4</b> tavanın üstüne yalnızca satın alma,
    /// fazlalık tüketilince geri gelmez.</para>
    /// </summary>
    public sealed class ReserveAmmoTests
    {
        private const float Half = 0.5f;

        private static WeaponState Weapon(int startingReserve, int reserveCapacity = 300)
        {
            return new WeaponState(new WeaponConfig(
                magazineCapacity: 30,
                magazineStartingReserve: startingReserve,
                magazineReserveCapacity: reserveCapacity));
        }

        private static WeaponModifiers ReserveCard(int bonus) =>
            new WeaponModifiers(fireRate: 0f, reloadSpeed: 0f, damage: 0f,
                                magazine: 0f, reserve: bonus, headshotMultiplier: 0f);

        // ---------------------------------------------------------------- K1

        [Test]
        public void K1_TurSonu_TavaninYarisiGelir()
        {
            Assert.AreEqual(150, ReserveAmmo.RoundEndRestock(0, 300, Half));
        }

        /// <summary>Eksiğin yarısı olsaydı 100 gelirdi; kural tavanın yarısı.</summary>
        [Test]
        public void K1_IkmalEksiginDegil_TavaninOrani()
        {
            Assert.AreEqual(150, ReserveAmmo.RoundEndRestock(100, 300, Half));
        }

        // ---------------------------------------------------------------- K2

        [Test]
        public void K2_Ikmal_TavaniAsmaz()
        {
            Assert.AreEqual(50, ReserveAmmo.RoundEndRestock(250, 300, Half),
                            "250 + 150 = 400 degil, 300'e kadar");
        }

        [Test]
        public void K2_TavandaykenIkmalGelmez()
        {
            Assert.AreEqual(0, ReserveAmmo.RoundEndRestock(300, 300, Half));
        }

        // ---------------------------------------------------------------- K3

        [Test]
        public void K3_KartTavaniBuyutur_IkmalYeniTavandanHesaplanir()
        {
            WeaponState weapon = Weapon(startingReserve: 0, reserveCapacity: 300);

            weapon.ApplyModifiers(ReserveCard(200));

            Assert.AreEqual(500, weapon.ReserveCapacity);
            Assert.AreEqual(250, ReserveAmmo.RoundEndRestock(weapon.Reserve, weapon.ReserveCapacity, Half));
        }

        // ---------------------------------------------------------------- K4

        [Test]
        public void K4_SatinAlma_TavaniAsar()
        {
            WeaponState weapon = Weapon(startingReserve: 280, reserveCapacity: 300);

            weapon.AddReserve(90);

            Assert.AreEqual(370, weapon.Reserve, "odenen mermi kirpilmaz");
        }

        [Test]
        public void K4_BedavaMermi_TavaniAsmaz()
        {
            Assert.AreEqual(20, ReserveAmmo.UpToCapacity(280, 300, 24),
                            "yerden toplama / oldurme odulu tavana kadar doldurur");
        }

        [Test]
        public void K4_TavanUstundeyken_BedavaMermiFazlaligiGeriAlmaz()
        {
            Assert.AreEqual(0, ReserveAmmo.UpToCapacity(370, 300, 24));
            Assert.AreEqual(0, ReserveAmmo.RoundEndRestock(370, 300, Half));
        }

        [Test]
        public void K4_FazlalikTuketilince_TurSonuTavanaKadarDoldurur()
        {
            WeaponState weapon = Weapon(startingReserve: 280, reserveCapacity: 300);
            weapon.AddReserve(90);                       // satin alma: 370
            weapon.RemoveReserve(120);                   // harcandi: 250

            weapon.AddReserve(ReserveAmmo.RoundEndRestock(weapon.Reserve, weapon.ReserveCapacity, Half));

            Assert.AreEqual(300, weapon.Reserve, "fazlalik geri gelmez: 370 degil 300");
        }

        // ---------------------------------------------------------------- kenarlar

        [Test]
        public void Kenar_SifirVeNegatifMiktar_HicbirSeyEklemez()
        {
            Assert.AreEqual(0, ReserveAmmo.UpToCapacity(0, 300, 0));
            Assert.AreEqual(0, ReserveAmmo.UpToCapacity(0, 300, -5));
            Assert.AreEqual(0, ReserveAmmo.RoundEndRestock(0, 0, Half));
            Assert.AreEqual(0, ReserveAmmo.RoundEndRestock(0, 300, 0f));
            Assert.AreEqual(0, ReserveAmmo.RoundEndRestock(0, 300, -1f));
        }

        [Test]
        public void Kenar_BirdenBuyukOran_TavanaKirpilir()
        {
            Assert.AreEqual(300, ReserveAmmo.RoundEndRestock(0, 300, 2f));
        }

        [Test]
        public void Kenar_TekSayiliTavan_YukariYuvarlanir()
        {
            // Pompali 90 -> 45; 5 mermilik tavanda 2.5 -> 3 (asagi yuvarlamak kucuk
            // tavanli silahi her turda bir mermi eksik birakirdi).
            Assert.AreEqual(45, ReserveAmmo.RoundEndRestock(0, 90, Half));
            Assert.AreEqual(3, ReserveAmmo.RoundEndRestock(0, 5, Half));
        }
    }
}
