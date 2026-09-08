using Bunker.Config;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Dış hava: <b>yağmur ve kalınlaşan sis</b>. 2026-09-07.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"dış ortama sis falan koy, böyle loş bir
    /// hissiyat versin, hatta yağmur da yağıyor olsun."</i> Ama bu yalnızca bir cila
    /// değil: dışarısı şu ana kadar düz, boş, gri bir zemindi ve oyuncunun oradan
    /// <i>çıkmak istemesi</i> için bir sebep yoktu. Görüşün kapanması dışarıyı
    /// <b>tehlikeli</b> yapar — bunker artık sığınılan yer, dışarısı geldikleri
    /// yer.</para>
    ///
    /// <para><b>Sis içeride ve dışarıda FARKLI</b> ve bu tek numara: Unity'nin sisi
    /// küreseldir, tek bir mesafe değeri vardır. İçeride kalın sis, dört metre
    /// ötedeki barikatı görünmez yapardı — yani okunabilirliği (PILLAR-04) sanata
    /// feda ederdi. Oyuncu binadan çıkınca sis <i>yumuşakça</i> kalınlaşıyor; anlık
    /// geçiş, kamera bir eşikten geçtiğinde göz kırpması gibi okunurdu.</para>
    ///
    /// <para><b>Yağmur yalnızca dışarıda düşer</b> ve kameranın üstünde yaşar. Bütün
    /// haritayı kaplayan bir yağmur, iç mekânda tavanın içinden yağardı — çatının
    /// altında yağmur, hiç yağmur olmamasından daha kötü.</para>
    ///
    /// <para><b>Kapatılabilir</b> (F10, <see cref="Bunker.UI.AtmosphereToggle"/> aynı
    /// mantıkla): ÇK-17 ölçümü temiz gri kutuda tekrarlanabilmeli.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Outdoor Weather")]
    public sealed class OutdoorWeather : MonoBehaviour
    {
        [Header("Sis (ussel yogunluk, metre basina)")]
        [Tooltip("Bina ICINDE sis yogunlugu. Dusuk: ic mekan neredeyse temiz kalmali, " +
                 "yoksa dort metredeki barikat okunmaz olur (PILLAR-04).")]
        [SerializeField] private float indoorFogDensity = 0.012f;

        [Tooltip("Bina DISINDA sis yogunlugu. 0.05 = 20 m'de belirgin sisli. " +
                 "Buyutursen zombiler hic gorunmeden barikata varir - disarisi " +
                 "kapanmali ama kor etmemeli.")]
        [SerializeField] private float outdoorFogDensity = 0.055f;

        [Tooltip("Sisin ic/dis arasinda gecis hizi (saniyede oran). Anlik gecis, " +
                 "esikten gecerken goz kirpmasi gibi okunur.")]
        [SerializeField] private float fogBlendPerSecond = 1.6f;

        [SerializeField] private Color fogColor = new Color(0.15f, 0.16f, 0.19f);

        [Header("Yagmur")]
        [Tooltip("Kameranin kac metre ustunden yagar. Yakin olursa damlalar " +
                 "goz hizasinda belirir ve 'yagmur' degil 'parazit' gibi gorunur.")]
        [SerializeField] private float rainHeightMeters = 9f;

        [Tooltip("Yagmur kutusunun yaricapi. Kameradan bu kadar genis bir alana yagar.")]
        [SerializeField] private float rainRadiusMeters = 16f;

        [SerializeField] private int rainParticles = 900;

        private Camera _camera;
        private float _lastCameraSearch = -99f;

        private ParticleSystem _rain;
        private ParticleSystem.EmissionModule _rainEmission;

        private float _outdoor01;          // 0 icerideyim, 1 disaridayim
        private bool _rainOn = true;

        private bool _fogWasOn;
        private float _fogStartAtStart;
        private float _fogEndAtStart;
        private float _fogDensityAtStart;
        private Color _fogColorAtStart;
        private FogMode _fogModeAtStart;
        private bool _captured;

        /// <summary>
        /// <b>Sahnede yoksa kendini kurar.</b> 2026-09-08.
        ///
        /// <para><b>Neden gerekti</b> (geliştirici: <i>"yağmur ve sisi sildim diye
        /// hatırlıyorum, atmosphere diye bir şeydi, bunu düzgünce tekrar
        /// oluştur"</i>): hava sistemi <c>_Hud</c> nesnesinin bir bileşeniydi ve o
        /// nesneden silinince <b>sessizce</b> yok oldu. Hiçbir hata basılmadı, hiçbir
        /// şey bozulmadı — yalnızca dışarısı bir daha hiç kapanmadı. Bu projedeki
        /// hataların en sık türü tam olarak bu: <i>eklenmeyi unutulan teşhis/atmosfer
        /// nesnesi</i> (<c>ZombieHealthLabels.EnsureInstalled</c> ile aynı ders).</para>
        ///
        /// <para><b>Neden <see cref="BunkerInterior"/> şartı:</b> sis KÜRESEL bir
        /// ayardır. Ana menüde ya da lobi sahnesinde kurulan bir hava sistemi,
        /// "dışarıdayım" diye okuyup menüyü sisin içinde bırakırdı. Bunkerin ayak izi
        /// yalnızca oyun sahnesinde var; yani bu şart, "burası oynanan sahne mi"
        /// sorusunun zaten var olan cevabı.</para>
        ///
        /// <para><b>Kurulum aracı hâlâ ekliyor</b> (<c>ZombieSetup</c>): iki yol
        /// birbirini bozmaz, çünkü bileşen zaten varsa buradan hiçbir şey olmaz.
        /// Araçtan gelen nesne ayarları Inspector'da düzenlenebilir kalıyor; bu yol
        /// yalnızca ağı deliği kapatıyor.</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstalled()
        {
            if (FindFirstObjectByType<OutdoorWeather>() != null) return;

            // Oyun sahnesi mi? Bunkerin ayak izi yalnizca orada var.
            if (FindFirstObjectByType<BunkerInterior>() == null) return;

            var host = new GameObject("_OutdoorWeather");
            host.AddComponent<OutdoorWeather>();

            Debug.Log("[Hava] Sahnede atmosfer bileseni yoktu, kendi kendine kuruldu " +
                      "(yagmur + disarida kalinlasan sis). F10 ile kapatilabilir.");
        }

        private void Start()
        {
            CaptureSceneFog();
            BuildRain();
        }

        private void OnDestroy()
        {
            // Awake'in kurdugunu OnDestroy bozar (csharp-code.md). Sis KURESEL bir
            // ayar: birakilmazsa sonraki sahne, hatta editorde sonraki Play oturumu
            // bu bilesenin biraktigi degerle acilir.
            RestoreSceneFog();
        }

        /// <summary>
        /// Atmosfer kapatıldığında (F10) yağmuru da durdurur.
        /// <see cref="Bunker.UI.AtmosphereToggle"/> bunu çağırır.
        /// </summary>
        public void SetEnabled(bool on)
        {
            _rainOn = on;

            if (_rain == null) return;

            _rainEmission.enabled = on && _outdoor01 > 0.5f;
            if (!on) RestoreSceneFog();
        }

        private void LateUpdate()
        {
            if (_camera == null) AcquireCamera();
            if (_camera == null) return;

            Transform cameraTransform = _camera.transform;
            Vector3 position = cameraTransform.position;

            bool outside = !BunkerInterior.IsInside(position);

            _outdoor01 = Mathf.MoveTowards(_outdoor01, outside ? 1f : 0f,
                                           fogBlendPerSecond * Time.deltaTime);

            if (_rainOn) ApplyFog();
            MoveRain(position, outside);
        }

        /// <summary>
        /// Sisi uygular. <b>Üssel, doğrusal değil</b> (2026-09-08).
        ///
        /// <para><b>Neden değişti</b> (geliştirici: <i>"dışarıya uyguladığın sis
        /// gözükmüyor, bazı açılarda fark edilebilir gibi oluyor ama hiç etkili
        /// değil"</i>): iki hata birden vardı.</para>
        ///
        /// <para><b>(1) Mesafe yanlış ölçekteydi.</b> Dış alan (apron) yalnızca 18 m
        /// genişliğinde ve çevre duvarı onu kapatıyor — yani dışarıda görülebilecek en
        /// uzak nokta ~25 m. Sisin bitişi 34 m'ye ayarlanmıştı, yani <i>hiçbir şey</i>
        /// sisin içine girmiyordu. Doğrusal sis, bitiş mesafesine kadar neredeyse
        /// şeffaftır; etkisi son metrelerde toplanır.</para>
        ///
        /// <para><b>(2) Doğrusal sis bu iş için yanlış araç.</b> Üssel sis <i>ilk
        /// metreden itibaren</i> birikir — on metredeki bir zombi bile hafifçe
        /// soluklaşır, ve "loş" hissi tam olarak budur. Yoğunluk metre başına
        /// okunur, sahne ölçeğine bağlı değildir.</para>
        /// </summary>
        private void ApplyFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;

            RenderSettings.fogDensity = Mathf.Lerp(indoorFogDensity, outdoorFogDensity,
                                                   _outdoor01);
        }

        private void MoveRain(Vector3 cameraPosition, bool outside)
        {
            if (_rain == null) return;

            // Yagmur kameranin USTUNDE yasar ve onunla gezer: butun haritayi kaplayan
            // bir yagmur, kirk bin parcacik demek olurdu (PERF-BUDGET).
            _rain.transform.position = cameraPosition + Vector3.up * rainHeightMeters;

            bool shouldEmit = _rainOn && outside;
            if (_rainEmission.enabled != shouldEmit) _rainEmission.enabled = shouldEmit;
        }

        /// <summary>
        /// Yağmur parçacık sistemi. <b>Koddan kuruluyor</b> çünkü prefab'a yazılsaydı
        /// ayarları YAML'da yaşar ve elle düzenlenemezdi (unity-conventions.md).
        /// </summary>
        private void BuildRain()
        {
            var host = new GameObject("Rain");
            host.transform.SetParent(transform, false);

            _rain = host.AddComponent<ParticleSystem>();

            // Modul ayarlari once, Play sonra: calisan bir sisteme yazmak Unity'de
            // sessizce yok sayilir.
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _rain.main;
            main.startLifetime = rainHeightMeters / 13f;   // yere varinca sonsun
            main.startSpeed = 13f;
            main.startSize = 0.045f;
            main.startColor = new Color(0.68f, 0.74f, 0.82f, 0.5f);
            main.maxParticles = rainParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.4f;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(rainRadiusMeters * 2f, 0.1f, rainRadiusMeters * 2f);
            shape.rotation = new Vector3(90f, 0f, 0f);   // asagi dogru

            _rainEmission = _rain.emission;
            _rainEmission.rateOverTime = rainParticles * 0.9f;
            _rainEmission.enabled = false;

            // Damla UZUN cizilir: kure seklinde bir damla kar tanesi gibi okunur.
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 3.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = RainMaterial();

            _rain.Play();
        }

        /// <summary>
        /// Damla malzemesi. <b>Unlit ve saydam:</b> yağmurun ışıktan etkilenmesi
        /// gereksiz bir hesap ve karanlık sahnede damlaları görünmez yapar.
        /// </summary>
        private static Material RainMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = "mat_rain_runtime" };

            material.SetColor("_BaseColor", new Color(0.7f, 0.76f, 0.85f, 0.5f));
            material.SetColor("_Color", new Color(0.7f, 0.76f, 0.85f, 0.5f));

            // Saydam kip: URP/Lit ailesinde _Surface 1 = Transparent.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);

            material.renderQueue = 3000;
            return material;
        }

        private void CaptureSceneFog()
        {
            _fogWasOn = RenderSettings.fog;
            _fogModeAtStart = RenderSettings.fogMode;
            _fogStartAtStart = RenderSettings.fogStartDistance;
            _fogEndAtStart = RenderSettings.fogEndDistance;
            _fogDensityAtStart = RenderSettings.fogDensity;
            _fogColorAtStart = RenderSettings.fogColor;
            _captured = true;
        }

        private void RestoreSceneFog()
        {
            if (!_captured) return;

            RenderSettings.fog = _fogWasOn;
            RenderSettings.fogMode = _fogModeAtStart;
            RenderSettings.fogStartDistance = _fogStartAtStart;
            RenderSettings.fogEndDistance = _fogEndAtStart;
            RenderSettings.fogDensity = _fogDensityAtStart;
            RenderSettings.fogColor = _fogColorAtStart;
        }

        /// <summary>
        /// Kamerayı bulur. <c>Camera.main</c> yalnızca "MainCamera" etiketli kamerayı
        /// döner; etiketsiz kalırsa hava hiç değişmez. Arama saniyede bir.
        /// </summary>
        private void AcquireCamera()
        {
            if (Time.unscaledTime - _lastCameraSearch < 1f) return;
            _lastCameraSearch = Time.unscaledTime;

            _camera = Camera.main;
            if (_camera == null) _camera = FindFirstObjectByType<Camera>();
        }
    }
}
