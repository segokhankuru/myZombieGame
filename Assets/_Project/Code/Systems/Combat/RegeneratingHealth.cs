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
        private readonly float _baseRegenDelaySeconds;
        private readonly float _baseMaxPoints;

        private float _regenDelaySeconds;
        private float _damageTakenMultiplier = 1f;
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
            _baseRegenDelaySeconds = regenDelaySeconds;
            _regenDelaySeconds = regenDelaySeconds;
            _baseMaxPoints = maxPoints;
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

        /// <summary>Bu can havuzunun kendisi; alt vuruş kutusu yok.</summary>
        public IDamageable DamageRoot => this;

        /// <summary>
        /// Şu anki ham can. Durum paneli <b>kesin sayı</b> ister (2026-09-06):
        /// "%40 canım var" ile "40/125 canım var" arasındaki fark, kaç vuruş
        /// dayanabileceğini bilmekle bilmemek arasındaki farktır.
        /// </summary>
        public float CurrentPoints => _pool.Current;

        /// <summary>Kart ve tezgâh etkileriyle birlikte maksimum can.</summary>
        public float MaxPoints => _pool.Max;

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
            // Kart etkisi: hasar EFEKTIF CAN uzerinden azalir (SYS-02 §3.2), yuzdeyle
            // degil - yuzde olsaydi bes kart olumsuzluk yapardi.
            var scaled = new DamageInfo(damage.Amount * _damageTakenMultiplier,
                                        damage.Kind, damage.Headshot);

            DamageResult result = _pool.ApplyDamage(scaled);

            // Emilen hasar sifirsa oyuncu zaten olu ya da hasar sifir: ikisinde de
            // gecikmeyi sifirlamak yanlis olur - olu bir oyuncuyu vurmak, dirildiginde
            // yenilenmesini geciktirmemeli.
            if (result.Absorbed > 0f) _secondsSinceDamage = 0f;

            return result;
        }

        /// <summary>
        /// Kart etkilerini uygular (M-03): maks can, hasar azaltma, yenilenme gecikmesi.
        ///
        /// <para><b>Mevcut can ORANI korunur.</b> Tavan buyudugunde cani otomatik
        /// doldurmak bedava bir iyilesme olurdu; %40 canla kart alan oyuncu yine %40
        /// canla devam eder, ama artik daha buyuk bir %40.</para>
        /// </summary>
        public void ApplyModifiers(float maxHealthBonus, float damageTakenMultiplier,
                                   float regenDelayBonus)
        {
            float fraction = _pool.Fraction01;

            _damageTakenMultiplier = damageTakenMultiplier <= 0f ? 1f : damageTakenMultiplier;
            _regenDelaySeconds = _baseRegenDelaySeconds / (1f + Math.Max(-0.9f, regenDelayBonus));

            float newMax = _baseMaxPoints * (1f + Math.Max(-0.9f, maxHealthBonus));

            if (Math.Abs(newMax - _pool.Max) > 0.01f)
            {
                _pool.ResetTo(newMax);
                _pool.ApplyDamage(new DamageInfo(newMax * (1f - fraction)));
            }
        }

        /// <summary>
        /// İyileştirir (M-03 "öldürünce can" kartı).
        ///
        /// <para><b>Ölü iyileşmez</b>: ölümden dönüş bir tasarım kararıdır ve M-01'de
        /// yok. Yenilenme gecikmesine <b>dokunmaz</b> — kart bir ödül, vurulmamış
        /// olmanın yerine geçen bir şey değil.</para>
        /// </summary>
        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive) return;

            _pool.Heal(amount);
        }

        /// <summary>Yeni bir run için tam cana döner (AC-5).</summary>
        public void ResetFull()
        {
            _pool.ResetTo(_pool.Max);
            _secondsSinceDamage = _regenDelaySeconds;
        }
    }
}
