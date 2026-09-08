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

        // ---------------------------------------------------------------- sarjor karti

        /// <summary>
        /// Şarjör kartı <b>oransal</b> (2026-09-07): aynı kart her silahta aynı vaadi
        /// tutar. Mutlak mermiyken +6, tabancada +%50 pompalıda +%100 ediyordu ve
        /// kartın metni her silahta başka bir şey söylüyordu.
        /// </summary>
        [Test]
        public void SarjorKarti_ORANSAL_HerSilahtaAyniVaat()
        {
            WeaponState pistol = Weapon(capacity: 12);
            WeaponState shotgun = Shotgun(capacity: 6);

            var plusForty = new WeaponModifiers(
                fireRate: 0f, reloadSpeed: 0f, damage: 0f,
                magazine: 0.40f, headshotMultiplier: 0f);

            pistol.ApplyModifiers(plusForty);
            shotgun.ApplyModifiers(plusForty);

            Assert.AreEqual(17, pistol.MagazineCapacity, "12 * 1.40 = 16.8 -> 17");
            Assert.AreEqual(9, shotgun.MagazineCapacity, "6 * 1.40 = 8.4 -> 9");
        }

        /// <summary>
        /// Yukarı yuvarlama bilinçli: küçük şarjörlü bir silahta aşağı yuvarlamak,
        /// kartı sessizce etkisiz bırakırdı — oyuncunun seçtiği ve hiçbir şey
        /// hissetmediği kart, kart sisteminin en pahalı hatası.
        /// </summary>
        [Test]
        public void SarjorKarti_KucukSilahta_EnAzBirMermi()
        {
            WeaponState shotgun = Shotgun(capacity: 6);

            shotgun.ApplyModifiers(new WeaponModifiers(
                fireRate: 0f, reloadSpeed: 0f, damage: 0f,
                magazine: 0.10f, headshotMultiplier: 0f));

            Assert.AreEqual(7, shotgun.MagazineCapacity, "6 * 1.10 = 6.6 -> 7");
        }

        [Test]
        public void SarjorKarti_SarjordekiMermiyi_KENDILIGINDEN_Doldurmaz()
        {
            WeaponState w = Weapon(capacity: 12);
            w.TryFire(true);
            w.Tick(1f);

            w.ApplyModifiers(new WeaponModifiers(
                fireRate: 0f, reloadSpeed: 0f, damage: 0f,
                magazine: 0.50f, headshotMultiplier: 0f));

            Assert.AreEqual(18, w.MagazineCapacity);
            Assert.AreEqual(11, w.RoundsInMagazine, "kapasite buyudu diye bedava dolum olmaz");
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

        /// <summary>
        /// 2026-09-07: yedek merminin TAVANI KALDIRILDI. Eski iki test tavanın
        /// çalıştığını doğruluyordu; artık kuralın kendisi yok, o yüzden yerlerine
        /// tavansızlığı doğrulayan bu test geçti. Kaldırılan bir kuralın testini
        /// bırakmak, bir sonraki okuyanın hangisinin geçerli olduğunu bilememesi
        /// demektir.
        /// </summary>
        [Test]
        public void AC4_YedekTavani_YOK_EklenenHerMermiGirer()
        {
            WeaponState w = Weapon(startingReserve: 120, reserveCapacity: 300);

            int added = w.AddReserve(500);

            Assert.AreEqual(620, w.Reserve, "tavan yok: eklenen her mermi yedege girer");
            Assert.AreEqual(500, added, "eklenen miktar, istenen miktardir");
        }

        [Test]
        public void AC4_BaslangicYedegi_KirpilmAZ()
        {
            WeaponState w = Weapon(startingReserve: 999, reserveCapacity: 100);

            Assert.AreEqual(999, w.Reserve, "baslangic yedegi bir tavana kirpilmaz");
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

        // ------------------------------------------------- pompali: mermi mermi dolum

        /// <summary>
        /// Pompalı: <c>reloadPerShell</c> açıkken <c>reloadSeconds</c> TEK BIR
        /// merminin süresidir. Tanım katalogdan geldiği için burada elle kuruluyor —
        /// <see cref="WeaponConfig"/> başlangıç silahını (tabanca) tarif ediyor ve
        /// onun dolumu bölünmez.
        /// </summary>
        private static WeaponState Shotgun(int capacity = 6, int startingReserve = 36,
                                           float shellSeconds = 0.45f)
        {
            return new WeaponState(new WeaponDefinition(
                "weapon.shotgun", "POMPALI",
                damage: 24f, roundsPerMinute: 90f, headshotMultiplier: 1.4f,
                rangeMeters: 18f, spreadDegrees: 3f, pelletCount: 8,
                magazineCapacity: capacity, reserveCapacity: 90,
                startingReserve: startingReserve, reloadSeconds: shellSeconds,
                recoilPitchPerShot: 4f, recoilYawPerShot: 0.8f,
                recoilRecoveryPerSecond: 16f, recoilMaxPitch: 12f,
                tracerSeconds: 0.05f, hitMarkerSeconds: 0.12f, inputBufferSeconds: 0.15f,
                price: 1200, ammoPrice: 400, reloadPerShell: true));
        }

        /// <summary>Şarjörü boşaltır. Dolum testlerinin ortak başlangıcı.</summary>
        private static void Empty(WeaponState w)
        {
            while (w.RoundsInMagazine > 0)
            {
                w.TryFire(true);
                w.Tick(1f);
            }
        }

        [Test]
        public void Pompali_BirAdimda_TEK_MermiGirer()
        {
            WeaponState w = Shotgun();
            Empty(w);

            Assert.IsTrue(w.TryStartReload());
            w.Tick(0.45f);

            Assert.AreEqual(1, w.RoundsInMagazine, "bir adim = bir fisek");
            Assert.AreEqual(35, w.Reserve);
        }

        [Test]
        public void Pompali_KesilmezseSarjorDolanaKadarDevamEder()
        {
            WeaponState w = Shotgun(capacity: 6);
            Empty(w);

            w.TryStartReload();

            // Alti adim: 6 x 0.45 = 2.7 sn. Tick'ler ayri ayri, cunku her adim
            // tamamlandiginda bir sonraki BASLATILIYOR.
            for (int i = 0; i < 6; i++) w.Tick(0.45f);

            Assert.AreEqual(6, w.RoundsInMagazine);
            Assert.IsFalse(w.IsReloading, "sarjor dolunca dolum durur");
        }

        [Test]
        public void Pompali_YedekBitince_DolumDurur()
        {
            WeaponState w = Shotgun(capacity: 6, startingReserve: 2);
            Empty(w);

            w.TryStartReload();
            for (int i = 0; i < 6; i++) w.Tick(0.45f);

            Assert.AreEqual(2, w.RoundsInMagazine, "yedekte iki fisek vardi");
            Assert.AreEqual(0, w.Reserve);
            Assert.IsFalse(w.IsReloading);
        }

        /// <summary>
        /// Pompalıyı pompalı yapan kural: <b>iki fişek koyup ateş edebilmek</b>. Bunu
        /// engellemek, dolumu bölünebilir yapmanın bütün anlamını götürür.
        /// </summary>
        [Test]
        public void Pompali_IkiFisektenSonra_AtesEdilebilir()
        {
            WeaponState w = Shotgun();
            Empty(w);

            w.TryStartReload();
            w.Tick(0.45f);
            w.Tick(0.45f);

            Assert.AreEqual(2, w.RoundsInMagazine);
            Assert.IsTrue(w.IsReloading, "ucuncu fisek yolda");

            Assert.AreEqual(FireResult.Fired, w.TryFire(true));
            Assert.AreEqual(1, w.RoundsInMagazine);
            Assert.IsFalse(w.IsReloading, "ates dolumu KESER");
        }

        [Test]
        public void Pompali_SarjorBOSKEN_AtesEtmekDolumuKesmez()
        {
            WeaponState w = Shotgun();
            Empty(w);

            w.TryStartReload();

            // Ilk fisek daha girmedi: kesecek bir sey yok, silah dolumda kalmali.
            Assert.AreEqual(FireResult.Reloading, w.TryFire(true));
            Assert.IsTrue(w.IsReloading);
        }

        [Test]
        public void TekParcaDolum_DEGISMEDI_TabancaSarjoruBirKeredeDolar()
        {
            WeaponState w = Weapon(capacity: 12, reloadSeconds: 1.6f);
            Empty(w);

            w.TryStartReload();
            w.Tick(1.6f);

            Assert.AreEqual(12, w.RoundsInMagazine, "tabancanin dolumu bolunmez");
            Assert.IsFalse(w.IsReloading);
        }
    }
}
