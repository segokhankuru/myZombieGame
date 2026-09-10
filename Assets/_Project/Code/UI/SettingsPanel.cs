using System.Collections.Generic;
using Bunker.Systems.Settings;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// Ayar paneli: dört sekme, hepsi <b>gerçekten çalışan</b> ayarlar. M-04.
    ///
    /// <para><b>Tek yerde</b> duruyor çünkü iki yerden açılıyor (ana menü ve oyun içi
    /// duraklatma). İkisini ayrı ayrı çizmek, bir ayarın yalnızca birine eklenmesiyle
    /// biterdi — "menüden değiştirdiğim ayar oyun içinde yok" (csharp-code.md: aynı iş
    /// kuralı iki yerde duramaz).</para>
    ///
    /// <para><b>Buraya hiçbir zaman "ileride bağlanacak" bir anahtar eklenmez.</b>
    /// Hiçbir şey yapmayan bir ayar, oyuncuya oyunun bozuk olduğunu öğretir.</para>
    /// </summary>
    public static class SettingsPanel
    {
        /// <summary>Panelin kapladığı yükseklik. Çağıran yerleşimi buna göre kurar.</summary>
        public const float Height = 400f;

        private enum Tab
        {
            Goruntu,
            Ses,
            Kontrol,
            Arayuz
        }

        private static readonly string[] TabNames = { "GORUNTU", "SES", "KONTROL", "ARAYUZ" };

        private static Tab _tab = Tab.Goruntu;

        // Cozunurluk listesi bir kez kurulur: Screen.resolutions her cagrida diziyi
        // yeniden ayirir ve OnGUI kare basina birkac kez kosar.
        private static Resolution[] _resolutions;
        private static string[] _resolutionLabels;

        private static readonly int[] FrameLimits = { 0, 30, 60, 120, 144, 240 };
        private static readonly string[] FrameLimitLabels =
            { "Sinirsiz", "30", "60", "120", "144", "240" };

        private static readonly string[] VSyncLabels = { "Kapali", "Acik", "Yarim" };
        private static readonly string[] DisplayLabels = { "Kenarliksiz", "Tam ekran", "Pencere" };
        private static readonly string[] HoldLabels = { "Basili tut", "Ac/kapat" };

        /// <summary>Ayarları çizer ve değişiklikleri <b>anında</b> uygular.</summary>
        public static void Draw(Rect rect, GUIStyle labelStyle)
        {
            EnsureResolutions();

            // --- sekmeler
            _tab = (Tab)GUI.Toolbar(new Rect(rect.x, rect.y, rect.width, 28f), (int)_tab, TabNames);

            var body = new Rect(rect.x, rect.y + 40f, rect.width, rect.height - 40f);

            switch (_tab)
            {
                case Tab.Ses: DrawAudio(body, labelStyle); break;
                case Tab.Kontrol: DrawControls(body, labelStyle); break;
                case Tab.Arayuz: DrawInterface(body, labelStyle); break;
                default: DrawVideo(body, labelStyle); break;
            }

            if (GUI.Button(new Rect(rect.x, rect.y + rect.height - 30f, 180f, 26f),
                           "Fabrika ayarlari"))
            {
                GameSettings.ResetToDefaults();
            }
        }

        /// <summary>Panel kapanırken diske yazar. Her kaydırmada yazmak yüzlerce yazma olurdu.</summary>
        public static void Close() => GameSettings.Save();

        // ---------------------------------------------------------------- goruntu

        private static void DrawVideo(Rect rect, GUIStyle labelStyle)
        {
            float y = rect.y;

            // --- cozunurluk
            int current = CurrentResolutionIndex();
            int picked = Cycle(rect, ref y, "COZUNURLUK", _resolutionLabels, current, labelStyle);

            if (picked != current && picked >= 0 && picked < _resolutions.Length)
            {
                GameSettings.ResolutionWidth = _resolutions[picked].width;
                GameSettings.ResolutionHeight = _resolutions[picked].height;
            }

            // --- ekran bicimi
            int display = Cycle(rect, ref y, "EKRAN", DisplayLabels, (int)GameSettings.Display, labelStyle);
            if (display != (int)GameSettings.Display) GameSettings.Display = (DisplayMode)display;

            // --- vsync
            int vsync = Cycle(rect, ref y, "DIKEY ESITLEME (VSync)", VSyncLabels,
                              GameSettings.VSyncCount, labelStyle);
            if (vsync != GameSettings.VSyncCount) GameSettings.VSyncCount = vsync;

            // --- kare siniri
            int limitIndex = System.Array.IndexOf(FrameLimits, GameSettings.FrameRateLimit);
            if (limitIndex < 0) limitIndex = 0;

            int newLimit = Cycle(rect, ref y, "KARE SINIRI", FrameLimitLabels, limitIndex, labelStyle);
            if (newLimit != limitIndex) GameSettings.FrameRateLimit = FrameLimits[newLimit];

            if (GameSettings.VSyncCount > 0)
            {
                // Calismayan bir ayarin SEBEBINI soyle: yoksa oyuncu sinirin bozuk
                // oldugunu dusunur.
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(rect.x, y, rect.width, 20f),
                          "VSync acikken kare siniri etkisizdir.", labelStyle);
                GUI.color = Color.white;
            }

            y += 24f;

            // --- kalite
            string[] qualities = QualitySettings.names;
            int quality = GameSettings.QualityLevel < 0
                ? QualitySettings.GetQualityLevel()
                : GameSettings.QualityLevel;

            int newQuality = Cycle(rect, ref y, "KALITE", qualities, quality, labelStyle);
            if (newQuality != quality) GameSettings.QualityLevel = newQuality;

            // --- gorus acisi
            Slider(rect, ref y, $"GORUS ACISI   {Mathf.RoundToInt(GameSettings.FieldOfView)}",
                   GameSettings.FieldOfView, GameSettings.MinFieldOfView, GameSettings.MaxFieldOfView,
                   labelStyle, v => GameSettings.FieldOfView = v);
        }

        // ---------------------------------------------------------------- ses

        private static void DrawAudio(Rect rect, GUIStyle labelStyle)
        {
            float y = rect.y;

            Slider(rect, ref y, $"ANA SES   %{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}",
                   GameSettings.MasterVolume, 0f, 1f, labelStyle, v => GameSettings.MasterVolume = v);

            Slider(rect, ref y, $"EFEKTLER   %{Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}",
                   GameSettings.SfxVolume, 0f, 1f, labelStyle, v => GameSettings.SfxVolume = v);

            // MUZIK (2026-09-09): artik gercek bir muzik var (ana menu dongusu), yani
            // ayar da gercek bir sey yapiyor. Onceki surumde bilerek YOKTU ve yerinde
            // "muzik ayari yok cunku oyunda muzik yok" yaziyordu - hicbir sey yapmayan
            // bir kaydirac, oyuncuya oyunun bozuk oldugunu ogretir.
            Slider(rect, ref y, $"MUZIK   %{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}",
                   GameSettings.MusicVolume, 0f, 1f, labelStyle, v => GameSettings.MusicVolume = v);

            Toggle(rect, ref y, "Oyun arka plandayken sesi kes",
                   GameSettings.MuteWhenUnfocused, v => GameSettings.MuteWhenUnfocused = v);

            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(rect.x, y, rect.width, 40f),
                      "Muzik yalnizca ANA MENUDE calar. Tur icinde muzik yok - " +
                      "surunun sesi bir bilgi kaynagi ve muzik onu ortuyor.", labelStyle);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- kontrol

        private static void DrawControls(Rect rect, GUIStyle labelStyle)
        {
            float y = rect.y;

            Slider(rect, ref y, $"FARE HASSASIYETI   {GameSettings.MouseSensitivity:0.000}",
                   GameSettings.MouseSensitivity, GameSettings.MinSensitivity,
                   GameSettings.MaxSensitivity, labelStyle, v => GameSettings.MouseSensitivity = v);

            Toggle(rect, ref y, "Dikey bakisi ters cevir",
                   GameSettings.InvertY, v => GameSettings.InvertY = v);

            int sprint = Cycle(rect, ref y, "KOSU TUSU (Shift)", HoldLabels,
                               (int)GameSettings.SprintMode, labelStyle);
            if (sprint != (int)GameSettings.SprintMode) GameSettings.SprintMode = (HoldMode)sprint;

            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(rect.x, y, rect.width, 60f),
                      "Tuslar: WASD hareket, Space ziplama, Shift kosu.\n" +
                      "1 bicak, 2-3 atesli silahlar, 4-8 esyalar (tekerlek de gecer).\n" +
                      "Sol tik ates/savurus, sag tik durbun (M4 ve M107), R dolum,\n" +
                      "E etkilesim/tezgah, F tur arasinda HAZIR, ESC menu.\n" +
                      "Tus degistirme M-05'te (girdi haritasi tasinacak).", labelStyle);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- arayuz

        private static void DrawInterface(Rect rect, GUIStyle labelStyle)
        {
            float y = rect.y;

            Toggle(rect, ref y, "Hasar sayilarini goster",
                   GameSettings.ShowDamageNumbers, v => GameSettings.ShowDamageNumbers = v);

            Toggle(rect, ref y, "Hasar yonu gostergesi",
                   GameSettings.ShowDamageDirection, v => GameSettings.ShowDamageDirection = v);

            Toggle(rect, ref y, "Isabet isareti",
                   GameSettings.ShowHitMarker, v => GameSettings.ShowHitMarker = v);

            Toggle(rect, ref y, "Nisangah",
                   GameSettings.ShowCrosshair, v => GameSettings.ShowCrosshair = v);
        }

        // ---------------------------------------------------------------- parcalar

        private static void Slider(Rect rect, ref float y, string label, float value,
                                   float min, float max, GUIStyle labelStyle,
                                   System.Action<float> apply)
        {
            GUI.Label(new Rect(rect.x, y, rect.width, 22f), label, labelStyle);
            y += 22f;

            float next = GUI.HorizontalSlider(new Rect(rect.x, y + 4f, rect.width, 18f),
                                              value, min, max);
            if (!Mathf.Approximately(next, value)) apply(next);

            y += 32f;
        }

        private static void Toggle(Rect rect, ref float y, string label, bool value,
                                   System.Action<bool> apply)
        {
            bool next = GUI.Toggle(new Rect(rect.x, y, rect.width, 24f), value, "  " + label);
            if (next != value) apply(next);

            y += 30f;
        }

        /// <summary>
        /// Sol/sağ oklarla dolaşılan bir seçim.
        ///
        /// <para><b>Neden açılır liste değil:</b> IMGUI'de açılır liste yok; elle
        /// yazmak bu geçici arayüz için harcanacak en yanlış yer. Ok tuşlu seçim
        /// kontrolcüyle de çalışır.</para>
        /// </summary>
        private static int Cycle(Rect rect, ref float y, string label, string[] options,
                                 int index, GUIStyle labelStyle)
        {
            if (options == null || options.Length == 0)
            {
                y += 30f;
                return index;
            }

            index = Mathf.Clamp(index, 0, options.Length - 1);

            GUI.Label(new Rect(rect.x, y, rect.width, 22f), label, labelStyle);
            y += 22f;

            if (GUI.Button(new Rect(rect.x, y, 30f, 24f), "<"))
            {
                index = (index - 1 + options.Length) % options.Length;
            }

            GUI.Label(new Rect(rect.x + 36f, y + 2f, rect.width - 80f, 22f), options[index], labelStyle);

            if (GUI.Button(new Rect(rect.x + rect.width - 30f, y, 30f, 24f), ">"))
            {
                index = (index + 1) % options.Length;
            }

            y += 32f;
            return index;
        }

        private static void EnsureResolutions()
        {
            if (_resolutions != null) return;

            // Ayni cozunurluk farkli tazeleme hizlariyla birden fazla kez gelir;
            // oyuncuya "1920x1080" secenegini alti kez gostermek listeyi okunmaz yapar.
            var seen = new HashSet<long>();
            var list = new List<Resolution>(16);

            Resolution[] all = Screen.resolutions;

            for (int i = 0; i < all.Length; i++)
            {
                long key = (long)all[i].width * 100000 + all[i].height;
                if (!seen.Add(key)) continue;

                list.Add(all[i]);
            }

            if (list.Count == 0) list.Add(Screen.currentResolution);

            _resolutions = list.ToArray();
            _resolutionLabels = new string[_resolutions.Length];

            for (int i = 0; i < _resolutions.Length; i++)
            {
                _resolutionLabels[i] = $"{_resolutions[i].width} x {_resolutions[i].height}";
            }
        }

        private static int CurrentResolutionIndex()
        {
            int width = GameSettings.ResolutionWidth > 0 ? GameSettings.ResolutionWidth : Screen.width;
            int height = GameSettings.ResolutionHeight > 0 ? GameSettings.ResolutionHeight : Screen.height;

            for (int i = 0; i < _resolutions.Length; i++)
            {
                if (_resolutions[i].width == width && _resolutions[i].height == height) return i;
            }

            return 0;
        }
    }
}
