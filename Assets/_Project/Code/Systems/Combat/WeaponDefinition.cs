using System;
using Bunker.Systems.Config;

namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Bir silahın tanımı. <b>İçerikten üretilir</b> (<c>config/content/weapons.json</c>),
    /// koda gömülmez.
    ///
    /// <para><b>Neden <c>WeaponConfig</c>'in yanına ayrı bir tip</b> (2026-09-06):
    /// üretilen <c>WeaponConfig</c> <i>tek bir</i> silahı tarif eder — config importer
    /// düz skaler alanlar üretir, bir liste üretemez. Farklı silahlar bir
    /// <b>liste</b> ve kartlarla aynı yolu izliyor (<c>CardDefinition</c>): içerik
    /// JSON'unda tanımlı, katalog varlığına aktarılıyor, çalışma anında okunuyor.</para>
    ///
    /// <para><b>Id kalıcıdır</b> (config-data.md): kaydedilen silah, telemetri ve duvar
    /// satın alma noktaları ona bağlanır. Görünen ad değişebilir, id değişemez.</para>
    ///
    /// <para><b>Saf C#.</b> Silah durumunun (<see cref="WeaponState"/>) ve sunucu
    /// doğrulayıcısının (<see cref="ServerFireGuard"/>) tek bildiği tip bu — ikisi de
    /// Unity açmadan test edilebilir kalır (ÇK-16).</para>
    /// </summary>
    public readonly struct WeaponDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;

        /// <summary>Atış başına taban hasar (kart çarpanları ayrı).</summary>
        public readonly float Damage;

        /// <summary>Dakikadaki atış. Hissedilen atış hızının tek sayısı.</summary>
        public readonly float RoundsPerMinute;

        public readonly float HeadshotMultiplier;
        public readonly float RangeMeters;

        /// <summary>Dağılma açısı. Pompalıda saçmanın yayılımı, diğerlerinde nişan hatası.</summary>
        public readonly float SpreadDegrees;

        /// <summary>
        /// Tek tetikte çıkan saçma sayısı. <b>1 = normal silah</b>, fazlası pompalı.
        ///
        /// <para>Hasar saçma <b>başına</b>dır: 8 saçma × 22 hasar, yakında 176, uzakta
        /// dağıldığı için çok daha az. Menzil ayarını yapan şey budur.</para>
        /// </summary>
        public readonly int PelletCount;

        public readonly int MagazineCapacity;
        public readonly int ReserveCapacity;
        public readonly int StartingReserve;
        public readonly float ReloadSeconds;

        public readonly float RecoilPitchPerShot;
        public readonly float RecoilYawPerShot;
        public readonly float RecoilRecoveryPerSecond;
        public readonly float RecoilMaxPitch;

        public readonly float TracerSeconds;
        public readonly float HitMarkerSeconds;
        public readonly float InputBufferSeconds;

        /// <summary>Duvardan satın alma fiyatı. <c>0</c> = satılmıyor (başlangıç silahı).</summary>
        public readonly int Price;

        /// <summary>Mermi dolumunun fiyatı.</summary>
        public readonly int AmmoPrice;

        /// <summary>
        /// Dolum <b>mermi mermi</b> mi ilerliyor (pompalı), yoksa şarjör bir kerede mi
        /// dolduruluyor.
        ///
        /// <para><b>Neden bir bayrak, iki ayrı silah sınıfı değil</b> (2026-09-06,
        /// geliştirici: <i>"tek şarjör dolduruyormuş düşüncesi pompalı metasına
        /// aykırı"</i>): doğru gözlem. Pompalıyı pompalı yapan şey, dolumun
        /// <b>bölünebilir</b> olması — iki fişek koyup ateş edebilmek bir karardır ve
        /// o karar silahın karakteridir. Bayrak açıkken <see cref="ReloadSeconds"/>
        /// TEK BIR merminin süresidir.</para>
        /// </summary>
        public readonly bool ReloadPerShell;

        public WeaponDefinition(string id, string displayName, float damage, float roundsPerMinute,
                                float headshotMultiplier, float rangeMeters, float spreadDegrees,
                                int pelletCount, int magazineCapacity, int reserveCapacity,
                                int startingReserve, float reloadSeconds,
                                float recoilPitchPerShot, float recoilYawPerShot,
                                float recoilRecoveryPerSecond, float recoilMaxPitch,
                                float tracerSeconds, float hitMarkerSeconds,
                                float inputBufferSeconds, int price, int ammoPrice,
                                bool reloadPerShell = false)
        {
            ReloadPerShell = reloadPerShell;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? id;
            Damage = damage;
            RoundsPerMinute = roundsPerMinute;
            HeadshotMultiplier = headshotMultiplier;
            RangeMeters = rangeMeters;
            SpreadDegrees = spreadDegrees;
            PelletCount = pelletCount < 1 ? 1 : pelletCount;
            MagazineCapacity = magazineCapacity;
            ReserveCapacity = reserveCapacity;
            StartingReserve = startingReserve;
            ReloadSeconds = reloadSeconds;
            RecoilPitchPerShot = recoilPitchPerShot;
            RecoilYawPerShot = recoilYawPerShot;
            RecoilRecoveryPerSecond = recoilRecoveryPerSecond;
            RecoilMaxPitch = recoilMaxPitch;
            TracerSeconds = tracerSeconds;
            HitMarkerSeconds = hitMarkerSeconds;
            InputBufferSeconds = inputBufferSeconds;
            Price = price;
            AmmoPrice = ammoPrice;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id);

        /// <summary>
        /// Başlangıç silahı: <c>config/balance/weapon.json</c>'dan üretilen tanım.
        ///
        /// <para><b>Neden hâlâ ayrı bir dosya:</b> başlangıç silahı bir <i>denge
        /// referansı</i> — bütün eğriler (zombi canı, ekonomi) onun hasarına göre
        /// ayarlandı ve <c>weapon.json</c>'daki açıklamalar o gerekçeyi taşıyor.
        /// Katalogdaki diğer silahlar ona <b>göre</b> tanımlanır.</para>
        /// </summary>
        public static WeaponDefinition FromConfig(WeaponConfig config, string id = "weapon.pistol",
                                                  string displayName = "TABANCA")
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            return new WeaponDefinition(
                id, displayName,
                config.FireDamage, config.FireRoundsPerMinute, config.FireHeadshotMultiplier,
                config.FireRangeMeters, config.FireSpreadDegrees, pelletCount: 1,
                config.MagazineCapacity, config.MagazineReserveCapacity,
                config.MagazineStartingReserve, config.MagazineReloadSeconds,
                config.RecoilPitchDegreesPerShot, config.RecoilYawDegreesPerShot,
                config.RecoilRecoverySpeedDegreesPerSecond, config.RecoilMaxPitchDegrees,
                config.FeelTracerSeconds, config.FeelHitMarkerSeconds,
                config.FeelInputBufferSeconds,
                price: 0, ammoPrice: 0);
        }
    }
}
