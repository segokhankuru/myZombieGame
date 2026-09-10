using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Sunucunun atış denetleyicisi (BUG-002'nin regresyon testleri).
    ///
    /// <para>Buradaki asıl soru: <b>dürüst bir oyuncunun tıkı yeniyor mu?</b> Ağ payı
    /// olmayan bir denetleyici, komut bir kare geç geldiği için meşru atışları reddeder
    /// ve bu doğrudan silahın hissiyatını bozar.</para>
    /// </summary>
    public sealed class ServerFireGuardTests
    {
        private const float Tolerance = 0.12f;

        private static ServerFireGuard Guard(
            float roundsPerMinute = 400f,   // 0.15 sn/atis
            int capacity = 12,
            int startingReserve = 120,
            int reserveCapacity = 300,
            float reloadSeconds = 1.6f,
            float damage = 55f,
            float headshotMultiplier = 2f)
        {
            return new ServerFireGuard(new WeaponConfig(
                fireRoundsPerMinute: roundsPerMinute,
                fireDamage: damage,
                fireHeadshotMultiplier: headshotMultiplier,
                magazineCapacity: capacity,
                magazineStartingReserve: startingReserve,
                magazineReserveCapacity: reserveCapacity,
                magazineReloadSeconds: reloadSeconds), Tolerance);
        }

        // ---------------------------------------------------------------- BUG-002

        [Test]
        public void BUG002_BirKareErkenGelenAtis_Reddedilmez()
        {
            // Komut agdan bir kare sonra gelir; sunucunun saati istemciyle birebir
            // ayni degildir. Tolerans olmadan bu mesru atis reddedilirdi ve oyuncu
            // "arada bir tik yeniyor" derdi.
            ServerFireGuard g = Guard(roundsPerMinute: 400f);   // 0.15 sn

            Assert.AreEqual(FireRejection.None, g.TryAcceptShot(0f));
            Assert.AreEqual(FireRejection.None, g.TryAcceptShot(0.15f - 0.03f),
                "bir kare erken gelen atis kabul edilmeli");
        }

        [Test]
        public void BUG002_DolumBitmeden_BirKareErkenGelenAtis_Reddedilmez()
        {
            ServerFireGuard g = Guard(capacity: 2, roundsPerMinute: 6000f, reloadSeconds: 1.6f);
            g.TryAcceptShot(0f);
            g.TryAcceptShot(0.02f);

            g.NoteReload(0.03f);

            Assert.AreEqual(FireRejection.None, g.TryAcceptShot(0.03f + 1.6f - 0.05f),
                "dolumun son karesinde gelen atis yenmez");
        }

        [Test]
        public void ToleransSiniri_Asilirsa_ReddedilirHilePenceresiSinirliKalir()
        {
            ServerFireGuard g = Guard(roundsPerMinute: 400f);   // 0.15 sn
            g.TryAcceptShot(0f);

            Assert.AreEqual(FireRejection.TooFast, g.TryAcceptShot(0.01f),
                "tolerans bir kare icindir, sinirsiz atis hakki degil");
        }

        // ---------------------------------------------------------------- BUG-001

        [Test]
        public void BUG001_DolumBildirilince_SunucuninMermisiGeriGelir()
        {
            // Ilk surumde bu yol hic yoktu: sunucunun sarjoru ilk sarjorden sonra
            // sonsuza kadar bos kaliyor ve butun atislar sessizce dusuyordu.
            ServerFireGuard g = Guard(capacity: 3, roundsPerMinute: 6000f, reloadSeconds: 1f);

            for (int i = 0; i < 3; i++) g.TryAcceptShot(i * 0.02f);
            Assert.AreEqual(FireRejection.NoRounds, g.TryAcceptShot(0.1f));

            g.NoteReload(0.1f);

            Assert.AreEqual(FireRejection.None, g.TryAcceptShot(1.2f));
        }

        [Test]
        public void BUG001_UzunAtisDizisi_DolumlarlaBirlikte_HicReddedilmez()
        {
            // Oyun testindeki senaryonun aynisi: uc sarjor dolusu atis.
            ServerFireGuard g = Guard(capacity: 12, roundsPerMinute: 400f, reloadSeconds: 1.6f);

            float t = 0f;
            int rejected = 0;

            for (int magazine = 0; magazine < 3; magazine++)
            {
                for (int shot = 0; shot < 12; shot++)
                {
                    if (g.TryAcceptShot(t) != FireRejection.None) rejected++;
                    t += 0.15f;
                }

                g.NoteReload(t);
                t += 1.6f;
            }

            Assert.AreEqual(0, rejected, "dolum bildirilen bir silahta hicbir atis yenmemeli");
        }

        // ---------------------------------------------------------------- hile onleme

        [Test]
        public void DolumSpami_AnindaDolumVermez()
        {
            ServerFireGuard g = Guard(capacity: 2, roundsPerMinute: 6000f, reloadSeconds: 1.6f);
            g.TryAcceptShot(0f);
            g.TryAcceptShot(0.02f);

            // Istemci saniyede yuz kez "doldurdum" desin.
            for (int i = 0; i < 100; i++) g.NoteReload(0.03f + i * 0.01f);

            Assert.AreEqual(FireRejection.NoRounds, g.TryAcceptShot(0.5f),
                "her bildirim sureyi bastan baslatir; spam anlik dolum vermez");
        }

        [Test]
        public void SonsuzHizliAtis_SurekliBirAvantajVermez()
        {
            // Hile senaryosu: istemci saniyede bin kez ates ettigini soyluyor.
            // Tolerans BIRIKMEMELI - ilk surumde her atistan ayri ayri dusuluyordu ve
            // 400 RPM'lik silah fiilen 2000 RPM atiyordu. Bu testin yakaladigi seydi.
            ServerFireGuard g = Guard(roundsPerMinute: 400f, capacity: 1000);

            int accepted = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (g.TryAcceptShot(i * 0.001f) == FireRejection.None) accepted++;
            }

            // Bir saniyede 400 RPM = 6.7 atis. Tolerans bir kerelik bir one gecme
            // hakki verir, o yuzden tavan 8.
            Assert.LessOrEqual(accepted, 8,
                $"bir saniyede {accepted} atis gecti; 400 RPM'de tavan 8 olmali");
            Assert.GreaterOrEqual(accepted, 6, "durust bir oyuncunun atisi da yenmemeli");
        }

        [Test]
        public void YedekBitince_DolumSarjoruDoldurmaz()
        {
            ServerFireGuard g = Guard(capacity: 2, startingReserve: 0,
                                      roundsPerMinute: 6000f, reloadSeconds: 0.5f);
            g.TryAcceptShot(0f);
            g.TryAcceptShot(0.02f);

            g.NoteReload(0.03f);

            Assert.AreEqual(FireRejection.NoReserve, g.TryAcceptShot(1f));
        }

        [Test]
        public void YedekEksikse_OlanKadarDoldurulur()
        {
            ServerFireGuard g = Guard(capacity: 10, startingReserve: 3,
                                      roundsPerMinute: 6000f, reloadSeconds: 0.5f);
            for (int i = 0; i < 10; i++) g.TryAcceptShot(i * 0.02f);

            g.NoteReload(0.3f);
            g.TryAcceptShot(1f);

            Assert.AreEqual(2, g.RoundsInMagazine);
            Assert.AreEqual(0, g.Reserve);
        }

        /// <summary>
        /// 2026-09-07: yedek tavanı kaldırıldı (bkz. <c>WeaponState.AddReserve</c>).
        /// Sunucu doğrulayıcısının istemciyle <b>aynı</b> kuralı uygulaması şart —
        /// biri kırpıp diğeri kırpmasaydı iki taraf farklı mermi sayardı.
        /// </summary>
        [Test]
        public void YedekTavani_YOK_IstemciyleAyniKural()
        {
            ServerFireGuard g = Guard(startingReserve: 120, reserveCapacity: 300);

            g.AddReserve(500);

            Assert.AreEqual(620, g.Reserve);
        }

        [Test]
        public void KafaVurusuCarpani_SunucudaUygulanir()
        {
            ServerFireGuard g = Guard(damage: 55f, headshotMultiplier: 2f);

            Assert.AreEqual(55f, g.DamageFor(false), 0.001f);
            Assert.AreEqual(110f, g.DamageFor(true), 0.001f);
        }

        [Test]
        public void Reset_YeniRunIcinTemizler()
        {
            ServerFireGuard g = Guard(capacity: 3, roundsPerMinute: 6000f);
            for (int i = 0; i < 3; i++) g.TryAcceptShot(i * 0.02f);

            g.Reset();

            Assert.AreEqual(3, g.RoundsInMagazine);
            Assert.AreEqual(FireRejection.None, g.TryAcceptShot(0.1f));
        }

        // ------------------------------------------- atis hizi karti (2026-09-06)

        /// <summary>
        /// Kart atış hızını artırdığında <b>sunucunun hız sınırı da hızlanmalı</b>.
        ///
        /// <para><b>Bulundugu yer bir oyun logu</b>: onlarca satır
        /// <c>"Sunucu atisi reddetti: TooFast"</c>. Sınırlayıcının aralığı kurucuda
        /// sabitleniyordu; istemci kartla hızlanıyor, sunucu eski aralıkta kalıyordu.
        /// Ağ payı birikmediği için izinli an yavaşça öne kaçıyor ve bir noktadan
        /// sonra <b>her atış</b> reddediliyordu — oyuncu tarafında "silahım hasar
        /// vermiyor".</para>
        /// </summary>
        [Test]
        public void BUG_AtisHiziKartiSonrasi_MesruAtislarReddedilmez()
        {
            // 400 atis/dk = 0.15 sn. Kart +%50 -> 0.10 sn.
            ServerFireGuard guard = Guard(roundsPerMinute: 400f, capacity: 200,
                                          startingReserve: 0, reserveCapacity: 0);

            guard.ApplyModifiers(new WeaponModifiers(
                fireRate: 0.5f, reloadSpeed: 0f, damage: 0f,
                magazine: 0f, reserve: 0, headshotMultiplier: 0f));

            // Istemcinin surdugu ritimle otuz atis. Bir tanesi bile reddedilirse
            // hata geri gelmis demektir - reddedilenler birikerek gelir.
            float now = 100f;

            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual(FireRejection.None, guard.TryAcceptShot(now),
                                $"{i}. atis reddedildi - sunucu hala eski aralikta");
                now += 0.10f;
            }
        }

        [Test]
        public void AtisHiziKartiSonrasi_HALA_COK_HIZLI_ATIS_REDDEDILIR()
        {
            ServerFireGuard guard = Guard(roundsPerMinute: 400f, capacity: 200,
                                          startingReserve: 0, reserveCapacity: 0);

            guard.ApplyModifiers(new WeaponModifiers(
                fireRate: 0.5f, reloadSpeed: 0f, damage: 0f,
                magazine: 0f, reserve: 0, headshotMultiplier: 0f));

            // Izin 0.10 sn; 0.01 sn araliklarla otuz atis acikca hile.
            float now = 100f;
            int rejected = 0;

            for (int i = 0; i < 30; i++)
            {
                if (guard.TryAcceptShot(now) == FireRejection.TooFast) rejected++;
                now += 0.01f;
            }

            Assert.Greater(rejected, 15, "hiz siniri gevsedi - kart bir hile kapisi oldu");
        }
    }
}
