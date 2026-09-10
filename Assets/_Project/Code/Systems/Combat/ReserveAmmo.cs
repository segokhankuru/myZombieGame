using System;

namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Yedek mermi kuralları — <b>tek yerde</b>. 2026-09-10.
    ///
    /// <para><b>Neden ayrı bir sınıf</b> (geliştirici: <i>"yedek mermi konusu düzgün
    /// çalışmıyor hissi veriyor"</i>): kural üç ayrı yerde yaşıyordu — tur sonu
    /// ikmali tavana kırpıyor, yerden toplanan mermi ve öldürme ödülü kırpmıyordu,
    /// kart etkisi ise yalnızca eldeki silaha uygulanıyordu. Oyuncunun gördüğü şey
    /// bir kural değil, silaha ve mermiyi nereden aldığına göre değişen bir
    /// sonuçtu. Görünmeyen bir kural öğrenilemez.</para>
    ///
    /// <para><b>Dört kural</b> (geliştirici, 2026-09-10):</para>
    /// <list type="number">
    /// <item>Tur sonunda her silah <b>kendi tavanının yarısı</b> kadar mermi alır
    /// (oran <c>rounds.json → roundEnd.reserveAmmoFraction01</c>).</item>
    /// <item>İkmal sonrası tavanı <b>aşmaz</b>.</item>
    /// <item>Tavan <b>kartlarla</b> büyür (<c>CardStat.ReserveCapacity</c>) — sahip
    /// olunan her silahta, yalnızca eldekinde değil.</item>
    /// <item>Tavanın üstüne <b>yalnızca satın alarak</b> çıkılır. Fazlalık harcanır
    /// ve geri gelmez: tüketildikçe sayı tavana iner ve bedava gelen mermi onu bir
    /// daha yukarı taşımaz.</item>
    /// </list>
    ///
    /// <para><b>Saf C#</b>: kural sahnesiz, milisaniyede test edilir. Sunucu da istemci
    /// de aynı sayıyı buradan alır — iki tarafın ayrı kırpması BUG-001'in sınıfıdır.</para>
    /// </summary>
    public static class ReserveAmmo
    {
        /// <summary>
        /// Tur sonu ikmali (kural 1 ve 2): tavanın oranı kadar, <b>tavanı aşmadan</b>.
        ///
        /// <para><b>Eksiğin değil tavanın oranı:</b> eksiğin oranı olsaydı mermisi
        /// bitmiş oyuncu en az mermiyi alırdı — cezanın üstüne ceza.</para>
        /// </summary>
        /// <returns>Yedeğe eklenecek mermi. Tavandaysa ya da üstündeyse sıfır.</returns>
        public static int RoundEndRestock(int reserve, int capacity, float fraction01)
        {
            if (capacity <= 0 || fraction01 <= 0f) return 0;
            if (fraction01 > 1f) fraction01 = 1f;

            int amount = (int)Math.Round(capacity * fraction01, MidpointRounding.AwayFromZero);
            return UpToCapacity(reserve, capacity, amount);
        }

        /// <summary>
        /// Bedava gelen mermi (kural 4): tur sonu ikmali, yerden toplama, öldürme
        /// ödülü. <b>Yalnızca tavana kadar</b> doldurur.
        ///
        /// <para><b>Satın alma buradan geçmez.</b> Ödenen merminin kırpılması, parası
        /// gitmiş ama mermisi gelmemiş bir oyuncu demektir — 2026-09-07'de tavanın
        /// tamamen kaldırılma sebebi tam olarak buydu.</para>
        ///
        /// <para><b>Tavanın üstündeki fazlalığı geri almaz</b>: satın alınmış 340
        /// mermiyle yerden mermi toplayan oyuncu 340'ta kalır, 300'e düşmez.</para>
        /// </summary>
        /// <returns>Yedeğe eklenecek mermi (0 .. <paramref name="amount"/>).</returns>
        public static int UpToCapacity(int reserve, int capacity, int amount)
        {
            if (amount <= 0) return 0;

            int headroom = capacity - reserve;
            if (headroom <= 0) return 0;

            return amount < headroom ? amount : headroom;
        }
    }
}
