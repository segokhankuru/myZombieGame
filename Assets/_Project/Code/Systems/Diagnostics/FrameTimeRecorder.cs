using System;

namespace Bunker.Systems.Diagnostics
{
    /// <summary>Bir ölçüm penceresinin özeti.</summary>
    public readonly struct FrameStats
    {
        public readonly int SampleCount;
        public readonly long TotalSamples;
        public readonly float P50;
        public readonly float P95;
        public readonly float P99;
        public readonly float Min;
        public readonly float Max;

        public FrameStats(int sampleCount, long totalSamples,
                          float p50, float p95, float p99, float min, float max)
        {
            SampleCount = sampleCount;
            TotalSamples = totalSamples;
            P50 = p50;
            P95 = p95;
            P99 = p99;
            Min = min;
            Max = max;
        }

        public bool IsEmpty => SampleCount == 0;
    }

    /// <summary>
    /// Kare süresi örneklerini halka tamponda toplar ve yüzdelik özet üretir.
    ///
    /// <para><b>Neden `Bunker.Systems` içinde:</b> yüzdelik hesabı saf mantıktır,
    /// Unity'ye ihtiyacı yoktur ve Unity açmadan test edilebilir. test-code.md'nin
    /// kuralı net — bir kural yalnızca PlayMode'da test edilebiliyorsa kod yanlış
    /// yerdedir. Bu sınıfı hem `Bunker.UI` (ekranda göstermek için) hem `Bunker.AI`
    /// (otomatik tarama için) kullanır; ikisi de birbirini tanımaz.</para>
    ///
    /// <para><b>Tahsis:</b> tamponlar kurucuda bir kez ayrılır. <see cref="Add"/> hiçbir
    /// şey tahsis etmez — ölçüm aracının kendisi ölçümü bozmamalı.</para>
    /// </summary>
    public sealed class FrameTimeRecorder
    {
        private readonly float[] _samples;
        private readonly float[] _sortBuffer;
        private int _count;
        private int _cursor;
        private long _totalSamples;

        public FrameTimeRecorder(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _samples = new float[capacity];
            _sortBuffer = new float[capacity];
        }

        public int Capacity => _samples.Length;
        public int Count => _count;
        public long TotalSamples => _totalSamples;

        /// <summary>Bir örnek ekler. Tampon doluysa en eskisinin üstüne yazar.</summary>
        public void Add(float milliseconds)
        {
            _samples[_cursor] = milliseconds;
            _cursor = (_cursor + 1) % _samples.Length;
            if (_count < _samples.Length) _count++;
            _totalSamples++;
        }

        public void Reset()
        {
            _count = 0;
            _cursor = 0;
            _totalSamples = 0;
        }

        /// <summary>
        /// Toplanan örneklerin özetini üretir. Sıralama ayrı bir tampona kopyalanıp
        /// yerinde yapılır; kaynak tampon bozulmaz ve kopya tahsis edilmez.
        /// </summary>
        public FrameStats Snapshot()
        {
            if (_count == 0) return default;

            Array.Copy(_samples, _sortBuffer, _count);
            Array.Sort(_sortBuffer, 0, _count);

            return new FrameStats(
                _count,
                _totalSamples,
                Percentile(_sortBuffer, _count, 0.50f),
                Percentile(_sortBuffer, _count, 0.95f),
                Percentile(_sortBuffer, _count, 0.99f),
                _sortBuffer[0],
                _sortBuffer[_count - 1]);
        }

        internal static float Percentile(float[] sortedAscending, int count, float fraction)
        {
            if (count <= 0) return 0f;
            int index = (int)Math.Round(fraction * (count - 1), MidpointRounding.AwayFromZero);
            if (index < 0) index = 0;
            if (index > count - 1) index = count - 1;
            return sortedAscending[index];
        }
    }
}
