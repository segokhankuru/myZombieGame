using System;
using Bunker.Systems.Net;
using NUnit.Framework;

namespace Bunker.Systems.Tests
{
    /// <summary>
    /// Zombi konum paketi. <b>Ağda sessizce bozulan bir paket, teşhisi en pahalı hata
    /// türüdür</b> — iki makine gerektirir, gözle görünmez ve "bazen zombiler garip
    /// duruyor" diye rapor edilir. O yüzden kodlayıcı burada, tek makinede, sıkı test
    /// edilir (M1-05).
    /// </summary>
    public sealed class ZombieSnapshotTests
    {
        private static byte[] NewBuffer() =>
            new byte[ZombieSnapshot.SizeFor(ZombieSnapshot.MaxZombies)];

        private static ZombieState[] Sample(int count)
        {
            var states = new ZombieState[count];

            for (int i = 0; i < count; i++)
            {
                states[i] = new ZombieState
                {
                    Id = (ushort)(i + 1),
                    X = i * 1.5f,
                    Y = 0.25f,
                    Z = -i * 0.75f,
                    Yaw = i * 7f % 360f
                };
            }

            return states;
        }

        [Test]
        public void YazilanPaket_AynenGeriOkunur()
        {
            byte[] buffer = NewBuffer();
            ZombieState[] written = Sample(5);
            var read = new ZombieState[ZombieSnapshot.MaxZombies];

            int length = ZombieSnapshot.Write(buffer, written, written.Length);
            int count = ZombieSnapshot.Read(buffer, length, read);

            Assert.AreEqual(5, count);

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(written[i].Id, read[i].Id, $"[{i}] id");
                Assert.AreEqual(written[i].X, read[i].X, 0.01f, $"[{i}] x");
                Assert.AreEqual(written[i].Y, read[i].Y, 0.01f, $"[{i}] y");
                Assert.AreEqual(written[i].Z, read[i].Z, 0.01f, $"[{i}] z");
            }
        }

        [Test]
        public void KonumHassasiyeti_SantimetreSeviyesinde()
        {
            byte[] buffer = NewBuffer();
            var read = new ZombieState[4];
            var states = new[]
            {
                new ZombieState { Id = 1, X = 12.34f, Y = 0.07f, Z = -8.91f }
            };

            int length = ZombieSnapshot.Write(buffer, states, 1);
            ZombieSnapshot.Read(buffer, length, read);

            Assert.AreEqual(12.34f, read[0].X, 0.005f);
            Assert.AreEqual(0.07f, read[0].Y, 0.005f);
            Assert.AreEqual(-8.91f, read[0].Z, 0.005f);
        }

        [Test]
        public void NegatifKoordinatlar_IsaretiniKorur()
        {
            // LVL-01'in bati ve guney yarisi negatif koordinatta. Isaret hatasi
            // zombileri haritanin diger ucuna koyardi.
            byte[] buffer = NewBuffer();
            var read = new ZombieState[4];
            var states = new[]
            {
                new ZombieState { Id = 7, X = -14f, Y = 0f, Z = -8f }
            };

            int length = ZombieSnapshot.Write(buffer, states, 1);
            ZombieSnapshot.Read(buffer, length, read);

            Assert.AreEqual(-14f, read[0].X, 0.01f);
            Assert.AreEqual(-8f, read[0].Z, 0.01f);
        }

        [Test]
        public void Yaw_YaklasikKorunur_VeSarmalanir()
        {
            byte[] buffer = NewBuffer();
            var read = new ZombieState[4];
            var states = new[]
            {
                new ZombieState { Id = 1, Yaw = 90f },
                new ZombieState { Id = 2, Yaw = 359f },
                new ZombieState { Id = 3, Yaw = -90f }   // 270 dereceye sarmalanmali
            };

            int length = ZombieSnapshot.Write(buffer, states, 3);
            ZombieSnapshot.Read(buffer, length, read);

            Assert.AreEqual(90f, read[0].Yaw, 2f);
            Assert.AreEqual(359f, read[1].Yaw, 2f);
            Assert.AreEqual(270f, read[2].Yaw, 2f);
        }

