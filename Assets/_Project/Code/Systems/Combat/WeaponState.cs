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
        private readonly WeaponDefinition _config;

        private float _cycleRemaining;
        private float _reloadRemaining;
        private float _bufferedFireRemaining;

        private WeaponModifiers _mods = WeaponModifiers.None;

        /// <summary>Uretilen ayardan (baslangic silahi) kurar. Testler bunu kullanir.</summary>
        public WeaponState(WeaponConfig config) : this(WeaponDefinition.FromConfig(config)) { }

        public WeaponState(WeaponDefinition config)
        {
            // Struct: null olamaz ama GECERSIZ olabilir (bos id). Gecersiz bir tanimla
            // kurulan silah, hic ates etmeyen bir silahtir - sessiz kalmasin.
            if (!config.IsValid)
                throw new ArgumentException("Gecersiz silah tanimi (id bos).", nameof(config));

            _config = config;

            RoundsInMagazine = MagazineCapacity;
            Reserve = _config.StartingReserve;
        }

        /// <summary>
        /// Kart etkilerini uygular (M-03).
        ///
        /// <para><b>Sarjordeki mermi TASINIR.</b> Kapasite buyudugunde silahi
        /// kendiliginden doldurmak bedava bir dolum olurdu; kucultmek gerekirse
        /// fazlasi kirpilir.</para>
        /// </summary>
        public void ApplyModifiers(in WeaponModifiers modifiers)
        {
            _mods = modifiers;

            if (RoundsInMagazine > MagazineCapacity) RoundsInMagazine = MagazineCapacity;
        }

        /// <summary>
        /// Kart etkileriyle şarjör kapasitesi: <b>silahın kendi kapasitesinin katı</b>
        /// (2026-09-07). Yukarı yuvarlanır — 6 mermilik pompalıda +%20, "hiçbir şey"
        /// değil bir mermidir; aşağı yuvarlamak kartı bazı silahlarda sessizce
        /// etkisiz bırakırdı.
        /// </summary>
        public int MagazineCapacity =>
            Math.Max(1, (int)Math.Ceiling(_config.MagazineCapacity * _mods.MagazineMultiplier));

        /// <summary>
        /// Yedek mermi tavanı: silahın kendi <c>reserveCapacity</c>'si artı kart
        /// katkısı (2026-09-10).
        ///
        /// <para><b>Kısmi bir tavan.</b> 2026-09-07'de sınır tamamen kalkmıştı çünkü
        /// tek görünür sonucu satın alınan merminin sessizce buharlaşmasıydı. Geri
        /// gelen şey o değil: tavan yalnızca <b>bedava gelen</b> mermiyi sınırlar — tur
        /// sonu ikmali, yerden toplama, öldürme ödülü. Tavanı <b>yalnızca satın alınan
        /// mermi</b> aşabilir, çünkü orada buharlaşan şey ödenmiş bir bedel olurdu
        /// (2026-09-10, geliştiricinin dört kuralı: <see cref="ReserveAmmo"/>).</para>
        ///
        /// <para>Aynı zamanda ikmalin <b>ölçü birimi</b>: bir tur sonu bu sayının
        /// <c>rounds.json → roundEnd.reserveAmmoFraction01</c> kadarını verir.</para>
        /// </summary>
        public int ReserveCapacity => Math.Max(0, _config.ReserveCapacity + _mods.Reserve);

        /// <summary>Tur sonu ikmalinin ölçü birimi — tavanın kendisi.</summary>
        public int ReserveRestockReference => ReserveCapacity;

        /// <summary>Kart etkileriyle dolum suresi.</summary>
        public float ReloadSeconds => _config.ReloadSeconds / _mods.ReloadSpeedMultiplier;
        public int RoundsInMagazine { get; private set; }
        public int Reserve { get; private set; }

        public bool IsReloading => _reloadRemaining > 0f;

        /// <summary>Dolumun tamamlanma oranı (0..1). İlerleme çubuğu için.</summary>
        public float ReloadProgress01 =>
            !IsReloading || ReloadSeconds <= 0f
                ? 0f
                : 1f - _reloadRemaining / ReloadSeconds;

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
            _config.RoundsPerMinute <= 0f
                ? 0f
                : 60f / (_config.RoundsPerMinute * _mods.FireRateMultiplier);

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
            if (pressedThisFrame) _bufferedFireRemaining = _config.InputBufferSeconds;

            bool wants = pressedThisFrame || _bufferedFireRemaining > 0f;
            if (!wants) return FireResult.Cycling;

            // POMPALI DOLUMU KESILEBILIR (2026-09-06). Mermi mermi dolan bir silahta
            // "dolum bitene kadar ates edemezsin" kurali, dolumu bolunebilir yapmanin
            // butun anlamini goturur - iki fisek koyup surunun ustune donmek bir
            // KARAR olmali. Sarjorde mermi varsa dolum kesilir ve ates edilir.
            if (IsReloading)
            {
                if (!_config.ReloadPerShell || RoundsInMagazine <= 0) return FireResult.Reloading;

                _reloadRemaining = 0f;
            }

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

            _reloadRemaining = ReloadSeconds;
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

        /// <summary>
        /// Yedeğe mermi ekler. <b>Tavan yok</b> (2026-09-07): eklenen ne varsa girer,
        /// dönen değer eklenen miktardır.
        ///
        /// <para><b>Neden tavan kaldırıldı:</b> tavanın oyuncuya görünen tek sonucu,
        /// duvardan satın aldığı ya da yerden topladığı merminin sessizce yok
        /// olmasıydı. Görünmeyen bir kural, öğrenilemeyen bir kuraldır.</para>
        /// </summary>
        public int AddReserve(int amount)
        {
            if (amount <= 0) return 0;

            Reserve += amount;
            return amount;
        }

        /// <summary>
        /// Yedekten mermi <b>siler</b> (2026-09-09: olum cezasi).
        ///
        /// <para><b>Neden ayri bir metot, negatif AddReserve degil:</b> AddReserve'in
        /// sozlesmesi "eklenen ne varsa girer" ve negatifi sessizce yutuyor. O
        /// sozlesmeyi gevsetmek, mermi ekleyen her cagriya "ya negatif gelirse"
        /// sorusunu tasimak olurdu. Silme ayri bir niyet, ayri bir metot.</para>
        ///
        /// <para>Sifirin altina inmez.</para>
        /// </summary>
        public void RemoveReserve(int amount)
        {
            if (amount <= 0) return;

            Reserve = Math.Max(0, Reserve - amount);
        }

        /// <summary>Silahı ilk hâline döndürür — yeni run, ya da havuzdan çıkan oyuncu.</summary>
        public void Reset()
        {
            RoundsInMagazine = MagazineCapacity;
            Reserve = _config.StartingReserve;
            _cycleRemaining = 0f;
            _reloadRemaining = 0f;
            _bufferedFireRemaining = 0f;
        }

        /// <summary>Bir isabetin hasarı. Kafa çarpanı burada uygulanır, atış anında değil.</summary>
        public float DamageFor(bool headshot)
        {
            float damage = _config.Damage * _mods.DamageMultiplier;

            return headshot
                ? damage * (_config.HeadshotMultiplier + _mods.HeadshotMultiplier)
                : damage;
        }

        /// <summary>
        /// Dolum adımı tamamlandı.
        ///
        /// <para><b>Pompalıda tek fişek</b> (2026-09-06): şarjör dolana ya da yedek
        /// bitene kadar adım tekrarlanır. Aradaki her an ateş etmeye açıktır —
        /// <see cref="TryFire"/> dolumu keser. Pompalıyı pompalı yapan karar budur:
        /// iki fişek koyup dönmek mi, altıyı da doldurmak mı.</para>
        /// </summary>
        private void CompleteReload()
        {
            if (_config.ReloadPerShell)
            {
                if (Reserve <= 0 || RoundsInMagazine >= MagazineCapacity) return;

                RoundsInMagazine++;
                Reserve--;

                // Daha dolacak yer ve mermi varsa bir adim daha.
                if (Reserve > 0 && RoundsInMagazine < MagazineCapacity)
                {
                    _reloadRemaining = ReloadSeconds;
                }

                return;
            }

            int needed = MagazineCapacity - RoundsInMagazine;
            int moved = Math.Min(needed, Reserve);

            RoundsInMagazine += moved;
            Reserve -= moved;
        }
    }
}
