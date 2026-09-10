using System;

namespace Bunker.Systems.Ui
{
    /// <summary>
    /// Oyun içi duraklatma menüsünün durumu. M-04.
    ///
    /// <para><b>Neden ayrı bir sınıf:</b> kart ekranı ve tezgâh <c>CardSignals</c>'ta
    /// yaşıyor çünkü ikisi de kart sisteminin parçası. Duraklatma değil — o, oyunun
    /// kendisine ait. <c>CardSignals</c>'a koymak, üçüncü bir menü geldiğinde
    /// "menü durumu" bilgisinin kart sisteminde saklandığı bir yapı bırakırdı.</para>
    ///
    /// <para><b>Duraklatma solo'da dünyayı durdurur, co-op'ta durdurmaz</b> — dört
    /// kişilik bir oturumda bir oyuncunun menüyü açması diğerlerinin oyununu
    /// donduramaz. Kararı menünün kendisi verir; burası yalnızca <i>açık mı</i>
    /// der.</para>
    /// </summary>
    public static class MenuSignals
    {
        /// <summary>Duraklatma menüsü açık mı.</summary>
        public static bool IsPauseOpen { get; private set; }

        /// <summary>Duraklatma açıldı ya da kapandı.</summary>
        public static event Action<bool> PauseVisibilityChanged;

        public static void SetPauseOpen(bool open)
        {
            if (IsPauseOpen == open) return;

            IsPauseOpen = open;
            PauseVisibilityChanged?.Invoke(open);
        }

        /// <summary>
        /// Silah tezgâhı açık mı (2026-09-06).
        ///
        /// <para><b>Neden burada, <c>CardSignals</c>'ta değil:</b> silah seçmek kart
        /// sisteminin bir parçası değil. Aynı gerekçeyle duraklatma da burada — bu
        /// sınıf, "kart olmayan menüler"in evi.</para>
        /// </summary>
        public static bool IsWeaponShopOpen { get; private set; }

        /// <summary>Silah tezgâhı açıldı ya da kapandı.</summary>
        public static event Action<bool> WeaponShopVisibilityChanged;

        /// <summary>
        /// Bir menünün <b>kapandığı kare</b> (2026-09-09).
        ///
        /// <para><b>Neden gerekli</b> (geliştirici: <i>"tezgâh açıkken E ile tezgâhtan
        /// çıkamıyoruz, ESC'ye basmak hoş olmuyor"</i>): E hem menüyü kapatıyor hem
        /// menüyü açıyordu ve ikisi <b>aynı karede</b> oluyordu. Bileşen sırası
        /// tanımsız: tezgâh ekranı önce koşarsa menü kapanıyor, hemen ardından
        /// <c>PlayerInteract</c> aynı karenin <c>wasPressedThisFrame</c>'ini görüp
        /// tezgâhı yeniden açıyordu. Oyuncuya görünen şey: E hiçbir şey yapmıyor.</para>
        ///
        /// <para><b>Neden bir kare damgası, "tuş bırakılana kadar bekle" değil:</b>
        /// bırakma şartı kullanıcının tuşu ne kadar tuttuğuna bağlı bir durum tutmayı
        /// gerektirir ve iki bileşenin ikisinde de tekrarlanırdı. Kare numarası tek bir
        /// sayı ve sorunun kendisini —<i>aynı kare</i>— tarif ediyor.</para>
        ///
        /// <para>Aynı sorun her "aynı tuşla aç-kapa" menüsünde çıkar; o yüzden çözüm
        /// menüde değil <b>burada</b>, ortak yerde.</para>
        /// </summary>
        public static int LastMenuClosedFrame { get; private set; } = int.MinValue;

        /// <summary>
        /// Bir menünün kapandığını damgalar. <b>Unity'yi bilmeyen bir katmanda</b>
        /// olduğumuz için kare numarasını çağıran taraf veriyor
        /// (<c>Bunker.Systems</c> motor referansı taşımaz — ADR'ler bunun üstüne
        /// kurulu).
        /// </summary>
        public static void NoteMenuClosed(int frame) => LastMenuClosedFrame = frame;

        /// <summary>
        /// Bu karede bir menü kapandı mı — etkileşim tuşu <b>yutulmalı</b> mı.
        /// </summary>
        public static bool ClosedThisFrame(int frame) => frame == LastMenuClosedFrame;

        public static void SetWeaponShopOpen(bool open)
        {
            if (IsWeaponShopOpen == open) return;

            IsWeaponShopOpen = open;
            WeaponShopVisibilityChanged?.Invoke(open);
        }

        /// <summary>Yalnızca açılışta.</summary>
        public static void Clear()
        {
            PauseVisibilityChanged = null;
            IsPauseOpen = false;

            WeaponShopVisibilityChanged = null;
            IsWeaponShopOpen = false;

            LastMenuClosedFrame = int.MinValue;
        }
    }
}
