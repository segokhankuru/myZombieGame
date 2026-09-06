using Bunker.Systems.Ai;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// 2026-09-05 oyun testi bulgusu: <i>"zombiler arka arkaya fazla ip gibi
    /// diziliyorlar, hedef alması kolay oluyor."</i>
    ///
    /// <para>Burada test edilen şey <b>dağılımın gerçekten dağılması</b>: arka arkaya
    /// doğan zombilerin farklı şeritlere düşmesi ve hepsinin aynı hızda gitmemesi.
    /// Rastgele olsaydı bu dosya yazılamazdı — ve rastgele bir dağılımın üç zombiyi
    /// aynı tarafa koyması hiç de nadir değildir.</para>
    /// </summary>
    public sealed class SwarmFormationTests
    {
        [Test]
        public void ArkaArkayaIkiZombi_ZitSeritlereDuser()
        {
            // Dizilimin daha ILK iki zombide bozulmasi sart: turun ilk saniyeleri
            // oyuncunun "bu sefer nasil gelecekler" sorusunu sordugu andir.
            float a = SwarmFormation.LateralOffsetMeters(1, 2.5f);
            float b = SwarmFormation.LateralOffsetMeters(2, 2.5f);

            Assert.Greater(a, 0f);
            Assert.Less(b, 0f);
        }

        [Test]
        public void Seritler_SpreadDisinaTasmaz()
        {
            for (int i = 0; i < 50; i++)
            {
                float lateral = SwarmFormation.LateralOffsetMeters(i, 2.5f);

                Assert.LessOrEqual(System.Math.Abs(lateral), 2.5f + 0.001f,
                    $"index {i} seridi spread disina tasti");
            }
        }

        [Test]
        public void BesZombi_BesFarkliSeride_Dagilir()
        {
            var seen = new System.Collections.Generic.HashSet<float>();

            for (int i = 0; i < SwarmFormation.LaneCount; i++)
            {
                seen.Add(SwarmFormation.LateralOffsetMeters(i, 2.5f));
            }

            Assert.AreEqual(SwarmFormation.LaneCount, seen.Count,
                "Bes ardisik zombi bes AYRI seride dusmeli.");
        }

        [Test]
        public void SpreadSifirsa_KaymaYok()
        {
            // Ayari sifirlamak eski davranisa donmenin yolu olmali: bir tuning
            // degerinin kapatilabilir olmasi, oyun testinde A/B yapmayi mumkun kilar.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(0f, SwarmFormation.LateralOffsetMeters(i, 0f));
            }
        }

        // ---------------------------------------------------------------- hiz

        [Test]
        public void HizSapmasi_BandinIcindeKalir()
        {
            for (int i = 0; i < 50; i++)
            {
                float multiplier = SwarmFormation.SpeedMultiplier(i, 0.12f);

                Assert.GreaterOrEqual(multiplier, 0.88f - 0.001f);
                Assert.LessOrEqual(multiplier, 1.12f + 0.001f);
            }
        }

        [Test]
        public void HizSapmasi_ArdisikZombilerdeFarkli()
        {
            // Ayni hizda giden zombiler konvoy halinde kalir: aradaki mesafe hic
            // degismez ve dizilim kendiliginden bozulmaz.
            Assert.AreNotEqual(SwarmFormation.SpeedMultiplier(0, 0.12f),
                               SwarmFormation.SpeedMultiplier(1, 0.12f));
        }

        [Test]
        public void HizSapmasi_SifirsaHerkesAyniHizda()
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(1f, SwarmFormation.SpeedMultiplier(i, 0f));
            }
        }

        // ---------------------------------------------------------------- sonme

        [Test]
        public void Serit_YakindaSoner()
        {
            // Sonmeseydi zombi oyuncunun iki metre yanina gidip orada dururdu:
            // saldiramayan bir zombi tehdit degil dekordur.
            Assert.AreEqual(0f, SwarmFormation.SpreadWeight01(2f, 4f));
            Assert.AreEqual(0f, SwarmFormation.SpreadWeight01(4f, 4f));
        }

        [Test]
        public void Serit_UzaktaTamGenislik()
        {
            Assert.AreEqual(1f, SwarmFormation.SpreadWeight01(8f, 4f), 0.001f);
            Assert.AreEqual(1f, SwarmFormation.SpreadWeight01(30f, 4f), 0.001f);
        }

        [Test]
        public void Serit_AradaYUMUSAKGecer()
        {
            // Bir esikte aniden sifirlanan serit, zombinin yana sicramasi olarak
            // gorunurdu - okunabilirlik (PILLAR-04) burada kirilirdi.
            float mid = SwarmFormation.SpreadWeight01(6f, 4f);

            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
        }

        // ---------------------------------------------------------------- oncelik

        [Test]
        public void KacinmaOnceligi_ArdisikZombilerdeFarkli()
        {
            // Esit oncelikli ajanlar birbirini itmez, KUYRUGA girer - tek sira
            // diziliminin ucuncu sebebi buydu.
            Assert.AreNotEqual(SwarmFormation.AvoidancePriority(0),
                               SwarmFormation.AvoidancePriority(1));
        }

        [Test]
        public void KacinmaOnceligi_UcDegerlerdenUzakDurur()
        {
            for (int i = 0; i < 200; i++)
            {
                int priority = SwarmFormation.AvoidancePriority(i);

                Assert.GreaterOrEqual(priority, 1, "0 'hic kacinma' gibi davranir");
                Assert.LessOrEqual(priority, 98, "99 ajani tamamen ezilebilir yapar");
            }
        }
    }
}
