using System;

namespace Bunker.Systems.Rounds
{
    /// <summary>
    /// Tur olaylarının yayın noktası. M1-08 / M1-13.
    ///
    /// <para><b>Neden statik bir yayın noktası:</b> turu yürüten <c>ZombieDirector</c>
    /// <c>Bunker.AI</c>'da, silah ve puan <c>Bunker.Gameplay</c>'de yaşıyor ve bu iki
    /// assembly birbirini <b>göremez</b> (gameplay-code.md). İkisinin de gördüğü tek
    /// yer <c>Bunker.Systems</c>. <see cref="ZombieTargets"/> ile aynı desen: bağımlılık
    /// ters çevrilir, kimse kimseye doğrudan uzanmaz.</para>
    ///
    /// <para><b>Statik olmanın bedeli ve önlemi:</b> Unity'de statik alanlar Play
    /// oturumları arasında yaşar. Abonelik sızarsa ikinci oturumda olaylar iki kez
    /// tetiklenir — sessiz ve şaşırtıcı bir hata. Bu yüzden iki kural zorunlu:
    /// <b>her abone <c>OnDisable</c>'da aboneliğini bırakır</b>, ve turu yürüten taraf
    /// açılışta <see cref="Clear"/> çağırır.</para>
    ///
    /// <para>M1-11'de gerçek bir oyun durumu servisi geldiğinde bu sınıf ona devredilir.</para>
    /// </summary>
    public static class RoundSignals
    {
        /// <summary>Yeni tur başladı (tur numarası).</summary>
        public static event Action<int> RoundStarted;

        /// <summary>Tur temizlendi (tur numarası).</summary>
        public static event Action<int> RoundCleared;

        /// <summary>
        /// Tur temizlendi ve <b>kısmi yenilenme</b> zamanı geldi (2026-09-05).
        /// Birinci sayı yedek mermi oranı, ikincisi barikat tahtası oranı — ikisi de
        /// 0..1 ve <c>rounds.json</c>'daki <c>roundEnd</c> grubundan gelir.
        ///
        /// <para><b>Neden oranlar olayla taşınıyor:</b> silah <c>Bunker.Gameplay</c>'de,
        /// barikat <c>Bunker.AI</c>'da; ikisi de tur ayarını okumaz ve okumamalı. Turu
        /// yürüten taraf (tek config sahibi) sayıyı hesaplayıp yayınlar, tüketiciler
        /// yalnızca uygular. Aksi hâlde aynı denge sayısı üç ayrı bileşene bağlanırdı
        /// (config-data.md).</para>
        ///
        /// <para><b>RoundCleared'dan ayrı bir olay</b>, çünkü <c>RoundCleared</c>'ın
        /// aboneleri (kart draft'ı, ses) yenilenmeyi umursamaz ve imzasını değiştirmek
        /// hepsini kırardı.</para>
        /// </summary>
        public static event Action<float, float> RoundEndRestock;

        /// <summary>
        /// Şu an mola mı (tur açık değil).
        ///
        /// <para><b>Neden burada duruyor:</b> tezgâh <c>Bunker.Gameplay</c>'de, turu
        /// yürüten <c>ZombieDirector</c> <c>Bunker.AI</c>'da ve ikisi birbirini görmez.
        /// Tezgâhın "şimdi açılabilir miyim" sorusunu sorabildiği tek yer burası.</para>
        ///
        /// <para>Run başında <c>true</c>: oyun molayla başlar (RoundRunner).</para>
        /// </summary>
        public static bool IsBreather { get; private set; } = true;

        /// <summary>
        /// Boss olduruldu; carpani puani yazacak tarafa gider.
        ///
        /// <para><b>Neden ayri bir olay:</b> "bu bir boss'du" bilgisi yalnizca AI
        /// tarafinda var - silah hangi zombiyi vurdugunu bilmez ve bilmemeli
        /// (IDamageable'in tamami bu ayrimin uzerine kurulu).</para>
        /// </summary>
        public static event Action<float> BossKilled;

        public static void RaiseBossKilled(float pointsMultiplier) =>
            BossKilled?.Invoke(pointsMultiplier);

