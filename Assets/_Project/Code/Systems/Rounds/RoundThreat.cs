namespace Bunker.Systems.Rounds
{
    /// <summary>
    /// Bu turun zombisi <b>ne kadar vuruyor ve ne kadar hızlı</b> — arayüzün okuduğu
    /// tek yer (2026-09-09).
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"zombilerin hasarını ve her tur artan
    /// hız değerini ekranın solunda belirt."</i> Bilgi daha önce her zombinin can
    /// barının yanında duruyordu ve orada iki sorunu vardı: kalabalıkta kırk kere
    /// tekrar ediyordu (PILLAR-04'ün tam tersi) ve zombi ekranda yokken —yani turun
    /// başında, hazırlık yaparken— hiç görünmüyordu. Oysa "bu tur ne kadar sert"
    /// sorusunun sorulduğu an tam olarak odur.</para>
    ///
    /// <para><b>Neden statik bir yayın noktası, arayüzün kendi hesabı değil:</b>
    /// <c>Bunker.UI</c>, <c>Bunker.AI</c>'ı göremez (ARCHITECTURE.md) ve turun
    /// ölçeklemesini bilen taraf <c>ZombieDirector</c>. Arayüzün aynı hesabı
    /// tekrarlaması, aynı iş kuralının iki yerde durması olurdu (csharp-code.md) — ve
    /// ikisi ilk denge ayarında sessizce ayrışırdı: ekranda yazan sayı ile oyuncuyu
    /// öldüren sayı farklı olurdu, ki bu gösterilebilecek en kötü sayıdır.</para>
    ///
    /// <para><b>Saf veri, olay değil:</b> arayüz her kare okuyor, yani bir olayın
    /// tetiklediği önbellek hiçbir şey kazandırmazdı. Değerler tur başında bir kez
    /// yazılıyor.</para>
    /// </summary>
    public static class RoundThreat
    {
        /// <summary>Şu anki tur. 0 = henüz başlamadı.</summary>
        public static int Round { get; private set; }

        /// <summary>
        /// Bu turun normal zombisinin <b>vuruş başına</b> hasarı.
        ///
        /// <para><b>2026-09-11'den beri turla ARTIYOR</b>: taban
        /// <c>zombie.json → attack.damage</c>, çarpanı <c>rounds.json → damage</c>
        /// (<see cref="RoundScaling.ZombieDamageMultiplierForRound"/>). Burada yazan
        /// sayı, zombinin gerçekten vurduğu sayının kendisi — ikisi aynı çarpandan
        /// geliyor.</para>
        /// </summary>
        public static float ZombieDamage { get; private set; }

        /// <summary>Bu turun zombi hızı, metre/saniye. <b>Tur kademesine göre artar.</b></summary>
        public static float ZombieSpeedMetersPerSecond { get; private set; }

        /// <summary>
        /// Hız kademesinin adı (YURUME / TEMPOLU / KOSU). <b>Çıplak bir sayı bir
        /// eşiği anlatmaz:</b> 3.1 m/s'nin oyuncunun 5 m/s'sine göre ne demek olduğunu
        /// söyleyen şey kademenin adıdır.
        /// </summary>
        public static ZombieSpeedTier SpeedTier { get; private set; }

        /// <summary>
        /// Bu turda kaç boss çıkacak (2026-09-11). 0 = boss turu değil. Sayı tur
        /// başında sabitlenir; co-op ve tek oyuncuda farklı hızda artar.
        /// </summary>
        public static int BossCount { get; private set; }

        /// <summary>Bu tur bir boss turu mu.</summary>
        public static bool IsBossRound => BossCount > 0;

        /// <summary>Boss'un vuruş hasarı — boss turlarında ayrıca gösterilir.</summary>
        public static float BossDamage { get; private set; }

        /// <summary>
        /// Tur başında bir kez yazılır. <b>Tek çağıran</b> <c>ZombieDirector</c>;
        /// ikinci bir yazar, ekrandaki sayının hangisi olduğunu belirsiz kılardı.
        /// </summary>
        public static void Set(int round, float zombieDamage, float speedMetersPerSecond,
                               ZombieSpeedTier tier, int bossCount, float bossDamage)
        {
            Round = round;
            ZombieDamage = zombieDamage;
            ZombieSpeedMetersPerSecond = speedMetersPerSecond;
            SpeedTier = tier;
            BossCount = bossCount < 0 ? 0 : bossCount;
            BossDamage = bossDamage;
        }

        /// <summary>
        /// Yeni run ve oyun açılışı: sayılar sıfırlanır.
        ///
        /// <para>Bu satırın eksikliği, ikinci run'ın birincinin 20. tur sayılarıyla
        /// açılması demek — <c>CardLoadout.Reset</c> ile aynı sınıf hata.</para>
        /// </summary>
        public static void Clear()
        {
            Round = 0;
            ZombieDamage = 0f;
            ZombieSpeedMetersPerSecond = 0f;
            SpeedTier = ZombieSpeedTier.Walk;
            BossCount = 0;
            BossDamage = 0f;
        }
    }
}
