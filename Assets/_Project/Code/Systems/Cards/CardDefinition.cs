using System;

namespace Bunker.Systems.Cards
{
    /// <summary>Kartın etiketi. Üçü toplanınca etiket bonusu açılır (SYS-02 §2).</summary>
    public enum CardTag
    {
        Ballistics,
        Demolition,
        Blood,
        Tempo,
        Loot,

        /// <summary>Takım kartı. Etiket bonusu saymaz, yalnızca co-op'ta çıkar.</summary>
        Team
    }

    /// <summary>
    /// Bir kartın oyuna dokunduğu yer.
    ///
    /// <para><b>Neden bir enum, serbest metin değil:</b> kartın etkisi bir <i>sözleşme</i>.
    /// Metin olsaydı hangi sistemin neyi okuyacağı yazıya bağlı olurdu ve bir yazım
    /// hatası sessizce hiçbir şey yapmayan bir kart üretirdi — kart sisteminin en pahalı
    /// hata türü, çünkü oyuncu kartı seçer ve hiçbir şey hissetmez.</para>
    ///
    /// <para><b>Buradaki her değer, var olan bir sisteme bağlanabilir.</b> SYS-02'nin
    /// havuzunda 55 kart var ama çoğu henüz olmayan sistemlere dokunuyor (patlama,
    /// kayma, drop, diriltme). Bu liste <b>bugün uygulanabilir</b> olanları tarif eder;
    /// gerisi tasarımda bekler.</para>
    /// </summary>
    public enum CardStat
    {
        None = 0,

        /// <summary>Silah hasarı, oransal (0.20 = +%20). Kartlar arasında TOPLANIR (§3.1).</summary>
        WeaponDamage,

        /// <summary>Atış hızı, oransal.</summary>
        FireRate,

        /// <summary>Dolum süresi, oransal AZALMA (0.40 = %40 daha hızlı).</summary>
        ReloadSpeed,

        /// <summary>
        /// Şarjör kapasitesi, <b>oransal</b> (0.20 = silahın kendi şarjörünün +%20'si).
        ///
        /// <para><b>2026-09-07'ye kadar mutlak mermiydi</b> ve aynı kart silaha göre
        /// bambaşka bir ödüldü: +6 mermi tabancada (12) +%50, pompalıda (6) +%100,
        /// SMG'de (30) +%20. Oyuncunun kartı okuyup ne alacağını bilmesi imkânsızdı.
        /// Oran, kartı bütün silahlarda aynı vaade bağlar.</para>
        /// </summary>
        MagazineCapacity,

        /// <summary>Kafa vuruşu çarpanına eklenir (1.0 = 2x'ten 3x'e).</summary>
        HeadshotMultiplier,

        /// <summary>Bıçak hasarı, oransal.</summary>
        MeleeDamage,

        /// <summary>Yürüme hızı, oransal.</summary>
        MoveSpeed,

        /// <summary>Maksimum can, oransal.</summary>
        MaxHealth,

        /// <summary>
        /// Alınan hasar azaltma — <b>efektif can olarak</b> uygulanır (§3.2).
        /// 0.25 = hasar / 1.25. Yüzde olarak istiflenirse 5 kart ölümsüzlük yapar.
        /// </summary>
        EffectiveHealth,

        /// <summary>Can yenilenme gecikmesi, oransal AZALMA.</summary>
        RegenDelay,

        /// <summary>Öldürme puanı, oransal.</summary>
        KillPoints,

        /// <summary>Barikat tamir puanı, oransal.</summary>
        RepairPoints,

        /// <summary>Barikat tamir hızı, oransal.</summary>
        RepairSpeed,

        /// <summary>Vurulan zombinin yavaşlaması, oransal.</summary>
        SlowOnHit,

        /// <summary>Mermi bir sonraki zombiye geçer. Değer = kaç geçiş.</summary>
        Penetration,