        /// <summary>
        /// <b>Silahın vurmadığı</b> bir öldürme: nuke eşyası, ölen zombinin patlaması,
        /// barikat. 2026-09-08.
        ///
        /// <para><b>Neden gerekti</b> (geliştirici: <i>"nuke drobunu alınca bütün
        /// zombiler ölüyor ama puan karşılığı yansımıyor"</i>): puanı bugüne kadar
        /// <b>silah</b> yazıyordu (<c>PlayerWeapon.KillConfirmed</c>). Silahın
        /// tetiklemediği her ölüm — nuke, Yıkım kartının zincirleme patlaması —
        /// sessizce puansız kalıyordu. Öldürme puanı, öldürmenin <i>aracına</i> değil
        /// <b>olayına</b> bağlı olmalı.</para>
        ///
        /// <para><b>Neden bir sinyal, doğrudan çağrı değil:</b> öldüren taraf
        /// <c>Bunker.AI</c>'da, cüzdan <c>Bunker.Gameplay</c>'de ve Gameplay AI'ı
        /// göremez (gameplay-code.md). <c>PowerupSignals</c> ile aynı desen.</para>
        ///
        /// <para><b>Yük: kaç öldürme.</b> Nuke bir seferde otuz zombi öldürür; otuz
        /// ayrı olay yayınlamak otuz ayrı <c>SyncVar</c> yazması demekti.</para>
        /// </summary>
        public static event Action<int> FieldKills;

        /// <summary>Yayınlar. <b>Yalnızca sunucu</b> çağırır (ADR-0004).</summary>
        public static void RaiseFieldKills(int count)
        {
            if (count <= 0) return;

            FieldKills?.Invoke(count);
        }

        public static void RaiseRoundStarted(int round)
        {
            IsBreather = false;
            RoundStarted?.Invoke(round);
        }

        public static void RaiseRoundCleared(int round)
        {
            IsBreather = true;
            RoundCleared?.Invoke(round);
        }

        /// <summary>
        /// Kısmi yenilenmeyi yayınlar. <b>Yalnızca turu yürüten taraf çağırır</b>
        /// (ADR-0004: kalıcı sonucu olan her şey host'ta).
        /// </summary>
        public static void RaiseRoundEndRestock(float reserveAmmoFraction01,
                                                float barricadeBoardsFraction01)
        {
            RoundEndRestock?.Invoke(reserveAmmoFraction01, barricadeBoardsFraction01);
        }

        /// <summary>
        /// Bütün abonelikleri siler.
        ///
        /// <para><b>Yalnızca oyun başlarken, hiçbir sahne nesnesi uyanmadan önce
        /// çağrılır</b> (<c>RoundSignalsBootstrap</c>). Bir sahne nesnesinin
        /// <c>Awake</c>'inden çağrılması bir sıralama yarışıdır: ondan önce uyanmış
        /// abonelerin kaydı silinir ve o aboneler sessizce hiçbir olay almaz. Bir kez
        /// yaşandı — pencerelerin bir kısmı tur başında yenilenmiyordu.</para>
        /// </summary>
        /// <summary>
        /// Molayi ERKEN bitirme istegi (2026-09-09, gelistirici: "herkes ready (F
        /// tusu) verirse zaman direk bitsin ve sonraki tur baslasin").
        ///
        /// <para><b>Neden bir sinyal:</b> "herkes hazir mi" sorusunu cevaplayan taraf
        /// oyuncular (<c>Bunker.Gameplay</c>), molayi yuruten taraf yonetmen
        /// (<c>Bunker.AI</c>) ve <b>ikisi birbirini goremiyor</b> (ARCHITECTURE.md).
        /// <c>PowerupSignals</c> ile ayni desen: bagimlilik ters cevrilir.</para>
        ///
        /// <para><b>Yalnizca sunucu yayinlar</b> (ADR-0004): tur akisi otoritenin isi
        /// ve bir istemcinin kendi basina tur baslatabilmesi, netcode.md'nin "her RPC
        /// bir guven siniridir" kuralinin en pahali ihlali olurdu.</para>
        /// </summary>
        public static event Action BreatherSkipRequested;

        /// <summary>Molayi erken bitirmeyi ister. <b>Yalnizca sunucu.</b></summary>
        public static void RaiseBreatherSkip() => BreatherSkipRequested?.Invoke();

        public static void Clear()
        {
            BreatherSkipRequested = null;
            RoundStarted = null;
            RoundCleared = null;
            RoundEndRestock = null;
            BossKilled = null;
            FieldKills = null;
            IsBreather = true;
        }
    }
}
