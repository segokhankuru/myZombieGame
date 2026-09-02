using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// M1-06 silah kuralları. <b>His oynayarak ayarlanır, aritmetik okunarak
    /// doğrulanır</b> — oyun testinde aranacak şey hissiyat olsun, mermi sayısı değil.
    /// </summary>
    public sealed class WeaponStateTests
    {
        private static WeaponState Weapon(
            float roundsPerMinute = 600f,   // 0.1 sn/atis - test aritmetigi kolay olsun
            float damage = 55f,
            float headshotMultiplier = 2f,
            int capacity = 12,
            int startingReserve = 120,
            int reserveCapacity = 300,
            float reloadSeconds = 1.6f,
            float inputBuffer = 0.15f)
        {
            return new WeaponState(new WeaponConfig(
                fireRoundsPerMinute: roundsPerMinute,
                fireDamage: damage,
                fireHeadshotMultiplier: headshotMultiplier,
                magazineCapacity: capacity,
                magazineStartingReserve: startingReserve,
                magazineReserveCapacity: reserveCapacity,
                magazineReloadSeconds: reloadSeconds,
                feelInputBufferSeconds: inputBuffer));
        }

        // ---------------------------------------------------------------- ates

        [Test]
        public void AC1_TetigeBasinca_MermiCikarVeSarjordenDuser()
        {
            WeaponState w = Weapon(capacity: 12);

            FireResult result = w.TryFire(true);

            Assert.AreEqual(FireResult.Fired, result);
            Assert.AreEqual(11, w.RoundsInMagazine);
        }

        [Test]
        public void AC1_AtisHiziBeklenir_AyniKaredeIkiMermiCikmaz()
        {
            WeaponState w = Weapon(roundsPerMinute: 600f);
            w.TryFire(true);

            Assert.AreEqual(FireResult.Cycling, w.TryFire(true));
            Assert.AreEqual(11, w.RoundsInMagazine);
        }

        [Test]
        public void AC1_BeklemeDolunca_TekrarAtesEdilir()
        {
            WeaponState w = Weapon(roundsPerMinute: 600f);
            w.TryFire(true);

            w.Tick(0.11f);

            Assert.AreEqual(FireResult.Fired, w.TryFire(true));
            Assert.AreEqual(10, w.RoundsInMagazine);
        }

        [Test]
        public void AC1_AtisHizi_DakikadakiMermiSayisinaUyar()
        {
            WeaponState w = Weapon(roundsPerMinute: 400f, capacity: 100);

            Assert.AreEqual(0.15f, w.SecondsBetweenShots, 0.001f);
        }

        // ---------------------------------------------------------------- girdi tamponu

        [Test]
        public void AC2_BeklemeSirasindaBasilanTetik_HafizadaTutulur()
        {
            // "Bastim ama atmadi", oyunun olu hissettirmesinin en yaygin sebebidir
            // (gameplay-code.md).
            WeaponState w = Weapon(roundsPerMinute: 600f, inputBuffer: 0.15f);
            w.TryFire(true);              // 1. atis
            w.TryFire(true);              // bekleme sirasinda basildi -> tamponlandi

            w.Tick(0.11f);

            Assert.AreEqual(FireResult.Fired, w.TryFire(false),
                "tampon sayesinde tusa tekrar basmadan atis cikmali");
        }

        [Test]
        public void AC2_TamponSuresiDolunca_IstekDusurulur()
        {
            WeaponState w = Weapon(roundsPerMinute: 600f, inputBuffer: 0.05f);
            w.TryFire(true);
            w.TryFire(true);              // tamponlandi

            w.Tick(0.2f);                 // tampon suresi doldu

            Assert.AreNotEqual(FireResult.Fired, w.TryFire(false),
                "eski bir istek sonsuza kadar bekleyemez");
        }

        [Test]
        public void AC2_TamponsuzSilah_EskiIstegiTasimaz()
        {
            WeaponState w = Weapon(roundsPerMinute: 600f, inputBuffer: 0f);
            w.TryFire(true);
            w.TryFire(true);

            w.Tick(0.11f);

            Assert.AreNotEqual(FireResult.Fired, w.TryFire(false));
        }

        // ---------------------------------------------------------------- sarjor ve dolum

        [Test]
        public void AC3_SarjorBitince_AtesEdilemez()
        {
            WeaponState w = Weapon(capacity: 2, roundsPerMinute: 6000f);
            w.TryFire(true); w.Tick(0.02f);
            w.TryFire(true); w.Tick(0.02f);

            Assert.AreEqual(0, w.RoundsInMagazine);
            Assert.AreEqual(FireResult.Empty, w.TryFire(true));
        }

        [Test]
        public void AC3_DolumSuresiDolunca_SarjorDolarVeYedekAzalir()
        {
            WeaponState w = Weapon(capacity: 12, startingReserve: 30, reloadSeconds: 1.6f);
            for (int i = 0; i < 5; i++) { w.TryFire(true); w.Tick(0.11f); }

            Assert.IsTrue(w.TryStartReload());
            w.Tick(1.7f);

            Assert.AreEqual(12, w.RoundsInMagazine);
            Assert.AreEqual(25, w.Reserve, "eksik olan 5 mermi yedekten alinir");
        }

        [Test]
        public void AC3_DolumSirasinda_AtesEdilemez()
        {
            WeaponState w = Weapon(capacity: 12, reloadSeconds: 1.6f);
            w.TryFire(true); w.Tick(0.11f);
            w.TryStartReload();

            Assert.AreEqual(FireResult.Reloading, w.TryFire(true));
        }

        [Test]
        public void AC3_YarideKesilenDolum_HicOlmamisSayilir()
        {
            // Aksi halde tusa basip birakarak sonsuz mermi uretilebilirdi.
            WeaponState w = Weapon(capacity: 12, startingReserve: 30, reloadSeconds: 1.6f);
            for (int i = 0; i < 5; i++) { w.TryFire(true); w.Tick(0.11f); }
            w.TryStartReload();
            w.Tick(1.5f);

            w.CancelReload();

            Assert.AreEqual(7, w.RoundsInMagazine, "yarim dolum mermi vermez");
            Assert.AreEqual(30, w.Reserve, "yedekten de dusmez");
        }

        [Test]
        public void AC3_DoluSarjor_BosunaDoldurulmaz()
        {
            // Bosuna baslatilan bir dolum, oyuncuyu sebepsiz savunmasiz birakir.
            WeaponState w = Weapon(capacity: 12);

            Assert.IsFalse(w.TryStartReload());
        }

        [Test]
        public void AC3_YedekYoksa_DolumBaslamaz()
        {
            WeaponState w = Weapon(capacity: 2, startingReserve: 0, roundsPerMinute: 6000f);
            w.TryFire(true); w.Tick(0.02f);
            w.TryFire(true); w.Tick(0.02f);

            Assert.IsFalse(w.TryStartReload());
            Assert.AreEqual(WeaponPhase.Dry, w.Phase);
            Assert.AreEqual(FireResult.Dry, w.TryFire(true));
        }

        [Test]
        public void AC3_YedekEksikse_OlanKadarDoldurulur()
        {
            WeaponState w = Weapon(capacity: 12, startingReserve: 3, roundsPerMinute: 6000f);
            for (int i = 0; i < 12; i++) { w.TryFire(true); w.Tick(0.02f); }

            w.TryStartReload();
            w.Tick(2f);

            Assert.AreEqual(3, w.RoundsInMagazine);
            Assert.AreEqual(0, w.Reserve);
        }

        [Test]
        public void AC3_DolumIlerlemesi_SifirdanBire_Yurur()
        {
            WeaponState w = Weapon(capacity: 12, reloadSeconds: 2f);
            w.TryFire(true); w.Tick(0.11f);
            w.TryStartReload();

            w.Tick(1f);

            Assert.AreEqual(0.5f, w.ReloadProgress01, 0.02f);
        }

        // ---------------------------------------------------------------- yedek mermi

        [Test]
        public void AC4_YedekTavani_Asilmaz()
        {
            WeaponState w = Weapon(startingReserve: 120, reserveCapacity: 300);

            int added = w.AddReserve(500);

            Assert.AreEqual(300, w.Reserve);
            Assert.AreEqual(180, added, "eklenen miktar gercekten sigan kadardir");
        }

        [Test]
        public void AC4_BaslangicYedegi_TavaniAsamaz()
        {
            WeaponState w = Weapon(startingReserve: 999, reserveCapacity: 100);

            Assert.AreEqual(100, w.Reserve);
        }

        [Test]
        public void AC4_NegatifMermi_Eklenmez()
        {
            WeaponState w = Weapon(startingReserve: 50);

            Assert.AreEqual(0, w.AddReserve(-20));
            Assert.AreEqual(50, w.Reserve);
        }

        // ---------------------------------------------------------------- hasar

        [Test]
        public void AC5_KafaVurusu_CarpanUygular()
        {
            WeaponState w = Weapon(damage: 55f, headshotMultiplier: 2f);

            Assert.AreEqual(55f, w.DamageFor(false), 0.001f);
            Assert.AreEqual(110f, w.DamageFor(true), 0.001f);
        }

        [Test]
        public void AC5_Tur1Zombisi_UcGovdeVurusuyla_Duser()
        {
            // Tasarim niyetinin korumasi: tur 1 zombisi 150 can. Bu oran degisirse
            // erken turlarin temposu degisir ve bu bilincli bir karar olmali.
            WeaponState w = Weapon(damage: 55f);

            Assert.Greater(w.DamageFor(false) * 3f, 150f, "uc govde vurusu oldurmeli");
            Assert.Less(w.DamageFor(false) * 2f, 150f, "iki vurus oldurmemeli");
        }

        // ---------------------------------------------------------------- kenar durumlar

        [Test]
        public void Reset_SilahiIlkHalineDondurur()
        {
            WeaponState w = Weapon(capacity: 12, startingReserve: 120);
            for (int i = 0; i < 5; i++) { w.TryFire(true); w.Tick(0.11f); }
            w.TryStartReload();

            w.Reset();

            Assert.AreEqual(12, w.RoundsInMagazine);
            Assert.AreEqual(120, w.Reserve);
            Assert.IsFalse(w.IsReloading);
            Assert.AreEqual(WeaponPhase.Ready, w.Phase);
        }

        [Test]
        public void TetigeBasilmadan_MermiHarcanmaz()
        {
            WeaponState w = Weapon();

            w.TryFire(false);
            w.Tick(1f);
            w.TryFire(false);

            Assert.AreEqual(12, w.RoundsInMagazine);
        }

        [Test]
        public void SifirVeNegatifZaman_DurumuIlerletmez()
        {
            WeaponState w = Weapon(roundsPerMinute: 600f);
            w.TryFire(true);

            w.Tick(0f);
            w.Tick(-5f);

            Assert.AreEqual(FireResult.Cycling, w.TryFire(true));
        }
    }
}
