using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Elde görünen silah ve bıçak. <b>Yalnızca yerel oyuncuda</b> kurulur ve kameranın
    /// altında yaşar.
    ///
    /// <para><b>Neden var:</b> oyun testinde silah da bıçak da görünmüyordu — yani
    /// oyuncunun elinde bir şey olduğuna dair tek kanıt HUD'daki mermi sayacıydı. Bir
    /// nişancı oyununda ekranın alt köşesindeki silah bir süs değil, <b>durum
    /// göstergesidir</b>: ne taşıdığını, ne zaman dolum yaptığını, ateşin gerçekten
    /// çıktığını oradan okursun. Geri tepmeyi kamera zaten uyguluyordu, ama görünecek
    /// bir şey olmadığı için hissedilmiyordu.</para>
    ///
    /// <para><b>Neden çalışma anında ilkel şekillerden kuruluyor:</b> sanat yönü henüz
    /// kilitlenmedi (`/art-direction` çalışmadı) ve kilitlenmeden yapılmış bir silah
    /// modeli atılacak iştir. Gri kutunun kuralı burada da geçerli: <i>okunabilir olsun,
    /// güzel olmasın</i>. Model bir prefab'a yazılmadığı için sanat geldiğinde
    /// değiştirilecek tek yer bu dosyadır.</para>
    ///
    /// <para><b>Animasyon durumu takip eder, durum animasyonu beklemez</b>
    /// (gameplay-code.md). Silah modeli ateşin olup olmadığına karar vermez; olan şeyi
    /// gösterir. Bu yüzden burada tek bir oyun kuralı yoktur ve olmamalıdır.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Viewmodel")]
    public sealed class PlayerViewmodel : NetworkBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerWeapon weapon;
        [SerializeField] private PlayerMelee melee;

        // --- yerlesim (muhendislik sabitleri: denge degeri degil, el konumu) ---
        private static readonly Vector3 GunHome = new Vector3(0.17f, -0.15f, 0.42f);
        private static readonly Vector3 KnifeHome = new Vector3(0.22f, -0.18f, 0.36f);
        private static readonly Vector3 KnifeStowed = new Vector3(0.30f, -0.45f, 0.30f);

        private const float SwingSeconds = 0.42f;
        private const float MuzzleFlashSeconds = 0.045f;

        private Transform _rig;
        private Transform _gun;
        private Transform _knife;
        private GameObject _muzzleFlash;
        private Light _muzzleLight;

        private float _kick;              // geri tepmenin gorsel payi, 0..1
        private float _swingTimer = -1f;  // negatif: savurus yok
        private float _reloadBlend;
        private float _muzzleRemaining;
        private float _bobPhase;

        private Vector3 _swayVelocity;
        private Quaternion _lastCameraRotation;

        // ---------------------------------------------------------------- kurulum

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
            if (weapon == null) weapon = GetComponent<PlayerWeapon>();
            if (melee == null) melee = GetComponent<PlayerMelee>();
        }

        /// <summary>
        /// Model <b>yalnızca sahibinde</b> kurulur. Uzak oyuncunun kamerasının altına
        /// silah koymak, hiç görülmeyecek bir çizim çağrısı ödemektir.
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            if (playerCamera == null)
            {
                Debug.LogError("[Viewmodel] Oyuncu kamerasi yok; el modeli kurulamadi.", this);
                enabled = false;
                return;
            }

            // Yakin kesme duzlemi: FPS elinin kamerayla arasi yarim metreden kisadir.
            // Unity varsayilani 0.3 m ve bicagi tam ortasindan keser.
            if (playerCamera.nearClipPlane > 0.05f) playerCamera.nearClipPlane = 0.05f;

            BuildRig();

            _lastCameraRotation = playerCamera.transform.rotation;
        }

        private void OnEnable()
        {
            if (weapon != null) weapon.Fired += OnFired;

            if (melee != null) melee.SwingStarted += OnSwingStarted;
        }

        private void OnDisable()
        {
            // Awake'in kurdugunu OnDisable bozar (csharp-code.md): her abonelik bir
            // birakma yolu ile birlikte yazilir.
            if (weapon != null) weapon.Fired -= OnFired;

            if (melee != null) melee.SwingStarted -= OnSwingStarted;
        }

        // ---------------------------------------------------------------- olaylar

        private void OnFired()
        {
            // Geri tepme YIGILIR ama tavani var: arka arkaya ates ederken silah
            // ekrandan cikmamali.
            _kick = Mathf.Min(1f, _kick + 0.75f);
            _muzzleRemaining = MuzzleFlashSeconds;
        }

        private void OnSwingStarted() => _swingTimer = 0f;

        // ---------------------------------------------------------------- kare dongusu

        private void LateUpdate()
        {
            // Kamera LateUpdate'te donuyor (PlayerController); el ondan SONRA
            // yerlesmeli, yoksa hizli donuste bir kare geride kalir ve titrer.
            if (_rig == null) return;

            float dt = Time.deltaTime;

            TickKick(dt);
            TickSwing(dt);
            TickReload(dt);
            TickMuzzle(dt);
            TickSway(dt);
        }

        private void TickKick(float dt)
        {
            if (_kick <= 0f) return;

            // Hizli geri, yavas ileri: gercek geri tepmenin egrisi budur ve gozle
            // "vurdu" olarak okunur.
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 6f);
        }

        private void TickSwing(float dt)
        {
            if (_swingTimer < 0f) return;

            _swingTimer += dt;
            if (_swingTimer >= SwingSeconds) _swingTimer = -1f;
        }

        private void TickReload(float dt)
        {
            bool reloading = weapon != null && weapon.IsReloading;

            // Yumusak gecis: dolum aninda silahin bir kare icinde asagi firlamasi
            // "bozuldu" gibi okunur.
            _reloadBlend = Mathf.MoveTowards(_reloadBlend, reloading ? 1f : 0f, dt * 5f);
        }

        private void TickMuzzle(float dt)
        {
            if (_muzzleRemaining <= 0f) return;

            _muzzleRemaining -= dt;

            bool visible = _muzzleRemaining > 0f;
            if (_muzzleFlash.activeSelf != visible) _muzzleFlash.SetActive(visible);
            if (_muzzleLight != null) _muzzleLight.enabled = visible;
        }

        /// <summary>
        /// Silahın konumunu her karede sıfırdan kurar: ev konumu + geri tepme + dolum +
        /// savuruş + salınım. <b>Toplama değil, tek noktadan yazma</b> — birbirinin
        /// üstüne ekleyen animasyonlar zamanla sürüklenir.
        /// </summary>
        private void TickSway(float dt)
        {
            Transform cam = playerCamera.transform;

            // Bakis salinimi: kamera ne kadar dondu, el o kadar geride kalir. Sabit
            // duran bir el, elin kameraya yapistirildigini soyler.
            Quaternion delta = cam.rotation * Quaternion.Inverse(_lastCameraRotation);
            _lastCameraRotation = cam.rotation;

            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            Vector3 local = cam.InverseTransformDirection(axis) * (angle * Mathf.Deg2Rad);
            _swayVelocity = Vector3.Lerp(_swayVelocity, local, 1f - Mathf.Exp(-12f * dt));

            Vector3 sway = new Vector3(-_swayVelocity.y, _swayVelocity.x, 0f) * 0.06f;

            // Nefes: hicbir sey yapmayan bir el olu gorunur.
            _bobPhase += dt * 1.6f;
            Vector3 breathe = new Vector3(Mathf.Sin(_bobPhase) * 0.004f,
                                          Mathf.Sin(_bobPhase * 2.1f) * 0.003f, 0f);

            bool swinging = _swingTimer >= 0f;
            float swing01 = swinging ? _swingTimer / SwingSeconds : 0f;

            // --- silah
            Vector3 gunPos = GunHome + sway + breathe;
            gunPos.z -= _kick * 0.05f;
            gunPos.y -= _kick * 0.012f;

            // Dolum: silah asagi ve ice doner. Sarjorun cikip girdigini gostermek icin
            // ayri parcalar gerekmiyor - acinin kendisi okunur.
            gunPos += new Vector3(0.02f, -0.12f, -0.06f) * _reloadBlend;

            // Bicak savururken silah ELDEN CIKAR: ikisinin ayni anda gorunmesi hangi
            // tusun ne yaptigini okunmaz kilar.
            gunPos += new Vector3(0.05f, -0.30f, -0.05f) * Mathf.Sin(swing01 * Mathf.PI);

            _gun.localPosition = gunPos;
            _gun.localRotation = Quaternion.Euler(
                -_kick * 9f - _reloadBlend * 45f,
                _reloadBlend * 25f + sway.x * 40f,
                _reloadBlend * 12f);

            // --- bicak
            if (swinging)
            {
                // Sagdan sola, once hizli sonra yavas: savurusun agirligi burada okunur.
                float arc = Mathf.Sin(swing01 * Mathf.PI);
                float across = Mathf.SmoothStep(0f, 1f, swing01);

                _knife.localPosition = Vector3.Lerp(KnifeHome, KnifeHome + new Vector3(-0.45f, 0.10f, 0.10f), across)
                                       + Vector3.up * (arc * 0.06f) + sway;

                _knife.localRotation = Quaternion.Euler(
                    -20f + arc * 40f,
                    -30f + across * 90f,
                    -25f + arc * 55f);
            }
            else
            {
                // Bicak bekleme konumunda: ekranin altinda, gorunur ama yolda degil.
                _knife.localPosition = Vector3.Lerp(_knife.localPosition, KnifeStowed + sway, 1f - Mathf.Exp(-10f * dt));
                _knife.localRotation = Quaternion.Slerp(_knife.localRotation,
                                                        Quaternion.Euler(15f, -20f, 20f),
                                                        1f - Mathf.Exp(-10f * dt));
            }
        }

        // ---------------------------------------------------------------- model

        /// <summary>
        /// Gri kutu silahını ve bıçağını kurar. Ölçüler santimetre düzeyinde: kameraya
        /// yakın duran bir nesne birkaç santimetre büyüdüğünde ekranın yarısını kaplar.
        /// </summary>
        private void BuildRig()
        {
            Material gunMaterial = MakeMaterial(new Color(0.16f, 0.17f, 0.19f));
            Material accentMaterial = MakeMaterial(new Color(0.35f, 0.32f, 0.28f));
            Material bladeMaterial = MakeMaterial(new Color(0.70f, 0.72f, 0.76f));

            var rig = new GameObject("Viewmodel");
            rig.transform.SetParent(playerCamera.transform, false);
            _rig = rig.transform;

            // --- silah
            var gun = new GameObject("Gun");
            gun.transform.SetParent(_rig, false);
            gun.transform.localPosition = GunHome;
            _gun = gun.transform;

            Part(_gun, "Slide", new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.055f, 0.24f), gunMaterial);
            Part(_gun, "Barrel", new Vector3(0f, 0.005f, 0.16f), new Vector3(0.022f, 0.022f, 0.12f), gunMaterial);
            Part(_gun, "Grip", new Vector3(0f, -0.075f, -0.06f), new Vector3(0.040f, 0.110f, 0.055f), accentMaterial);
            Part(_gun, "Sight", new Vector3(0f, 0.038f, 0.10f), new Vector3(0.010f, 0.014f, 0.012f), accentMaterial);

            // --- namlu alevi: atisin CIKTIGINI soyleyen sey. Isik da var, cunku
            //     karanlik bir kosede yalnizca alev yeterince okunmuyor.
            _muzzleFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _muzzleFlash.name = "MuzzleFlash";
            _muzzleFlash.transform.SetParent(_gun, false);
            _muzzleFlash.transform.localPosition = new Vector3(0f, 0.005f, 0.235f);
            _muzzleFlash.transform.localScale = new Vector3(0.075f, 0.075f, 0.11f);
            Destroy(_muzzleFlash.GetComponent<Collider>());

            var flashRenderer = _muzzleFlash.GetComponent<Renderer>();
            flashRenderer.sharedMaterial = MakeUnlitMaterial(new Color(1f, 0.85f, 0.45f));
            flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightHost = new GameObject("MuzzleLight");
            lightHost.transform.SetParent(_gun, false);
            lightHost.transform.localPosition = new Vector3(0f, 0.02f, 0.28f);

            _muzzleLight = lightHost.AddComponent<Light>();
            _muzzleLight.type = LightType.Point;
            _muzzleLight.color = new Color(1f, 0.86f, 0.55f);
            _muzzleLight.range = 7f;
            _muzzleLight.intensity = 3.5f;
            _muzzleLight.shadows = LightShadows.None;   // tek karelik isik golge cizmez
            _muzzleLight.enabled = false;

            _muzzleFlash.SetActive(false);

            // --- bicak
            var knife = new GameObject("Knife");
            knife.transform.SetParent(_rig, false);
            knife.transform.localPosition = KnifeStowed;
            _knife = knife.transform;

            Part(_knife, "Blade", new Vector3(0f, 0.02f, 0.14f), new Vector3(0.016f, 0.042f, 0.22f), bladeMaterial);
            Part(_knife, "Guard", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.060f, 0.016f, 0.020f), gunMaterial);
            Part(_knife, "Handle", new Vector3(0f, 0.00f, -0.05f), new Vector3(0.028f, 0.032f, 0.10f), accentMaterial);
        }

        private static void Part(Transform parent, string name, Vector3 localPosition,
                                 Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            // Carpistiricisi YOK: el modeli dunyaya dokunmamali. Bir kutu birakmak,
            // oyuncunun kendi silahiyla kendi isinini engellemesi demektir.
            Destroy(go.GetComponent<Collider>());

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            return material;
        }

        private static Material MakeUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            return material;
        }
    }
}
