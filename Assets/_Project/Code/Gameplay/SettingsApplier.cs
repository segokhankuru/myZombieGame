using Bunker.Audio;
using Bunker.Systems.Settings;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Ayarları <b>motora uygulayan</b> tek yer. M-04.
    ///
    /// <para><b>Neden tek bir yer:</b> çözünürlük, VSync ve ses her ekrandan
    /// değiştirilebiliyor (ana menü, duraklatma). Uygulamayı ekranların içine
    /// dağıtmak, birinin diğerini unutmasıyla biterdi — ayar ekranından çıkınca geri
    /// dönen bir çözünürlük, bulunması en can sıkıcı hatalardan biridir.</para>
    ///
    /// <para><b>Değişince uygulanır, kare başına değil</b> (ui-code.md):
    /// <c>GameSettings.Changed</c> dinleniyor. <c>Screen.SetResolution</c>'ı her karede
    /// çağırmak ekranı sürekli yeniden kurar.</para>
    ///
    /// <para><b>Kendi kendine doğar</b> (<c>RuntimeInitializeOnLoadMethod</c>): bir
    /// sahne nesnesine bağlı olsaydı, o nesnenin bulunmadığı sahnede ayarlar sessizce
    /// uygulanmazdı — menü sahnesi tam olarak öyle bir sahne.</para>
    /// </summary>
    public sealed class SettingsApplier : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Zaten varsa ikinci bir tane yaratma: sahneler arasi yasiyor.
            if (FindAnyObjectByType<SettingsApplier>() != null) return;

            var host = new GameObject("SettingsApplier");
            host.AddComponent<SettingsApplier>();
            DontDestroyOnLoad(host);
        }

        private void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable() => GameSettings.Changed -= Apply;

        /// <summary>
        /// Oyun arka plana düştü: ses kesilir (ayar açıksa).
        ///
        /// <para>Alt+Tab yapıp başka bir şey dinlerken arkadan gelen zombi homurtusu,
        /// oyuncunun kapattığı ilk şeydir.</para>
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!GameSettings.MuteWhenUnfocused)
            {
                AudioListener.volume = 1f;
                return;
            }

            AudioListener.volume = hasFocus ? 1f : 0f;
        }

        private static void Apply()
        {
            ApplyAudio();
            ApplyDisplay();
            ApplyFrameRate();
            ApplyQuality();
        }

        private static void ApplyAudio()
        {
            // Ses servisi ayari OKUR; ayar servisi sesi bilmez. Bagimlilik yonu tek.
            GameAudio.MasterVolume = GameSettings.EffectiveSfxVolume;
        }

        /// <summary>
        /// Çözünürlük ve tam ekran biçimi.
        ///
        /// <para><b>Zaten doğruysa dokunulmaz.</b> <c>Screen.SetResolution</c> her
        /// çağrıda ekranı yeniden kurar; ayar ekranında bir kaydırıcıyı oynatmak
        /// çözünürlüğü değiştirmese bile ekranı karartırdı.</para>
        /// </summary>
        private static void ApplyDisplay()
        {
            FullScreenMode mode = GameSettings.Display switch
            {
                DisplayMode.Exclusive => FullScreenMode.ExclusiveFullScreen,
                DisplayMode.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };

            int width = GameSettings.ResolutionWidth;
            int height = GameSettings.ResolutionHeight;

            if (width <= 0 || height <= 0)
            {
                // Ayar hic secilmemis: ekranin kendi cozunurlugu.
                width = Screen.currentResolution.width;
                height = Screen.currentResolution.height;
            }

            if (Screen.width == width && Screen.height == height && Screen.fullScreenMode == mode)
            {
                return;
            }

            Screen.SetResolution(width, height, mode);
        }

        private static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = GameSettings.VSyncCount;

            // VSync acikken targetFrameRate yok sayilir; yine de yaziyoruz ki oyuncu
            // VSync'i kapattigi an sinir devreye girsin.
            Application.targetFrameRate =
                GameSettings.FrameRateLimit <= 0 ? -1 : GameSettings.FrameRateLimit;
        }

        private static void ApplyQuality()
        {
            int level = GameSettings.QualityLevel;

            if (level < 0 || level >= QualitySettings.names.Length) return;
            if (QualitySettings.GetQualityLevel() == level) return;

            // applyExpensiveChanges: false - kalite kademesi degistirmek gölge ve
            // doku ayarlarini yeniden kurar; pahali olanlari atlamak, ayar ekranindaki
            // secimi anlik ve takilmasiz yapar.
            QualitySettings.SetQualityLevel(level, false);
        }
    }
}
