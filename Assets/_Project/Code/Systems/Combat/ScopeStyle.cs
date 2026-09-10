namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Nişan alırken ekranda ne göründüğü (2026-09-09).
    ///
    /// <para><b>Neden bir tür, tek bir "zoom" sayısı değil</b> (geliştirici: <i>"m107
    /// scope olayında gerçekten scope'tan bakıyormuşuz gibi göster, sadece
    /// yakınlaşmasın"</i> ve <i>"HAMR scope zoomu farklı görünüyor, gerçekteki gibi
    /// uygula"</i>): görüş açısını daraltmak <i>yakınlaştırır</i> ama <b>bir optikten
    /// baktığını söylemez</b>. İki gerçek optik iki farklı şey yapar ve oyuncunun
    /// hissetmesi gereken fark tam olarak budur — yoksa iki silah aynı silahtır.</para>
    ///
    /// <para><b>Neden bir enum, arayüzde bir bayrak değil:</b> hangi silahın nasıl
    /// göründüğü bir <i>içerik</i> kararı ve <c>weapons.json</c>'da yaşamalı
    /// (config-data.md). Arayüzde <c>if (id == "weapon.sniper")</c> yazmak, üçüncü
    /// dürbünlü silah geldiğinde arayüz kodunu değiştirmek demekti.</para>
    /// </summary>
    public enum ScopeStyle
    {
        /// <summary>Dürbün yok. Sağ tık hiçbir şey yapmaz.</summary>
        None = 0,

        /// <summary>
        /// Klasik <b>nişancı dürbünü</b> (M107 + TAN_LR_Scope_01).
        ///
        /// <para>Ekran kararır, ortada yuvarlak bir alan kalır; ince artı ve mil
        /// noktaları. <b>Çevreni feda edersin</b> — dürbünün içindeyken yanından gelen
        /// zombiyi görmezsin ve bu, uzun menzilli gücün bedeli.</para>
        /// </summary>
        LongRange,

        /// <summary>
        /// <b>Prizmalı optik</b> (M4 + ELCAN, gerçekteki HAMR sınıfı).
        ///
        /// <para>Gövde görünür kalır, alan çok daha geniş, nişangâh kırmızı bir
        /// <i>chevron</i>. Ekran kararmaz — yalnızca çevre hafif söner.</para>
        ///
        /// <para><b>Fark bir süs değil:</b> prizmalı bir optikte iki gözün de açık
        /// kalabilmesi ve çevreyi görebilmek o optiğin <i>varlık sebebi</i>. Nişancı
        /// dürbünü tam tersine çevreni alır. Oyunda bu, "M4 kalabalıkta da
        /// kullanılabilir, M107 kullanılamaz" cümlesine dönüşüyor.</para>
        /// </summary>
        Prism
    }
}
