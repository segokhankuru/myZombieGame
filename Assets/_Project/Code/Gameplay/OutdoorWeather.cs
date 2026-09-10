using Bunker.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Dış hava: <b>yağmur ve dışarıyı kapatan sis</b>. 2026-09-07, yeniden: 2026-09-10.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"dış ortama sis falan koy, böyle loş bir
    /// hissiyat versin, hatta yağmur da yağıyor olsun."</i> Ama bu yalnızca bir cila
    /// değil: görüşün kapanması dışarıyı <b>tehlikeli</b> yapar — bunker sığınılan yer,
    /// dışarısı geldikleri yer.</para>
    ///
    /// <para><b>2026-09-10 — sis artık bir YER, bir kamera durumu değil</b>
    /// (geliştirici: <i>"Bunker içinden dışarısı yağmurlu ve sisli gözükmüyor. Bunker
    /// içinden dışarısı sisli ve yağmurlu gözükmeli"</i>). Önceki sürüm Unity'nin global
    /// sisini <i>kameranın</i> içeride mi dışarıda mı olduğuna göre kalınlaştırıyordu.
    /// Unity sisinin tek bir yoğunluğu var ve bütün sahneye uygulanıyor: kamera
    /// içerideyken dışarısı da "içeri" sayılıyordu, pencereden bakınca açık ve temiz bir
    /// dünya görünüyordu. Yağmur aynı hatayı yapıyordu — kamera içerideyken hiç
    /// yağmıyordu.</para>
    ///
    /// <para><b>Şimdi üç katman:</b></para>
    /// <list type="number">
    /// <item><b>İnce global sis</b> her yerde (iç yoğunluk). PILLAR-04: dört metredeki
    /// barikat okunmalı.</item>
    /// <item><b>Dış sis hacmi</b> (<c>Bunker/Weather/Outdoor Fog</c>): her pikselde kamera
    /// ışınının <b>bina kutusunun dışında kalan</b> boyu ölçülür ve sis yalnızca o boy
    /// kadar birikir. Pencereden 20 m ötedeki çit sisin içinde, odanın karşı duvarı
    /// temiz. İç/dış arasında bir geçiş anı yok, çünkü sis kameraya değil ışına bağlı.</item>
    /// <item><b>Yağmur her zaman yağar</b>, ama damla <b>çatının altında doğmaz</b>.
    /// Damla dikey düştüğü için dışarıda doğan dışarıda kalır — çarpışma ya da tetikleyici
    /// gerekmez.</item>
    /// </list>
    ///
    /// <para><b>Kapatılabilir</b> (F10, <see cref="Bunker.UI.AtmosphereToggle"/>): ÇK-17
    /// ölçümü temiz gri kutuda tekrarlanabilmeli.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Outdoor Weather")]
    public sealed class OutdoorWeather : MonoBehaviour
    {
        /// <summary>Dış sis hacminin shader'ı. Build'e <c>WeatherShaders</c> dahil eder.</summary>
        public const string FogShaderName = "Bunker/Weather/Outdoor Fog";

        /// <summary>Yağmur damlasının shader'ı. Build'e <c>WeatherShaders</c> dahil eder.</summary>
        public const string RainShaderName = "Bunker/Weather/Rain Streak";

        [Header("Sis (us kare yogunluk, metre basina)")]
        [Tooltip("HER YERDE gecerli ince sis (Unity global sisi). Dusuk kalmali: ic mekanda " +
                 "dort metredeki barikat okunmali (PILLAR-04).")]
        [SerializeField] private float indoorFogDensity = 0.012f;

        [Tooltip("Bina DISINDA biriken sis: kamera isininin bina kutusunun disinda kalan boyu " +
                 "uzerinden. 0.055 = 20 m'de belirgin sisli. Buyutursen zombiler hic " +
                 "gorunmeden barikata varir - disarisi kapanmali ama kor etmemeli.")]
        [SerializeField] private float outdoorFogDensity = 0.055f;

        [Tooltip("Gokyuzu ve cok uzak pikseller icin sayilan en uzun mesafe. Dusurursen " +
                 "gokyuzu sisin arkasindan gorunur; 60 m'de 0.055 yogunlukla zaten tam kapali.")]
        [SerializeField] private float fogMaxDistanceMeters = 60f;

        [SerializeField] private Color fogColor = new Color(0.15f, 0.16f, 0.19f);

        [Header("Yagmur")]
        [Tooltip("Damlalar kameranin kac metre ustunde dogar. Yakin olursa damlalar goz " +
                 "hizasinda belirir ve 'yagmur' degil 'parazit' gibi gorunur.")]
        [SerializeField] private float rainHeightMeters = 9f;

        [Tooltip("Kameranin cevresinde yagan dairenin yaricapi. Iceriden bakinca pencerenin " +
                 "disindaki seridi kaplamali: bina 16 m derin, 24 m her pencerenin disinda " +
                 "yagmur demek.")]
        [SerializeField] private float rainRadiusMeters = 24f;

        [Tooltip("Saniyede dogan damla. Yaricapla birlikte buyutulmeli: ayni sayi daha genis " +
                 "alana dagilirsa yagmur seyrelir.")]
        [SerializeField] private float rainDropsPerSecond = 1400f;

        [Tooltip("Dis zeminin yuksekligi (m). Damla buraya varinca soner; altini zemin " +
                 "zaten ortuyor.")]
        [SerializeField] private float groundY = 0f;

        /// <summary>
        /// Damla hızı. Ayar değil fiziksel görünüş: yağmur ~9-13 m/s düşer ve çizginin
        /// boyu (<c>velocityScale</c>) bu hıza göre seçildi.
        /// </summary>
        private const float RainSpeedMetersPerSecond = 13f;

        /// <summary>Takılma sonrası tek karede bin damla doğmasın.</summary>
        private const int MaxDropsPerFrame = 200;

        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistance");
        private static readonly int BoxMinId = Shader.PropertyToID("_BoxMin");
        private static readonly int BoxMaxId = Shader.PropertyToID("_BoxMax");

        /// <summary>
        /// Bina yoksa kutu: sıfır genişlikte ve haritanın çok altında. Işın hiç içinden
        /// geçmez, yani her yer "dışarı" sayılır — sessizce hiç sis çizmemekten iyidir.
        /// </summary>
        private static readonly Vector3 NoBox = new Vector3(0f, -10000f, 0f);

        private Camera _camera;
        private float _lastCameraSearch = -99f;

        private ParticleSystem _rain;
        private Material _rainMaterial;
        private ParticleSystem.EmitParams _emit;
        private float _emitDebt;

        private Mesh _fogMesh;
        private Material _fogMaterial;

        private bool _on = true;

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
        /// nesneden silinince <b>sessizce</b> yok oldu (<c>ZombieHealthLabels.EnsureInstalled</c>
        /// ile aynı ders).</para>
        ///
        /// <para><b>Neden <see cref="BunkerInterior"/> şartı:</b> sis küresel bir ayardır.
        /// Ana menüde kurulan bir hava sistemi menüyü sisin içinde bırakırdı. Bunkerin
        /// ayak izi yalnızca oyun sahnesinde var.</para>
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
                      "(yagmur + disarida biriken sis). F10 ile kapatilabilir.");
        }

        private void Start()
        {
            CaptureSceneFog();
            BuildRain();
            BuildFogVolume();
        }

        private void OnDestroy()
        {
            // Start'in kurdugunu OnDestroy bozar (csharp-code.md). Sis KURESEL bir
            // ayar: birakilmazsa sonraki sahne bu bilesenin biraktigi degerle acilir.
            RestoreSceneFog();

            if (_fogMesh != null) Destroy(_fogMesh);
            if (_fogMaterial != null) Destroy(_fogMaterial);
            if (_rainMaterial != null) Destroy(_rainMaterial);
        }

        /// <summary>
        /// Atmosfer kapatıldığında (F10) sisi ve yağmuru durdurur.
        /// <see cref="Bunker.UI.AtmosphereToggle"/> bunu çağırır.
        /// </summary>
        public void SetEnabled(bool on)
        {
            _on = on;

            if (on) return;   // sis bir sonraki karede yeniden yazilir

            RestoreSceneFog();
            if (_rain != null) _rain.Clear();
        }

        private void LateUpdate()
        {
            if (!_on) return;

            if (_camera == null) AcquireCamera();
            if (_camera == null) return;

            // HER KARE yazilir: AtmosphereToggle F10'u geri acarken RenderSettings.fog'u
            // sahnenin acilistaki degerine donduruyor. Bir kez yazmak, F10 iki kez
            // basildiginda sisin kapali kalmasi demek olurdu.
            ApplyGlobalFog();

            BunkerInterior interior = BunkerInterior.Active;

            EmitRain(_camera.transform.position, interior);
            DrawFogVolume(interior);
        }

        private void ApplyGlobalFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = indoorFogDensity;
        }

        // ---------------------------------------------------------------- dis sis

        /// <summary>
        /// Ekranı kaplayan dörtgen ve malzemesi. Köşeler <b>doğrudan kırpma uzayında</b>
        /// (-1..1); shader dönüşüm yapmaz.
        /// </summary>
        private void BuildFogVolume()
        {
            Shader shader = Shader.Find(FogShaderName);

            if (shader == null)
            {
                Debug.LogError($"[Hava] '{FogShaderName}' shader'i bulunamadi - DIS SIS " +
                               "CIZILMIYOR (yalnizca ince global sis var). Build'deysen shader " +
                               "dahil edilmemis: 'Bunker/Gorunum/Hava Shader'larini Build'e " +
                               "Dahil Et' calistir ve yeniden build al.", this);
                return;
            }

            _fogMaterial = new Material(shader) { name = "mat_outdoor_fog_runtime" };

            _fogMesh = new Mesh { name = "OutdoorFogQuad" };
            _fogMesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f),
                new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f)
            };
            _fogMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };

            // Sinirlar dev: kamera kirpmasi bu dortgeni hicbir acidan ekranin disinda
            // saymamali. Konumu anlamsiz, cunku shader dunya uzayini kullanmiyor.
            _fogMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        }

        private void DrawFogVolume(BunkerInterior interior)
        {
            if (_fogMaterial == null) return;

            _fogMaterial.SetColor(FogColorId, fogColor);
            _fogMaterial.SetFloat(DensityId, outdoorFogDensity);
            _fogMaterial.SetFloat(MaxDistanceId, fogMaxDistanceMeters);
            _fogMaterial.SetVector(BoxMinId, interior != null ? interior.Min : NoBox);
            _fogMaterial.SetVector(BoxMaxId, interior != null ? interior.Max : NoBox);

            // YALNIZCA oyuncu kamerasina: durbun ya da onizleme kameralarinin kendi
            // derinlik dokusu olmayabilir ve bos bir derinlik "her sey sonsuz uzakta"
            // okunur - ekran tamamen sise gomulurdu.
            Graphics.DrawMesh(_fogMesh, Matrix4x4.identity, _fogMaterial, 0, _camera, 0, null,
                              ShadowCastingMode.Off, false);
        }

        // ---------------------------------------------------------------- yagmur

        /// <summary>
        /// Yağmur parçacık sistemi. <b>Koddan kuruluyor</b> çünkü prefab'a yazılsaydı
        /// ayarları YAML'da yaşar ve elle düzenlenemezdi (unity-conventions.md).
        ///
        /// <para><b>Yayım kapalı, damlalar elle doğar</b> (<see cref="EmitRain"/>): Unity'nin
        /// şekil modülü "çatının altında doğma" kuralını bilmez.</para>
        /// </summary>
        private void BuildRain()
        {
            Shader shader = Shader.Find(RainShaderName);

            if (shader == null)
            {
                Debug.LogError($"[Hava] '{RainShaderName}' shader'i bulunamadi - YAGMUR " +
                               "KURULMADI. 'Bunker/Gorunum/Hava Shader'larini Build'e Dahil Et' " +
                               "calistir.", this);
                return;
            }

            var host = new GameObject("Rain");
            host.transform.SetParent(transform, false);

            _rain = host.AddComponent<ParticleSystem>();

            // Modul ayarlari once, Play sonra: calisan bir sisteme yazmak Unity'de
            // sessizce yok sayilir.
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _rain.main;
            main.startSpeed = 0f;                 // hiz her damlaya Emit'te veriliyor
            main.startSize = 0.045f;
            main.startColor = new Color(0.68f, 0.74f, 0.82f, 0.5f);
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;            // sabit hiz: omur = dusus boyu / hiz
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _rain.shape;
            shape.enabled = false;

            // Damla UZUN cizilir: kure seklinde bir damla kar tanesi gibi okunur.
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 3.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _rainMaterial = new Material(shader) { name = "mat_rain_runtime" };
            renderer.sharedMaterial = _rainMaterial;

            _rain.Play();
        }

        /// <summary>
        /// Bu karenin damlaları. <b>Çatının altına düşecek damla doğmaz.</b>
        ///
        /// <para>Kare başına tahsis yok: <c>EmitParams</c> bir yapı ve alanı yeniden
        /// kullanılıyor (csharp-code.md). Rastgelelik tohumsuz: yağmur tekrar oynatılmıyor
        /// ve kaydedilmiyor, yani determinizm kuralının kapsamında değil.</para>
        /// </summary>
        private void EmitRain(Vector3 cameraPosition, BunkerInterior interior)
        {
            if (_rain == null) return;

            _emitDebt += rainDropsPerSecond * Time.deltaTime;

            int count = (int)_emitDebt;
            if (count <= 0) return;

            _emitDebt -= count;
            if (count > MaxDropsPerFrame) count = MaxDropsPerFrame;

            // Ust kattaki kamera icin de zemine kadar dusmeli: omur, dogum yuksekliginden
            // zemine olan mesafeden hesaplanir. Pencereden asagi bakinca yagmur kesilmez.
            float spawnY = Mathf.Max(cameraPosition.y, groundY) + rainHeightMeters;

            _emit.velocity = new Vector3(0f, -RainSpeedMetersPerSecond, 0f);
            _emit.startLifetime = (spawnY - groundY) / RainSpeedMetersPerSecond;

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * rainRadiusMeters;
                float x = cameraPosition.x + offset.x;
                float z = cameraPosition.z + offset.y;

                // CATININ ALTINDA DAMLA DOGMAZ. Bina bulunamadiysa her yere yagar -
                // BunkerInterior kendi eksikligini zaten bir kez yuksek sesle soyluyor.
                if (interior != null && interior.CoversXZ(x, z)) continue;

                _emit.position = new Vector3(x, spawnY, z);
                _rain.Emit(_emit, 1);
            }
        }

        // ---------------------------------------------------------------- global sis

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
