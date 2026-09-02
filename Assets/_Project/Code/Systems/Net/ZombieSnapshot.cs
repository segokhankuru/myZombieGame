using System;

namespace Bunker.Systems.Net
{
    /// <summary>Tek bir zombinin ağda taşınan hâli.</summary>
    public struct ZombieState
    {
        public ushort Id;
        public float X;
        public float Y;
        public float Z;

        /// <summary>Yatay bakış açısı, derece (0–360).</summary>
        public float Yaw;
    }

    /// <summary>
    /// Zombi konum paketinin kodlayıcısı. M1-05, ADR-0004'ün seam'i.
    ///
    /// <para><b>Neden tek paket:</b> zombide <c>NetworkTransform</c> yasak. 40 nesnenin
    /// her biri kendi bileşeniyle konum yayınlarsa 40 ayrı mesaj, 40 ayrı başlık ve
    /// nesne başına senkron ayarı olur. Tek seam, tek paket, tek yerden ölçülebilir bir
    /// bant genişliği demektir.</para>
    ///
    /// <para><b>Paket başına maliyet</b> (netcode.md hikâyede sayı ister):
    /// zombi başına <b>12 bayt</b> — id 2, konum 3×3 (santimetre çözünürlüğünde
    /// sıkıştırılmış), yaw 1. Başlıkla birlikte 40 zombi = <b>482 bayt</b>; 10 Hz'de
    /// istemci başına <b>~4.8 KB/sn</b>. Ham float'larla (3×4 bayt + yaw 4) aynı paket
    /// 25 bayt/zombi, yani <b>~10 KB/sn</b> olurdu.</para>
    ///
    /// <para><b>Saf C#.</b> Mirror'a bağlı değil; bu yüzden kodlama/çözme sahne ve ağ
    /// açmadan test edilebilir. Ağda sessizce bozulan bir paket, teşhisi en pahalı
    /// hata türüdür.</para>
    /// </summary>
    public static class ZombieSnapshot
    {
        /// <summary>Zombi başına bayt.</summary>
        public const int BytesPerZombie = 12;

        /// <summary>Paket başlığı: zombi sayısı (2 bayt).</summary>
        public const int HeaderBytes = 2;

        /// <summary>Bir paketin taşıyabileceği en fazla zombi. PERF-BUDGET tavanının üstünde.</summary>
        public const int MaxZombies = 64;

        /// <summary>
        /// Konum çözünürlüğü: santimetre. Zombi hareketinde milimetre farkın oyuncuya
        /// bir anlamı yok; 3 baytlık sabit noktalı sayı ±83 metreyi santimetre
        /// hassasiyetiyle taşır — LVL-01'in 42 metrelik ayak izi rahat sığar.
        /// </summary>
        public const float PositionScale = 100f;

        private const int PositionRange = 1 << 23;   // isaretli 24 bit

        /// <summary>Paket için gereken bayt sayısı.</summary>
        public static int SizeFor(int count) => HeaderBytes + count * BytesPerZombie;

        /// <summary>
        /// Paketi yazar ve yazılan bayt sayısını döner. Tampon çağıran tarafından bir
        /// kez ayrılır — kare başına tahsis yasak (csharp-code.md).
        /// </summary>
        public static int Write(byte[] buffer, ZombieState[] states, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (states == null) throw new ArgumentNullException(nameof(states));

            if (count < 0) count = 0;
            if (count > states.Length) count = states.Length;
            if (count > MaxZombies) count = MaxZombies;

            int needed = SizeFor(count);
            if (buffer.Length < needed)
            {
                throw new ArgumentException(
                    $"Tampon {buffer.Length} bayt, {count} zombi icin {needed} bayt gerekli. " +
                    "Tampon ZombieSnapshot.SizeFor(MaxZombies) ile ayrilmali.", nameof(buffer));
            }

            int offset = 0;
            WriteUInt16(buffer, ref offset, (ushort)count);

            for (int i = 0; i < count; i++)
            {
                ZombieState s = states[i];

                WriteUInt16(buffer, ref offset, s.Id);
                WriteFixed24(buffer, ref offset, s.X);
                WriteFixed24(buffer, ref offset, s.Y);
                WriteFixed24(buffer, ref offset, s.Z);

                // Yaw 256 kademeye bolunuyor: 1.4 derece hassasiyet. Bir zombinin
                // hangi yone baktigini okumak icin fazlasiyla yeterli.
                buffer[offset++] = (byte)(NormalizeDegrees(s.Yaw) / 360f * 255f);
            }

            return offset;
        }

        /// <summary>
        /// Paketi okur ve okunan zombi sayısını döner. <b>Bozuk paket sessizce
        /// yutulmaz</b> — çağıran taraf negatif sonuç görürse paketi atmalı.
        /// </summary>
        /// <returns>Okunan zombi sayısı, paket bozuksa -1.</returns>
        public static int Read(byte[] buffer, int length, ZombieState[] into)
        {
            if (buffer == null || into == null) return -1;
            if (length < HeaderBytes || length > buffer.Length) return -1;

            int offset = 0;
            int count = ReadUInt16(buffer, ref offset);

            if (count > MaxZombies) return -1;
            if (count > into.Length) return -1;
            if (length < SizeFor(count)) return -1;

            for (int i = 0; i < count; i++)
            {
                into[i] = new ZombieState
                {
                    Id = ReadUInt16(buffer, ref offset),
                    X = ReadFixed24(buffer, ref offset),
                    Y = ReadFixed24(buffer, ref offset),
                    Z = ReadFixed24(buffer, ref offset),
                    Yaw = buffer[offset++] / 255f * 360f
                };
            }

            return count;
        }

        // ---------------------------------------------------------------- ilkel yazma

        private static void WriteUInt16(byte[] b, ref int o, ushort value)
        {
            b[o++] = (byte)(value & 0xFF);
            b[o++] = (byte)(value >> 8);
        }

        private static ushort ReadUInt16(byte[] b, ref int o)
        {
            // Baytlar ayri satirlarda okunuyor: tek ifadede 'b[o++] | (b[o++] << 8)'
            // yazmak dogru calisir ama okuyanin degerlendirme sirasini ezberlemesini
            // gerektirir. Ag kodunda bu tur zeka, sessiz bayt sirasi hatasidir.
            int low = b[o++];
            int high = b[o++];
            return (ushort)(low | (high << 8));
        }

        private static void WriteFixed24(byte[] b, ref int o, float value)
        {
            int fixedValue = (int)Math.Round(value * PositionScale);

            // Kirpma sessiz degil ama cokme de degil: harita disina cikan bir zombi
            // konumu, paketin tamamini bozmaktansa sinirda durur.
            if (fixedValue > PositionRange - 1) fixedValue = PositionRange - 1;
            if (fixedValue < -PositionRange) fixedValue = -PositionRange;

            int unsigned = fixedValue + PositionRange;   // isaretsize kaydir

            b[o++] = (byte)(unsigned & 0xFF);
            b[o++] = (byte)((unsigned >> 8) & 0xFF);
            b[o++] = (byte)((unsigned >> 16) & 0xFF);
        }

        private static float ReadFixed24(byte[] b, ref int o)
        {
            int b0 = b[o++];
            int b1 = b[o++];
            int b2 = b[o++];
            int unsigned = b0 | (b1 << 8) | (b2 << 16);
            return (unsigned - PositionRange) / PositionScale;
        }

        private static float NormalizeDegrees(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
