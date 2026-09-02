using System;

namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Can havuzu. Zombinin de, oyuncunun da, barikatın da canı budur.
    ///
    /// <para><b>Ölüm bir kez olur.</b> Aynı karede iki mermi isabet ederse iki ölüm
    /// olayı üretmez — bu, ekonomiye iki öldürme puanı yazdıran ve yüksek atış hızlı
    /// silahla fark edilen klasik hatadır. <see cref="DamageResult.Killed"/> yalnızca
    /// canı sıfıra indiren ilk vuruşta true döner.</para>
    ///
    /// <para><b>Saf C#.</b> Tur ölçeklemesinden gelen can değeriyle kurulur
    /// (<c>RoundScaling.HealthForRound</c>), Unity açmadan test edilir.</para>
    /// </summary>
    public sealed class HealthPool
    {
        private float _current;

        public HealthPool(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Can sifirdan buyuk olmali.");
            Max = max;
            _current = max;
        }

        public float Max { get; private set; }
        public float Current => _current;
        public bool IsAlive => _current > 0f;

        /// <summary>0 (ölü) ile 1 (tam) arası. Can barı ve görsel geri bildirim için.</summary>
        public float Fraction01 => Max <= 0f ? 0f : _current / Max;

        /// <summary>
        /// Havuzu yeni bir tavanla doldurur. Havuzlanmış zombiler yeniden kullanılırken
        /// çağrılır — önceki hayatından can taşıyan bir nesne, bir saat oynadıktan sonra
        /// ortaya çıkan türden bir hatadır (systems-code.md).
        /// </summary>
        public void ResetTo(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max), "Can sifirdan buyuk olmali.");
            Max = max;
            _current = max;
        }

        public DamageResult ApplyDamage(in DamageInfo damage)
        {
            if (!IsAlive) return new DamageResult(0f, false, damage.Amount);

            float before = _current;
            _current -= damage.Amount;

            if (_current > 0f) return new DamageResult(damage.Amount, false, 0f);

            float overkill = -_current;
            _current = 0f;
            return new DamageResult(before, true, overkill);
        }

        /// <summary>Anında öldürür. Tur temizliği ve hata ayıklama içindir.</summary>
        public DamageResult Kill()
        {
            if (!IsAlive) return new DamageResult(0f, false, 0f);

            float absorbed = _current;
            _current = 0f;
            return new DamageResult(absorbed, true, 0f);
        }
    }
}
