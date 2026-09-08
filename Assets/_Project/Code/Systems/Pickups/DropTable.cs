using System;

namespace Bunker.Systems.Pickups
{
    /// <summary>
    /// "Bu ölen zombi bir şey bıraktı mı, bıraktıysa ne?" sorusunun tek cevabı.
    ///
    /// <para><b>Saf C#.</b> Unity'yi bilmez, sahne gerektirmez: yüz bin ölümün eşya
    /// dağılımını Unity açmadan ölçebilmek, drop'un dengeli olup olmadığını anlamanın
    /// tek ucuz yolu (ÇK-16 ile aynı gerekçe).</para>
    ///
    /// <para><b>Tohumlu</b> (csharp-code.md): aynı tohum aynı diziyi verir, yani bir
    /// oyun testinde "nuke on saniyede iki kere düştü" şikâyeti tekrar üretilebilir.
    /// Tohumsuz bir <c>Random</c> ile o şikâyet sonsuza kadar anekdot kalırdı.</para>
    ///
    /// <para><b>Ağırlıklar birbirine göre okunur:</b> bir türün payı, kendi ağırlığının
    /// toplam ağırlığa oranıdır. Tek tek yüzde yazmak, yeni bir eşya eklendiğinde
    /// diğerlerinin hepsini elle düzeltmeyi gerektirirdi.</para>
    /// </summary>
    public sealed class DropTable
    {
        private readonly float _baseChance01;
        private readonly int[] _weights;
        private readonly int _weightTotal;
        private readonly Random _random;

        /// <param name="baseChance01">Kart etkisi olmadan, ölüm başına eşya şansı.</param>
        /// <param name="weights">
        /// <see cref="PowerupKind"/> sırasına birebir denk gelen ağırlıklar.
        /// </param>
        /// <param name="seed">Run tohumu.</param>
        public DropTable(float baseChance01, int[] weights, int seed)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));

            int kinds = Enum.GetValues(typeof(PowerupKind)).Length;

            if (weights.Length != kinds)
            {
                // Sessiz kirpma yok: eksik agirlik, bir esya turunun HIC cikmamasi
                // demek ve bu, oyun testinde "nuke diye bir sey yok galiba" olarak
                // okunur - tespiti en zor hata turu.
                throw new ArgumentException(
                    $"Agirlik sayisi PowerupKind sayisiyla ayni olmali ({kinds}), " +
                    $"gelen {weights.Length}.", nameof(weights));
            }

            _baseChance01 = baseChance01 < 0f ? 0f : baseChance01;
            _weights = new int[kinds];

            for (int i = 0; i < kinds; i++)
            {
                _weights[i] = weights[i] < 0 ? 0 : weights[i];
                _weightTotal += _weights[i];
            }

            _random = new Random(seed);
        }

        /// <summary>Bütün ağırlıklar sıfırsa tablo kapalıdır — hiç eşya düşmez.</summary>
        public bool IsEmpty => _weightTotal <= 0;

        /// <summary>
        /// Bir ölüm için zar atar.
        /// </summary>
        /// <param name="chanceMultiplier">
        /// Kart etkisi (<c>1 + CardStat.DropRate</c>). Taban şans burada çarpılır;
        /// ikinci bir taban şans <b>yok</b> — oyuncunun sıklığı öğrenebilmesi için
        /// tek bir sayının katı olmalı.
        /// </param>
        /// <param name="kind">Düştüyse ne düştüğü.</param>
        /// <returns>Bir şey düştüyse <c>true</c>.</returns>
        public bool TryRoll(float chanceMultiplier, out PowerupKind kind)
        {
            kind = default;

            if (IsEmpty) return false;

            float chance = _baseChance01 * (chanceMultiplier < 0f ? 0f : chanceMultiplier);
            if (chance <= 0f) return false;

            if (chance < 1f && _random.NextDouble() >= chance) return false;

            int roll = _random.Next(_weightTotal);

            for (int i = 0; i < _weights.Length; i++)
            {
                roll -= _weights[i];

                if (roll < 0)
                {
                    kind = (PowerupKind)i;
                    return true;
                }
            }

            // Buraya duselmez (roll < toplam agirlik). Yine de sessiz kalmamak icin
            // en sik esyaya duser: bir esya dusurmek, hicbir sey dusurmemekten iyi.
            kind = PowerupKind.Ammo;
            return true;
        }
    }
}
