using System.IO;
using Bunker.Systems.Telemetry;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Savaş günlüğü. <b>Gerçek dosya sistemine karşı</b>, Unity açmadan
    /// (<see cref="RunLogWriterTests"/> ile aynı desen ve aynı gerekçe): yazıcının tek
    /// işi diske yazmak, sahte bir dosya sistemine karşı test etmek onu hiç test
    /// etmemek olurdu.
    /// </summary>
    public sealed class CombatLogTests
    {
        private string _directory;

        [SetUp]
        public void Setup()
        {
            _directory = Path.Combine(Path.GetTempPath(), "bunker-combatlog-" + Path.GetRandomFileName());
        }

        [TearDown]
        public void TearDown()
        {
            // Kurulan her sey yikilir (test-code.md): birakilan gecici klasorler
            // sonraki kosulari kirletir.
            CombatLog.Install(null);

            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private CombatLogWriter NewWriter() =>
            new CombatLogWriter(_directory, "session-test.log");

        [Test]
        public void AC1_KurulmadanCagrilarSessizceDuser()
        {
            CombatLog.Install(null);

            Assert.IsFalse(CombatLog.IsInstalled);

            // Patlamamali: olcum araci kurulmadiginda oyun yine calismali.
            CombatLog.Damage("Oyuncu", "TUFEK", "Zombi#1", "kafa", 130f, 20f, 150f);
            CombatLog.PlayerDamage("Zombi#1", null, 30f, 70f, 100f, false);
            CombatLog.PlayerDied("Zombi#1");
            CombatLog.Flush();

            Assert.Pass();
        }

        [Test]
        public void AC2_HasarSatiriVuraniHedefiVeKalanCaniYazar()
        {
            CombatLogWriter writer = NewWriter();
            CombatLog.Install(writer);

            CombatLog.SetRound(7);
            CombatLog.Damage("Oyuncu[TUFEK]", null, "Zombi#42", "kafa", 325f, 875f, 1200f);
            CombatLog.Flush();

            string text = File.ReadAllText(writer.FilePath);

            StringAssert.Contains("Oyuncu[TUFEK]", text);
            StringAssert.Contains("Zombi#42", text);
            StringAssert.Contains("kafa", text);
            StringAssert.Contains("325", text);
            StringAssert.Contains("875", text);
            StringAssert.Contains("tur 7", text);
        }

        [Test]
        public void AC3_OlumDokumuSonVuruslariAralariylaBirlikteYazar()
        {
            CombatLogWriter writer = NewWriter();
            CombatLog.Install(writer);

            // Dort vurus 0.3 saniyeye sigiyor - "tek yedim" diye okunan sey bu.
            CombatLog.SetClock(10.0f);
            CombatLog.PlayerDamage("Zombi#1", null, 30f, 70f, 100f, false);

            CombatLog.SetClock(10.1f);
            CombatLog.PlayerDamage("Zombi#2", null, 30f, 40f, 100f, false);

            CombatLog.SetClock(10.2f);
            CombatLog.PlayerDamage("Zombi#3", null, 30f, 10f, 100f, false);

            CombatLog.SetClock(10.3f);
            CombatLog.PlayerDamage("Zombi#4", null, 30f, 0f, 100f, true);

            CombatLog.PlayerDied("Zombi#4");

            string text = File.ReadAllText(writer.FilePath);

            StringAssert.Contains("Zombi#1", text);
            StringAssert.Contains("Zombi#4", text);

            // Asil bilgi: dort vurus, toplam 120 hasar, 0.3 saniye.
            StringAssert.Contains("120", text);
            StringAssert.Contains("0.3", text);
            StringAssert.Contains("4 vurusta", text);
        }

        [Test]
        public void AC4_OlumdenSonraHafizaSifirlanir()
        {
            CombatLogWriter writer = NewWriter();
            CombatLog.Install(writer);

            CombatLog.PlayerDamage("Zombi#1", null, 30f, 70f, 100f, false);
            CombatLog.PlayerDied("Zombi#1");

            // Ikinci olumde birinci run'in vuruslari GORUNMEMELI - CardLoadout.Reset
            // ile ayni sinif hata.
            CombatLog.PlayerDied("Barikat");

            string[] lines = File.ReadAllLines(writer.FilePath);

            // Ikinci olumden SONRAKI satirlar sayilir: oncekilerde Zombi#1 elbette
            // var (hasar satiri ve ilk olum dokumu). Sorulan soru "hafiza tasindi mi",
            // "hic yazildi mi" degil.
            int secondDeath = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("Barikat")) continue;

                secondDeath = i;
                break;
            }

            Assert.Greater(secondDeath, 0, "ikinci olum satiri yazilmali");

            for (int i = secondDeath; i < lines.Length; i++)
            {
                StringAssert.DoesNotContain("Zombi#1", lines[i],
                    "olum dokumunden sonra halka tamponu bosalmali");
            }
        }

        [Test]
        public void AC5_SayilarKulturdenBagimsizYazilir()
        {
            CombatLogWriter writer = NewWriter();
            CombatLog.Install(writer);

            CombatLog.Damage("Oyuncu", null, "Zombi#1", null, 12.5f, 37.5f, 150f);
            CombatLog.Flush();

            string text = File.ReadAllText(writer.FilePath);

            // Turkce Windows'ta 12.5f.ToString() "12,5" uretir ve dosya bir daha
            // ayristirilamaz. RunLogWriter'daki kultur savunmasinin aynisi.
            StringAssert.Contains("12.5", text);
            Assert.IsFalse(text.Contains("12,5"), "ondalik ayirici nokta olmali");
        }

        [Test]
        public void AC6_YazmaHatasiOyunuDurdurmaz()
        {
            // Klasorun DURACAGI yerde bir DOSYA var: CreateDirectory patlar.
            // Gercek dunyadaki karsiligi salt okunur bir klasor ya da dolu bir disk;
            // ikisi de olur ve ikisi de run'i, skor ekranini ve yeniden baslatmayi
            // engellememeli.
            Directory.CreateDirectory(_directory);

            string blocked = Path.Combine(_directory, "engel");
            File.WriteAllText(blocked, "bu bir dosya, klasor degil");

            var writer = new CombatLogWriter(blocked, "session-test.log");

            CombatLog.Install(writer);
            CombatLog.Damage("Oyuncu", null, "Zombi#1", null, 10f, 90f, 100f);
            CombatLog.Flush();

            Assert.IsNotNull(writer.LastError, "hata TUTULMALI, yutulmamali");

            // Ikinci bir yazma da patlamamali ve hata TEKRAR bildirilmemeli: her
            // satirda bagiran bir olcum araci, olctugu seyden cok gurultu uretir.
            string first = writer.LastError;

            CombatLog.Damage("Oyuncu", null, "Zombi#2", null, 10f, 80f, 100f);
            CombatLog.Flush();

            Assert.AreEqual(first, writer.LastError, "ilk hata korunmali");
        }

        [Test]
        public void AC7_TamponDolunucaKendiliginedenDiskeYazilir()
        {
            CombatLogWriter writer = NewWriter();
            CombatLog.Install(writer);

            for (int i = 0; i < CombatLogWriter.FlushEveryLines; i++)
            {
                CombatLog.Damage("Oyuncu", null, "Zombi#" + i, null, 10f, 90f, 100f);
            }

            // Flush() CAGRILMADI: tampon esigi kendi basina yazmali, yoksa coken bir
            // oyun bir oturumun tamamini goturur.
            Assert.IsTrue(File.Exists(writer.FilePath));
        }
    }
}
