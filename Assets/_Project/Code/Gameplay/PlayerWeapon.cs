using System;
using Bunker.Audio;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Başlangıç silahı: hitscan, şarjör, dolum, geri tepme, isabet geri bildirimi.
    /// M1-06 — bu milestone'un en kritik işi.
    ///
    /// <para><b>Girdi aynı karede karşılanır.</b> Tetiğe basıldığı an geri tepme,
    /// iz ve mermi sayacı yereldeki oyuncuda hemen olur; host'un onayı beklenmez.
    /// Beklemek, tek kişilik oyunda bile hissedilen bir gecikme üretir ve oyunun ölü
    /// hissetmesinin en yaygın sebebidir (gameplay-code.md, 60 ms hedefi).</para>
    ///
    /// <para><b>Hasar host'ta uygulanır</b> (ADR-0004). İstemci "şuraya nişan aldım"
    /// der; kimin öldüğüne host karar verir. İstemcinin gönderdiği yön ve konum
    /// doğrulanır: atış hızı sunucu tarafında ayrıca sayılır, çünkü <b>her RPC bir güven
    /// sınırıdır</b> (netcode.md) ve döngüde çağrılabilen her şey hız sınırlı olmalıdır.</para>
    ///
    /// <para><b>Sayıların hiçbiri burada değil.</b> Hepsi
    /// <c>config/balance/weapon.json</c>'dan geliyor; bu dosyada bir denge sabiti
    /// bulursan o bir defadır (config-data.md).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Weapon")]
    public sealed class PlayerWeapon : NetworkBehaviour
    {
        [Header("Ayar")]
        [Tooltip("config/balance/weapon.json'dan uretilen varlik. Baslangic silahinin " +
                 "dengesi ve HIS ayarlari (iz, isabet isareti, girdi tamponu) buradan gelir.")]
        [SerializeField] private WeaponConfigAsset weaponConfig;

        [Tooltip("config/content/weapons.json'dan uretilen silah katalogu (M-05).")]
        [SerializeField] private WeaponCatalogAsset catalog;

        [Header("Referanslar")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private Camera playerCamera;

        [Tooltip("Merminin ciktigi nokta. Bos birakilirsa kamera kullanilir - gri " +
                 "kutuda dogru olan da budur, cunku silah modeli yok.")]
        [SerializeField] private Transform muzzle;

        [Header("Gri kutu geri bildirimi")]
        [Tooltip("Mermi izi cizgisi. Sanat gelene kadar atisin nereye gittigini " +
                 "gosteren tek sey bu.")]
        [SerializeField] private LineRenderer tracer;

        /// <summary>Oyuncunun her run'a basladigi silah. Katalogdaki id ile ayni.</summary>
        private const string StarterWeaponId = "weapon.pistol";

        /// <summary>
        /// Bu atis hizinin ustundeki silahlar OTOMATIK ates eder.
        ///
        /// <para>Denge degil, HIS esigi: dakikada 500 atis bir silahta tek tek
        /// tiklanamaz - oyuncunun parmagi silahin ritmini tasiyamaz ve silahin varlik
        /// sebebi (hizli ritim) kaybolur.</para>
        /// </summary>
        private const float AutoFireThresholdRpm = 500f;

        private System.Collections.Generic.List<WeaponDefinition> _catalog;

        // Envanter: her silahin KENDI mermisi ve kendi dogrulayicisi var. Ortak bir
        // sayac, pompaliyla tabancanin ayni mermiyi paylasmasi demek olurdu.
        private readonly System.Collections.Generic.List<WeaponDefinition> _owned =
            new System.Collections.Generic.List<WeaponDefinition>(4);
        private readonly System.Collections.Generic.List<WeaponState> _states =
            new System.Collections.Generic.List<WeaponState>(4);
        private readonly System.Collections.Generic.List<ServerFireGuard> _guards =
            new System.Collections.Generic.List<ServerFireGuard>(4);

        private int _slot = -1;

        private WeaponDefinition _config;
        private WeaponState _state;

        // Sunucunun hile denetimi. Istemcinin simulasyonunu TEKRARLAMAZ; makul olup
        // olmadigina bakar. Istemci otoritesi burada biter (BUG-002).
        private ServerFireGuard _guard;

        private float _tracerRemaining;
        private float _hitMarkerRemaining;
        private bool _lastShotWasHeadshot;
        private float _lastRejectWarnTime = -99f;

        // Delici mermi tamponlari bir kez ayrilir (csharp-code.md). Sekiz katman derin
        // bir sura zaten penetrasyon kartinin tavaninin cok ustunde.
        private static readonly RaycastHit[] PenetrationHits = new RaycastHit[16];
        private static readonly System.Collections.Generic.HashSet<IDamageable> _penetrationTargets =
            new System.Collections.Generic.HashSet<IDamageable>();

        /// <summary>
        /// Tetik çekildi ve mermi çıktı. <b>Yerel, aynı karede</b> — el modeli ve ses
        /// buna bağlanır. Sunucunun onayı beklenmez; beklemek 60 ms hedefini
        /// (gameplay-code.md) kaçırmanın en kolay yoludur.
        /// </summary>
        public event Action Fired;

        /// <summary>Bir isabet onaylandı (kafa mı, öldürdü mü). HUD buna bağlanır.</summary>
        public event Action<bool, bool> HitConfirmed;

        /// <summary>Bir zombi öldürüldü — ekonomi buna bağlanır.</summary>
        public event Action<DamageKind, bool> KillConfirmed;

        public int RoundsInMagazine => _state?.RoundsInMagazine ?? 0;
        public int Reserve => _state?.Reserve ?? 0;
        public int MagazineCapacity => _state?.MagazineCapacity ?? 0;
        public bool IsReloading => _state?.IsReloading ?? false;
        public float ReloadProgress01 => _state?.ReloadProgress01 ?? 0f;

        /// <summary>İsabet işaretinin kalan süresi (0 = gösterme).</summary>
        public float HitMarkerRemaining => _hitMarkerRemaining;

        public bool LastHitWasHeadshot => _lastShotWasHeadshot;

        // ---------------------------------------------------------------- kurulum

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);

            if (weaponConfig == null)
            {
                // Eksik ayar sessiz varsayilanla gecistirilmez (config-protocol.md).
                Debug.LogError("[Silah] weapon.asset atanmamis. 'Bunker/Config/Ice Aktar' " +
                               "ile uret, 'Bunker/Zombi/Test Alanini Kur' ile bagla.", this);
                enabled = false;
                return;
            }

            WeaponConfig starter = weaponConfig.ToRuntime();

            // Katalog: farkli silahlar (2026-09-06). Katalog yoksa oyun yalnizca
            // baslangic silahiyla calisir - eksik bir katalog oyunu durdurmamali,
            // ama SESSIZ de kalmamali.
            if (catalog != null && catalog.Count > 0)
            {
                _catalog = catalog.ToRuntime(starter.FeelTracerSeconds,
                                             starter.FeelHitMarkerSeconds,
                                             starter.FeelInputBufferSeconds);
            }
            else
            {
                Debug.LogWarning("[Silah] weapons.asset yok - yalnizca baslangic silahi. " +
                                 "'Bunker/Config/Silahlari Ice Aktar' calistir.", this);
                _catalog = new System.Collections.Generic.List<WeaponDefinition>(1);
            }

            // Baslangic silahi: katalogda ayni id varsa O kullanilir (tek kaynak),
            // yoksa weapon.json'dan uretilen tanim.
            WeaponDefinition pistol = FindInCatalog(StarterWeaponId);
            if (!pistol.IsValid) pistol = WeaponDefinition.FromConfig(starter);

            AddWeapon(pistol);
            EquipSlot(0);

            if (tracer != null) tracer.enabled = false;
        }

        /// <summary>
        /// Bir silahı envantere ekler ve durumunu kurar.
        ///
        /// <para><b>Her silahın kendi mermisi var</b>: durum listesi silahla birlikte
        /// taşınır. Tek bir ortak sayaç, pompalıyla tabancanın aynı mermiyi paylaşması
        /// demek olurdu ve silah seçimi bir karar olmaktan çıkardı.</para>
        /// </summary>
        /// <returns>Yeni eklendiyse <c>true</c>; zaten varsa <c>false</c>.</returns>
        public bool AddWeapon(in WeaponDefinition definition)
        {
            if (!definition.IsValid) return false;

            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Id == definition.Id) return false;
            }

            _owned.Add(definition);
            _states.Add(new WeaponState(definition));
            _guards.Add(new ServerFireGuard(definition));

            return true;
        }

        /// <summary>Envanterdeki bir silaha geçer.</summary>
        public void EquipSlot(int slot)
        {
            if (slot < 0 || slot >= _owned.Count) return;
            if (slot == _slot && _state != null) return;

            _slot = slot;
            _config = _owned[slot];
            _state = _states[slot];
            _guard = _guards[slot];

            // Kart etkileri silaha OZEL hesaplanir: yeni silahin sarjoru ve dolum
            // suresi kartlarla birlikte kurulmali, yoksa gecilen silah kartsiz kalir.
            WeaponModifiers mods = BuildModifiers();
            _state.ApplyModifiers(mods);
            _guard.ApplyModifiers(mods);

            // Geri tepmenin toparlanmasi kameranin sahibinde yasar ama sayisi silahin
            // ayarindan gelir - tek kaynak. Silah degisince yeniden kurulur.
            if (controller != null)
            {
                controller.ConfigureRecoilRecovery(
                    _config.RecoilRecoveryPerSecond, _config.RecoilMaxPitch);
            }

            GameAudio.Play(SfxId.ReloadIn, 0.7f);
        }

        /// <summary>Bu id envanterde var mı (duvar satın alma noktası sorar).</summary>
        public bool Owns(string weaponId)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Id == weaponId) return true;
            }

            return false;
        }

        /// <summary>Katalogdan bir silah tanımı. Bulunamazsa geçersiz tanım döner.</summary>
        public WeaponDefinition FindInCatalog(string weaponId)
        {
            if (_catalog == null) return default;

            for (int i = 0; i < _catalog.Count; i++)
            {
                if (_catalog[i].Id == weaponId) return _catalog[i];
            }

            return default;
        }

        /// <summary>Eldeki silahın tanımı. HUD ve durum paneli okur.</summary>
        public WeaponDefinition Current => _config;

        /// <summary>Envanterdeki silah sayısı.</summary>
        public int OwnedCount => _owned.Count;

        /// <summary>
        /// Silahı envantere ekler ve isteğe bağlı olarak <b>ele alır</b>.
        /// <b>Yalnızca sunucu</b> (ADR-0004): silah kalıcı sonucu olan bir kazanım.
        /// </summary>
        [Server]
        public void ServerGrantWeapon(WeaponDefinition definition, bool equip)
        {
            if (!AddWeapon(definition)) return;

            TargetGrantWeapon(connectionToClient, definition.Id, equip);

            if (equip) EquipSlot(_owned.Count - 1);
        }

        /// <summary>
        /// Silahı <b>istemcide de</b> envantere ekler.
        ///
        /// <para>Envanter iki tarafta da yaşar: sunucu hasarın, istemci görünen
        /// şarjörün sahibi. Yalnızca sunucuda eklenseydi, satın alan oyuncu silahı
        /// kendi ekranında hiç görmezdi.</para>
        /// </summary>
        [TargetRpc]
        private void TargetGrantWeapon(NetworkConnection target, string weaponId, bool equip)
        {
            if (isServer) return;   // host: sunucu tarafi zaten ekledi

            WeaponDefinition definition = FindInCatalog(weaponId);
            if (!definition.IsValid) return;

            if (!AddWeapon(definition)) return;
            if (equip) EquipSlot(_owned.Count - 1);
        }

        public string OwnedName(int slot) =>
            slot >= 0 && slot < _owned.Count ? _owned[slot].DisplayName : string.Empty;

        public int EquippedSlot => _slot;

        private void OnEnable()
        {
            RunSignals.RunRestarted += OnRunRestarted;
            CardSignals.LoadoutChanged += OnLoadoutChanged;
            RoundSignals.RoundEndRestock += OnRoundEndRestock;
        }

        private void OnDisable()
        {
            RunSignals.RunRestarted -= OnRunRestarted;
            CardSignals.LoadoutChanged -= OnLoadoutChanged;
            RoundSignals.RoundEndRestock -= OnRoundEndRestock;
        }

        /// <summary>
        /// Tur bitti: yedek merminin bir kısmı kendiliğinden geri gelir (2026-09-05,
        /// geliştirici kararı).
        ///
        /// <para><b>Tavanın oranı kadar, eksiğin değil.</b> Eksiğin oranı olsaydı mermisi
        /// bitmiş oyuncu en az mermiyi alırdı — cezanın üstüne ceza.</para>
        ///
        /// <para><b>Otorite sunucuda</b> (ADR-0004): mermi kalıcı sonucu olan bir kaynak.
        /// <see cref="ServerAddReserve"/> zaten istemcinin görünen sayacını da
        /// tazeliyor, yani ayrı bir senkron gerekmiyor.</para>
        ///
        /// <para>Oran <c>rounds.json → roundEnd.reserveAmmoFraction01</c>'de; buraya bir
        /// sayı yazılmaz (config-data.md).</para>
        /// </summary>
        private void OnRoundEndRestock(float reserveAmmoFraction01, float boardsFraction01)
        {
            if (!isServer || _state == null) return;
            if (reserveAmmoFraction01 <= 0f) return;

            int amount = Mathf.RoundToInt(_state.ReserveCapacity * reserveAmmoFraction01);
            if (amount <= 0) return;

            ServerAddReserve(amount);
        }

        /// <summary>
        /// Kart yığını değişti: silahın türetilmiş sayıları yeniden kurulur.
        ///
        /// <para><b>İstemci ve sunucu AYNI anda güncellenir.</b> Biri güncellenip
        /// diğeri kalsaydı doğrulayıcı meşru atışları reddederdi — BUG-002'nin
        /// birebir tekrarı.</para>
        ///
        /// <para><b>Neden olayla, kare başına okumayla değil:</b> şarjör kapasitesi ve
        /// dolum süresi silahın <i>durumuna</i> giriyor; her karede yeniden hesaplamak
        /// hem gereksiz hem de dolum ortasında süreyi değiştirir.</para>
        /// </summary>
        private void OnLoadoutChanged(CardLoadout loadout)
        {
            WeaponModifiers mods = BuildModifiers();

            _state?.ApplyModifiers(mods);
            _guard?.ApplyModifiers(mods);
        }

        /// <summary>Kart VE tezgah etkileri tek noktadan okunur (RunModifiers).</summary>
        private static WeaponModifiers BuildModifiers() =>
            new WeaponModifiers(
                fireRate: RunModifiers.Total(CardStat.FireRate),
                reloadSpeed: RunModifiers.Total(CardStat.ReloadSpeed),
                damage: RunModifiers.Total(CardStat.WeaponDamage),
                magazine: Mathf.RoundToInt(RunModifiers.Total(CardStat.MagazineCapacity)),
                reserve: Mathf.RoundToInt(RunModifiers.Total(CardStat.ReserveCapacity)),
                headshotMultiplier: RunModifiers.Total(CardStat.HeadshotMultiplier));

        /// <summary>
        /// Yeni run: mermi başlangıç değerine döner.
        ///
        /// <para><b>Tur başında DEĞİL, RUN başında.</b> Önceki sürüm her tur başında
        /// mermiyi tazeliyordu ve kendi yorumu bunu şöyle gerekçelendiriyordu:
        /// <i>"M-01'de mermi kaynağı (duvar silahı, dağıtıcı) henüz yok... Bu bir denge
        /// kararı değil, eksik sistemin geçici yerine geçen şey — <b>M1-10 duvar silahı
        /// gelince kaldırılacak</b>."</i></para>
        ///
        /// <para><b>M1-10 geldi ve bu iskele kalmıştı.</b> Yani bedava mermi bir tasarım
        /// tercihi değil, silinmesi unutulmuş bir geçici çözümdü — ve oyunu tam olarak
        /// geliştiricinin 2026-09-04'te işaret ettiği kadar kolaylaştırıyordu.</para>
        ///
        /// <para><b>Sonucu küçük değil:</b> denge simülasyonu mermiyi satın alınan bir
        /// kaynak varsayarak koştu ve gelirin %77–98'inin mermiye gittiğini buldu
        /// (<c>design/economy/curves.md</c>). Yani oyun bugüne kadar simülasyonun
        /// anlattığından <b>belirgin şekilde kolaydı</b>; bu satırla ikisi hizalanıyor.</para>
        ///
        /// <para><b>Run sıfırlaması ŞART:</b> tur sıfırlaması kalkınca <c>R</c> ile
        /// başlayan ikinci run, birincinin kalan mermisiyle başlardı (M1-11 AC-6).</para>
        /// </summary>
        private void OnRunRestarted()
        {
            _state.Reset();
            _guard.Reset();
        }

        // ---------------------------------------------------------------- kare dongusu

        private void Update()
        {
            if (_state == null) return;

            float dt = Time.deltaTime;

            bool wasReloading = _state.IsReloading;

            _state.Tick(dt);
            TickFeedback(dt);

            // Dolumun BITTIGI an: sarjorun oturdugu ses. Zamanlayici tutmuyoruz,
            // durumun kendisinden okuyoruz - iki ayri sayac hep birbirinden kayar
            // (audio-code.md: geri bildirim oyun durumundan tetiklenir).
            if (isLocalPlayer && wasReloading && !_state.IsReloading) GameAudio.Play(SfxId.ReloadIn);

            // Yalnizca yerel oyuncu kendi silahini surer.
            if (!isLocalPlayer) return;

            // Run bitti: girdi kesilir (M1-11, AC-3). Skor ekraninin arkasindan ates
            // etmek, olumu bir sonuc olmaktan cikarir. Silahin kendi zamani (dolum,
            // geri bildirim) yukarida akmaya devam eder - durdurulan sey KOMUT.
            // Run bitti YA DA tur arasi ekrani acik: girdi kesilir. Ekran acikken
            // ates etmek, bakis cevirmek ya da satin almak, fareyle kart secmeyi
            // imkansiz kilardi.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen) return;

            ReadInput();
        }

        /// <summary>
        /// Silah değiştirme: <b>1..4</b> ya da fare tekerleği.
        ///
        /// <para><b>İki yol birden</b>, çünkü ikisi iki farklı ana ait: sayı tuşu
        /// "şimdi pompalıyı istiyorum" der, tekerlek "bir öncekine dön" der. Sürünün
        /// içinde ikincisi hayat kurtarır, çünkü hangi yuvada ne olduğunu düşünmeye
        /// zaman yoktur.</para>
        ///
        /// <para><b>Dolum sırasında değiştirmek serbest</b> ve dolumu iptal eder — bu
        /// bir hile değil, bir bedel: yarım kalan dolum baştan başlar.</para>
        /// </summary>
        private void ReadWeaponSwitch(Keyboard keyboard, Mouse mouse)
        {
            if (_owned.Count <= 1) return;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SwitchTo(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) SwitchTo(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) SwitchTo(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) SwitchTo(3);
            }

            if (mouse == null) return;

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f) return;

            int step = wheel > 0f ? 1 : -1;
            SwitchTo((_slot + step + _owned.Count) % _owned.Count);
        }

        private void SwitchTo(int slot)
        {
            if (slot < 0 || slot >= _owned.Count || slot == _slot) return;

            // Yarim kalan dolum iptal olur: silah degistirmek onu tamamlamis saymak,
            // dolumu bedava bir iptal tusuna cevirirdi.
            _state?.CancelReload();

            EquipSlot(slot);
        }

        private void ReadInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                StartReload();
            }

            ReadWeaponSwitch(keyboard, mouse);

            if (mouse == null) return;

            // OTOMATIK ATES: dakikada 400'un ustundeki silahlar basili tutmayla ateş
            // eder (2026-09-06). Yari otomatik his tabancanin KARAKTERI, bir motor
            // kisiti degil - MP'yi tek tek tiklatmak, o silahin varlik sebebini
            // (ritim farki) yok ederdi.
            bool automatic = _config.RoundsPerMinute >= AutoFireThresholdRpm;

            bool pressed = automatic
                ? mouse.leftButton.isPressed
                : mouse.leftButton.wasPressedThisFrame;
            if (!pressed && _state.Phase != WeaponPhase.Ready) return;

            FireResult result = _state.TryFire(pressed);

            switch (result)
            {
                case FireResult.Fired:
                    FireLocally();
                    break;

                case FireResult.Empty:
                    // Bos sarjorde tetige basmak dolum baslatir. Oyuncunun ayrica
                    // R.ye basmasini beklemek, sürünün icinde ceza gibi hissettirir.
                    // Bos tetik SESI de var: hicbir sey olmamasi, tusun calismadigi
                    // gibi okunur.
                    GameAudio.Play(SfxId.GunDryFire);
                    StartReload();
                    break;
            }
        }

        /// <summary>
        /// Dolumu başlatır <b>ve sunucuya bildirir</b>.
        ///
        /// <para><b>Bu bildirimin yokluğu BUG-001'di:</b> istemci kendi şarjörünü
        /// dolduruyor, sunucunun saydığı mermi ise ilk şarjörden sonra hiç geri
        /// gelmiyordu. Sunucuda bir kaynağı sayan sistem, o kaynağı geri veren yolu
        /// <b>aynı anda</b> yazmak zorundadır.</para>
        /// </summary>
        private void StartReload()
        {
            if (!_state.TryStartReload()) return;

            GameAudio.Play(SfxId.ReloadOut);
            CmdReload();
        }

        /// <summary>
        /// Yedege mermi ekler. <b>Yalnizca sunucu cagirir</b> (duvar silahi, dagitici).
        /// Istemcinin gorunen sayaci bir sonraki senkronda degil, hemen guncellensin
        /// diye yerel durum da tazelenir - mermi almanin karsiligi aninda gorulmeli.
        /// </summary>
        [Server]
        public void ServerAddReserve(int amount)
        {
            if (amount <= 0) return;

            _guard.AddReserve(amount);
            TargetAddReserve(connectionToClient, amount);
        }

        [TargetRpc]
        private void TargetAddReserve(NetworkConnection target, int amount)
        {
            _state.AddReserve(amount);
        }

        /// <summary>
        /// Sunucuya dolumu bildirir. İstemci <i>ne zaman</i> doldurduğunu söyler;
        /// <b>ne kadar süreceğine sunucu kendi ayarından karar verir</b>, yani dolum
        /// süresini kısaltarak avantaj alınamaz.
        /// </summary>
        [Command]
        private void CmdReload()
        {
            _guard.NoteReload(Time.time);
        }

        /// <summary>
        /// Yerel geri bildirim: geri tepme, iz, sayaç. <b>Host'un onayı beklenmez.</b>
        /// </summary>
        private void FireLocally()
        {
            Transform origin = muzzle != null ? muzzle : playerCamera.transform;
            Vector3 direction = ApplySpread(playerCamera.transform.forward);

            // Geri tepme atisin ayni karesinde. Bir kare sonrasi bile "gecikmis"
            // hissettirir.
            if (controller != null)
            {
                float yaw = UnityEngine.Random.Range(-1f, 1f) * _config.RecoilYawPerShot;
                controller.AddRecoil(_config.RecoilPitchPerShot, yaw);
            }

            Vector3 endPoint = origin.position + direction * _config.RangeMeters;

            // Yerel isin YALNIZCA gorsel icindir - hasari host uygular. Iki taraf
            // farkli sonuc bulursa gecerli olan host'unkidir.
            if (Physics.Raycast(origin.position, direction, out RaycastHit hit, _config.RangeMeters,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
            }

            ShowTracer(origin.position, endPoint);

            // El modeli ve ses ayni karede: ates ettigini gosteren sey namlu alevi ve
            // patlama sesidir, sunucunun bir kare sonra donen onayi degil.
            GameAudio.Play(SfxId.GunShot);
            Fired?.Invoke();

            CmdFire(origin.position, direction);
        }

        private Vector3 ApplySpread(Vector3 forward)
        {
            if (_config.SpreadDegrees <= 0f) return forward;

            // Koni icinde rastgele sapma. Determinizm gerekmiyor: atis tekrar
            // oynatilmiyor ve kaydedilmiyor (csharp-code.md'nin seed kurali
            // tekrarlanabilir seyler icindir).
            float radius = Mathf.Tan(_config.SpreadDegrees * Mathf.Deg2Rad);
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;

            Transform cam = playerCamera.transform;
            return (forward + cam.right * offset.x + cam.up * offset.y).normalized;
        }

        // ---------------------------------------------------------------- otorite

        /// <summary>
        /// Hasarın uygulandığı tek yer. <b>Her RPC bir güven sınırıdır</b> (netcode.md):
        /// atış hızı sunucuda ayrıca sayılır, böylece istemci döngüde çağırarak
        /// sınırsız hasar üretemez.
        /// </summary>
        [Command]
        private void CmdFire(Vector3 origin, Vector3 direction, NetworkConnectionToClient sender = null)
        {
            // Hile denetimi: sunucu simulasyonu tekrarlamaz, MAKUL olup olmadigina
            // bakar. Kare kare aynilik beklemek, komut agdan bir kare sonra geldigi
            // icin oyuncunun tikini yiyordu (BUG-002).
            FireRejection rejection = _guard.TryAcceptShot(Time.time);

            if (rejection != FireRejection.None)
            {
                // Reddedilen atis SESSIZ olmaz. Bu satirin yoklugu, sunucunun sarjoru
                // bittikten sonra butun atislarin sessizce dusmesine ve oyunun
                // "zombiler hasar yemiyor" gibi gorunmesine sebep oldu; teshis bir
                // oyun testi surdu (BUG-001).
                WarnRejectedShot(rejection);
                return;
            }

            if (direction.sqrMagnitude < 0.001f) return;
            direction.Normalize();

            // Konum dogrulamasi: istemci kendi konumu uzerinde otorite sahibidir
            // (ADR-0004) ama silahin oyuncudan kopmasina izin verilmez.
            const float maxOriginDriftMeters = 3f;
            if ((origin - transform.position).sqrMagnitude >
                maxOriginDriftMeters * maxOriginDriftMeters)
            {
                return;
            }

            // POMPALI: tek tetikte birden fazla sacma (2026-09-06).
            //
            // <b>Dagilimi SUNUCU uretir</b>, istemci degil: sacmalarin yonunu istemci
            // gonderseydi, hepsini ayni noktaya toplayan bir istemci pompaliyi
            // keskin nisanci tufegine cevirirdi (netcode.md: her RPC bir guven siniri).
            if (_config.PelletCount > 1)
            {
                FirePellets(origin, direction, sender);
                return;
            }

            // Tek isin, EN YAKIN isabet. Onceki surum RaycastNonAlloc + elle en yakini
            // bulma kullaniyordu; o cagri sekiz slotluk tamponu SIRASIZ doldurur ve
            // isin uzerinde sekizden fazla carpisan varsa gercek en yakini atabilir -
            // kalabalik bir surunun icinde tam olarak "mermi gitmedi" hatasi uretir.
            // Physics.Raycast en yakini garanti eder ve tahsis yapmaz.
            // DELICI MERMI (M-03 kart): penetrasyon varsa isin ilk hedefte durmaz.
            // Ayri bir yol, cunku RaycastAll her atista dizi ayirir ve SIRASIZ doner;
            // penetrasyon yokken - yani atislarin cogunda - o bedeli odemenin sebebi
            // yok (csharp-code.md).
            int penetration = Mathf.RoundToInt(RunModifiers.Total(CardStat.Penetration));

            if (penetration > 0)
            {
                FirePenetrating(origin, direction, penetration, sender);
                return;
            }

            if (!Physics.Raycast(origin, direction, out RaycastHit serverHit,
                                 _config.RangeMeters, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            // Isabet eden sey ne oldugunu KENDISI soyler: silah kafa kutusunu bilmez,
            // katman testi yapmaz. Bilmediginde yeni bir hedef tipi eklemek silahi
            // degistirmeyi gerektirmez.
            var target = serverHit.collider.GetComponent<IDamageable>();
            if (target == null || !target.IsAlive) return;

            // Vurulan sey kafa kutusu olup olmadigini KENDISI soyler - hasar
            // uygulanmadan once. Once taban hasari gonderip sonra carpani ikinci bir
            // uygulamayla eklemek, tek atistan iki hasar olayi uretirdi; can havuzu
            // olumu bir kez bildirdigi icin de ikincisi sessizce yutulurdu.
            bool headshot = target.CountsAsHeadshot;

            DamageResult result = target.ApplyDamage(
                new DamageInfo(_guard.DamageFor(headshot), DamageKind.Bullet, headshot));

            // Isabet noktasi ve GERCEKTEN EMILEN hasar birlikte gonderiliyor: hasar
            // sayisini istemcide yeniden hesaplamak, kart etkileri degistiginde iki
            // tarafin farkli sayilar gostermesi demek olurdu (BUG-002'nin dersi).
            TargetReportHit(sender, headshot, result.Killed, serverHit.point, result.Absorbed);

            if (result.Killed) KillConfirmed?.Invoke(DamageKind.Bullet, headshot);
        }

        /// <summary>
        /// Delici mermi: ışın <b>ilk hedefte durmaz</b> (M-03 Balistik kartı).
        ///
        /// <para><b>Duvar mermiyi durdurur.</b> Penetrasyon kartı zombiden geçmeyi
        /// sağlar, geometriden değil — aksi hâlde duvarın arkasındaki sürüyü taramak
        /// mümkün olurdu ve haritanın anlamı kalmazdı.</para>
        ///
        /// <para><b>Her zombiye bir kez.</b> Aynı zombinin kafası ve gövdesi ışının
        /// üstünde art arda durabilir; ikisine birden vurmak penetrasyonu sessizce iki
        /// katına çıkarırdı.</para>
        /// </summary>
        private void FirePenetrating(Vector3 origin, Vector3 direction, int penetration,
                                     NetworkConnectionToClient sender)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, PenetrationHits,
                                                _config.RangeMeters, ~0,
                                                QueryTriggerInteraction.Ignore);
            if (count == 0) return;

            // RaycastNonAlloc SIRASIZ doner: mesafeye gore siralamak sart, yoksa mermi
            // arkadaki zombiye onden gecmeden vurur ve duvar kontrolu anlamsizlasir.
            System.Array.Sort(PenetrationHits, 0, count, RaycastDistanceComparer.Instance);

            int remaining = penetration + 1;
            _penetrationTargets.Clear();

            for (int i = 0; i < count && remaining > 0; i++)
            {
                RaycastHit hit = PenetrationHits[i];
                if (hit.collider == null) continue;

                var target = hit.collider.GetComponent<IDamageable>();

                if (target == null)
                {
                    // Zombi olmayan bir sey: duvar, zemin, tahta. Mermi burada durur.
                    return;
                }

                if (!target.IsAlive) continue;

                // Ayni yaratiga iki kez vurma (kafa + govde ayni isin uzerinde).
                //
                // KIMLIK HEDEFTEN GELIR, sahne hiyerarsisinden DEGIL (2026-09-05 hatasi):
                // ilk surum transform.root kullaniyordu ve delici mermi HIC calismadi -
                // zombiler havuzun altinda yasiyor, yani hepsinin root'u ayni nesne ve
                // mermi ilk zombiden sonra herkesi "zaten vurdum" diye eliyordu.
                IDamageable identity = target.DamageRoot ?? target;
                if (!_penetrationTargets.Add(identity)) continue;

                bool headshot = target.CountsAsHeadshot;

                DamageResult result = target.ApplyDamage(
                    new DamageInfo(_guard.DamageFor(headshot), DamageKind.Bullet, headshot));

                TargetReportHit(sender, headshot, result.Killed, hit.point, result.Absorbed);

                if (result.Killed) KillConfirmed?.Invoke(DamageKind.Bullet, headshot);

                remaining--;
            }
        }

        /// <summary>
        /// Pompalının saçmaları. Her saçma kendi ışını, kendi hasarı.
        ///
        /// <para><b>Menzil ayarını dağılım yapar, bir menzil sayısı değil:</b> yakında
        /// sekiz saçmanın hepsi aynı zombiye girer (ağır hasar), uzakta koni genişler
        /// ve çoğu ıskalar. Bu, "pompalı uzakta işe yaramaz" kuralını bir <i>sayıya</i>
        /// değil <b>geometriye</b> bağlar — oyuncu mesafeyi gözüyle öğrenir.</para>
        ///
        /// <para><b>Aynı zombiye birden fazla saçma girebilir</b> ve bu doğru: pompalının
        /// yakın mesafedeki gücü tam olarak budur. Delici merminin "aynı hedefe iki kez
        /// vurma" kuralı buraya uygulanmaz — orada tek bir mermi vardı, burada sekiz
        /// ayrı saçma var.</para>
        /// </summary>
        private void FirePellets(Vector3 origin, Vector3 direction, NetworkConnectionToClient sender)
        {
            int pellets = _config.PelletCount;
            float spreadRadius = Mathf.Tan(_config.SpreadDegrees * Mathf.Deg2Rad);

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(direction, right);

            for (int i = 0; i < pellets; i++)
            {
                // Dagilim RASTGELE: pompali her atista ayni deseni verseydi, oyuncu
                // deseni ezberler ve "sacma" olmaktan cikardi.
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spreadRadius;
                Vector3 pelletDirection =
                    (direction + right * offset.x + up * offset.y).normalized;

                if (!Physics.Raycast(origin, pelletDirection, out RaycastHit hit,
                                     _config.RangeMeters, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                var target = hit.collider.GetComponent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                bool headshot = target.CountsAsHeadshot;

                DamageResult result = target.ApplyDamage(
                    new DamageInfo(_guard.DamageFor(headshot), DamageKind.Bullet, headshot));

                TargetReportHit(sender, headshot, result.Killed, hit.point, result.Absorbed);

                if (result.Killed) KillConfirmed?.Invoke(DamageKind.Bullet, headshot);
            }
        }

        /// <summary>Işın üzerindeki isabetleri mesafeye göre sıralar. Tahsissiz.</summary>
        private sealed class RaycastDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastDistanceComparer Instance = new RaycastDistanceComparer();

            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        /// <summary>
        /// Sonucu <b>yalnızca atan oyuncuya</b> bildirir. İsabet geri bildirimi kişisel
        /// bir bilgidir; herkese yayınlamak hem bant genişliği hem gürültüdür.
        /// </summary>
        [TargetRpc]
        private void TargetReportHit(NetworkConnection target, bool headshot, bool killed,
                                     Vector3 hitPoint, float damage)
        {
            _hitMarkerRemaining = _config.HitMarkerSeconds;
            _lastShotWasHeadshot = headshot;

            GameAudio.Play(headshot ? SfxId.HeadshotMarker : SfxId.HitMarker);
            HitConfirmed?.Invoke(headshot, killed);

            // Hasar sayisi (2026-09-05): vurusun ne kadar ise yaradigini soyleyen tek
            // sey. Gec turlarda "silahim ise yariyor mu" sorusunun cevabi budur.
            CombatFeedback.RaiseDamageDealt(hitPoint, damage, headshot, killed);
        }

        // ---------------------------------------------------------------- geri bildirim

        /// <summary>
        /// Sunucunun reddettiği atışı görünür kılar — <b>saniyede en fazla bir kez</b>,
        /// çünkü döngüde çağrılan bir istemci Console'u da doldurabilmemeli.
        /// </summary>
        private void WarnRejectedShot(FireRejection reason)
        {
            if (Time.unscaledTime - _lastRejectWarnTime < 1f) return;
            _lastRejectWarnTime = Time.unscaledTime;

            Debug.LogWarning($"[Silah] Sunucu atisi reddetti: {reason}. " +
                             "Istemci ile sunucunun silah durumu ayrismis olabilir " +
                             "(dolum bildirilmedi, ya da atis hizi asildi).", this);
        }

        private void ShowTracer(Vector3 from, Vector3 to)
        {
            if (tracer == null) return;

            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.enabled = true;
            _tracerRemaining = _config.TracerSeconds;
        }

        private void TickFeedback(float dt)
        {
            if (_tracerRemaining > 0f)
            {
                _tracerRemaining -= dt;
                if (_tracerRemaining <= 0f && tracer != null) tracer.enabled = false;
            }

            if (_hitMarkerRemaining > 0f) _hitMarkerRemaining -= dt;
        }
    }
}
