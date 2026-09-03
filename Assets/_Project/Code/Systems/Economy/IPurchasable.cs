namespace Bunker.Systems.Economy
{
    /// <summary>
    /// Puanla satın alınabilen şey: kapı (M1-09), duvar silahı (M1-10), ileride
    /// rastgele dağıtıcı ve tuzaklar.
    ///
    /// <para><b>Neden arayüz:</b> satın alma etkileşimi tek yerde yazılsın diye. Oyuncu
    /// tarafı neye baktığını bilmez — baktığı şey <i>kaç puan</i> olduğunu ve satın
    /// alınınca <i>ne olacağını</i> kendisi söyler. Yeni bir satın alınabilir eklemek
    /// oyuncu koduna dokunmayı gerektirmez (<c>IDamageable</c> ile aynı desen).</para>
    ///
    /// <para><b>Fiyatı taşır, ödemeyi yapmaz.</b> Cüzdanı düşürmek ve otoriteyi
    /// doğrulamak sunucunun işi; bu arayüz yalnızca "ne kadar" ve "sonra ne olur"
    /// sorularına cevap verir.</para>
    /// </summary>
    public interface IPurchasable
    {
        /// <summary>Hâlâ satın alınabilir mi (alınmış bir kapı artık değildir).</summary>
        bool IsAvailable { get; }

        /// <summary>Fiyat. <c>config/balance/economy.json</c>'dan gelir.</summary>
        int Cost { get; }

        /// <summary>
        /// Oyuncuya gösterilecek metin — gri kutuda düz yazı.
        /// <b>M1-11'de yerelleştirme anahtarına dönecek</b> (ui-code.md: oyuncuya
        /// görünen hiçbir metin kod içinde düz yazı kalmaz).
        /// </summary>
        string Prompt { get; }

        /// <summary>
        /// Satın alındı. <b>Yalnızca sunucu çağırır</b> ve ödeme çoktan alınmıştır.
        /// </summary>
        void OnPurchased();
    }
}
