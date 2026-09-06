using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Run durumu ve yayınları (M1-11). Buradaki asıl iş <b>run sonunun bir kez
    /// olması</b> — aynı karede iki zombi vurursa iki skor ekranı, iki dondurma ve
    /// iki telemetri kaydı üretilir.
    ///
    /// <para><see cref="RunSignals"/> statiktir; her test <see cref="RunSignals.Clear"/>
    /// ile başlar. Bu, üretimde <c>RoundSignalsBootstrap</c>'in yaptığı şeyin
    /// aynısıdır — testler arası sızan statik durum, testlerin kendisini
    /// güvenilmez yapar (test-code.md).</para>
    /// </summary>
    public sealed class RunSignalsTests
    {
        [SetUp]
        public void Setup() => RunSignals.Clear();

        [TearDown]
        public void Teardown() => RunSignals.Clear();

        [Test]
        public void Baslangicta_RunBitmemis()
        {
            Assert.IsFalse(RunSignals.IsRunOver);
        }

        [Test]
        public void AC3_Olum_RunuBitirirVeOzetiYayar()
        {
            RunSignals.Current.NoteRound(4);
            RunSignals.Current.NoteKill(DamageKind.Bullet, headshot: true);
            RunSignals.Current.NoteScore(320);

            int calls = 0;
            RunSummary received = default;
            RunSignals.RunEnded += s => { calls++; received = s; };

            RunSignals.RaisePlayerDied();

            Assert.IsTrue(RunSignals.IsRunOver);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(4, received.RoundReached);
            Assert.AreEqual(1, received.Kills);
            Assert.AreEqual(320, received.PointsEarned);
        }

        [Test]
        public void AC3_AyniKaredeIkinciOlum_IkinciRunSonuUretmez()
        {
            int calls = 0;
            RunSignals.RunEnded += _ => calls++;

            RunSignals.RaisePlayerDied();
            RunSignals.RaisePlayerDied();

            Assert.AreEqual(1, calls, "Run sonu bir kez olur.");
        }

        [Test]
        public void AC4_SonOzet_OlumdenSonraOkunabilir()
        {
            RunSignals.Current.NoteRound(9);
            RunSignals.RaisePlayerDied();

            // Skor ekrani gec uyanmis olabilir; olayi kacirmis olmak, ekranin hic
            // gelmemesi anlamina gelmemeli.
            Assert.AreEqual(9, RunSignals.LastSummary.RoundReached);
        }

        [Test]
        public void AC5_YenidenBaslatma_DurumuVeSayaclariSifirlar()
        {
            RunSignals.Current.NoteRound(9);
            RunSignals.Current.NoteScore(5000);
            RunSignals.RaisePlayerDied();

            int restarts = 0;
            RunSignals.RunRestarted += () => restarts++;

            RunSignals.RequestRestart();

            Assert.IsFalse(RunSignals.IsRunOver);
            Assert.AreEqual(1, restarts);
            Assert.AreEqual(0, RunSignals.Current.RoundReached);
            Assert.AreEqual(0, RunSignals.Current.PointsEarned);
            Assert.IsFalse(RunSignals.Current.IsFrozen);
        }

        [Test]
        public void AC6_YenidenBaslatmadanSonra_IkinciRunAyriSayilir()
        {
            RunSignals.Current.NoteRound(9);
            RunSignals.RaisePlayerDied();
            RunSignals.RequestRestart();

            RunSignals.Current.NoteRound(3);
            RunSignals.Current.NoteKill(DamageKind.Melee, headshot: false);
            RunSignals.RaisePlayerDied();

            Assert.AreEqual(3, RunSignals.LastSummary.RoundReached,
                            "Ikinci run birincinin turunu gostermemeli.");
            Assert.AreEqual(1, RunSignals.LastSummary.Kills);
        }

        [Test]
        public void Clear_AbonelikleriDeSiler()
        {
            int calls = 0;
            RunSignals.RunEnded += _ => calls++;

            RunSignals.Clear();
            RunSignals.RaisePlayerDied();

            Assert.AreEqual(0, calls, "Temizlikten sonra eski abone olay almamali.");
        }
    }
}
