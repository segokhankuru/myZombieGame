using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using Bunker.Systems.Telemetry;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Telemetri yazıcısı (M1-12). Gerçek dosya sistemine, geçici bir klasöre yazar —
    /// sahte bir dosya soyutlamasıyla test etmek, tam da bu sınıfın başarısız olacağı
    /// yeri (izin, kilit, kültür) test dışında bırakırdı.
    ///
    /// <para>Buradaki asıl iş <b>kültür bağımsızlığı</b>: Türkçe Windows'ta ondalık
    /// ayırıcı virgüldür ve kültürsüz bir <c>ToString</c> geçersiz JSON üretir. O hata
    /// aylar sonra, dosyayı okumaya çalışan araçta ortaya çıkar.</para>
    /// </summary>
    public sealed class RunLogWriterTests
    {
        private string _dir;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "bunker-telemetry-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void Teardown()
        {
            // Yarattigini geri alir: sizan bir gecici klasor, sonraki testleri
            // etkilemese de diski doldurur (test-code.md - izolasyon).
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private static RunSummary Summary(int round = 4, float duration = 125.5f,
                                          int kills = 37, int headshots = 9,
                                          int melee = 3, int points = 4180,
                                          bool withDeath = true) =>
            new RunSummary(round, duration, kills, headshots, melee, points,
                           12.5f, 1.25f, -8.75f, withDeath);

        private static readonly DateTime EndedAt =
            new DateTime(2026, 9, 3, 14, 5, 9, DateTimeKind.Utc);

        [Test]
        public void AC1_IlkRun_DosyayiVeKlasoruYaratir()
        {
            var writer = new RunLogWriter(_dir);

            bool ok = writer.Append(Summary(), new float[] { 0f, 30f }, EndedAt);

            Assert.IsTrue(ok, writer.LastError);
            Assert.IsTrue(File.Exists(writer.FilePath));
            Assert.AreEqual(1, writer.WrittenCount);
            Assert.IsNull(writer.LastError);
        }

        [Test]
        public void AC1_IkiRun_IkiSatirYazar_BirincisiBozulmaz()
        {
            var writer = new RunLogWriter(_dir);

            writer.Append(Summary(round: 3), new float[] { 0f }, EndedAt);
            writer.Append(Summary(round: 11), new float[] { 0f, 40f }, EndedAt);

            string[] lines = File.ReadAllLines(writer.FilePath);

            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual(3, (int)JsonValue.Parse(lines[0])["roundReached"].AsNumber);
            Assert.AreEqual(11, (int)JsonValue.Parse(lines[1])["roundReached"].AsNumber);
        }

        [Test]
        public void AC2_YazilanSatir_SayilariBirebirGeriVerir()
        {
            var writer = new RunLogWriter(_dir);
            RunSummary summary = Summary();

            writer.Append(summary, new float[] { 0f, 30f, 75f }, EndedAt);

            JsonValue line = JsonValue.Parse(File.ReadAllLines(writer.FilePath)[0]);

            Assert.AreEqual(RunLogWriter.SchemaVersion, (int)line["schema"].AsNumber);
            Assert.AreEqual(summary.RoundReached, (int)line["roundReached"].AsNumber);
            Assert.AreEqual(summary.Kills, (int)line["kills"].AsNumber);
            Assert.AreEqual(summary.HeadshotKills, (int)line["headshotKills"].AsNumber);
            Assert.AreEqual(summary.MeleeKills, (int)line["meleeKills"].AsNumber);
            Assert.AreEqual(summary.PointsEarned, (int)line["pointsEarned"].AsNumber);
            Assert.AreEqual(summary.DurationSeconds, (float)line["durationSeconds"].AsNumber, 0.01f);
            Assert.AreEqual("2026-09-03T14:05:09Z", line["endedAtUtc"].AsString);
        }

        [Test]
        public void AC2_OlumYeri_UcKoordinatOlarakYazilir()
        {
            var writer = new RunLogWriter(_dir);

            writer.Append(Summary(), Array.Empty<float>(), EndedAt);

            JsonValue death = JsonValue.Parse(File.ReadAllLines(writer.FilePath)[0])["death"];

            Assert.IsTrue(death.IsObject);
            Assert.AreEqual(12.5f, (float)death["x"].AsNumber, 0.01f);
            Assert.AreEqual(1.25f, (float)death["y"].AsNumber, 0.01f);
            Assert.AreEqual(-8.75f, (float)death["z"].AsNumber, 0.01f);
        }

        [Test]
        public void AC2_OlumYeriBildirilmemisse_NullYazilir()
        {
            var writer = new RunLogWriter(_dir);

            writer.Append(Summary(withDeath: false), Array.Empty<float>(), EndedAt);

            JsonValue line = JsonValue.Parse(File.ReadAllLines(writer.FilePath)[0]);

            // Sifir bir koordinattir, "yok" degil: sifir yazmak haritanin merkezinde
            // sahte bir olum yigini olustururdu.
            Assert.IsTrue(line["death"].Kind == JsonKind.Null);
        }

        [Test]
        public void AC3_TurZamanlari_SirayaGoreArtanYazilir()
        {
            var writer = new RunLogWriter(_dir);
            var starts = new List<float> { 0f, 32.5f, 71f, 118.25f };

            writer.Append(Summary(round: 4), starts, EndedAt);

            JsonValue array = JsonValue.Parse(File.ReadAllLines(writer.FilePath)[0])["roundStartSeconds"];

            Assert.IsTrue(array.IsArray);
            Assert.AreEqual(4, array.Items.Count);

            for (int i = 0; i < starts.Count; i++)
            {
                Assert.AreEqual(starts[i], (float)array.Items[i].AsNumber, 0.01f);
            }
        }

        [Test]
        public void AC3_TurZamaniYoksa_BosDiziYazilir()
        {
            var writer = new RunLogWriter(_dir);

            writer.Append(Summary(round: 0), null, EndedAt);

            JsonValue array = JsonValue.Parse(File.ReadAllLines(writer.FilePath)[0])["roundStartSeconds"];

            Assert.IsTrue(array.IsArray);
            Assert.AreEqual(0, array.Items.Count);
        }

        [Test]
        public void AC4_YazilamayanYol_Patlamaz_HatayiBirKezBildirir()
        {
            // Var olan bir DOSYAYI klasor gibi kullanmak: her platformda hata verir.
            string blocker = Path.Combine(Path.GetTempPath(), "bunker-blocker-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "bu bir dosya, klasor degil");

            try
            {
                var writer = new RunLogWriter(Path.Combine(blocker, "telemetry"));

                bool first = writer.Append(Summary(), Array.Empty<float>(), EndedAt);
                string afterFirst = writer.LastError;
                bool second = writer.Append(Summary(), Array.Empty<float>(), EndedAt);

                Assert.IsFalse(first, "Yazilamayan yol basarili donmemeli.");
                Assert.IsFalse(second);
                Assert.IsNotNull(afterFirst, "Hata sessizce yutulmamali.");
                Assert.AreEqual(afterFirst, writer.LastError, "Yalnizca ilk hata tutulur.");
                Assert.AreEqual(0, writer.WrittenCount);
            }
            finally
            {
                if (File.Exists(blocker)) File.Delete(blocker);
            }
        }

        [Test]
        public void AC4_BosKlasorYolu_KurulumdaPatlar()
        {
            // Yol hatasi CALISMA aninda degil KURULUMDA yakalanir: sessiz varsayilan
            // yok (systems-code.md).
            Assert.Throws<ArgumentException>(() => new RunLogWriter(null));
            Assert.Throws<ArgumentException>(() => new RunLogWriter("   "));
        }

        [Test]
        public void AC6_TurkceKulturde_OndalikAyiriciNoktaKalir()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");

                // Kulturun gercekten virgul kullandigini once dogrula, yoksa bu test
                // hicbir sey kanitlamayan bir tiyatro olur.
                Assert.AreEqual("12,5", 12.5f.ToString("0.##"),
                                "Test kurulumu hatali: tr-TR kulturu uygulanmamis.");

                var writer = new RunLogWriter(_dir);
                writer.Append(Summary(duration: 125.5f), new float[] { 0f, 32.5f }, EndedAt);

                string raw = File.ReadAllLines(writer.FilePath)[0];

                Assert.IsTrue(raw.Contains("\"durationSeconds\":125.5"),
                              $"Kulture bagli sayi yazilmis: {raw}");

                // Ve en onemlisi: hala gecerli JSON.
                JsonValue parsed = JsonValue.Parse(raw);
                Assert.AreEqual(125.5f, (float)parsed["durationSeconds"].AsNumber, 0.01f);
                Assert.AreEqual(32.5f, (float)parsed["roundStartSeconds"].Items[1].AsNumber, 0.01f);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void HerSatir_TekSatirdirVeSatirSonuylaBiter()
        {
            var writer = new RunLogWriter(_dir);

            writer.Append(Summary(), new float[] { 0f, 30f }, EndedAt);

            string raw = File.ReadAllText(writer.FilePath);

            // JSONL'in tek kurali: bir run, bir satir. Icinde satir sonu olsa,
            // dosyayi satir satir okuyan her arac bozulurdu.
            Assert.AreEqual(1, raw.Split('\n').Length - 1);
            Assert.IsTrue(raw.EndsWith("\n"));
        }

        [Test]
        public void TurAtlamasi_SatiraIsaretlenir()
        {
            var writer = new RunLogWriter(_dir);

            writer.Append(Summary(), new float[] { 0f }, EndedAt, usedRoundSkip: true);
            writer.Append(Summary(), new float[] { 0f }, EndedAt, usedRoundSkip: false);

            string[] lines = File.ReadAllLines(writer.FilePath);

            // Ozet araci bu bayraga bakip atlamali run'i CK-13 hesabinin disinda
            // birakir: atlanan turlarin zamanlamasi uydurmadir.
            Assert.IsTrue(JsonValue.Parse(lines[0])["usedRoundSkip"].AsBool);
            Assert.IsFalse(JsonValue.Parse(lines[1])["usedRoundSkip"].AsBool);
        }

        [Test]
        public void SifirTurlukRun_YineDeYazilir()
        {
            var writer = new RunLogWriter(_dir);

            // Molada olen bir oyuncu da veridir: "ilk turu bile goremedi" bir bulgudur.
            bool ok = writer.Append(new RunSummary(0, 3.5f, 0, 0, 0, 0),
                                    Array.Empty<float>(), EndedAt);

            Assert.IsTrue(ok, writer.LastError);
            Assert.AreEqual(0, (int)JsonValue.Parse(File.ReadAllLines(writer.FilePath)[0])["roundReached"].AsNumber);
        }
    }
}