        [Test]
        public void BosPaket_Gecerlidir()
        {
            byte[] buffer = NewBuffer();
            var read = new ZombieState[4];

            int length = ZombieSnapshot.Write(buffer, Array.Empty<ZombieState>(), 0);

            Assert.AreEqual(ZombieSnapshot.HeaderBytes, length);
            Assert.AreEqual(0, ZombieSnapshot.Read(buffer, length, read),
                "sahada zombi olmamasi gecerli bir durumdur, bozuk paket degil");
        }

        [Test]
        public void PaketBoyutu_Beklenen_BantGenisligiHesabiyla_Uyusur()
        {
            // netcode.md: hikayede bayt sayisi yazar. Bu test o sayiyi korur -
            // paket buyurse burasi kirmizi yanar.
            Assert.AreEqual(12, ZombieSnapshot.BytesPerZombie);
            Assert.AreEqual(482, ZombieSnapshot.SizeFor(40), "40 zombi = 482 bayt");
        }

        [Test]
        public void TavanUstuSayi_KirpilirCokmez()
        {
            byte[] buffer = NewBuffer();
            ZombieState[] states = Sample(ZombieSnapshot.MaxZombies);
            var read = new ZombieState[ZombieSnapshot.MaxZombies];

            int length = ZombieSnapshot.Write(buffer, states, 999);
            int count = ZombieSnapshot.Read(buffer, length, read);

            Assert.AreEqual(ZombieSnapshot.MaxZombies, count);
        }

        [Test]
        public void KucukTampon_SessizceYazmaz_HataVerir()
        {
            var tooSmall = new byte[10];

            Assert.Throws<ArgumentException>(
                () => ZombieSnapshot.Write(tooSmall, Sample(5), 5));
        }

        // ---------------------------------------------------------------- bozuk paket

        [Test]
        public void KisaPaket_BozukSayilir()
        {
            byte[] buffer = NewBuffer();
            var read = new ZombieState[ZombieSnapshot.MaxZombies];

            int length = ZombieSnapshot.Write(buffer, Sample(5), 5);

            // Paket yolda kesildi: basligin soyledigi kadar veri yok.
            Assert.AreEqual(-1, ZombieSnapshot.Read(buffer, length - 5, read),
                "eksik paket sessizce yarim uygulanmaz");
        }

        [Test]
        public void BasligindaSacmaSayiOlanPaket_Reddedilir()
        {
            var buffer = new byte[64];
            buffer[0] = 0xFF;
            buffer[1] = 0xFF;   // 65535 zombi

            var read = new ZombieState[ZombieSnapshot.MaxZombies];

            Assert.AreEqual(-1, ZombieSnapshot.Read(buffer, buffer.Length, read),
                "guvenilmeyen veri: baslik kontrol edilmeden okunmaz");
        }

        [Test]
        public void HedefDiziKucukse_Reddedilir()
        {
            byte[] buffer = NewBuffer();
            var tooSmall = new ZombieState[2];

            int length = ZombieSnapshot.Write(buffer, Sample(5), 5);

            Assert.AreEqual(-1, ZombieSnapshot.Read(buffer, length, tooSmall));
        }

        [Test]
        public void BosPaketVeNullGirdiler_Cokmez()
        {
            var read = new ZombieState[4];

            Assert.AreEqual(-1, ZombieSnapshot.Read(null, 10, read));
            Assert.AreEqual(-1, ZombieSnapshot.Read(new byte[4], 10, read), "uzunluk tampondan buyuk");
            Assert.AreEqual(-1, ZombieSnapshot.Read(new byte[4], 1, read), "baslik bile sigmiyor");
        }
    }
}
