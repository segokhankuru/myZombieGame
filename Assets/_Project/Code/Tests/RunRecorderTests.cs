using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Run sayaçları (M1-11). Buradaki asıl iş <b>skorun oyuncunun gözü önünde
    /// değişmemesi</b> ve <b>ikinci run'ın birinciden sayı taşımaması</b> — ikisi de
    /// oyun testinde fark edilen, testte fark edilmeyen türden hatalar.
    /// </summary>
    public sealed class RunRecorderTests
    {
        [Test]
        public void YeniKaydedici_TumSayaclarSifir()
        {
            var recorder = new RunRecorder();

            Assert.AreEqual(0, recorder.RoundReached);
            Assert.AreEqual(0, recorder.Kills);
            Assert.AreEqual(0, recorder.PointsEarned);
            Assert.AreEqual(0f, recorder.DurationSeconds, 0.001f);
            Assert.IsFalse(recorder.IsFrozen);
        }

        [Test]
        public void AC4_SayaclarGerceklesenegoreIlerler()
        {
            var recorder = new RunRecorder();

            recorder.Tick(1.5f);
            recorder.NoteRound(1);
            recorder.NoteRound(2);
            recorder.NoteKill(DamageKind.Bullet, headshot: true);
            recorder.NoteKill(DamageKind.Bullet, headshot: false);
            recorder.NoteKill(DamageKind.Melee, headshot: false);
            recorder.NoteScore(430);

            RunSummary summary = recorder.Snapshot();

            Assert.AreEqual(2, summary.RoundReached);
            Assert.AreEqual(3, summary.Kills);
            Assert.AreEqual(1, summary.HeadshotKills);
            Assert.AreEqual(1, summary.MeleeKills);
            Assert.AreEqual(430, summary.PointsEarned);
            Assert.AreEqual(1.5f, summary.DurationSeconds, 0.001f);
        }

        [Test]
        public void AC4_DondurulduktanSonraGelenBildirimler_OzetiDegistirmez()
        {
            var recorder = new RunRecorder();
            recorder.NoteRound(5);
            recorder.NoteKill(DamageKind.Bullet, headshot: true);
            recorder.NoteScore(200);

            RunSummary frozen = recorder.Freeze();

            // Oyuncu oldukten sonra havadaki mermi bir zombiyi oldurebilir.
            recorder.NoteKill(DamageKind.Bullet, headshot: true);
            recorder.NoteScore(500);
            recorder.NoteRound(6);
            recorder.Tick(10f);

            Assert.AreEqual(1, frozen.Kills);
            Assert.AreEqual(1, recorder.Kills, "Donmus kaydedici bildirim kabul etmemeli.");
            Assert.AreEqual(200, recorder.PointsEarned);
            Assert.AreEqual(5, recorder.RoundReached);
            Assert.AreEqual(0f, recorder.DurationSeconds, 0.001f);
        }

        [Test]
        public void AC4_IkinciDondurma_AyniSonucuVerir()
        {
            var recorder = new RunRecorder();
            recorder.NoteRound(3);
            recorder.NoteKill(DamageKind.Bullet, headshot: false);

            RunSummary first = recorder.Freeze();
            RunSummary second = recorder.Freeze();

            Assert.AreEqual(first.RoundReached, second.RoundReached);
            Assert.AreEqual(first.Kills, second.Kills);
        }

        [Test]
        public void AC6_Reset_HerSayaciSifirlar()
        {
            var recorder = new RunRecorder();
            recorder.Tick(90f);
            recorder.NoteRound(7);
            recorder.NoteKill(DamageKind.Melee, headshot: false);
            recorder.NoteScore(1200);
            recorder.Freeze();

            recorder.Reset();

            Assert.IsFalse(recorder.IsFrozen);
            Assert.AreEqual(0, recorder.RoundReached);
            Assert.AreEqual(0, recorder.Kills);
            Assert.AreEqual(0, recorder.MeleeKills);
            Assert.AreEqual(0, recorder.PointsEarned);
            Assert.AreEqual(0f, recorder.DurationSeconds, 0.001f);
        }

        [Test]
        public void AC6_ResetSonrasi_YeniRunSayabilir()
        {
            var recorder = new RunRecorder();
            recorder.NoteRound(9);
            recorder.NoteScore(5000);
            recorder.Freeze();
            recorder.Reset();

            recorder.NoteRound(2);
            recorder.NoteScore(180);

            Assert.AreEqual(2, recorder.RoundReached, "Ikinci run birincinin turunu tasimamali.");
            Assert.AreEqual(180, recorder.PointsEarned);
        }

        [Test]
        public void UlasilanTur_GeriGitmez()
        {
            var recorder = new RunRecorder();

            recorder.NoteRound(6);
            recorder.NoteRound(2);

            // F7/F8 ile tur atlayan hata ayiklama kisayolu skoru DUSURMEMELI:
            // ulasilan tur bir rekordur.
            Assert.AreEqual(6, recorder.RoundReached);
        }

        [Test]
        public void KazanilanPuan_ToplamAlir_ArtisDegil()
        {
            var recorder = new RunRecorder();

            recorder.NoteScore(100);
            recorder.NoteScore(250);
            recorder.NoteScore(250);

            Assert.AreEqual(250, recorder.PointsEarned, "Toplamlar toplanmamali.");
        }

        [Test]
        public void NegatifVeSifirDelta_SureyiBozmaz()
        {
            var recorder = new RunRecorder();

            recorder.Tick(-5f);
            recorder.Tick(0f);
            recorder.Tick(2f);

            Assert.AreEqual(2f, recorder.DurationSeconds, 0.001f);
        }

        [Test]
        public void HicOldurmeYok_KafaOraniSifir()
        {
            var summary = new RunRecorder().Snapshot();

            Assert.AreEqual(0f, summary.HeadshotRatio01, 0.001f);
        }

        [Test]
        public void KafaOrani_OldurmeSayisinaGoreHesaplanir()
        {
            var recorder = new RunRecorder();

            recorder.NoteKill(DamageKind.Bullet, headshot: true);
            recorder.NoteKill(DamageKind.Bullet, headshot: true);
            recorder.NoteKill(DamageKind.Bullet, headshot: false);
            recorder.NoteKill(DamageKind.Bullet, headshot: false);

            Assert.AreEqual(0.5f, recorder.Snapshot().HeadshotRatio01, 0.001f);
        }

        [Test]
        public void BicakOldurmesi_KafaVurusuSayilmaz()
        {
            var recorder = new RunRecorder();

            // Bicak kafa vurusu bayragiyla gelse bile ekonomi onu MeleeKill sayar;
            // sayac da ayni sinifi kullanmali, yoksa skor ekrani ile puan ayrisir.
            recorder.NoteKill(DamageKind.Melee, headshot: true);

            Assert.AreEqual(1, recorder.MeleeKills);
            Assert.AreEqual(0, recorder.HeadshotKills);
            Assert.AreEqual(1, recorder.Kills);
        }
    }
}
