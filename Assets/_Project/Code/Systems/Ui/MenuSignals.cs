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
        }
    }
}