        /// <summary>
        /// Ölen zombi PATLAR. Değer = patlamanın hasarı, ölenin maksimum canının
        /// oranı olarak (0.60 = maks canının %60'ı kadar hasar).
        ///
        /// <para><b>Yıkım etiketinin çekirdeği</b> (2026-09-05). Yarıçap
        /// <c>zombie.json → cards.explosionRadiusMeters</c>'ten gelir; kart yalnızca
        /// gücü söyler.</para>
        /// </summary>
        ExplodeOnKill,

        /// <summary>Öldürme başına iyileşme, maksimum canın oranı (0.05 = %5).</summary>
        HealOnKill,

        /// <summary>Öldürme başına yedeğe eklenen mermi (mutlak sayı).</summary>
        AmmoOnKill,

        /// <summary>
        /// Ölen zombinin yere eşya bırakma şansı, <b>oransal</b> (0.60 = şans x1.6).
        ///
        /// <para>Taban şans <c>zombie.json → drops.chance01</c>; kart yalnızca onu
        /// çarpar. İki ayrı yerde iki taban şans olsaydı, oyuncunun eşya sıklığını
        /// öğrenmesi imkânsız olurdu.</para>
        /// </summary>
        DropRate,

        /// <summary>
        /// Koşabilme süresi, <b>oransal</b> (0.50 = +%50 daha uzun koşu). 2026-09-07.
        ///
        /// <para><b>Neden süre, neden hız değil:</b> koşu hızını artırmak zombi hız
        /// kademelerini (<c>rounds.json → speed</c>) anlamsız kılar — geç turda
        /// yakalanabilir olmak turların zorlaşmasının yarısı. Süreyi uzatmak ise
        /// <i>menzili</i> büyütür: aynı hızla daha uzağa gidebilirsin. Kaçışın
        /// karakteri değişmez, mesafesi değişir.</para>
        ///
        /// <para>Taban süre <c>player.json → sprint.maxSeconds</c>; kart yalnızca onu
        /// çarpar (config-data.md: iki ayrı taban, öğrenilemeyen bir kural olurdu).</para>
        /// </summary>
        SprintDuration
    }

    /// <summary>
    /// Bir kartın tanımı. <b>İçerikten üretilir</b> (<c>config/content/cards.json</c>),
    /// koda gömülmez.
    ///
    /// <para><b>Id kalıcıdır</b> (config-data.md): kaydedilen yığınlar, telemetri
    /// geçmişi ve ileride mod desteği ona bağlanır. Görünen ad değişebilir, id
    /// değişemez.</para>
    /// </summary>
    public readonly struct CardDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly CardTag Tag;
        public readonly CardStat Stat;
        public readonly float Value;
        public readonly bool SoloValid;
        public readonly bool CoopValid;

        /// <summary>
        /// Bu kart <b>bir kez</b> alınır ve sonra havuzdan çıkar.
        ///
        /// <para>Varsayılan <c>false</c>: kartların çoğu tekrar çıkabilir ve etkileri
        /// toplanır (geliştirici kararı, 2026-09-05). Alınan her kartın havuzdan
        /// silinmesi, 21 kartlık havuzu yirmi turda tüketiyor ve geç turlarda draft'ı
        /// boş açıyordu. Ayrıca tekrar edebilen kart bir <b>karar</b> üretir: aynı şeyi
        /// bir daha mı, yeni bir şey mi.</para>
        ///
        /// <para><c>true</c> yalnızca mutlak bir şeyi bir kez değiştiren kartlar için
        /// (kafa çarpanı 2x→3x, tamir puanı iki katı). İkinci kopyası ya hiçbir şey
        /// yapmaz ya da kartın metniyle yalan söyler.</para>
        /// </summary>
        public readonly bool Unique;

        public CardDefinition(string id, string displayName, string description,
                              CardTag tag, CardStat stat, float value,
                              bool soloValid = true, bool coopValid = true,
                              bool unique = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? id;
            Description = description ?? string.Empty;
            Tag = tag;
            Stat = stat;
            Value = value;
            SoloValid = soloValid;
            CoopValid = coopValid;
            Unique = unique;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id);

        /// <summary>Bu kart verilen bağlamda çıkabilir mi (SYS-02 §6).</summary>
        public bool AllowedIn(bool solo) => solo ? SoloValid : CoopValid;
    }
}
