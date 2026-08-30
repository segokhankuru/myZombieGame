using Bunker.Systems.Diagnostics;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// `FrameTimeRecorder` için EditMode testleri.
    ///
    /// Bu testler Unity açmadan, sahne yüklemeden, milisaniyeler içinde koşar — çünkü
    /// ölçtükleri şey saf mantık. Projedeki ilk testler oldukları için aynı zamanda
    /// test altyapısının çalıştığının kanıtı.
    /// </summary>
    public sealed class FrameTimeRecorderTests
    {
        [Test]
        public void BosKayitci_BosOzetDoner()
        {
            var recorder = new FrameTimeRecorder(16);

            FrameStats stats = recorder.Snapshot();

            Assert.IsTrue(stats.IsEmpty, "Hic ornek yokken ozet bos olmali.");
        }

        [Test]
        public void TekOrnek_TumYuzdelikleriAyniDeger()
        {
            var recorder = new FrameTimeRecorder(16);
            recorder.Add(7.5f);

            FrameStats stats = recorder.Snapshot();

            Assert.AreEqual(1, stats.SampleCount);
            Assert.AreEqual(7.5f, stats.P50, 0.0001f);
            Assert.AreEqual(7.5f, stats.P99, 0.0001f);
            Assert.AreEqual(7.5f, stats.Min, 0.0001f);
            Assert.AreEqual(7.5f, stats.Max, 0.0001f);
        }

        [Test]
        public void SirasizGirdi_DogruMinMaxVeMedyan()
        {
            var recorder = new FrameTimeRecorder(16);
            // Bilerek sirasiz: kayitci siralamayi kendi yapmali.
            foreach (float v in new[] { 9f, 1f, 5f, 3f, 7f })
            {
                recorder.Add(v);
            }

            FrameStats stats = recorder.Snapshot();

            Assert.AreEqual(5, stats.SampleCount);
            Assert.AreEqual(1f, stats.Min, 0.0001f);
            Assert.AreEqual(9f, stats.Max, 0.0001f);
            Assert.AreEqual(5f, stats.P50, 0.0001f, "Bes ornegin medyani ortadaki deger olmali.");
        }

        [Test]
        public void KaynakTamponBozulmaz_IkiSnapshotAyniSonucuVerir()
        {
            var recorder = new FrameTimeRecorder(8);
            foreach (float v in new[] { 4f, 2f, 8f, 6f })
            {
                recorder.Add(v);
            }

            FrameStats first = recorder.Snapshot();
            FrameStats second = recorder.Snapshot();

            // Siralama ayri bir tampona kopyalanip yapiliyor; kaynak bozulursa
            // ikinci snapshot farkli cikardi.
            Assert.AreEqual(first.P50, second.P50, 0.0001f);
            Assert.AreEqual(first.Min, second.Min, 0.0001f);
            Assert.AreEqual(first.Max, second.Max, 0.0001f);
        }

        [Test]
        public void KapasiteAsilinca_EnEskiOrnekUstuneYazilir()
        {
            var recorder = new FrameTimeRecorder(3);
            recorder.Add(100f);   // bu uzerine yazilacak
            recorder.Add(1f);
            recorder.Add(2f);
            recorder.Add(3f);     // 100f'i ezer

            FrameStats stats = recorder.Snapshot();

            Assert.AreEqual(3, stats.SampleCount, "Ornek sayisi kapasiteyi asmamali.");
            Assert.AreEqual(4, stats.TotalSamples, "Toplam sayac eklenen her ornegi saymali.");
            Assert.AreEqual(3f, stats.Max, 0.0001f, "En eski ornek (100) tamponda kalmamali.");
        }

        [Test]
        public void Reset_SayaclariSifirlar()
        {
            var recorder = new FrameTimeRecorder(4);
            recorder.Add(5f);
            recorder.Add(6f);

            recorder.Reset();

            Assert.AreEqual(0, recorder.Count);
            Assert.AreEqual(0, recorder.TotalSamples);
            Assert.IsTrue(recorder.Snapshot().IsEmpty);
        }

        [Test]
        public void YuzYirmiOrnek_P99UstUcteBirdeOlmali()
        {
            var recorder = new FrameTimeRecorder(128);
            // 1..100 ms arasi duzgun dagilim.
            for (int i = 1; i <= 100; i++)
            {
                recorder.Add(i);
            }

            FrameStats stats = recorder.Snapshot();

            Assert.AreEqual(50f, stats.P50, 1.5f, "Medyan ~50 olmali.");
            Assert.AreEqual(95f, stats.P95, 1.5f, "p95 ~95 olmali.");
            Assert.AreEqual(99f, stats.P99, 1.5f, "p99 ~99 olmali.");
        }
    }
}
