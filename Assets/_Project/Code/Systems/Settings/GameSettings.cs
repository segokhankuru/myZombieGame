using System;

namespace Bunker.Systems.Settings
{
    /// <summary>
    /// Ayarların kalıcı hâlini tutan depo. <b>Uygulaması motor tarafında</b>
    /// (<c>PlayerPrefs</c>), tanımı burada.
    ///
    /// <para><b>Neden ters çevrildi:</b> <c>Bunker.Systems</c> hiçbir oyun assembly'sine
    /// ve <b>Unity'ye</b> bağlı değil (<c>noEngineReferences</c>). Buraya
    /// <c>PlayerPrefs</c> yazmak, kuralın kendisini kırar ve ayarların EditMode
    /// testlerinde Unity açmadan sınanmasını imkânsız kılardı (systems-code.md).</para>
    /// </summary>
    public interface ISettingsStore
    {
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);

        int GetInt(string key, int fallback);
        void SetInt(string key, int value);

        bool GetBool(string key, bool fallback);
        void SetBool(string key, bool value);

        /// <summary>Diske yazar.</summary>
        void Flush();
    }

    /// <summary>Tam ekran biçimi. Unity'nin <c>FullScreenMode</c>'una çevrilir.</summary>
    public enum DisplayMode
    {
        /// <summary>Tam ekran, kenarlıksız pencere. Alt+Tab en hızlı burada.</summary>
        Borderless,
        /// <summary>Özel tam ekran. Bir-iki kare daha iyi, geçiş daha yavaş.</summary>
        Exclusive,
        /// <summary>Pencere.</summary>
        Windowed
    }

    /// <summary>Koşu tuşu nasıl davranır.</summary>
    public enum HoldMode
    {
        /// <summary>Basılı tuttuğun sürece.</summary>
        Hold,
        /// <summary>Bir bas aç, bir bas kapat.</summary>
        Toggle
    }

    /// <summary>
    /// Oyuncunun ayarları. M-04.
    ///
    /// <para><b>Neden <c>config/</c> değil:</b> <c>config/</c> oyunun <i>dengesidir</i>
    /// ve herkeste aynıdır (config-data.md). Buradakiler <b>oyuncuya ait tercihler</b> —
    /// makineden makineye değişir, dengeyi etkilemez ve çalışma anında yazılır.</para>
    ///
    /// <para><b>Buradaki her ayar GERÇEKTEN bir şey yapar</b> (geliştirici, 2026-09-05:
    /// <i>"gerçek bir oyunda ne kadar alakalı ayar varsa ekle"</i>). Hiçbir şey
    /// yapmayan bir ayar, oyuncuya oyunun bozuk olduğunu öğretir; o yüzden burada
    /// "ileride bağlanacak" bir anahtar yok — ekran sarsıntısı ve altyazı ayarları,
    /// o sistemler yazılana kadar <b>eklenmedi</b>.</para>
    ///
    /// <para><b>Sınırlar burada, ayar ekranında değil:</b> hassasiyeti sıfır yapabilen
    /// bir ekran, oyuncunun kendi oyununu bozmasına izin verir.</para>
    /// </summary>
    public static class GameSettings
    {
        // ---------------------------------------------------------------- sinirlar

        public const float MinSensitivity = 0.02f;
        public const float MaxSensitivity = 0.30f;
        public const float DefaultSensitivity = 0.08f;

        public const float MinFieldOfView = 60f;
        public const float MaxFieldOfView = 110f;
        public const float DefaultFieldOfView = 75f;

        private static ISettingsStore _store;
        private static bool _loaded;

        /// <summary>Bir ayar değişti. Uygulayıcılar burayı dinler.</summary>
        public static event Action Changed;

        // ---------------------------------------------------------------- degerler

        // --- girdi
        private static float _mouseSensitivity = DefaultSensitivity;
        private static bool _invertY;
        private static HoldMode _sprintMode = HoldMode.Hold;

        // --- ses
        private static float _masterVolume = 1f;
        private static float _sfxVolume = 1f;
        private static bool _muteWhenUnfocused = true;

        // --- goruntu
        private static int _resolutionWidth;      // 0 = mevcut ekran cozunurlugu
        private static int _resolutionHeight;
        private static DisplayMode _displayMode = DisplayMode.Borderless;
        private static int _vSyncCount = 1;
        private static int _frameRateLimit;       // 0 = sinirsiz
        private static int _qualityLevel = -1;    // -1 = projenin varsayilani
        private static float _fieldOfView = DefaultFieldOfView;

        // --- arayuz
        private static bool _showDamageNumbers = true;
        private static bool _showDamageDirection = true;
        private static bool _showHitMarker = true;
        private static bool _showCrosshair = true;

        // ---------------------------------------------------------------- girdi

        /// <summary>Fare hassasiyeti.</summary>
        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return _mouseSensitivity; }
            set => SetFloat(ref _mouseSensitivity, Clamp(value, MinSensitivity, MaxSensitivity),
                            "mouseSensitivity");
        }

        /// <summary>Dikey bakış ters mi. Tercih değil, <b>erişilebilirlik</b> ayarı.</summary>
        public static bool InvertY
        {
            get { EnsureLoaded(); return _invertY; }
            set => SetBool(ref _invertY, value, "invertY");
        }

        /// <summary>
        /// Koşu tuşu basılı mı tutulur, bir kez mi basılır.
        ///
        /// <para>Uzun oturumlarda Shift'i basılı tutmak <b>fiziksel bir yük</b>; bu bir
        /// zevk meselesi değil, el sağlığı meselesi.</para>
        /// </summary>
        public static HoldMode SprintMode
        {
            get { EnsureLoaded(); return _sprintMode; }
            set => SetEnum(ref _sprintMode, value, "sprintMode");
        }

        // ---------------------------------------------------------------- ses

        /// <summary>Ana ses (0..1).</summary>
        public static float MasterVolume
        {
            get { EnsureLoaded(); return _masterVolume; }
            set => SetFloat(ref _masterVolume, Clamp(value, 0f, 1f), "masterVolume");
        }

        /// <summary>Efekt sesi (0..1). Ana sesle <b>çarpılır</b>.</summary>
        public static float SfxVolume
        {
            get { EnsureLoaded(); return _sfxVolume; }
            set => SetFloat(ref _sfxVolume, Clamp(value, 0f, 1f), "sfxVolume");
        }

        /// <summary>Oyun arka plandayken ses kesilsin mi.</summary>
        public static bool MuteWhenUnfocused
        {
            get { EnsureLoaded(); return _muteWhenUnfocused; }
            set => SetBool(ref _muteWhenUnfocused, value, "muteWhenUnfocused");
        }

        /// <summary>Ses servisine gidecek nihai seviye.</summary>
        public static float EffectiveSfxVolume => MasterVolume * SfxVolume;

        // ---------------------------------------------------------------- goruntu

        /// <summary>Seçili çözünürlük genişliği. <c>0</c> = ekranın kendi çözünürlüğü.</summary>
        public static int ResolutionWidth
        {
            get { EnsureLoaded(); return _resolutionWidth; }
            set => SetInt(ref _resolutionWidth, value < 0 ? 0 : value, "resolutionWidth");
        }

        public static int ResolutionHeight
        {
            get { EnsureLoaded(); return _resolutionHeight; }
            set => SetInt(ref _resolutionHeight, value < 0 ? 0 : value, "resolutionHeight");
        }

        public static DisplayMode Display
        {
            get { EnsureLoaded(); return _displayMode; }
            set => SetEnum(ref _displayMode, value, "displayMode");
        }

        /// <summary>0 kapalı, 1 her tarama, 2 iki taramada bir.</summary>
        public static int VSyncCount
        {
            get { EnsureLoaded(); return _vSyncCount; }
            set => SetInt(ref _vSyncCount, (int)Clamp(value, 0f, 2f), "vSyncCount");
        }

        /// <summary>Kare sınırı. <c>0</c> = sınırsız. VSync açıkken etkisizdir.</summary>
        public static int FrameRateLimit
        {
            get { EnsureLoaded(); return _frameRateLimit; }
            set => SetInt(ref _frameRateLimit, value < 0 ? 0 : value, "frameRateLimit");
        }

        /// <summary>Unity kalite kademesi. <c>-1</c> = projenin varsayılanı.</summary>
        public static int QualityLevel
        {
            get { EnsureLoaded(); return _qualityLevel; }
            set => SetInt(ref _qualityLevel, value, "qualityLevel");
        }

        /// <summary>
        /// Görüş açısı (derece).
        ///
        /// <para><b>Bir konfor ayarıdır, bir avantaj değil:</b> dar açı bazı oyuncuda
        /// mide bulantısı yapar. Üst sınır 110 — daha genişi birinci şahıs nişanı
        /// bozar.</para>
        /// </summary>
        public static float FieldOfView
        {
            get { EnsureLoaded(); return _fieldOfView; }
            set => SetFloat(ref _fieldOfView, Clamp(value, MinFieldOfView, MaxFieldOfView),
                            "fieldOfView");
        }

        // ---------------------------------------------------------------- arayuz

        /// <summary>Vurduğun hasarın sayısı görünsün mü.</summary>
        public static bool ShowDamageNumbers
        {
            get { EnsureLoaded(); return _showDamageNumbers; }
            set => SetBool(ref _showDamageNumbers, value, "showDamageNumbers");
        }

        /// <summary>Aldığın hasarın yön göstergesi görünsün mü.</summary>
        public static bool ShowDamageDirection
        {
            get { EnsureLoaded(); return _showDamageDirection; }
            set => SetBool(ref _showDamageDirection, value, "showDamageDirection");
        }

        /// <summary>İsabet işareti (nişangâhın kızarması).</summary>
        public static bool ShowHitMarker
        {
            get { EnsureLoaded(); return _showHitMarker; }
            set => SetBool(ref _showHitMarker, value, "showHitMarker");
        }

        /// <summary>Nişangâh görünsün mü.</summary>
        public static bool ShowCrosshair
        {
            get { EnsureLoaded(); return _showCrosshair; }
            set => SetBool(ref _showCrosshair, value, "showCrosshair");
        }

        // ---------------------------------------------------------------- yasam dongusu

        /// <summary>
        /// Kalıcı depoyu bağlar ve kayıtlı değerleri okur.
        /// <b>Açılışta bir kez</b>, hiçbir sahne nesnesi uyanmadan önce.
        /// </summary>
        public static void AttachStore(ISettingsStore store)
        {
            _store = store;
            _loaded = false;
            EnsureLoaded();
            Changed?.Invoke();
        }

        /// <summary>
        /// Diske yazar. <b>Ayar ekranı kapanırken</b> — her kaydırma hareketinde disk
        /// yazmak, tek bir sürüklemede yüzlerce yazma demek olurdu.
        /// </summary>
        public static void Save() => _store?.Flush();

        /// <summary>Fabrika ayarları.</summary>
        public static void ResetToDefaults()
        {
            _loaded = true;

            _mouseSensitivity = DefaultSensitivity;
            _invertY = false;
            _sprintMode = HoldMode.Hold;

            _masterVolume = 1f;
            _sfxVolume = 1f;
            _muteWhenUnfocused = true;

            _resolutionWidth = 0;
            _resolutionHeight = 0;
            _displayMode = DisplayMode.Borderless;
            _vSyncCount = 1;
            _frameRateLimit = 0;
            _qualityLevel = -1;
            _fieldOfView = DefaultFieldOfView;

            _showDamageNumbers = true;
            _showDamageDirection = true;
            _showHitMarker = true;
            _showCrosshair = true;

            WriteAll();
            Changed?.Invoke();
        }

        /// <summary>Testler için: depo bağlantısını ve abonelikleri siler.</summary>
        public static void Clear()
        {
            Changed = null;
            _store = null;
            _loaded = false;

            _mouseSensitivity = DefaultSensitivity;
            _invertY = false;
            _sprintMode = HoldMode.Hold;
            _masterVolume = 1f;
            _sfxVolume = 1f;
            _muteWhenUnfocused = true;
            _resolutionWidth = 0;
            _resolutionHeight = 0;
            _displayMode = DisplayMode.Borderless;
            _vSyncCount = 1;
            _frameRateLimit = 0;
            _qualityLevel = -1;
            _fieldOfView = DefaultFieldOfView;
            _showDamageNumbers = true;
            _showDamageDirection = true;
            _showHitMarker = true;
            _showCrosshair = true;
        }

        // ---------------------------------------------------------------- ic isler

        private const string Prefix = "bunker.settings.";

        private static void SetFloat(ref float field, float value, string key)
        {
            EnsureLoaded();
            if (Math.Abs(field - value) < 0.0001f) return;

            field = value;
            _store?.SetFloat(Prefix + key, value);
            Changed?.Invoke();
        }

        private static void SetInt(ref int field, int value, string key)
        {
            EnsureLoaded();
            if (field == value) return;

            field = value;
            _store?.SetInt(Prefix + key, value);
            Changed?.Invoke();
        }

        private static void SetBool(ref bool field, bool value, string key)
        {
            EnsureLoaded();
            if (field == value) return;

            field = value;
            _store?.SetBool(Prefix + key, value);
            Changed?.Invoke();
        }

        private static void SetEnum<T>(ref T field, T value, string key) where T : struct, Enum
        {
            EnsureLoaded();
            if (field.Equals(value)) return;

            field = value;
            _store?.SetInt(Prefix + key, Convert.ToInt32(value));
            Changed?.Invoke();
        }

        private static void WriteAll()
        {
            if (_store == null) return;

            _store.SetFloat(Prefix + "mouseSensitivity", _mouseSensitivity);
            _store.SetBool(Prefix + "invertY", _invertY);
            _store.SetInt(Prefix + "sprintMode", (int)_sprintMode);

            _store.SetFloat(Prefix + "masterVolume", _masterVolume);
            _store.SetFloat(Prefix + "sfxVolume", _sfxVolume);
            _store.SetBool(Prefix + "muteWhenUnfocused", _muteWhenUnfocused);

            _store.SetInt(Prefix + "resolutionWidth", _resolutionWidth);
            _store.SetInt(Prefix + "resolutionHeight", _resolutionHeight);
            _store.SetInt(Prefix + "displayMode", (int)_displayMode);
            _store.SetInt(Prefix + "vSyncCount", _vSyncCount);
            _store.SetInt(Prefix + "frameRateLimit", _frameRateLimit);
            _store.SetInt(Prefix + "qualityLevel", _qualityLevel);
            _store.SetFloat(Prefix + "fieldOfView", _fieldOfView);

            _store.SetBool(Prefix + "showDamageNumbers", _showDamageNumbers);
            _store.SetBool(Prefix + "showDamageDirection", _showDamageDirection);
            _store.SetBool(Prefix + "showHitMarker", _showHitMarker);
            _store.SetBool(Prefix + "showCrosshair", _showCrosshair);
        }

        /// <summary>
        /// İlk okumada depodan yükler.
        ///
        /// <para><b>Tembel yükleme bilinçli:</b> yüklemeyi bir <c>Awake</c>'e bağlamak,
        /// o nesneden önce uyanan herkesin varsayılanı okuması demekti — bu projede aynı
        /// sıralama yarışı bir kez yaşandı (<c>RoundSignals.Clear</c>).</para>
        /// </summary>
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            if (_store == null) return;

            _mouseSensitivity = Clamp(_store.GetFloat(Prefix + "mouseSensitivity", DefaultSensitivity),
                                      MinSensitivity, MaxSensitivity);
            _invertY = _store.GetBool(Prefix + "invertY", false);
            _sprintMode = (HoldMode)_store.GetInt(Prefix + "sprintMode", (int)HoldMode.Hold);

            _masterVolume = Clamp(_store.GetFloat(Prefix + "masterVolume", 1f), 0f, 1f);
            _sfxVolume = Clamp(_store.GetFloat(Prefix + "sfxVolume", 1f), 0f, 1f);
            _muteWhenUnfocused = _store.GetBool(Prefix + "muteWhenUnfocused", true);

            _resolutionWidth = _store.GetInt(Prefix + "resolutionWidth", 0);
            _resolutionHeight = _store.GetInt(Prefix + "resolutionHeight", 0);
            _displayMode = (DisplayMode)_store.GetInt(Prefix + "displayMode", (int)DisplayMode.Borderless);
            _vSyncCount = (int)Clamp(_store.GetInt(Prefix + "vSyncCount", 1), 0f, 2f);
            _frameRateLimit = _store.GetInt(Prefix + "frameRateLimit", 0);
            _qualityLevel = _store.GetInt(Prefix + "qualityLevel", -1);
            _fieldOfView = Clamp(_store.GetFloat(Prefix + "fieldOfView", DefaultFieldOfView),
                                 MinFieldOfView, MaxFieldOfView);

            _showDamageNumbers = _store.GetBool(Prefix + "showDamageNumbers", true);
            _showDamageDirection = _store.GetBool(Prefix + "showDamageDirection", true);
            _showHitMarker = _store.GetBool(Prefix + "showHitMarker", true);
            _showCrosshair = _store.GetBool(Prefix + "showCrosshair", true);
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);
    }
}
