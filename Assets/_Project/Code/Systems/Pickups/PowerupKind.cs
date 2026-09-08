namespace Bunker.Systems.Pickups
{
    /// <summary>
    /// Ölen zombinin yere bırakabileceği eşya türü (2026-09-07).
    ///
    /// <para><b>Neden bir enum, serbest bir etki nesnesi değil:</b> eşya türü hem
    /// sunucuda (kim düşürdü), hem dünyada (ne renk, ne yazıyor), hem de arayüzde
    /// (kalan süre) okunuyor. Tür bir <i>sözleşme</i>; her yerin kendi tanımını
    /// yapması, ikinci eşyanın eklendiği gün üç yeri birden değiştirmek demekti.</para>
    ///
    /// <para><b>İkiye ayrılırlar:</b> <see cref="Health"/> ve <see cref="Ammo"/> oyuncuya
    /// bir <i>kaynak</i> verir ve etkisi anlıktır; <see cref="Slow"/>, <see cref="Freeze"/>
    /// ve <see cref="Nuke"/> <i>sahaya</i> dokunur. Bu ayrım, hangi sistemin hangi eşyaya
    /// abone olacağını belirler.</para>
    /// </summary>
    public enum PowerupKind
    {
        /// <summary>Can. Oyuncunun maksimum canının bir oranı kadar iyileştirir.</summary>
        Health,

        /// <summary>Mermi. Eldeki silahın şarjörü cinsinden yedek verir.</summary>
        Ammo,

        /// <summary>Bütün zombiler bir süre yavaşlar.</summary>
        Slow,

        /// <summary>Bütün zombiler bir süre durur.</summary>
        Freeze,

        /// <summary>Sahadaki bütün zombiler ölür. En nadir eşya.</summary>
        Nuke
    }
}
