using System;
using Bunker.Systems.Config;

namespace Bunker.Systems.Combat
{
    /// <summary>Silahın o anda ne yapabildiği.</summary>
    public enum WeaponPhase
    {
        /// <summary>Ateş edebilir.</summary>
        Ready,
        /// <summary>Atışlar arası bekleme.</summary>
        Cycling,
        /// <summary>Dolum sürüyor — <b>bu turdeki en önemli risk penceresi.</b></summary>
        Reloading,
        /// <summary>Şarjör boş, yedek de yok.</summary>
        Dry
    }

    /// <summary>Bir ateş denemesinin sonucu.</summary>
    public enum FireResult
    {
        /// <summary>Mermi çıktı.</summary>
        Fired,
        /// <summary>Atış hızı beklemesi sürüyor.</summary>
        Cycling,
        /// <summary>Şarjör boş — dolum gerekiyor.</summary>
        Empty,
        /// <summary>Dolum sürüyor.</summary>
        Reloading,
        /// <summary>Hiç mermi kalmadı.</summary>
        Dry
    }

    /// <summary>
    /// Silahın kararı: ne zaman ateş eder, kaç mermisi var, ne zaman dolar. M1-06.
    ///
    /// <para><b>Saf C#, sahnesiz.</b> Bu türde oyunun kaderi silahın nasıl
    /// hissettirdiğine bağlı ve his oynayarak ayarlanır — ama <i>kural</i> okunarak
    /// doğrulanır. Şarjörün sıfırın altına inmemesi, dolumun iki kez tamamlanmaması,
    /// yedeğin eksiye düşmemesi burada garanti edilir; oyun testinde aranacak şey his
    /// olsun, aritmetik değil.</para>
    ///
    /// <para><b>Girdi tamponu</b> (gameplay-code.md): bekleme süresinin bir kare
    /// öncesine denk gelen tuşa basış yenmez, <c>inputBufferSeconds</c> kadar hafızada
    /// tutulur. "Bastım ama atmadı", oyunun ölü hissettirmesinin en yaygın sebebidir.</para>
    ///
    /// <para><b>Zaman dışarıdan verilir.</b> Sınıf kendi saatini okumaz; böylece bir tur
    /// boyunca atılan mermi sayısı testte saniyeler yerine mikrosaniyelerde
    /// hesaplanabilir.</para>
    /// </summary>
    public sealed class WeaponState
    {
        private readonly WeaponConfig _config;

        private float _cycleRemaining;
        private float _reloadRemaining;
        private float _bufferedFireRemaining;

        public WeaponState(WeaponConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            MagazineCapacity = Math.Max(1, _config.MagazineCapacity);
            RoundsInMagazine = MagazineCapacity;
            Reserve = Math.Min(_config.MagazineStartingReserve, _config.MagazineReserveCapacity);
        }

        public int MagazineCapacity { get; }
        public int RoundsInMagazine { get; private set; }
        public int Reserve { get; private set; }

        public bool IsReloading => _reloadRemaining > 0f;

        /// <summary>Dolumun tamamlanma oranı (0..1). İlerleme çubuğu için.</summary>
        public float ReloadProgress01 =>
            !IsReloading || _config.MagazineReloadSeconds <= 0f
                ? 0f
                : 1f - _reloadRemaining / _config.MagazineReloadSeconds;

        public WeaponPhase Phase
        {
            get
            {
                if (IsReloading) return WeaponPhase.Reloading;
                if (RoundsInMagazine > 0) return _cycleRemaining > 0f ? WeaponPhase.Cycling : WeaponPhase.Ready;
                return Reserve > 0 ? WeaponPhase.Ready : WeaponPhase.Dry;
            }
        }

        /// <summary>Atışlar arası süre. <c>roundsPerMinute</c>'dan türetilir.</summary>
        public float SecondsBetweenShots =>
            _config.FireRoundsPerMinute <= 0f ? 0f : 60f / _config.FireRoundsPerMinute;

