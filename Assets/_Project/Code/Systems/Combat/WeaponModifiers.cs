using System;

namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Silahın taban ayarına binen dış etkiler (M-03 kartları).
    ///
    /// <para><b>Neden ayrı bir tip:</b> <see cref="WeaponState"/> (istemcinin
    /// simülasyonu) ve <see cref="ServerFireGuard"/> (sunucunun doğrulayıcısı) aynı
    /// sayılara bakmak <b>zorunda</b>. İstemcinin atış hızı artıp sunucununki
    /// artmasaydı, doğrulayıcı meşru atışları reddederdi — BUG-002'nin birebir
    /// tekrarı, ve o hata oyun testinde "arada bir tık yeniyor" diye okunmuştu.</para>
    ///
    /// <para><b>Değerler oransal ve TOPLANMIŞ gelir</b> (SYS-02 §3.1): kart katmanı
    /// içinde toplama yapılır, burada yalnızca tabana çarpılır.</para>
    ///
    /// <para><b>Saf C#, salt okunur.</b> Kart yığını değiştiğinde yenisi kurulur;
    /// yerinde değiştirilmez, çünkü iki tarafın farklı anlarda farklı değer görmesi
    /// tam olarak kaçındığımız şey.</para>
    /// </summary>
    public readonly struct WeaponModifiers
    {
        public static readonly WeaponModifiers None = new WeaponModifiers(0f, 0f, 0f, 0f, 0, 0f);

        /// <summary>Atış hızı oranı (0.15 = +%15).</summary>
        public readonly float FireRate;

        /// <summary>Dolum hızı oranı (0.40 = %40 daha hızlı).</summary>
        public readonly float ReloadSpeed;

        /// <summary>Hasar oranı.</summary>
        public readonly float Damage;

        /// <summary>
        /// Şarjör kapasitesi oranı (0.40 = silahın kendi şarjörünün +%40'ı).
        ///
        /// <para><b>2026-09-07'ye kadar mutlak mermiydi.</b> Aynı kart pompalıda
        /// kapasiteyi ikiye katlarken SMG'de beşte bir artırıyordu; kart metni her
        /// silahta başka bir şey vaat ediyordu. Gerekçenin tamamı
        /// <see cref="Bunker.Systems.Cards.CardStat.MagazineCapacity"/>'de.</para>
        /// </summary>
        public readonly float Magazine;

        /// <summary>
        /// Yedek mermi tavanına eklenen mermi, <b>mutlak sayı</b> (2026-09-10'da geri
        /// geldi — geliştirici: <i>"yedek mermi kapasite artış kartlarını geri getir,
        /// artış oranları aynı kalsın"</i>).
        ///
        /// <para><b>Neden mutlak, şarjör kartının tersine:</b> tavan artık oyuncunun
        /// HUD'da gördüğü bir sayı ve tur sonu ikmalinin ölçüsü. "+%40 tavan" cümlesi
        /// o sayıya çevrilmeden okunamaz; "+120 mermi" doğrudan okunur. Silahlar arası
        /// fark burada kusur değil: 90 tavanlı pompalıda +120 tavanı ikiye katlar ve
        /// pompalı mermisi zaten en çok sıkışan kaynak.</para>
        /// </summary>
        public readonly int Reserve;

        /// <summary>Kafa vuruşu çarpanına eklenen değer.</summary>
        public readonly float HeadshotMultiplier;

        public WeaponModifiers(float fireRate, float reloadSpeed, float damage,
                               float magazine, int reserve, float headshotMultiplier)
        {
            // Negatif bir oran silahi tersine cevirir; -1 ise sifira boler. Kart
            // degerleri bugun hep pozitif ama bir gun "ates hizi -%20, hasar +%80"
            // gibi bir kart gelirse burasi onu guvenli tutar.
            FireRate = Math.Max(-0.9f, fireRate);
            ReloadSpeed = Math.Max(-0.9f, reloadSpeed);
            Damage = Math.Max(-0.9f, damage);
            Magazine = Math.Max(-0.9f, magazine);
            Reserve = Math.Max(0, reserve);
            HeadshotMultiplier = headshotMultiplier;
        }

        public float FireRateMultiplier => 1f + FireRate;
        public float ReloadSpeedMultiplier => 1f + ReloadSpeed;
        public float DamageMultiplier => 1f + Damage;

        /// <summary>Şarjör çarpanı: <c>1 + oran</c>.</summary>
        public float MagazineMultiplier => 1f + Magazine;
    }
}
