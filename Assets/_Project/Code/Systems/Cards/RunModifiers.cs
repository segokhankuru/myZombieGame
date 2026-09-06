namespace Bunker.Systems.Cards
{
    /// <summary>
    /// Kart yığını <b>ve</b> tezgâh yükseltmelerinin toplamı. Oyunun tek okuma noktası.
    ///
    /// <para><b>Neden tek nokta:</b> silah, can ve ekonomi iki ayrı kaynağı kendi
    /// içinde toplasaydı aynı iş kuralı üç yere dağılırdı (csharp-code.md) ve üçüncü
    /// kaynak geldiğinde (M-02'nin drop'ları, SYS-03'ün silah alışkanlığı) üç yeri
    /// birden değiştirmek gerekirdi.</para>
    ///
    /// <para><b>Kartlar ve tezgâh TOPLANIR</b> (SYS-02 §3.1): +%20 kart ve +%8 tezgâh
    /// kademesi 1.28x eder, 1.296x değil. Çarpımsal olsalardı iki kaynağın birlikte
    /// büyümesi geç turlarda dengeyi kaçırırdı.</para>
    /// </summary>
    public static class RunModifiers
    {
        /// <summary>Bu run'ın kartları.</summary>
        public static CardLoadout Loadout => CardSignals.Loadout;

        /// <summary>
        /// Bu run'ın tezgâh kademeleri. Tezgâh kurulmadan önce <c>null</c> —
        /// o hâlde yalnızca kartlar sayılır.
        /// </summary>
        public static ShopState Shop { get; private set; }

        /// <summary>Tezgâhı bağlar. <c>ShopController</c> boot'ta bir kez çağırır.</summary>
        public static void AttachShop(ShopState shop) => Shop = shop;

        /// <summary>Kart + tezgâh toplamı.</summary>
        public static float Total(CardStat stat) =>
            CardSignals.Loadout.Total(stat) + (Shop?.Total(stat) ?? 0f);

        /// <summary>Oransal bir stat'ın çarpanı: <c>1 + toplam</c>.</summary>
        public static float Multiplier(CardStat stat) => 1f + Total(stat);

        /// <summary>
        /// Alınan hasarın çarpanı. Tezgâhta hasar azaltma hattı <b>yok</b> —
        /// zırh bir kimlik kararı (Kan etiketi) ve tezgâh kimlik vermez.
        /// </summary>
        public static float DamageTakenMultiplier => CardSignals.Loadout.DamageTakenMultiplier;

        /// <summary>Yeni run: tezgâh kademeleri sıfırlanır. Kartları CardSignals sıfırlar.</summary>
        public static void ResetRun() => Shop?.Reset();

        /// <summary>Oyun açılışı: bağlantı da kopar (statik sızıntı önlemi).</summary>
        public static void Clear() => Shop = null;
    }
}
