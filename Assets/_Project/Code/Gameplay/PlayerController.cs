using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using Bunker.Systems.Settings;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Birinci şahıs oyuncu kontrolü. Hareket ve bakış <b>istemci otoritesindedir</b>
    /// (ADR-0004): oyuncu yalnızca nerede olduğunu ve nereye baktığını söyler,
    /// başka hiçbir şeyi. Hasar, puan ve tur mantığı host'ta kalır.
    ///
    /// Konumun ağa taşınması bu sınıfın işi değil — prefab üzerindeki
    /// NetworkTransformUnreliable bileşeni yapar (syncDirection = ClientToServer).
    /// ADR-0004'teki NetworkTransform yasağı <b>zombiler</b> içindir (40 nesne);
    /// dört oyuncu için NetworkTransform doğru araçtır.
    /// </summary>
    [AddComponentMenu("Bunker/Player Controller")]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : NetworkBehaviour
    {
        // TODO(gameplay-programmer, M1-01): Bu değerler config/balance/player.json'a taşınacak.
        // M0-02 bir iskelet doğrulamasıdır; denge katmanı M1-01'de kuruluyor.
        // csharp-code.md kuralının "referans veya mühendislik sabiti" istisnası altında,
        // gerekçesi bu yorumda.
        [Header("Hareket (M0 gecici degerleri)")]
        [SerializeField] private float moveSpeedMetersPerSecond = 5f;
        [SerializeField] private float gravityMetersPerSecondSquared = -20f;
        [SerializeField] private float jumpHeightMeters = 1.1f;

        [Header("Kosu (sprint)")]
        [Tooltip("config/balance/player.json'dan uretilen varlik. Kosu sayilari " +
                 "buradan gelir; koda yazilmaz (config-data.md).")]
        [SerializeField] private PlayerConfigAsset playerConfig;

        [Header("Bakis (M0 gecici degerleri)")]
        [SerializeField] private float lookSensitivity = 0.08f;
        [SerializeField] private float maxPitchDegrees = 85f;

        [Header("Referanslar")]
        [Tooltip("Oyuncunun goz hizasindaki kamera. Yalnizca yerel oyuncuda acik kalir.")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener playerAudioListener;

        private CharacterController _controller;
        private Transform _transform;
        private float _pitchDegrees;
        private float _verticalVelocity;

        // Geri tepme: nisandan AYRI tutulur ve uzerine binir (M1-06).
        // Sifir baslar ve silah tarafindan ConfigureRecoilRecovery ile kurulur -
        // buraya bir varsayilan yazmak, silahin ayarindan bagimsiz ikinci bir denge
        // sayisi olurdu (config-data.md). Silah yoksa geri tepme de yoktur.
        private float _recoilPitch;
        private float _recoilRecoverySpeed;
        private float _recoilMaxPitch;

        // M1-11: run basladigi yer. Yeniden baslatmada buraya donulur.
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        // --- kosu (2026-09-05)
        // Kalan kosu suresi SANIYE cinsinden tutulur, sifir-bir arasi bir "stamina"
        // olarak degil: config'teki sayi da saniye ve ikisi ayni birimde konusuyor.
        // Birim donusumu, dengelemede en sik yanlis okunan seydir (config-data.md).
        private PlayerConfig _config;
        private float _sprintSecondsLeft;
        private bool _sprinting;
        private bool _sprintToggled;

        private void Awake()
        {
            // Referanslar bir kez cozulur; kare basina GetComponent yasak (csharp-code.md).
            _controller = GetComponent<CharacterController>();
            _transform = transform;

            _spawnPosition = _transform.position;
            _spawnRotation = _transform.rotation;

            if (playerConfig == null)
            {
                // Sessiz varsayilan yok (config-protocol.md): kosu ayari yoksa kosu
                // KAPALIDIR ve sebebi soylenir. Buraya bir varsayilan yazmak,
                // player.json ile oyunun farkli seyler soylemesi demek olurdu.
                Debug.LogError("[Oyuncu] player.asset atanmamis - KOSU KAPALI. " +
                               "'Bunker/Config/Ice Aktar' ile uret, " +
                               "'Bunker/Zombi/Test Alanini Kur' ile bagla.", this);
            }
            else
            {
                _config = playerConfig.ToRuntime();
                _sprintSecondsLeft = _config.SprintMaxSeconds;
            }
        }

        /// <summary>
        /// Kalan koşu süresinin oranı (0..1). HUD bunu okur — 4,5 saniyelik bir
        /// kaynağın göstergesi olmadan oyuncu ne zaman koşabileceğini tahmin etmek
        /// zorunda kalır.
        /// </summary>
        public float SprintFraction01 =>
            _config == null || _config.SprintMaxSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(_sprintSecondsLeft / _config.SprintMaxSeconds);

        /// <summary>Şu an koşuyor mu. HUD ve ileride ses/animasyon buna bakar.</summary>
        public bool IsSprinting => _sprinting;

        private void OnEnable()
        {
            // Statik yayin noktasina abone olan herkes OnDisable'da birakir
            // (RunSignals'in iki kuralindan biri) - yoksa ikinci Play oturumunda
            // olaylar iki kez tetiklenir.
            RunSignals.RunEnded += OnRunEnded;
            RunSignals.RunRestarted += OnRunRestarted;
        }

        private void OnDisable()
        {
            RunSignals.RunEnded -= OnRunEnded;
            RunSignals.RunRestarted -= OnRunRestarted;
        }

        /// <summary>
        /// Run bitti: fare serbest bırakılır (AC-3).
        ///
        /// <para>Girdinin kesilmesi <see cref="Update"/>'teki tek satırda; burası
        /// yalnızca imleci geri verir. Kilitli bir imleçle açılan bir skor ekranı,
        /// oyuncunun tıklayamadığı bir ekrandır.</para>
        /// </summary>
        private void OnRunEnded(RunSummary summary)
        {
            if (!isLocalPlayer) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Yeni run: oyuncu başlangıç noktasına döner, imleç tekrar kilitlenir (AC-5).
        ///
        /// <para><b><c>CharacterController</c> kapatılmadan konum yazılamaz.</b> Açıkken
        /// controller her karede kendi konumunu geri yazar ve <c>transform.position</c>
        /// sessizce hiçbir şey yapmaz — BUG-005'in <c>NavMeshAgent</c>'taki birebir
        /// aynısı. Sessiz başarısızlık, oyun testine kadar görünmez.</para>
        /// </summary>
        private void OnRunRestarted()
        {
            if (!isLocalPlayer) return;

            _controller.enabled = false;
            _transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _controller.enabled = true;

            _verticalVelocity = 0f;
            _pitchDegrees = 0f;
            _recoilPitch = 0f;

            // Yeni run kosuyu da tam verir: aksi halde ikinci run, birincinin
            // tukenmis kosusuyla baslardi (M1-11 AC-6 ile ayni sinif).
            _sprinting = false;
            if (_config != null) _sprintSecondsLeft = _config.SprintMaxSeconds;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Gorus acisi ayardan gelir (M-04). Konfor ayaridir, avantaj degil.</summary>
        private void ApplyFieldOfView()
        {
            if (playerCamera == null) return;

            float fov = GameSettings.FieldOfView;
            if (Mathf.Approximately(playerCamera.fieldOfView, fov)) return;

            playerCamera.fieldOfView = fov;
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            GameSettings.Changed += ApplyFieldOfView;
            ApplyFieldOfView();

            // Kamera ve dinleyici yalnizca yerel oyuncuda acik. Aksi halde dort kamera
            // ve dort AudioListener olur; ikincisi Unity'de uyari uretir ve sesi bozar.
            if (playerCamera != null) playerCamera.enabled = true;
            if (playerAudioListener != null) playerAudioListener.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnStopLocalPlayer()
        {
            base.OnStopLocalPlayer();

            // Statik olaya abone olan herkes birakir (RunSignals'in kurali).
            GameSettings.Changed -= ApplyFieldOfView;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            // Uzak oyuncularda kamera ve dinleyici kapali baslar. Prefab uzerinde de
            // kapali birakilir; bu satirlar guvenlik agi.
            if (isLocalPlayer) return;

            if (playerCamera != null) playerCamera.enabled = false;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
        }

        private void Update()
        {
            // Yalnizca yerel oyuncu kendini kontrol eder. Bu satir olmadan her istemci
            // sahnedeki butun oyunculari surer.
            if (!isLocalPlayer) return;

            // Run bitti: girdi kesilir (AC-3). Olu bir oyuncunun skor ekraninin
            // arkasinda dolasmaya devam etmesi, olumu bir sonuc olmaktan cikarir.
            // Run bitti YA DA tur arasi ekrani acik: girdi kesilir. Ekran acikken
            // ates etmek, bakis cevirmek ya da satin almak, fareyle kart secmeyi
            // imkansiz kilardi.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen)
            {
                // Menu acikken kosu YENILENMEYE devam eder: kart secmek 10 saniye
                // surebiliyor ve o sure oyuncuyu kaynaksiz birakmamali. Kosunun
                // kendisi kesilir - girdi okunmuyor.
                _sprinting = false;
                RechargeSprint(Time.deltaTime);
                return;
            }

            ReadLook();
            ReadMove();
        }

        /// <summary>
        /// Silahın geri tepmesini kameraya bindirir (M1-06).
        ///
        /// <para><b>Neden burada:</b> dikey açı bu sınıfın durumu. Geri tepmeyi silahın
        /// doğrudan kameraya yazması, aynı sayıyı iki yerden süren iki sahip demek
        /// olurdu ve nişanın "kayması" tam olarak böyle doğar. Geri tepme birikir,
        /// sonra kendiliğinden toparlanır; oyuncunun fare hareketi <b>her zaman</b> onun
        /// üstünde çalışır.</para>
        /// </summary>
        public void AddRecoil(float pitchDegrees, float yawDegrees)
        {
            _recoilPitch += pitchDegrees;
            _transform.Rotate(0f, yawDegrees, 0f, Space.Self);
        }

        /// <summary>Geri tepme toparlanma hızını silahın ayarından alır.</summary>
        public void ConfigureRecoilRecovery(float degreesPerSecond, float maxPitchDegrees)
        {
            _recoilRecoverySpeed = degreesPerSecond;
            _recoilMaxPitch = maxPitchDegrees;
        }

        private void TickRecoilRecovery(float dt)
        {
            if (_recoilMaxPitch > 0f && _recoilPitch > _recoilMaxPitch)
            {
                _recoilPitch = _recoilMaxPitch;
            }

            if (_recoilPitch <= 0f) return;

            _recoilPitch = Mathf.Max(0f, _recoilPitch - _recoilRecoverySpeed * dt);
        }

        private void ReadLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Hassasiyet OYUNCU AYARI (M-04), denge degeri degil: makineden makineye
            // degisir ve config/ altinda duramaz (config-data.md). Serilestirilmis
            // alan yalnizca ayar hic kaydedilmemisken devreye giren tabandir.
            float sensitivity = GameSettings.MouseSensitivity;
            if (sensitivity <= 0f) sensitivity = lookSensitivity;

            Vector2 delta = mouse.delta.ReadValue() * sensitivity;

            // Ters dikey bakis bir tercih degil ERISILEBILIRLIK ayari: bazi oyuncular
            // ucak kontrolu disinda oynayamaz.
            if (GameSettings.InvertY) delta.y = -delta.y;

            // Yatay donus govdeyi, dikey donus kamerayi cevirir. Govdeyi dikeyde
            // cevirmek CharacterController'i bozar.
            _transform.Rotate(0f, delta.x, 0f, Space.Self);

            _pitchDegrees = Mathf.Clamp(_pitchDegrees - delta.y, -maxPitchDegrees, maxPitchDegrees);

            TickRecoilRecovery(Time.deltaTime);

            if (playerCamera != null)
            {
                // Geri tepme nisanin USTUNE binir, onun yerine gecmez: oyuncu geri
                // tepmeyi asagi cekerek bastirabilmeli, yoksa silah oyuncuyla degil
                // oyuncuya karsi calisir.
                float pitch = Mathf.Clamp(_pitchDegrees - _recoilPitch,
                                          -maxPitchDegrees, maxPitchDegrees);
                playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        /// <summary>
        /// Koşuyu bir adım ilerletir ve hız çarpanını döner (2026-09-05).
        ///
        /// <para><b>Neden süreli, sınırsız değil</b> (geliştirici: "EFT'deki gibi ama
        /// çok uzun olmasın, 4-5 sn"): sınırsız koşu haritayı küçültür ve zombilerin
        /// hız kademelerini (<c>rounds.json → speed</c>) anlamsız kılar — geç turlarda
        /// kaçmak bedava olurdu. Süreli koşu bir <b>karar</b> üretir: şimdi mi koşayım,
        /// yoksa pencere sökülünce mi.</para>
        ///
        /// <para><b>Yalnızca hareket ederken tükenir.</b> Yerinde durup Shift'e basılı
        /// tutmak kaynağı yakmaz; yakması, oyuncunun anlamadığı bir cezaya dönerdi.</para>
        ///
        /// <para><b>Bittiğinde kesilir ve <c>minSecondsToStart</c> birikene kadar
        /// yeniden başlamaz.</b> Aksi hâlde oyuncu her saniye yarım adım koşar ve
        /// hareket titrer — okunması zor bir his.</para>
        /// </summary>
        private float TickSprint(Keyboard keyboard, bool moving, float dt)
        {
            // Ayar yoksa kosu YOKTUR. Awake'te sebebi loglandi; burada sessizce
            // 1 donmek dogru davranis - oyun kosusuz ama calisir.
            if (_config == null) return 1f;

            // Kosu tusu AC/KAPAT modunda olabilir (M-04 ayari): uzun oturumlarda
            // Shift'i basili tutmak fiziksel bir yuk - bir zevk meselesi degil.
            bool pressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            if (GameSettings.SprintMode == HoldMode.Toggle)
            {
                bool tapped = keyboard.leftShiftKey.wasPressedThisFrame ||
                              keyboard.rightShiftKey.wasPressedThisFrame;

                if (tapped) _sprintToggled = !_sprintToggled;

                // Durmak ac/kapat modunu da kapatir: yerinde dururken "kosuyor"
                // durumunda kalmak, gosterge ile hareketin birbirine yalan soylemesi
                // demek olurdu.
                if (!moving) _sprintToggled = false;
            }
            else
            {
                _sprintToggled = false;
            }

            bool wants = GameSettings.SprintMode == HoldMode.Toggle ? _sprintToggled : pressed;

            if (_sprinting)
            {
                // Tusu birakmak ya da durmak kosuyu bitirir; kalan sure durur.
                if (!wants || !moving || _sprintSecondsLeft <= 0f) _sprinting = false;
            }
            else if (wants && moving && _sprintSecondsLeft >= _config.SprintMinSecondsToStart)
            {
                _sprinting = true;
            }

            if (_sprinting)
            {
                _sprintSecondsLeft -= dt;

                if (_sprintSecondsLeft <= 0f)
                {
                    _sprintSecondsLeft = 0f;
                    _sprinting = false;
                }

                return _config.SprintSpeedMultiplier;
            }

            RechargeSprint(dt);
            return 1f;
        }

        /// <summary>Koşmadığın her saniyede biraz koşu süresi geri gelir.</summary>
        private void RechargeSprint(float dt)
        {
            if (_config == null || _sprintSecondsLeft >= _config.SprintMaxSeconds) return;

            _sprintSecondsLeft = Mathf.Min(_config.SprintMaxSeconds,
                                           _sprintSecondsLeft + _config.SprintRechargePerSecond * dt);
        }

        private void ReadMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // TODO(gameplay-programmer, M1-01): InputSystem_Actions varligina tasinacak.
            // M0-02'de dogrudan cihaz okuma tercih edildi: sifir baglama adimi, sifir
            // varlik bagimliligi. Degisecek yer yalnizca bu metot.
            float x = 0f;
            float z = 0f;
            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed) z -= 1f;
            if (keyboard.wKey.isPressed) z += 1f;

            Vector3 input = new Vector3(x, 0f, z);
            if (input.sqrMagnitude > 1f) input.Normalize();

            // Kart etkisi: Tempo etiketi hizi buradan artirir (SYS-02). Carpan
            // CardLoadout'ta zaten toplanmis duruyor - bes kart carpimsal olsaydi
            // hiz kacar ve NavMesh takibi anlamsizlasirdi.
            float speed = moveSpeedMetersPerSecond *
                          RunModifiers.Multiplier(CardStat.MoveSpeed);

            speed *= TickSprint(keyboard, input.sqrMagnitude > 0.01f, Time.deltaTime);

            Vector3 horizontal = _transform.TransformDirection(input) * speed;

            if (_controller.isGrounded)
            {
                // Kucuk negatif deger, karakteri zemine yapisik tutar. Sifir olursa
                // isGrounded her ikinci karede false doner.
                _verticalVelocity = -2f;

                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeightMeters * -2f * gravityMetersPerSecondSquared);
                }
            }
            else
            {
                _verticalVelocity += gravityMetersPerSecondSquared * Time.deltaTime;
            }

            Vector3 motion = horizontal;
            motion.y = _verticalVelocity;

            // CharacterController kinematiktir, rigidbody fizigi degil. Update icinde
            // surulmesi dogru olandir (gameplay-code.md'deki FixedUpdate kurali
            // rigidbody kuvvetleri icindir).
            _controller.Move(motion * Time.deltaTime);
        }
    }
}