        /// <summary>
        /// Zamanı ilerletir. <b>Tamponlanmış bir atış isteği varsa ve silah hazırsa
        /// burada tetiklenmez</b> — istek <see cref="TryFire"/> ile tüketilir, böylece
        /// ateşin tek bir çıkış noktası olur.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (_cycleRemaining > 0f) _cycleRemaining -= deltaTime;
            if (_bufferedFireRemaining > 0f) _bufferedFireRemaining -= deltaTime;

            if (_reloadRemaining <= 0f) return;

            _reloadRemaining -= deltaTime;
            if (_reloadRemaining > 0f) return;

            _reloadRemaining = 0f;
            CompleteReload();
        }

        /// <summary>
        /// Ateş etmeyi dener. Silah hazır değilse ve tuşa basılmışsa istek
        /// <c>inputBufferSeconds</c> kadar hafızada tutulur.
        /// </summary>
        /// <param name="pressedThisFrame">Bu karede ateş isteği geldi mi.</param>
        public FireResult TryFire(bool pressedThisFrame)
        {
            if (pressedThisFrame) _bufferedFireRemaining = _config.FeelInputBufferSeconds;

            bool wants = pressedThisFrame || _bufferedFireRemaining > 0f;
            if (!wants) return FireResult.Cycling;

            if (IsReloading) return FireResult.Reloading;

            if (RoundsInMagazine <= 0)
            {
                return Reserve > 0 ? FireResult.Empty : FireResult.Dry;
            }

            if (_cycleRemaining > 0f) return FireResult.Cycling;

            RoundsInMagazine--;
            _cycleRemaining = SecondsBetweenShots;
            _bufferedFireRemaining = 0f;

            return FireResult.Fired;
        }

        /// <summary>
        /// Dolum başlatır. Şarjör doluysa ya da yedek yoksa <c>false</c> döner —
        /// boşuna başlatılan bir dolum, oyuncuyu sebepsiz savunmasız bırakır.
        /// </summary>
        public bool TryStartReload()
        {
            if (IsReloading) return false;
            if (Reserve <= 0) return false;
            if (RoundsInMagazine >= MagazineCapacity) return false;

            _reloadRemaining = _config.MagazineReloadSeconds;
            return true;
        }

        /// <summary>
        /// Dolumu iptal eder. <b>Mermi transferi tamamlanmadan olmaz</b> — yarıda
        /// kesilen dolum hiç olmamış sayılır; aksi hâlde tuşa basıp bırakarak sonsuz
        /// mermi üretilebilirdi.
        /// </summary>
        public void CancelReload()
        {
            _reloadRemaining = 0f;
        }

        /// <summary>Yedeğe mermi ekler. Tavanı aşan kısım kaybolur, döner değer eklenen miktardır.</summary>
        public int AddReserve(int amount)
        {
            if (amount <= 0) return 0;

            int before = Reserve;
            Reserve = Math.Min(Reserve + amount, _config.MagazineReserveCapacity);
            return Reserve - before;
        }

        /// <summary>Silahı ilk hâline döndürür — yeni run, ya da havuzdan çıkan oyuncu.</summary>
        public void Reset()
        {
            RoundsInMagazine = MagazineCapacity;
            Reserve = Math.Min(_config.MagazineStartingReserve, _config.MagazineReserveCapacity);
            _cycleRemaining = 0f;
            _reloadRemaining = 0f;
            _bufferedFireRemaining = 0f;
        }

        /// <summary>Bir isabetin hasarı. Kafa çarpanı burada uygulanır, atış anında değil.</summary>
        public float DamageFor(bool headshot) =>
            headshot ? _config.FireDamage * _config.FireHeadshotMultiplier : _config.FireDamage;

        private void CompleteReload()
        {
            int needed = MagazineCapacity - RoundsInMagazine;
            int moved = Math.Min(needed, Reserve);

            RoundsInMagazine += moved;
            Reserve -= moved;
        }
    }
}
