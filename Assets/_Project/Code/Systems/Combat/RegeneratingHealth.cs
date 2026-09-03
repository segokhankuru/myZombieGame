using System;

namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Vurulmayınca yenilenen can. Oyuncunun canı budur. M1-11.
    ///
    /// <para><b>Neden yenilenen can</b> (geliştirici kararı, 2026-09-03): ÇK-17 bu
    /// milestone'un asıl sorusu — 20 dakika oynadıktan sonra tekrar oynamak istiyor
    /// musun. Kalıcı hasar birikimi o sürede ceza hissi yaratır ve tekrar oynama
    /// isteğini düşürür. Kaçmak ve köşe tutmak bir taktik olarak kalmalı; bu sınıf o
    /// taktiğin ödülüdür.</para>
    ///
    /// <para><b>Saf C#.</b> <see cref="HealthPool"/>'un üstünde durur, ona sahip olur.
    /// Zaman dışarıdan verilir — Unity açmadan, saniye saniye test edilir (ÇK-16).</para>
    ///
    /// <para><b>Yenilenme, hasarın hemen ardından başlamaz.</b> Gecikme olmadan can bir
    /// kaynak olmaktan çıkar: sürünün içinde durmanın bedeli kalmaz. Yeni her hasar
    /// gecikmeyi <b>baştan</b> başlatır — sürekli vurulan bir oyuncu hiç yenilenmez.</para>
    /// </summary>
    public sealed class RegeneratingHealth : IDamageable
    {
        private readonly HealthPool _pool;
        private readonly float _regenDelaySeconds;
        private readonly float _regenPerSecond;
        private readonly float _lowFraction;

        private float _secondsSinceDamage;

        /// <param name="maxPoints">Tam can. <c>player.json → health.maxPoints</c>.</param>
        /// <param name="regenDelaySeconds">Son hasardan yenilenmeye kadar geçen süre.</param>
        /// <param name="regenPerSecond">Yenilenme hızı.</param>
        /// <param name="lowFraction">Uyarının göründüğü can oranı.</param>
        public RegeneratingHealth(float maxPoints, float regenDelaySeconds,
                                  float regenPerSecond, float lowFraction)
        {
            if (regenDelaySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(regenDelaySeconds), "Gecikme negatif olamaz.");
            if (regenPerSecond <= 0f)
                throw new ArgumentOutOfRangeException(nameof(regenPerSecond), "Yenilenme hizi sifirdan buyuk olmali.");

            _pool = new HealthPool(maxPoints);
            _regenDelaySeconds = regenDelaySeconds;
            _regenPerSecond = regenPerSecond;
            _lowFraction = lowFraction;

            // Hic vurulmamis bir oyuncu yenilenmeyi beklemez.
            _secondsSinceDamage = regenDelaySeconds;
        }

        public float Max => _pool.Max;

        public float Current => _pool.Current;

        public float Fraction01 => _pool.Fraction01;

        public bool IsAlive => _pool.IsAlive;

        /// <summary>Oyuncuda kafa kutusu yok — zombiler telegrafı olan tek bir vuruş yapar.</summary>
        public bool CountsAsHeadshot => false;

        /// <summary>Ekran kenarı uyarısının yanması gerekiyor mu (AC-7).</summary>
        public bool IsLow => IsAlive && Fraction01 <= _lowFraction;

        /// <summary>Şu an yenileniyor mu. HUD'da "toparlanıyorsun" göstergesi için.</summary>
        public bool IsRegenerating =>
            IsAlive && _secondsSinceDamage >= _regenDelaySeconds && Current < Max;

        /// <summary>Yenilenmenin başlamasına kalan süre. Yenilenirken sıfır.</summary>
        public float RegenDelayRemainingSeconds
        {
            get
            {
                float remaining = _regenDelaySeconds - _secondsSinceDamage;
                return remaining < 0f ? 0f : remaining;
            }
        }

        /// <summary>
        /// Zamanı ilerletir ve gerekiyorsa canı doldurur.
        ///
        /// <para><b>Ölü bir oyuncu yenilenmez.</b> Ölümden dönüş bir tasarım kararıdır
        /// ve M-01'de yoktur; buradan sessizce gelmesi, run sonunun hiç görünmemesine
        /// yol açardı — <c>DebugPlayerHealth</c>'in tam olarak yaptığı şey.</para>
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || !IsAlive) return;

            _secondsSinceDamage += deltaTime;

            if (_secondsSinceDamage < _regenDelaySeconds) return;
            if (_pool.Current >= _pool.Max) return;

            float healed = _pool.Current + _regenPerSecond * deltaTime;
            if (healed > _pool.Max) healed = _pool.Max;

            _pool.Heal(healed - _pool.Current);
        }

        public DamageResult ApplyDamage(in DamageInfo damage)
        {
            DamageResult result = _pool.ApplyDamage(damage);

            // Emilen hasar sifirsa oyuncu zaten olu ya da hasar sifir: ikisinde de
            // gecikmeyi sifirlamak yanlis olur - olu bir oyuncuyu vurmak, dirildiginde
            // yenilenmesini geciktirmemeli.
            if (result.Absorbed > 0f) _secondsSinceDamage = 0f;

            return result;
        }

        /// <summary>Yeni bir run için tam cana döner (AC-5).</summary>
        public void ResetFull()
        {
            _pool.ResetTo(_pool.Max);
            _secondsSinceDamage = _regenDelaySeconds;
        }
    }
}
