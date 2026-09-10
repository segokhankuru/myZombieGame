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

        [Tooltip("Silah modelleri. Bosken elde gri kutu silah gorunur - " +
                 "Bunker > Gorunum > Magaza Modellerini Bagla ile doldurulur.")]
        [SerializeField] private Bunker.Config.ArtCatalogAsset art;

        // --- yerlesim (muhendislik sabitleri: denge degeri degil, el konumu) ---
        private static readonly Vector3 GunHome = new Vector3(0.17f, -0.15f, 0.42f);
        private static readonly Vector3 KnifeHome = new Vector3(0.22f, -0.18f, 0.36f);
        private static readonly Vector3 KnifeStowed = new Vector3(0.30f, -0.45f, 0.30f);

        private const float SwingSeconds = 0.42f;
        private const float MuzzleFlashSeconds = 0.045f;

        private Transform _rig;
        private Transform _gun;
        private Transform _knife;

        /// <summary>0 = ates li silah elde, 1 = bicak elde. Gecis yumusatmasi.</summary>
        private float _handBlend;

        // --- eller ve kollar (2026-09-07)
        private Transform _armRight;
        private Transform _armLeft;
        private Transform _magazine;
        private Vector3 _magazineHome;
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

        // Silah gorunumu (2026-09-06): hangi silahin govdesi kurulu, hangi
        // materyaller kullanildi ve namlu ucu nerede.
        private string _builtWeaponId;
        private Material _gunMaterial;
        private Material _accentMaterial;
        private Vector3 _muzzleTip = new Vector3(0f, 0.005f, 0.235f);

        // Bicak gorunumu (2026-09-08): hangi bicagin govdesi kurulu, ucu nerede ve
        // gri kutu geri dusus yolunun materyalleri.
        private string _builtMeleeId;
        private Vector3 _knifeTip = new Vector3(0f, 0.02f, 0.25f);

        /// <summary>
        /// Bıçağın ucunun el modelindeki yeri. Savuruş izi ya da kan sıçraması
        /// buraya bağlanır; ölçüsü üreteçten geliyor
        /// (<c>ArtIntegration.MeasureMelee</c>), tahmin değil.
        /// </summary>
        public Vector3 MeleeTipLocal => _knifeTip;
        private Material _knifeBladeMaterial;
        private Material _knifeGuardMaterial;
        private Material _knifeHandleMaterial;

        /// <summary>
        /// Savuruşun görsel süresi. <b>Bıçağın kendi ritminden gelir</b> (2026-09-08):
        /// baltanın bekleme süresi hançerinkinin iki katı ve savuruş animasyonu sabit
        /// kalsaydı, balta hızlı savrulup uzun beklerdi — oyuncunun okuduğu şey
        /// "ağır bir silah" değil "gecikmeli bir silah" olurdu.
        /// </summary>
        private float SwingDurationSeconds
        {
            get
            {
                if (melee == null || !melee.Current.IsValid) return SwingSeconds;

                // Savurus, bekleme suresinin yarisi kadar surer: kalan yari
                // toparlanmadir ve o sirada bicak zaten bekleme konumuna doner.
                return Mathf.Clamp(melee.Current.CooldownSeconds * 0.5f, 0.18f, 1.2f);
            }
        }

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

        /// <summary>
        /// Bir eli görünür/görünmez yapar.
        ///
        /// <para><b><c>SetActive</c> değil, <c>Renderer.enabled</c>:</b> nesneyi
        /// kapatmak namlu alevini, ışığını ve el/kol hiyerarşisini de kapatır ve
        /// yeniden açıldığında bunların hepsi <c>Awake</c>'ten geçer. Görünürlük bir
        /// çizim sorusu; nesnenin var olup olmaması ayrı bir soru (ui-code.md'nin
        /// "Canvas bileşenini kapat, GameObject'i değil" kuralının aynısı).</para>
        ///
        /// <para><b>Zaten doğru durumdaysa dokunmaz:</b> kare başına <c>enabled</c>
        /// yazmak Unity'de gereksiz bir durum değişikliği bildirimi üretir
        /// (ui-code.md: değişmediyse yazma).</para>
        /// </summary>
        private static void SetVisible(Renderer[] renderers, bool visible)
        {
            if (renderers == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].enabled != visible) renderers[i].enabled = visible;
            }
        }

        /// <summary>
        /// Bir elin çizicilerini <b>bir kez</b> toplar.
        ///
        /// <para><c>GetComponentsInChildren</c> her çağrıda bir dizi ayırır; kare
        /// başına çağrılması yasak (csharp-code.md). Gövdeler yalnızca silah ya da
        /// bıçak <i>değiştiğinde</i> yeniden kuruluyor, yani önbelleğin tazelenmesi
        /// gereken tek an da orası.</para>
        /// </summary>
        private static Renderer[] Collect(Transform hand) =>
            hand == null ? System.Array.Empty<Renderer>()
                         : hand.GetComponentsInChildren<Renderer>(true);

        private Renderer[] _gunRenderers = System.Array.Empty<Renderer>();
        private Renderer[] _knifeRenderers = System.Array.Empty<Renderer>();

        // ---------------------------------------------------------------- kare dongusu

        private void LateUpdate()
        {
            // Silah degistiyse govdeyi yeniden kur. Kare basina bir string
            // karsilastirmasi; degismedigi surece hicbir sey yapmaz.
            RebuildGun();

            // Bicak da degisebilir (hancer -> kilic -> balta). Ayni desen, ayni
            // bedel: degismedigi surece bir string karsilastirmasi.
            if (melee != null && _knife != null && melee.Current.Id != _builtMeleeId)
            {
                BuildKnifeBody(melee.Current.Id);
            }

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
            if (_swingTimer >= SwingDurationSeconds) _swingTimer = -1f;
        }

        private void TickReload(float dt)
        {
            bool reloading = weapon != null && weapon.IsReloading;

            // Yumusak gecis: dolum aninda silahin bir kare icinde asagi firlamasi
            // "bozuldu" gibi okunur.
            _reloadBlend = Mathf.MoveTowards(_reloadBlend, reloading ? 1f : 0f, dt * 5f);

            TickMagazine(reloading);
        }

        /// <summary>
        /// Şarjörün <b>çıkıp geri girmesi</b>. 2026-09-07 (geliştirici:
        /// <i>"yapabiliyorsan şarjör değiştirme animasyonunu da ekle"</i>).
        ///
        /// <para><b>Faz silahın kendi ilerlemesinden okunur</b>
        /// (<c>ReloadProgress01</c>), ayrı bir sayaçtan değil. Ayrı sayaç tutsaydık,
        /// dolum süresini değiştiren bir kart (<c>ReloadSpeed</c>) animasyonu
        /// senkronunu bozardı — şarjör silah çoktan dolduktan sonra yerine oturur ve
        /// oyuncu "bitti mi bitmedi mi" sorusunu ekrandan okuyamazdı. Animasyon durumu
        /// takip eder, durum animasyonu beklemez (gameplay-code.md).</para>
        ///
        /// <para><b>Üç evre:</b> ilk %30 düşer, ortada aşağıda kalır, son %35'te geri
        /// sürülür. Ortadaki boşluk bilerek: doluma "bir şey oluyor" süresi veren şey
        /// hareketin kendisi değil, <i>duraklaması</i>.</para>
        /// </summary>
        private void TickMagazine(bool reloading)
        {
            if (_magazine == null) return;

            if (!reloading)
            {
                _magazine.localPosition = _magazineHome;
                _magazine.localRotation = Quaternion.identity;
                return;
            }

            float t = weapon != null ? weapon.ReloadProgress01 : 0f;

            // 0.0 - 0.30 dusus | 0.30 - 0.65 asagida | 0.65 - 1.0 geri surme
            float drop;
            if (t < 0.30f) drop = Mathf.SmoothStep(0f, 1f, t / 0.30f);
            else if (t < 0.65f) drop = 1f;
            else drop = Mathf.SmoothStep(1f, 0f, (t - 0.65f) / 0.35f);

            _magazine.localPosition = _magazineHome
                                      + new Vector3(0f, -0.16f, -0.03f) * drop;

            // Dusen sarjor hafif doner: dumduz asagi inen bir kutu, bir asansor
            // gibi okunur.
            _magazine.localRotation = Quaternion.Euler(drop * 22f, 0f, drop * -14f);
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
            float swing01 = swinging ? Mathf.Clamp01(_swingTimer / SwingDurationSeconds) : 0f;

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

            // BICAK 1 NUMARALI SLOT (2026-09-09): bicak elde iken silah tamamen
            // indirilir, bicak kaldirilir. Onceki hâlde ikisi ayni anda ekrandaydi
            // (silah elde, bicak altta bekliyor) cunku bicak bir KISAYOL'du. Artik
            // ikisi ayri slot ve ekran hangisinin elde oldugunu SOYLEMEK zorunda -
            // yoksa oyuncu neyle ates edecegini HUD'dan okumak zorunda kalir
            // (PILLAR-04: kaosta okunabilirlik).
            //
            // Gecis yumusatilmis: ani bir takas, elin bos oldugu tek karede goze
            // batar. 12'lik ussel yaklasim ~0.15 sn eder - ui-code.md'nin ekran
            // gecisi bandinin (150-300 ms) alt ucu.
            bool meleeActive = weapon != null && weapon.IsMeleeActive;
            _handBlend = Mathf.Lerp(_handBlend, meleeActive ? 1f : 0f,
                                    1f - Mathf.Exp(-12f * dt));

            gunPos += new Vector3(0.06f, -0.42f, -0.10f) * _handBlend;

            // ELDE OLMAYAN TAMAMEN GIZLENIR (2026-09-09, gelistirici: "melee silahla
            // atesli silahlar ayni anda gozukuyor, bunu kaldir - hangisi eldeyse
            // sadece o gozuksun").
            //
            // Ilk surum yalnizca KAYDIRIYORDU (silahi asagi, bicagi yukari) ve bu
            // yetmedi: ekranin alt kenarinda duran ikinci nesne hala goruluyor ve
            // "hangisiyle ates ediyorum" sorusunu ekran cevaplamak yerine
            // BULANIKLASTIRIYOR. Slotun butun anlami o soruya tek bir cevap vermek.
            //
            // Esik gecisin ORTASINDA: bicak yariya geldiginde silah kapanir, yani
            // takas aninda ikisi birden gorunmez bir kare bile olmaz.
            SetVisible(_gunRenderers, _handBlend < 0.5f);
            SetVisible(_knifeRenderers, _handBlend >= 0.5f);

            _gun.localPosition = gunPos;
            _gun.localRotation = Quaternion.Euler(
                -_kick * 9f - _reloadBlend * 45f,
                _reloadBlend * 25f + sway.x * 40f,
                _reloadBlend * 12f);

            // --- bicak
            if (swinging)
            {
                // SAVURUS UC PARCA: geri cek, indir, topla (2026-09-08).
                //
                // <b>Neden degisti</b> (gelistirici: "bu melee saldirisinin
                // animasyonunu guzelce elden gecir"): onceki hareket tek yonlu bir
                // kaydirmaydi - bicak sagdan sola suzuluyordu ve hicbir yerde
                // HIZLANMIYORDU. Bir savurusun okunmasini saglayan sey gerilimidir:
                // once geri gider (ilk %22), sonra hizla iner, sonra toparlanir.
                // Zombinin kol animasyonundaki egrinin aynisi, ayni sebeple.
                const float windup01 = 0.22f;

                float wind = Mathf.Clamp01(swing01 / windup01);
                float strike = Mathf.Clamp01((swing01 - windup01) / (1f - windup01));

                // Geri cekme yumusak baslar, iniş SERT: kare-alma egrisi hizlanmayi
                // gozle okunur kilar.
                float pull = Mathf.SmoothStep(0f, 1f, wind) * (1f - strike);
                float chop = strike * strike;

                // Toparlanma: son ceyrekte bicak bekleme yonune donmeye baslar.
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, swing01));

                // Yol: sag-yukari-geriden, sol-asagi-oneye. Capraz bir kesme, yatay
                // bir suzulmeden cok daha okunur ve bicagin agirligini tasir.
                Vector3 back = new Vector3(0.12f, 0.14f, -0.16f);
                Vector3 through = new Vector3(-0.34f, -0.22f, 0.18f);

                Vector3 offset = back * pull + through * chop;
                offset = Vector3.Lerp(offset, KnifeStowed - KnifeHome, settle * 0.55f);

                _knife.localPosition = KnifeHome + offset + sway;

                // Donus de ayni ucluyu izler: geri cekerken bilek burkulur, inerken
                // agiz one doner.
                _knife.localRotation = Quaternion.Euler(
                    -14f - pull * 46f + chop * 78f,
                    -22f - pull * 24f + chop * 64f,
                    -18f - pull * 30f + chop * 72f);
            }
            else
            {
                // Bicak BEKLEME konumu artik hangi slotun elde oldugunu soyluyor:
                // bicak seciliyse KALDIRILIR (KnifeHome), degilse indirilir
                // (KnifeStowed). Iki konum arasinda ayni yumusatma - silahla bicak
                // birbirinin tersine hareket ediyor ve takas gozle okunuyor.
                Vector3 rest = Vector3.Lerp(KnifeStowed, KnifeHome, _handBlend);

                _knife.localPosition = Vector3.Lerp(_knife.localPosition, rest + sway,
                                                    1f - Mathf.Exp(-10f * dt));

                Quaternion restRotation = Quaternion.Slerp(Quaternion.Euler(15f, -20f, 20f),
                                                           Quaternion.Euler(-14f, -22f, -18f),
                                                           _handBlend);

                _knife.localRotation = Quaternion.Slerp(_knife.localRotation, restRotation,
                                                        1f - Mathf.Exp(-10f * dt));
            }
        }

        // ---------------------------------------------------------------- model

        /// <summary>
        /// Gri kutu silahını ve bıçağını kurar. Ölçüler santimetre düzeyinde: kameraya
        /// yakın duran bir nesne birkaç santimetre büyüdüğünde ekranın yarısını kaplar.
        /// </summary>
        /// <summary>
        /// Eldeki silahın gövdesini kurar; silah değişince yeniden çağrılır.
        ///
        /// <para><b>Namlu ucu şekilden gelir</b>: pompalının namlusu tabancanınkinden
        /// 16 cm daha ileride ve alev orada patlamalı. Sabit bir konum, taramalıda
        /// alevin gövdenin içinde patlaması demek olurdu.</para>
        /// </summary>
        private void RebuildGun()
        {
            if (_gun == null) return;

            string id = weapon != null ? weapon.Current.Id : "weapon.pistol";
            if (id == _builtWeaponId) return;

            _builtWeaponId = id;

            // Onceki gövde temizlenir. Alev ve isik AYRI tutuluyor (onlar silahin
            // degil, atisin parcasi) - o yuzden yalnizca isimli govde parcalari gider.
            for (int i = _gun.childCount - 1; i >= 0; i--)
            {
                Transform child = _gun.GetChild(i);
                if (child == null) continue;

                // Alev, isik ve KOLLAR silahin degil elin parcasi: silah degisince
                // yeniden kurulmalari, her degistirmede iki kol yaratip eskisini
                // silmek demek olurdu (ve bir kare boyunca dort kol).
                if (child.name == "MuzzleFlash" || child.name == "MuzzleLight") continue;
                if (child.name == "ArmRight" || child.name == "ArmLeft") continue;

                Destroy(child.gameObject);
            }

            _muzzleTip = WeaponShape.Build(_gun, id, 1f, _gunMaterial, _accentMaterial, art);

            if (_muzzleFlash != null) _muzzleFlash.transform.localPosition = _muzzleTip;

            if (_muzzleLight != null)
            {
                _muzzleLight.transform.localPosition = _muzzleTip + new Vector3(0f, 0.015f, 0.045f);
            }

            // Destek eli silahin boyuna gore kayar: tabancada govdenin dibinde,
            // tufekte on govdede.
            PlaceSupportHand();

            // Govde degisti: gorunurluk onbellegi tazelenir (bkz. Collect).
            // Kollar silahin COCUGU oldugu icin bu dizi onlari da kapsiyor - bicaga
            // gecince eller de kaybolur, ki dogru olan da bu.
            _gunRenderers = Collect(_gun);
        }

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

            // Silahin GORUNUSU sinifina gore (2026-09-06): taramali tabanca gibi
            // gorunemez. Siluet, arayuzun en hizli okunan parcasi (PILLAR-04) ve
            // savasin ortasinda kimse yazi okumaz.
            _gunMaterial = gunMaterial;
            _accentMaterial = accentMaterial;

            RebuildGun();

            // --- namlu alevi: atisin CIKTIGINI soyleyen sey. Isik da var, cunku
            //     karanlik bir kosede yalnizca alev yeterince okunmuyor.
            _muzzleFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _muzzleFlash.name = "MuzzleFlash";
            _muzzleFlash.transform.SetParent(_gun, false);
            _muzzleFlash.transform.localPosition = _muzzleTip;
            _muzzleFlash.transform.localScale = new Vector3(0.075f, 0.075f, 0.11f);
            Destroy(_muzzleFlash.GetComponent<Collider>());

            var flashRenderer = _muzzleFlash.GetComponent<Renderer>();
            flashRenderer.sharedMaterial = MakeUnlitMaterial(new Color(1f, 0.85f, 0.45f));
            flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightHost = new GameObject("MuzzleLight");
            lightHost.transform.SetParent(_gun, false);
            lightHost.transform.localPosition = _muzzleTip + new Vector3(0f, 0.015f, 0.045f);

            _muzzleLight = lightHost.AddComponent<Light>();
            _muzzleLight.type = LightType.Point;
            _muzzleLight.color = new Color(1f, 0.86f, 0.55f);
            _muzzleLight.range = 7f;
            _muzzleLight.intensity = 3.5f;
            _muzzleLight.shadows = LightShadows.None;   // tek karelik isik golge cizmez
            _muzzleLight.enabled = false;

            _muzzleFlash.SetActive(false);

            BuildArms();

            // --- bicak
            var knife = new GameObject("Knife");
            knife.transform.SetParent(_rig, false);
            knife.transform.localPosition = KnifeStowed;
            _knife = knife.transform;

            _knifeBladeMaterial = bladeMaterial;
            _knifeGuardMaterial = gunMaterial;
            _knifeHandleMaterial = accentMaterial;

            BuildKnifeBody(melee != null ? melee.Current.Id : null);
        }

        /// <summary>
        /// Eldeki bıçağın <b>gövdesini</b> kurar; bıçak değişince yeniden çağrılır.
        /// 2026-09-08.
        ///
        /// <para><b>Neden gerçek model</b> (geliştirici: <i>"bu melee saldırısının
        /// meshlerini vs güzelce elden geçir, daha düzgün ve gerçekçi gözüksün"</i>):
        /// üç kutudan yapılmış bir bıçak, üç ayrı bıçağı da aynı gösterir — yani
        /// 2000 puana alınan baltanın karşılığı ekranda hiç görünmez. Model kataloğu
        /// ateşli silahlarda zaten çalışıyordu; bıçak tek istisna kalmıştı.</para>
        ///
        /// <para><b>Geri düşüş yolu duruyor:</b> katalog eksikse gri kutu bıçak kurulur.
        /// Elin boş kalması, oyuncunun ne taşıdığını göremediği <i>sessiz</i> bir hata
        /// olurdu — <see cref="WeaponShape"/>'teki aynı kural.</para>
        /// </summary>
        private void BuildKnifeBody(string meleeId)
        {
            if (_knife == null) return;

            // Onceki govde SILINIR: eklemek, hancerin uzerine kilic koymak demekti.
            for (int i = _knife.childCount - 1; i >= 0; i--)
            {
                Destroy(_knife.GetChild(i).gameObject);
            }

            _builtMeleeId = meleeId;

            if (!string.IsNullOrEmpty(meleeId) &&
                WeaponShape.BuildMelee(_knife, meleeId, 1f, art, out Vector3 tip))
            {
                _knifeTip = tip;
                return;
            }

            Part(_knife, "Blade", new Vector3(0f, 0.02f, 0.14f), new Vector3(0.016f, 0.042f, 0.22f), _knifeBladeMaterial);
            Part(_knife, "Guard", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.060f, 0.016f, 0.020f), _knifeGuardMaterial);
            Part(_knife, "Handle", new Vector3(0f, 0.00f, -0.05f), new Vector3(0.028f, 0.032f, 0.10f), _knifeHandleMaterial);

            _knifeTip = new Vector3(0f, 0.02f, 0.25f);

            // Govde degisti: gorunurluk onbellegi tazelenir (bkz. Collect).
            _knifeRenderers = Collect(_knife);
        }

        /// <summary>
        /// Silahı tutan <b>eller ve kollar</b>. 2026-09-07.
        ///
        /// <para><b>Neden gerekti</b> (geliştirici): <i>"silahlar çok havada
        /// gözüküyor, CS'de EFT'de olduğu gibi elle tuttuğu kolları falan
        /// gözüksün."</i> Doğru teşhis: havada süzülen bir silah, ekranın alt köşesine
        /// yapıştırılmış bir arayüz öğesi gibi okunur. Kol, silahı <b>bir bedene</b>
        /// bağlar; o bedenin de bir ağırlığı ve bir mesafesi olur ve geri tepme
        /// birdenbire <i>hissedilir</i>.</para>
        ///
        /// <para><b>Neden kutulardan, modellenmiş bir karakterden değil:</b> elle
        /// modellenmiş bir çift FPS eli, iskeleti, ağırlık boyaması ve tutuş pozları
        /// olan ayrı bir iştir — ve sanat yönü hâlâ kilitlenmedi (<c>/art-direction</c>
        /// çalışmadı). Kilitlenmeden yapılan bir karakter modeli atılacak iştir. Gri
        /// kutunun kuralı burada da geçerli: <i>okunabilir olsun, güzel olmasın.</i>
        /// Kol iki parçadan (üst kol + ön kol) ve bir avuçtan kuruluyor; siluet
        /// "silahı tutan bir kol" diyor, gerisi sanat geldiğinde değişecek.</para>
        ///
        /// <para><b>Sağ kol silahın kabzasında, sol kol ön gövdede</b> — gerçek bir
        /// tutuş. İkisi de silahın ÇOCUĞU: silah geri teptiğinde, dolum için indiğinde
        /// ve savuruşta çekildiğinde kollar onunla gider. Ayrı yaşasalardı her
        /// hareket için ikinci bir animasyon yazmak gerekirdi ve ikisi zamanla
        /// birbirinden ayrılırdı.</para>
        /// </summary>
        private void BuildArms()
        {
            Material skin = MakeMaterial(new Color(0.52f, 0.40f, 0.33f));
            Material sleeve = MakeMaterial(new Color(0.22f, 0.24f, 0.22f));

            // --- SAG KOL: kabzayi tutar. Kameradan asagi-saga dogru uzanir.
            var right = new GameObject("ArmRight");
            right.transform.SetParent(_gun, false);
            _armRight = right.transform;

            // Kol iki BORU parcasindan: ust kol ve on kol, dirsekte aciyla birlesir.
            // Tek duz bir boru "protez" gibi okunur; dirsek acisi kolu canli yapar.
            Limb(_armRight, "UpperArm", new Vector3(0.105f, -0.255f, -0.30f),
                 new Vector3(0.045f, -0.140f, -0.115f), sleeve, 1.15f);
            Limb(_armRight, "Forearm", new Vector3(0.045f, -0.140f, -0.115f),
                 new Vector3(0.014f, -0.070f, -0.020f), sleeve, 1f);

            Hand(_armRight, new Vector3(0.012f, -0.062f, -0.014f),
                 Quaternion.Euler(-22f, -8f, 0f), skin);

            // --- SOL KOL: on govdeyi destekler. Namluya dogru uzanir, yani
            //     silah uzadikca elin de ileri gitmesi gerekir - konumu silahin
            //     namlu ucundan turetiliyor (RebuildGun yeniden yerlestirir).
            var left = new GameObject("ArmLeft");
            left.transform.SetParent(_gun, false);
            _armLeft = left.transform;

            Limb(_armLeft, "UpperArm", new Vector3(-0.185f, -0.250f, -0.14f),
                 new Vector3(-0.115f, -0.140f, 0.030f), sleeve, 1.10f);
            Limb(_armLeft, "Forearm", new Vector3(-0.115f, -0.140f, 0.030f),
                 new Vector3(-0.056f, -0.062f, 0.125f), sleeve, 1f);

            Hand(_armLeft, new Vector3(-0.050f, -0.055f, 0.135f),
                 Quaternion.Euler(-34f, 14f, 0f), skin);

            // --- SARJOR: dolum animasyonunun tasidigi tek nesne. Silahin degil
            //     SOL ELIN yanindadir - dolum sirasinda el onu asagi cekip geri
            //     surer, yani hareket eden sey elin isi.
            var magazine = new GameObject("Magazine");
            magazine.transform.SetParent(_armLeft, false);
            _magazine = magazine.transform;
            _magazineHome = new Vector3(-0.045f, -0.045f, 0.10f);
            _magazine.localPosition = _magazineHome;

            Part(_magazine, "Body", Vector3.zero, new Vector3(0.030f, 0.095f, 0.055f), sleeve);

            // Kollar silahtan SONRA kuruldugu icin ilk yerlesim burada yapilir;
            // RebuildGun'daki cagri o an _armLeft henuz yokken bos donmustu.
            PlaceSupportHand();
        }

        /// <summary>
        /// Sol elin silahın boyuna göre yerleşmesi: <b>uzun silahta el daha ileride</b>.
        /// Sabit bir konum, tabancada eli namlunun ucuna, tüfekte gövdenin ortasına
        /// koyardı — ikisi de tutuş gibi görünmez.
        /// </summary>
        private void PlaceSupportHand()
        {
            if (_armLeft == null) return;

            // Namlu ucunun yarisi kadar ileride, ama makul bir aralikta: tabancada
            // el gövdenin dibinde, tufekte on gövdede durur.
            float forward = Mathf.Clamp(_muzzleTip.z * 0.45f, 0.06f, 0.34f);

            Vector3 position = _armLeft.localPosition;
            position.z = forward - 0.10f;
            _armLeft.localPosition = position;
        }

        /// <summary>
        /// İki nokta arasına bir <b>kol parçası</b> koyar (üretilmiş daralan boru).
        ///
        /// <para><b>Neden iki nokta, boyut değil:</b> bir kol parçasının doğal tarifi
        /// "şuradan şuraya" — omuzdan dirseğe, dirsekten bileğe. Konum + boyut ile
        /// tarif etmek, dirsek açısını her ayarda elle yeniden hesaplamak demekti ve
        /// eklem yerleri kaçıyordu.</para>
        /// </summary>
        private static void Limb(Transform parent, string name, Vector3 from, Vector3 to,
                                 Material material, float thickness)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f) return;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = from;
            go.transform.localRotation = Quaternion.LookRotation(delta / length, Vector3.up);

            // Mesh'in boyu 1 birim; olcek uzunlugu ve kalinligi birlikte verir.
            go.transform.localScale = new Vector3(thickness, thickness, length);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ArmMesh.Arm();

            Paint(go.AddComponent<MeshRenderer>(), material);
        }

        /// <summary>Eli koyar (avuç + parmaklar, üretilmiş mesh).</summary>
        private static void Hand(Transform parent, Vector3 localPosition,
                                 Quaternion localRotation, Material material)
        {
            var go = new GameObject("Hand");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = ArmMesh.Hand();

            Paint(go.AddComponent<MeshRenderer>(), material);
        }

        /// <summary>
        /// El modelinin çizim ayarları: <b>gölge yok, ışın yok</b>. Kameraya yapışık
        /// bir nesnenin gölge çizmesi hem bedava değil hem de yanlış görünür.
        /// </summary>
        private static void Paint(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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
