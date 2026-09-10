using System;
using Bunker.Audio;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Bunker.Systems.Pickups;
using Bunker.Systems.Rounds;
using Bunker.Systems.Ui;
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

        /// <summary>
        /// Oyuncunun her run'a basladigi silah. Katalogdaki id ile ayni. Silah Atolyesi
        /// bu silahin oyundan kaldirilmasina izin vermez.
        /// </summary>
        public const string StarterWeaponId = "weapon.pistol";

        /// <summary>
        /// Bu atis hizinin ustundeki silahlar OTOMATIK ates eder.
        ///
        /// <para>Denge degil, HIS esigi: dakikada 500 atis bir silahta tek tek
        /// tiklanamaz - oyuncunun parmagi silahin ritmini tasiyamaz ve silahin varlik
        /// sebebi (hizli ritim) kaybolur.</para>
        /// </summary>
        private const float AutoFireThresholdRpm = 500f;

        private System.Collections.Generic.List<WeaponDefinition> _catalog;

        /// <summary>
        /// <b>Cephanelik</b>: bu run'da satın alınmış BÜTÜN silahlar (2026-09-09).
        ///
        /// <para>Geliştirici: <i>"satın alınmış silahlara puan harcamadan
        /// değiştirebilme olayımız olsun."</i> Yani satın almak <b>kalıcı</b>; taşımak
        /// geçici. Tezgâhta daha önce aldığın bir silahı seçmek bedelsiz — çünkü onu
        /// zaten ödedin.</para>
        ///
        /// <para><b>Durum da burada kalıyor</b> (şarjör ve yedek mermi). Silah bırakılıp
        /// geri alındığında sıfırdan kurulsaydı, tezgâhta iki kez tıklamak <i>bedava
        /// dolum</i> olurdu — sömürülmesi en kolay tür. Cephanelikteki silah, bıraktığın
        /// mermiyle bekler.</para>
        ///
        /// <para>Her silahın KENDI mermisi ve kendi doğrulayıcısı var; ortak bir sayaç,
        /// pompalıyla tabancanın aynı mermiyi paylaşması demek olurdu.</para>
        /// </summary>
        private readonly System.Collections.Generic.List<WeaponDefinition> _arsenal =
            new System.Collections.Generic.List<WeaponDefinition>(6);
        private readonly System.Collections.Generic.List<WeaponState> _states =
            new System.Collections.Generic.List<WeaponState>(6);
        private readonly System.Collections.Generic.List<ServerFireGuard> _guards =
            new System.Collections.Generic.List<ServerFireGuard>(6);

        /// <summary>
        /// <b>Üzerinde taşınanlar</b>: cephanelikteki sıraları. En fazla
        /// <see cref="CarrySlots"/> tane.
        ///
        /// <para>Geliştirici: <i>"silahlar numaralara sığmadı, o yüzden 2 ateşli silah
        /// sınırımız olsun."</i> Şikâyet bir arayüz şikâyetiydi ama çözümü bir
        /// <b>tasarım</b> çözümü: taşıma sınırı, tezgâhta "hangisini bırakayım"
        /// sorusunu üretir ve altı silahın hepsini biriktirip hiç seçim yapmamayı
        /// imkânsız kılar. Tuş düzeni de kendiliğinden toparlanıyor — 1 bıçak,
        /// 2-3 silah, 4-8 eşya.</para>
        /// </summary>
        private readonly System.Collections.Generic.List<int> _carried =
            new System.Collections.Generic.List<int>(4);

        /// <summary>Aynı anda taşınabilen ateşli silah sayısı (weapon.json).</summary>
        private int CarrySlots => _carrySlots;

        private int _carrySlots = 2;

        private int _slot = -1;

        /// <summary>
        /// Elde bıçak mı var (slot 1). 2026-09-09.
        ///
        /// <para><b>Neden burada, ayrı bir bileşende değil:</b> "elimde ne var" tek bir
        /// sorudur ve tek bir yerde cevaplanmalı. İki bileşen kendi cevabını tutsaydı
        /// (silah "ben aktifim", bıçak "hayır ben") ikisinin ayrıştığı bir kare
        /// kaçınılmazdı — ve o karede oyuncu hem ateş eder hem bıçak sallardı.
        /// <c>PlayerMelee</c> buraya <b>bakar</b>, kendi bayrağını tutmaz
        /// (csharp-code.md: aynı iş kuralı iki yerde duramaz).</para>
        /// </summary>
        private bool _meleeActive;

        /// <summary>Elde bıçak mı var. <c>PlayerMelee</c> ve HUD bunu okur.</summary>
        public bool IsMeleeActive => _meleeActive;

        /// <summary>
        /// Ekranda görünen slot numarası: bıçak <b>1</b>, ateşli silahlar <b>2..5</b>.
        /// HUD çubuğu bunu yazar.
        /// </summary>
        public int ActiveSlotNumber => _meleeActive ? 1 : _slot + 2;

        private WeaponDefinition _config;

        /// <summary>Savas gunlugunde vuranin adi ("Oyuncu[TUFEK]"). Silah degisince kurulur.</summary>
        private string _logSource = "Oyuncu";
        private WeaponState _state;

        // Sunucunun hile denetimi. Istemcinin simulasyonunu TEKRARLAMAZ; makul olup
        // olmadigina bakar. Istemci otoritesi burada biter (BUG-002).
        private ServerFireGuard _guard;

        private float _tracerRemaining;

        /// <summary>
        /// Saçma izleri. <b>Bir kez yaratılır, yeniden kullanılır</b> — atış başına
        /// <c>Instantiate</c> bir hitch üreticisidir (csharp-code.md). İlk eleman
        /// silahın kendi iz çizeridir.
        /// </summary>
        private readonly System.Collections.Generic.List<LineRenderer> _pelletTracers =
            new System.Collections.Generic.List<LineRenderer>(8);
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

        /// <summary>
        /// Eldeki şey değişti: bıçak ↔ ateşli silah, ya da silahtan silaha
        /// (2026-09-09). El modeli (<c>PlayerViewmodel</c>) buna bağlanır.
        /// </summary>
        public event Action HandChanged;

        public int RoundsInMagazine => _state?.RoundsInMagazine ?? 0;
        public int Reserve => _state?.Reserve ?? 0;

        /// <summary>Eldeki silahın yedek tavanı, kartlarla. HUD yazar (2026-09-10).</summary>
        public int ReserveCapacity => _state?.ReserveCapacity ?? 0;
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

            // Tasima slotu sayisi config'ten (weapon.json loadout.firearmSlots).
            _carrySlots = Math.Max(1, starter.LoadoutFirearmSlots);

            // AddWeapon ele almayi da yapiyor - ayrica EquipSlot cagirmak gereksiz.
            AddWeapon(pistol);

            if (tracer != null) tracer.enabled = false;
        }

        /// <summary>
        /// Bir silahı envantere ekler ve durumunu kurar.
        ///
        /// <para><b>Her silahın kendi mermisi var</b>: durum listesi silahla birlikte
        /// taşınır. Tek bir ortak sayaç, pompalıyla tabancanın aynı mermiyi paylaşması
        /// demek olurdu ve silah seçimi bir karar olmaktan çıkardı.</para>
        /// </summary>
        /// <returns>
        /// Silah <b>ele alındıysa</b> <c>true</c>. Zaten elde tutuluyorsa
        /// <c>false</c> — çağıran taraf bunu "bir şey değişmedi" diye okur.
        /// </returns>
        public bool AddWeapon(in WeaponDefinition definition)
        {
            if (!definition.IsValid) return false;

            // 1) CEPHANELIK: bu run'da bir kez alinan silah bir daha unutulmaz.
            int arsenal = ArsenalIndexOf(definition.Id);

            if (arsenal < 0)
            {
                var newState = new WeaponState(definition);
                var newGuard = new ServerFireGuard(definition);

                // KART ETKILERI DOGUMDA UYGULANIR (2026-09-10). Onceki surum kartlari
                // yalnizca ELE ALINAN silaha uyguluyordu: cepteki silahin yedek tavani
                // kartsiz kaliyor ve tur sonu ikmali onu eski tavana kirpiyordu -
                // "yedek mermi duzgun calismiyor hissi"nin iki sebebinden biri.
                WeaponModifiers mods = BuildModifiers();
                newState.ApplyModifiers(mods);
                newGuard.ApplyModifiers(mods);

                _arsenal.Add(definition);
                _states.Add(newState);
                _guards.Add(newGuard);
                arsenal = _arsenal.Count - 1;
            }

            // 2) TASIMA: zaten uzerindeyse yalnizca ona gecilir.
            int carried = _carried.IndexOf(arsenal);

            if (carried >= 0)
            {
                if (carried == _slot && !_meleeActive) return false;

                EquipSlot(carried);
                return true;
            }

            // 3) YER VARSA EKLE, YOKSA ELDEKININ YERINE KOY (2026-09-09, gelistirici:
            //    "hangisi elimizdeyken tezgahtan silah alirsak onun yerine satin
            //    alinsin").
            //
            //    <b>Neden eldeki, en eski degil:</b> hangi silahin gidecegini oyuncu
            //    SECIYOR - tezgaha gitmeden once elini degistirerek. Otomatik bir
            //    kural (en eski, en ucuz, en az kullanilan) oyuncunun kontrolunu alir
            //    ve "yanlis silahimi sildi" diye okunur. Elindeki silah, oyuncunun
            //    zaten bildigi tek sey.
            if (_carried.Count < CarrySlots)
            {
                _carried.Add(arsenal);
                EquipSlot(_carried.Count - 1);
                return true;
            }

            int target = _slot >= 0 && _slot < _carried.Count ? _slot : 0;
            _carried[target] = arsenal;

            // Yerine koyma AYNI slota yaziyor, yani EquipSlot'un "zaten bu slottayim"
            // kisa devresi buraya takilir. Once bagi kopariyoruz.
            _slot = -1;
            EquipSlot(target);

            return true;
        }

        /// <summary>Bu id cephanelikte kaçıncı sırada; yoksa <c>-1</c>.</summary>
        private int ArsenalIndexOf(string weaponId)
        {
            for (int i = 0; i < _arsenal.Count; i++)
            {
                if (_arsenal[i].Id == weaponId) return i;
            }

            return -1;
        }

        /// <summary>Taşınan bir slota geçer (0 tabanlı; ekranda 2..N olarak görünür).</summary>
        public void EquipSlot(int slot)
        {
            if (slot < 0 || slot >= _carried.Count) return;
            if (slot == _slot && _state != null) return;

            int arsenal = _carried[slot];

            _slot = slot;
            _config = _arsenal[arsenal];
            _state = _states[arsenal];
            _guard = _guards[arsenal];

            // Savas gunlugunun etiketi silah DEGISINCE kurulur (2026-09-08): atis
            // basina string birlestirmek, otomatik atista saniyede on tahsis olurdu
            // (csharp-code.md).
            _logSource = "Oyuncu[" + _config.DisplayName + "]";

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

        /// <summary>
        /// Bu silah <b>satın alınmış mı</b> — yani bedelsiz geçilebilir mi.
        ///
        /// <para><b>Taşıyor olmakla aynı şey değil</b> (2026-09-09): cephanelikte olup
        /// üzerinde olmayan bir silah da "sahip olunmuş"tur ve tezgâhta puan
        /// istemez. Duvar satın alma noktası da bunu sorar — aynı silahı ikinci kez
        /// satmak, oyuncunun ödediği şeyi unutmak olurdu.</para>
        /// </summary>
        public bool Owns(string weaponId) => ArsenalIndexOf(weaponId) >= 0;

        /// <summary>Bu silah şu anda <b>üzerinde mi</b> (slot çubuğunda görünüyor mu).</summary>
        public bool IsCarrying(string weaponId)
        {
            int arsenal = ArsenalIndexOf(weaponId);
            return arsenal >= 0 && _carried.Contains(arsenal);
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

        /// <summary>
        /// Satılabilecek bütün silahlar. <b>Silah tezgâhı bunu listeler.</b>
        ///
        /// <para><b>Salt okunur</b>: katalog <c>config/content/weapons.json</c>'dan
        /// gelir ve çalışma anında değiştirilemez (config-data.md).</para>
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<WeaponDefinition> Catalog => _catalog;

        /// <summary>Eldeki silahın tanımı. HUD ve durum paneli okur.</summary>
        public WeaponDefinition Current => _config;

        /// <summary>Uzerinde tasinan silah sayisi. HUD slot cubugu bunu okur.</summary>
        public int OwnedCount => _carried.Count;

        /// <summary>
        /// Taşıma slotları dolu mu — yani <b>bir sonraki alım eldekinin yerine mi
        /// geçecek</b>. Tezgâh ekranı bunu yazar (2026-09-10): kuralı bilmeyen oyuncu,
        /// tabancasının neden hiç gitmediğini anlayamıyordu.
        /// </summary>
        public bool IsCarryFull => _carried.Count >= CarrySlots;

        /// <summary>
        /// Silahı envantere ekler ve isteğe bağlı olarak <b>ele alır</b>.
        /// <b>Yalnızca sunucu</b> (ADR-0004): silah kalıcı sonucu olan bir kazanım.
        /// </summary>
        [Server]
        public void ServerGrantWeapon(WeaponDefinition definition, bool equip)
        {
            // AddWeapon artik ele almayi da yapiyor (yer varsa ekler, yoksa eldekinin
            // yerine koyar). Ayrica EquipSlot cagirmak, yerine koyma durumunda YANLIS
            // slota gecmek olurdu - _owned.Count-1 artik "yeni eklenen" demek degil.
            if (!AddWeapon(definition)) return;

            TargetGrantWeapon(connectionToClient, definition.Id, equip);
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

            AddWeapon(definition);
        }

        public string OwnedName(int slot) =>
            slot >= 0 && slot < _carried.Count
                ? _arsenal[_carried[slot]].DisplayName
                : string.Empty;

        public int EquippedSlot => _slot;

        private void OnEnable()
        {
            RunSignals.RunRestarted += OnRunRestarted;
            CardSignals.LoadoutChanged += OnLoadoutChanged;
            RoundSignals.RoundEndRestock += OnRoundEndRestock;
            PowerupSignals.Picked += OnPowerupPicked;
        }

        private void OnDisable()
        {
            RunSignals.RunRestarted -= OnRunRestarted;
            CardSignals.LoadoutChanged -= OnLoadoutChanged;
            RoundSignals.RoundEndRestock -= OnRoundEndRestock;
            PowerupSignals.Picked -= OnPowerupPicked;
        }

        /// <summary>
        /// Yerden mermi eşyası toplandı (2026-09-07).
        ///
        /// <para><b>Ölçü şarjördür, mutlak sayı değil:</b> "3 şarjör" 6 mermilik
        /// pompalıda 18, 30 mermilik SMG'de 90 eder — eşyanın vaadi ikisinde de aynı:
        /// <i>üç dolum</i>. Mutlak bir sayı, elindeki silaha göre bambaşka bir ödül
        /// olurdu; şarjörü oransal yapan değişiklikle (2026-09-07) aynı gerekçe.</para>
        /// </summary>
        private void OnPowerupPicked(PowerupKind kind, float amount, float seconds)
        {
            if (kind != PowerupKind.Ammo) return;
            if (!isServer || _state == null) return;

            int rounds = Mathf.RoundToInt(_state.MagazineCapacity * amount);
            if (rounds <= 0) return;

            // YERDEN TOPLANAN MERMI BEDAVADIR (2026-09-10): tavana kadar doldurur,
            // tavanin ustune tasimaz - o yalnizca satin almanin hakki (ReserveAmmo, K4).
            ServerAddFreeReserve(rounds);
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
        ///
        /// <para><b>İkmal SAHIP OLUNAN HER SILAHA gider</b> (2026-09-09, geliştirici:
        /// <i>"her tur sonunda gelen mermiler kişinin elindeki silaha geliyor, böyle
        /// olmamalı, bütün silahlarına gelmeli"</i>). Doğru tespit ve önceki hâl bir
        /// hataydı: ikmali yalnızca elde tutulan silaha vermek, oyuncuyu <b>tur
        /// bitmeden önce doğru silahı eline almaya</b> zorluyordu — yani ödül,
        /// oynanışla ilgisi olmayan bir muhasebe hilesine bağlıydı ve o hileyi bilmeyen
        /// oyuncu, ikinci silahının hiç dolmadığını sebebini anlamadan yaşıyordu.
        /// Silah çeşitliliği tam da bu yüzden cezalandırılıyordu.</para>
        ///
        /// <para><b>Her silah KENDI referansına göre alır</b>, hepsi aynı sayıyı değil:
        /// pompalının 90'ı ile SMG'nin 480'i aynı şeyi söylüyor — "bir tur sonunun
        /// bu silahta ettiği mermi". Sabit bir sayı, pompalıyı tur başına on beş dolum
        /// zengini yapardı.</para>
        /// </summary>
        private void OnRoundEndRestock(float reserveAmmoFraction01, float boardsFraction01)
        {
            if (!isServer) return;
            if (reserveAmmoFraction01 <= 0f) return;

            for (int i = 0; i < _guards.Count; i++)
            {
                if (_guards[i] == null) continue;

                // KURAL TEK YERDE (2026-09-10): ReserveAmmo.RoundEndRestock - tavanin
                // yarisi, tavani asmadan. Olcu birimi silahin KENDI tavani (weapons.json
                // reserveCapacity + kartlar); kart katkisi artik cepteki silaha da
                // uygulaniyor (OnLoadoutChanged), yani tavan her silahta dogru.
                //
                // <b>Hesap SUNUCUNUN sayacindan</b> (_guards), istemcininkinden (_states)
                // degil: otorite sunucuda ve iki taraf ayni sayiyi eklemek zorunda.
                // Istemcinin sayaci bir kare geride oldugunda kendi kirpmasini yapsaydi,
                // ikisi ayrisir ve BUG-001'in sinifi geri gelirdi - istemci mermiyi
                // gorur, sunucu atisi reddeder.
                int amount = ReserveAmmo.RoundEndRestock(_guards[i].Reserve,
                                                         _guards[i].ReserveCapacity,
                                                         reserveAmmoFraction01);

                ServerAddReserve(i, amount);
            }
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

            // SAHIP OLUNAN HER SILAH (2026-09-10, gelistirici: "yedek mermi kapasitesi
            // kartlarla arttirilir"). Onceki surum yalnizca ELDEKI silahi guncelliyordu:
            // cepteki silah kartsiz tavanla kaliyor, tur sonu ikmali onu eski tavana
            // kirpiyordu ve EquipSlot'a kadar kimse fark etmiyordu. Liste en fazla alti
            // silah; kart secimi turda bir kez - maliyeti yok.
            for (int i = 0; i < _states.Count; i++)
            {
                _states[i]?.ApplyModifiers(mods);
                _guards[i]?.ApplyModifiers(mods);
            }
        }

        /// <summary>Kart VE tezgah etkileri tek noktadan okunur (RunModifiers).</summary>
        private static WeaponModifiers BuildModifiers() =>
            new WeaponModifiers(
                fireRate: RunModifiers.Total(CardStat.FireRate),
                reloadSpeed: RunModifiers.Total(CardStat.ReloadSpeed),
                damage: RunModifiers.Total(CardStat.WeaponDamage),
                magazine: RunModifiers.Total(CardStat.MagazineCapacity),
                // Yedek tavani MUTLAK mermi (kartlar toplanir): 120 + 200 = +320.
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
            // CEPHANELIK DE SIFIRLANIR (2026-09-09). Satin alinan silahlar run boyunca
            // hatirlaniyor; hatirlamaya devam ederlerse ikinci run, birincinin M107'si
            // elde baslardi - CardLoadout.Reset ile ayni sinif hata.
            //
            // Baslangic silahi yeniden kuruluyor: listeleri bosaltip birakmak, silahi
            // olmayan bir oyuncu birakirdi.
            WeaponDefinition pistol = _arsenal.Count > 0 ? _arsenal[0] : _config;

            _arsenal.Clear();
            _states.Clear();
            _guards.Clear();
            _carried.Clear();

            _slot = -1;
            _state = null;
            _guard = null;
            _meleeActive = false;

            AddWeapon(pistol);
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
            if (isLocalPlayer && wasReloading && !_state.IsReloading) PlayWeaponSound(WeaponSoundKind.ReloadIn);

            // Yalnizca yerel oyuncu kendi silahini surer.
            if (!isLocalPlayer) return;

            // Run bitti: girdi kesilir (M1-11, AC-3). Skor ekraninin arkasindan ates
            // etmek, olumu bir sonuc olmaktan cikarir. Silahin kendi zamani (dolum,
            // geri bildirim) yukarida akmaya devam eder - durdurulan sey KOMUT.
            // Run bitti YA DA tur arasi ekrani acik: girdi kesilir. Ekran acikken
            // ates etmek, bakis cevirmek ya da satin almak, fareyle kart secmeyi
            // imkansiz kilardi.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen)
            {
                // TEZGAH ACIKKEN EL DEGISTIRME SERBEST (2026-09-10, oyun testi:
                // "tabanca hic gitmiyor, tufek + M4 tasiyamiyorum").
                //
                // Tasima slotlari doluyken satin alinan silah ELDEKININ yerine gecer
                // - bu, hangisini birakacagina oyuncunun karar vermesi icin secilmis
                // bir kuraldi. Ama tezgah acikken butun girdi kesiliyordu, yani o
                // karari verecek tus calismyordu: oyuncu tezgahin onunde elindekini
                // degistiremiyor, dolayisiyla tabancayi asla birakamiyordu. Kural
                // dogruydu, kurali kullanmanin yolu yoktu.
                //
                // ATES VE DOLUM HALA KESIK: acik olan sey yalnizca el degistirme.
                // Menunun arkasindan ates etmek, fareyle secim yapmayi imkansiz
                // kilardi - o gerekce yerinde duruyor.
                if (MenuSignals.IsWeaponShopOpen && !RunSignals.IsRunOver)
                {
                    ReadWeaponSwitch(Keyboard.current, null);
                }

                return;
            }

            ReadInput();
        }

        /// <summary>
        /// El değiştirme: <b>1 bıçak, 2..5 ateşli silahlar</b>, ya da fare tekerleği.
        ///
        /// <para><b>Bıçak 2026-09-09'da bir SLOT oldu</b> (geliştirici: <i>"melee
        /// atağı V'ye basarak yapıyorduk, bunu değiştiriyoruz ve silah gibi slota
        /// yerleştiriyoruz"</i>). Önceki hâlde bıçak ayrı bir tuştaydı ve bu, onu
        /// bir <i>silah</i> değil bir <i>kısayol</i> yapıyordu: elindeki silahı
        /// bırakmadan bıçak sallayabilmek, bıçağın bedelini (ateş edememek) sıfıra
        /// indiriyordu. Artık bıçağa geçmek bir <b>karar</b> — mermi biriktirmenin
        /// yolu, ama o sırada uzaktaki zombiye cevabın yok.</para>
        ///
        /// <para><b>Tekerlek bıçağı da dolaşır</b>, çünkü döngü artık "elimdekiler"
        /// listesi ve bıçak onun ilk üyesi. Bıçağı döngüden çıkarmak, tekerleği
        /// kullanan oyuncunun bıçağa hiç ulaşamaması demekti.</para>
        ///
        /// <para><b>Dolum sırasında değiştirmek serbest</b> ve dolumu iptal eder — bu
        /// bir hile değil, bir bedel: yarım kalan dolum baştan başlar.</para>
        /// </summary>
        private void ReadWeaponSwitch(Keyboard keyboard, Mouse mouse)
        {
            if (keyboard != null)
            {
                // 1 = bicak. Her zaman var: baslangic bicagi hic kaybedilmez, yani
                // bu tus HER run'in her aninda bir sey yapar.
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    SwitchToMelee();
                }
                else
                {
                    // 2..(1 + tasima slotu). SINIR CONFIG'DEN, elle yazilmiyor
                    // (2026-09-09): esya slotlari hemen ardindan basliyor
                    // (PowerupInventory.FirstSlotNumber) ve iki taraf ayni tusu
                    // okursa bir tusa basmak hem silah degistirir hem esya harcar.
                    // Sinir tek bir sayidan turedigi surece bu cakisma imkansiz.
                    for (int i = 0; i < CarrySlots; i++)
                    {
                        if (!DigitPressed(keyboard, i + 2)) continue;

                        SwitchTo(i);
                        break;
                    }
                }
            }

            if (mouse == null) return;

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f) return;

            // Dongu: bicak (-1) + sahip olunan silahlar (0..n-1), toplam n+1 durak.
            int stops = _carried.Count + 1;
            if (stops <= 1) return;

            int current = _meleeActive ? 0 : _slot + 1;
            int next = (current + (wheel > 0f ? 1 : -1) + stops) % stops;

            if (next == 0) SwitchToMelee();
            else SwitchTo(next - 1);
        }

        /// <summary>
        /// Bir rakam tuşuna bu karede basıldı mı (2..9).
        ///
        /// <para>Yeni girdi sisteminde rakam tuşları ayrı ayrı alanlar; dizi yok. Bir
        /// <c>switch</c>, döngüyü config'ten gelen bir sayıya bağlayabilmenin tek
        /// yolu.</para>
        /// </summary>
        private static bool DigitPressed(Keyboard keyboard, int number) => number switch
        {
            2 => keyboard.digit2Key.wasPressedThisFrame,
            3 => keyboard.digit3Key.wasPressedThisFrame,
            4 => keyboard.digit4Key.wasPressedThisFrame,
            5 => keyboard.digit5Key.wasPressedThisFrame,
            _ => false
        };

        /// <summary>
        /// Bıçağa geçer. <b>Ateşli silah elden bırakılır</b> ve yarım dolum iptal olur.
        ///
        /// <para>Silahın kendi durumu (şarjör, yedek) korunur — geri döndüğünde
        /// bıraktığı yerden devam eder. Bıçağa geçmenin bedeli mermi kaybı değil,
        /// <i>menzil</i> kaybı olmalı.</para>
        /// </summary>
        private void SwitchToMelee()
        {
            if (_meleeActive) return;

            _state?.CancelReload();
            CloseScope();
            _meleeActive = true;

            HandChanged?.Invoke();
        }

        /// <summary>
        /// Dürbün: <b>sağ tık basılı tutulduğu sürece</b> yakınlaştırır (2026-09-09,
        /// geliştirici: <i>"bu pakette attachment olan scope'u da buna ekle"</i>).
        ///
        /// <para><b>Basılı tut, aç-kapa değil:</b> dürbün bir <i>taahhüt</i> — içinden
        /// bakarken çevreni göremezsin ve bir zombi yanına gelirse bunu fark etmen
        /// gerekir. Aç-kapa bir dürbün, oyuncuyu yanlışlıkla dürbünde bırakır ve ölüm
        /// bir okuma hatası değil bir tuş hatası olur (PILLAR-04).</para>
        ///
        /// <para><b>Yalnızca dürbünlü silahta çalışır</b> (<c>weapons.json →
        /// scopeMagnification</c>). Dürbünsüz silahta sağ tık hiçbir şey yapmaz —
        /// sessizce, çünkü her silaha bir nişan alma vermek "hangi silah uzun
        /// menzillidir" sorusunu ortadan kaldırırdı.</para>
        ///
        /// <para><b>Silah değişince ve bıçağa geçince kapanır:</b> açık kalan bir
        /// dürbün, elinde pompalıyla dört kat yakından bakmak demek olurdu.</para>
        /// </summary>
        private void ReadScope(Mouse mouse)
        {
            if (controller == null) return;

            bool scoped = _config.HasScope && mouse.rightButton.isPressed;

            IsScoped = scoped;
            controller.SetScopeMagnification(scoped ? _config.ScopeMagnification : 1f);
        }

        /// <summary>
        /// Şu an dürbünden mi bakılıyor. <b>HUD bunu okur</b> ve dürbün görüntüsünü
        /// çizer (<see cref="ScopeStyle"/>).
        ///
        /// <para><b>Yerel bir durum, senkronize edilmiyor:</b> dürbün görüntüsü yalnızca
        /// bakan oyuncunun ekranında var. Ağa göndermek, hiçbir yerde kullanılmayan bir
        /// alan için bant genişliği harcamak olurdu (netcode.md: yerelde türetilebilen
        /// şey replike edilmez).</para>
        /// </summary>
        public bool IsScoped { get; private set; }

        /// <summary>Eldeki silahın dürbün türü. Dürbün yoksa <c>None</c>.</summary>
        public ScopeStyle ScopeStyle => _config.ScopeStyle;

        /// <summary>Dürbünü kapatır. El değişiminin her yolu buradan geçer.</summary>
        private void CloseScope()
        {
            IsScoped = false;
            if (controller != null) controller.SetScopeMagnification(1f);
        }

        /// <summary>
        /// Silahın sesi: <b>önce katalogdan, yoksa sentezlenmiş</b> (2026-09-09).
        ///
        /// <para>Geliştirici Free Weapon Sound Effects paketini ekledi. Katalog o
        /// paketten gelen klibi taşıyor; bulunamazsa <see cref="SfxBank"/>'ın
        /// sentezlediği ses çalıyor — yani bir silahın ses ailesinin eksik olması onu
        /// <b>sessiz bırakmıyor</b>. Sessiz bir silah, oyun testinde teşhis edilmesi en
        /// pahalı hata türüdür (audio-code.md: her oyuncu eylemi bir ses üretmeli).</para>
        ///
        /// <para><b>Ses eldeki silahın id'sinden seçilir</b>, bir bileşen alanından
        /// değil: silah değiştiğinde sesin de değişmesi kendiliğinden olur ve
        /// unutulacak bir bağlantı kalmaz.</para>
        /// </summary>
        private void PlayWeaponSound(WeaponSoundKind kind)
        {
            AudioCatalogAsset.WeaponSounds family = GameAudio.Catalog?.FindWeapon(_config.Id);

            AudioClip clip = null;

            if (family != null)
            {
                switch (kind)
                {
                    case WeaponSoundKind.Fire:
                        if (family.fire != null && family.fire.Length > 0)
                        {
                            // Ayni varyant art arda calmaz: otomatik ates ederken iki
                            // ayni klip, sesi tek varyantli gibi okutur.
                            _lastFireVariant = family.fire.Length == 1
                                ? 0
                                : (_lastFireVariant + 1 +
                                   UnityEngine.Random.Range(0, family.fire.Length - 1)) %
                                  family.fire.Length;

                            clip = family.fire[_lastFireVariant];
                        }
                        break;

                    case WeaponSoundKind.ReloadOut: clip = family.reloadOut; break;
                    case WeaponSoundKind.ReloadIn: clip = family.reloadIn; break;
                    case WeaponSoundKind.DryFire: clip = family.dryFire; break;
                }
            }

            if (clip != null)
            {
                // Ates sesi en yuksek oncelikli: kalabalikta kesilmemesi gereken tek
                // sey, oyuncunun kendi tetiginin karsiligi.
                GameAudio.PlayClip(clip, Vector3.zero, spatial: false,
                                   volumeScale: kind == WeaponSoundKind.Fire ? 0.85f : 0.6f,
                                   pitchJitter: kind == WeaponSoundKind.Fire ? 0.05f : 0.03f,
                                   priority: kind == WeaponSoundKind.Fire ? 9 : 5,
                                   basePitch: family.basePitch);
                return;
            }

            // Yedek yol: sentezlenmis ses (ADR-0006'nin birakti gi hat).
            GameAudio.Play(kind switch
            {
                WeaponSoundKind.Fire => SfxId.GunShot,
                WeaponSoundKind.ReloadOut => SfxId.ReloadOut,
                WeaponSoundKind.ReloadIn => SfxId.ReloadIn,
                _ => SfxId.GunDryFire
            });
        }

        private enum WeaponSoundKind { Fire, ReloadOut, ReloadIn, DryFire }

        private int _lastFireVariant = -1;

        private void SwitchTo(int slot)
        {
            if (slot < 0 || slot >= _carried.Count) return;

            // Bicaktan ates li silaha donus: slot ayni olsa bile bir DEGISIM.
            if (_meleeActive)
            {
                _meleeActive = false;
                HandChanged?.Invoke();

                if (slot == _slot) return;
            }
            else if (slot == _slot)
            {
                return;
            }

            // Yarim kalan dolum iptal olur: silah degistirmek onu tamamlamis saymak,
            // dolumu bedava bir iptal tusuna cevirirdi.
            _state?.CancelReload();
            CloseScope();

            EquipSlot(slot);
        }

        private void ReadInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            // El degistirme HER ZAMAN okunur - bicak elindeyken de. Aksi hâlde bicaga
            // gecen oyuncu bir daha silaha donemezdi.
            ReadWeaponSwitch(keyboard, mouse);

            // BICAK ELDE: ates ve dolum yok. Sol tik PlayerMelee'ye ait ve o, buradaki
            // IsMeleeActive'e bakiyor - yani iki bilesen ayni karede ikisini birden
            // yapamaz.
            if (_meleeActive) return;

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                StartReload();
            }

            if (mouse == null) return;

            ReadScope(mouse);

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
                    PlayWeaponSound(WeaponSoundKind.DryFire);
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

            PlayWeaponSound(WeaponSoundKind.ReloadOut);
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
            // _slot bir TASIMA slotu, _states ise CEPHANELIK sirasiyla indeksleniyor.
            // Ikisini karistirmak, mermiyi baska bir silaha yazmak demek - hem de
            // sessizce (2026-09-09 yeniden yapilandirmasi).
            if (_slot < 0 || _slot >= _carried.Count) return;

            ServerAddReserve(_carried[_slot], amount);
        }

        /// <summary>
        /// <b>Belirli bir silaha</b> mermi ekler.
        ///
        /// <para><b>Neden gerekliydi</b> (2026-09-06): tezgâhta pompalının mermisini
        /// alırken elinde tabanca varsa, mermi <b>tabancaya</b> yazılıyordu — parayı
        /// ödüyor, yedeğin artmıyordu. Duvardaki musluk için "eldeki silah" doğru
        /// varsayımdı; katalogdan seçerek alınan mermi için değil.</para>
        /// </summary>
        [Server]
        public void ServerAddReserveTo(string weaponId, int amount)
        {
            for (int i = 0; i < _arsenal.Count; i++)
            {
                if (_arsenal[i].Id != weaponId) continue;

                // TAVAN KONTROLU KALKTI (2026-09-07): yedek merminin tavani yok artik.
                // Odenen her mermi yedege girer; "para gitti mermi gelmedi" durumu
                // kaynagindan kalktigi icin uyariya da gerek kalmadi.
                ServerAddReserve(i, amount);
                return;
            }

            Debug.LogWarning($"[Silah] '{weaponId}' envanterde yok - mermi yazilamadi.", this);
        }

        /// <summary>
        /// <b>Bedava</b> mermi ekler: yerden toplama, öldürme ödülü (2026-09-10). Eldeki
        /// silaha, <b>yalnızca tavana kadar</b> (<see cref="ReserveAmmo"/>, kural 4).
        ///
        /// <para><b>Satın alma buradan geçmez</b> — <see cref="ServerAddReserve(int)"/> ve
        /// <see cref="ServerAddReserveTo"/> kırpmaz, çünkü ödenmiş merminin buharlaşması
        /// 2026-09-07'de tavanın bütünüyle kaldırılma sebebiydi.</para>
        ///
        /// <para>Tavan <b>sunucunun</b> sayacından hesaplanır ve istemciye aynı sayı gider:
        /// iki tarafın ayrı ayrı kırpması BUG-001'in sınıfıdır.</para>
        /// </summary>
        [Server]
        public void ServerAddFreeReserve(int amount)
        {
            if (_slot < 0 || _slot >= _carried.Count) return;

            int arsenal = _carried[_slot];
            if (arsenal < 0 || arsenal >= _guards.Count) return;

            ServerAddReserve(arsenal, ReserveAmmo.UpToCapacity(_guards[arsenal].Reserve,
                                                               _guards[arsenal].ReserveCapacity,
                                                               amount));
        }

        /// <summary>
        /// Oyuncu öldü: <b>bütün silahların yedek mermisi yarıya iner</b> (2026-09-09,
        /// geliştirici: <i>"mevcut mermi kapasitesi kaça kadar birikmişse
        /// yarılanacak"</i>).
        ///
        /// <para><b>Cephanelikteki silahlar da dahil</b>: yalnızca eldekini cezalandırmak,
        /// ölmeden önce boş bir silaha geçmeyi bir hile hâline getirirdi — tur sonu
        /// ikmalinin bütün silahlara yayılmasıyla aynı gerekçe.</para>
        ///
        /// <para><b>Şarjördeki mermiye dokunulmaz</b>, yalnızca yedeğe: dirilen oyuncu
        /// dolu bir şarjörle kalkmalı. Boş silahla dirilmek, bir sonraki ölümü
        /// garantiler ve ceza kendini besler.</para>
        /// </summary>
        [Server]
        public void ServerHalveReserves()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                WeaponState state = _states[i];
                if (state == null) continue;

                int loss = state.Reserve / 2;
                if (loss <= 0) continue;

                // Otorite tarafi ve gorunen sayac AYRI AYRI: ikisi ayni anda
                // dusurulmezse istemci silahi dolu sanip ates etmeye calisir ve
                // sunucu reddeder - BUG-001'in tam olarak bu sinifi.
                _guards[i].RemoveReserve(loss);
                TargetRemoveReserve(connectionToClient, i, loss);
            }
        }

        [Server]
        private void ServerAddReserve(int arsenalIndex, int amount)
        {
            if (amount <= 0) return;
            if (arsenalIndex < 0 || arsenalIndex >= _guards.Count) return;

            _guards[arsenalIndex].AddReserve(amount);
            TargetAddReserve(connectionToClient, arsenalIndex, amount);
        }

        [TargetRpc]
        private void TargetRemoveReserve(NetworkConnection target, int arsenalIndex, int amount)
        {
            if (arsenalIndex < 0 || arsenalIndex >= _states.Count) return;

            _states[arsenalIndex].RemoveReserve(amount);
        }

        [TargetRpc]
        private void TargetAddReserve(NetworkConnection target, int arsenalIndex, int amount)
        {
            if (arsenalIndex < 0 || arsenalIndex >= _states.Count) return;

            _states[arsenalIndex].AddReserve(amount);
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

            // POMPALI: DAGILIMI ISTEMCI UYGULAMAZ (2026-09-06, oyun testi).
            //
            // <b>Bulgu:</b> <i>"pompali duzgun ates etmiyor, yakin mesafeden tek
            // partikul isabet etti".</i> Dagilim IKI KEZ uygulaniyordu: istemci
            // nisangahtan 7 dereceye kadar sapmis bir yon gonderiyor, sunucu da o
            // sapmis yonun etrafina bir 7 derecelik koni daha aciyordu. Sonuc: sekiz
            // sacmanin MERKEZI nisangahtan bagimsiz bir yere kayiyordu ve yakin
            // mesafede bile cogu iskaliyordu.
            //
            // Sacmali silahta koniyi yalnizca sunucu uretir (guven siniri, netcode.md);
            // istemcinin isi ham nisan yonunu gondermek.
            bool pellets = _config.PelletCount > 1;

            Vector3 aim = playerCamera.transform.forward;
            Vector3 direction = pellets ? aim : ApplySpread(aim);

            // Comelme dagilimi daraltir (2026-09-06). Istemci kendi izini bu carpanla
            // ciziyor; hasari uygulayan sunucu ayni carpani KENDI okuyor (CmdFire),
            // cunku istemcinin bildirdigi bir "ben comelmistim" iddiasi guvenilmez
            // veridir (netcode.md).

            // Geri tepme atisin ayni karesinde. Bir kare sonrasi bile "gecikmis"
            // hissettirir.
            if (controller != null)
            {
                float yaw = UnityEngine.Random.Range(-1f, 1f) * _config.RecoilYawPerShot;
                controller.AddRecoil(_config.RecoilPitchPerShot, yaw);
            }

            // Yerel isin YALNIZCA gorsel icindir - hasari host uygular. Iki taraf
            // farkli sonuc bulursa gecerli olan host'unkidir.
            if (pellets) ShowPelletFan(origin.position, direction);
            else ShowTracer(origin.position, TraceEnd(origin.position, direction));

            // El modeli ve ses ayni karede: ates ettigini gosteren sey namlu alevi ve
            // patlama sesidir, sunucunun bir kare sonra donen onayi degil.
            PlayWeaponSound(WeaponSoundKind.Fire);
            Fired?.Invoke();

            CmdFire(origin.position, direction);
        }

        /// <summary>
        /// Şu anki dağılma açısı: silahın açısı, <b>çömelme çarpanıyla</b>.
        ///
        /// <para><b>Hem istemci hem sunucu burayı okur</b> — iki yerde hesaplanan bir
        /// koni, zamanla ayrışan iki koni demektir.</para>
        /// </summary>
        /// <summary>
        /// Uygulanacak hasar: silahın hasarı, <b>çömelme çarpanıyla</b>.
        ///
        /// <para><b>Çarpanı sunucu KENDİ okur</b>, istemciden almaz (netcode.md: her
        /// RPC bir güven sınırı). İstemcinin "ben çömelmiştim" iddiası doğrulanamaz bir
        /// veridir; çömelme durumu zaten sunucuda da yaşıyor çünkü karakter
        /// <c>CharacterController</c>'ıyla birlikte host'ta da simüle ediliyor.</para>
        /// </summary>
        private float ServerDamage(bool headshot) =>
            _guard.DamageFor(headshot) *
            (controller != null ? controller.CrouchDamageMultiplier : 1f);

        private float CurrentSpreadDegrees =>
            _config.SpreadDegrees * (controller != null ? controller.CrouchSpreadMultiplier : 1f);

        private Vector3 ApplySpread(Vector3 forward)
        {
            if (CurrentSpreadDegrees <= 0f) return forward;

            // Koni icinde rastgele sapma. Determinizm gerekmiyor: atis tekrar
            // oynatilmiyor ve kaydedilmiyor (csharp-code.md'nin seed kurali
            // tekrarlanabilir seyler icindir).
            float radius = Mathf.Tan(CurrentSpreadDegrees * Mathf.Deg2Rad);
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

            // OYUNCU GOVDESI MERMIYI DURDURMAZ VE HASAR ALMAZ (2026-09-10). Build
            // gunluklerinde 'Oyuncu[M4] -> Oyuncu 112.93' satirlari vardi: isin bir oyuncu
            // kapsulune carpiyor ve PlayerHealth bir IDamageable oldugu icin hasar
            // yaziliyordu. Oyunda PvP yok; gecisin kurali RaycastPastPlayers'ta.
            if (!RaycastPastPlayers(origin, direction, out RaycastHit serverHit))
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
                new DamageInfo(ServerDamage(headshot), DamageKind.Bullet, headshot,
                                   origin.x, origin.z, _logSource));

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
                                                _config.RangeMeters, Bunker.Config.GameLayers.WorldMask,
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

                // Oyuncu govdesi: mermi GECER, hasar yazmaz ve penetrasyon hakki
                // harcanmaz (2026-09-10; gerekce RaycastPastPlayers'ta).
                if (IsPlayerCollider(hit.collider))
                {
                    NotePassedThroughPlayer();
                    continue;
                }

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
                    new DamageInfo(ServerDamage(headshot), DamageKind.Bullet, headshot,
                                   origin.x, origin.z, _logSource));

                TargetReportHit(sender, headshot, result.Killed, hit.point, result.Absorbed);

                if (result.Killed) KillConfirmed?.Invoke(DamageKind.Bullet, headshot);

                remaining--;
            }
        }

        /// <summary>
        /// En yakın isabeti bulur, <b>oyuncu gövdelerini yok sayarak</b>. 2026-09-10.
        ///
        /// <para><b>Neden</b> (geliştirici: <i>"zombiler bir şekilde uzak mesafeden hasar
        /// verebiliyor"</i>): build günlüklerinde zombi vuruşu (30 hasar) yerine
        /// <c>Oyuncu[M4] -> Oyuncu 112.93</c> satırları vardı — dört oturumda yedi kez,
        /// geç turlarda tek vuruşta ölüm. Işın bir oyuncu kapsülüne çarpıyordu ve
        /// <c>PlayerHealth</c> bir <see cref="IDamageable"/> olduğu için hasar yazılıyordu.
        /// Yakında zombi yokken gelen bu hasar "uzaktan vuruldum" diye okundu.</para>
        ///
        /// <para><b>Neden fizik katmanı değil:</b> oyuncuları ayrı bir katmana almak prefab
        /// ve proje ayarı değişikliği ister, ve katmanı bilmeyen yeni bir ışın sorgusu
        /// hatayı sessizce geri getirirdi. Kural hasarı yazan yerde duruyor.</para>
        ///
        /// <para><b>Maliyet:</b> önce tek <c>Physics.Raycast</c> — atışların neredeyse
        /// hepsi orada biter. Yalnızca ilk isabet bir oyuncuysa sıralı tampona düşülür;
        /// tahsis yok (csharp-code.md).</para>
        /// </summary>
        private bool RaycastPastPlayers(Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            int mask = Bunker.Config.GameLayers.WorldMask;

            if (!Physics.Raycast(origin, direction, out hit, _config.RangeMeters, mask,
                                 QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (!IsPlayerCollider(hit.collider)) return true;

            NotePassedThroughPlayer();

            int count = Physics.RaycastNonAlloc(origin, direction, PenetrationHits,
                                                _config.RangeMeters, mask,
                                                QueryTriggerInteraction.Ignore);

            // RaycastNonAlloc SIRASIZ doner; en yakin oyuncu-olmayan isabet aranir.
            System.Array.Sort(PenetrationHits, 0, count, RaycastDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider collider = PenetrationHits[i].collider;
                if (collider == null || IsPlayerCollider(collider)) continue;

                hit = PenetrationHits[i];
                return true;
            }

            return false;
        }

        /// <summary>Bu çarpıştırıcı bir oyuncuya mı ait — kendisi ya da takım arkadaşı.</summary>
        private static bool IsPlayerCollider(Collider collider) =>
            collider.GetComponentInParent<PlayerHealth>() != null;

        private static float _lastPlayerPassNoteTime = -99f;

        /// <summary>
        /// Işın bir oyuncu gövdesinden geçti. <b>Sessiz kalmaz</b> ama hız sınırlı: beş
        /// saniyede bir satır. Bir sonraki oyun testinde bu satırın varlığı, "kendi
        /// mermimle vuruluyordum" teşhisini tahminden ölçüme çevirir.
        /// </summary>
        private void NotePassedThroughPlayer()
        {
            if (Time.unscaledTime - _lastPlayerPassNoteTime < 5f) return;
            _lastPlayerPassNoteTime = Time.unscaledTime;

            Debug.Log("[Silah] Isin bir oyuncu govdesinden gecti - eskiden burada oyuncuya " +
                      "hasar yaziliyordu (dost atesi yok).", this);
            Bunker.Systems.Telemetry.CombatLog.Event(
                "DOST", "mermi bir oyuncu govdesinden gecti, hasar yazilmadi");
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
            float spreadRadius = Mathf.Tan(CurrentSpreadDegrees * Mathf.Deg2Rad);

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(direction, right);

            // TEK ATIS, TEK BILDIRIM (2026-09-06).
            //
            // Onceki surum her sacma icin ayri bir TargetRpc yolluyordu: sekiz
            // TargetRpc, sekiz ust uste calan isabet sesi ve neredeyse ayni noktada
            // sekiz hasar sayisi. Oyuncunun okudugu sey "168 hasar verdim" degil,
            // bulanik bir yigindi (audio-code.md: ayni olayin sekiz kopyasi gurultudur).
            //
            // Toplam gonderilir: pompalinin YAKIN mesafedeki gucunu ekranda gosteren
            // sey tek bir buyuk sayidir.
            float totalDamage = 0f;
            int hits = 0;
            bool anyHeadshot = false;
            bool anyKill = false;
            Vector3 firstHitPoint = Vector3.zero;

            for (int i = 0; i < pellets; i++)
            {
                // Dagilim RASTGELE: pompali her atista ayni deseni verseydi, oyuncu
                // deseni ezberler ve "sacma" olmaktan cikardi.
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spreadRadius;
                Vector3 pelletDirection =
                    (direction + right * offset.x + up * offset.y).normalized;

                // Sacma da oyuncu govdesinden gecer (2026-09-10).
                if (!RaycastPastPlayers(origin, pelletDirection, out RaycastHit hit))
                {
                    continue;
                }

                var target = hit.collider.GetComponent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                bool headshot = target.CountsAsHeadshot;

                DamageResult result = target.ApplyDamage(
                    new DamageInfo(ServerDamage(headshot), DamageKind.Bullet, headshot,
                                   origin.x, origin.z, _logSource));

                if (hits == 0) firstHitPoint = hit.point;

                hits++;
                totalDamage += result.Absorbed;
                anyHeadshot |= headshot;
                anyKill |= result.Killed;

                if (result.Killed) KillConfirmed?.Invoke(DamageKind.Bullet, headshot);
            }

            if (hits == 0) return;

            TargetReportHit(sender, anyHeadshot, anyKill, firstHitPoint, totalDamage);
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

        /// <summary>Işının nerede bittiği. Yalnızca iz çizmek için — hasar sunucuda.</summary>
        private Vector3 TraceEnd(Vector3 from, Vector3 direction)
        {
            return Physics.Raycast(from, direction, out RaycastHit hit, _config.RangeMeters,
                                   Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : from + direction * _config.RangeMeters;
        }

        /// <summary>
        /// Pompalının saçma yelpazesi: <b>her saçma için bir iz</b>.
        ///
        /// <para><b>Neden gerekliydi</b> (2026-09-06, oyun testi): sekiz saçma
        /// yalnızca sunucuda vardı, istemci tek bir iz çiziyordu. Oyuncunun gördüğü
        /// şey <i>"tek mermi gibi gidiyor"</i>du — silahın karakteri ekranda hiç
        /// görünmüyordu. Bir mekanik, oyuncunun göremediği yerde yaşayamaz.</para>
        ///
        /// <para><b>Desen sunucununkiyle aynı değil ve olmak zorunda da değil:</b> iz
        /// birkaç kare yaşayan bir çizgi. Aynı deseni paylaşmak, ya istemciye dağılımı
        /// seçtirmeyi (hile) ya da izi ağ gidiş-dönüşü kadar geciktirmeyi gerektirirdi;
        /// ikisi de bir görsel efekt için fazla bedel. Konisi aynı, saçmaların yeri
        /// farklı — oyuncunun okuduğu şey zaten koninin genişliği.</para>
        ///
        /// <para>Çizgiler <b>bir kez</b> yaratılır ve yeniden kullanılır: atış başına
        /// <c>Instantiate</c> bir hitch üreticisidir (csharp-code.md).</para>
        /// </summary>
        private void ShowPelletFan(Vector3 from, Vector3 aim)
        {
            if (tracer == null) return;

            EnsurePelletTracers();

            float radius = Mathf.Tan(CurrentSpreadDegrees * Mathf.Deg2Rad);

            Vector3 right = Vector3.Cross(Vector3.up, aim).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(aim, right);

            for (int i = 0; i < _config.PelletCount; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 direction = (aim + right * offset.x + up * offset.y).normalized;

                LineRenderer line = _pelletTracers[i];
                line.SetPosition(0, from);
                line.SetPosition(1, TraceEnd(from, direction));
                line.enabled = true;
            }

            _tracerRemaining = _config.TracerSeconds;
        }

        /// <summary>
        /// Saçma izlerini hazırlar. Silah değişince sayı da değişir — pompalıdan
        /// tabancaya geçen oyuncu, sekiz çizgiyle ateş etmemeli.
        /// </summary>
        private void EnsurePelletTracers()
        {
            int needed = _config.PelletCount;
            if (_pelletTracers.Count >= needed) return;

            // Ilk cizgi silahin kendi iz cizeri; gerisi ondan kopyalanir, boylece
            // malzemesi, genisligi ve rengi ayni yerden gelir (asset-art.md: bir sey
            // tek yerde tanimlanir).
            if (_pelletTracers.Count == 0) _pelletTracers.Add(tracer);

            // HAVUZ KUCULMEZ, yalnizca buyur: pompalidan tabancaya gecip geri donen
            // oyuncu her seferinde yeni cizgi yaratmamali. Fazlasi zaten kapali durur.
            while (_pelletTracers.Count < needed)
            {
                LineRenderer clone = Instantiate(tracer, tracer.transform.parent);
                clone.name = $"{tracer.name}_Sacma{_pelletTracers.Count}";
                clone.enabled = false;
                _pelletTracers.Add(clone);
            }
        }

        private void HidePelletTracers()
        {
            for (int i = 0; i < _pelletTracers.Count; i++)
            {
                if (_pelletTracers[i] != null) _pelletTracers[i].enabled = false;
            }
        }

        private void TickFeedback(float dt)
        {
            if (_tracerRemaining > 0f)
            {
                _tracerRemaining -= dt;

                if (_tracerRemaining <= 0f)
                {
                    if (tracer != null) tracer.enabled = false;

                    // Sacma izleri de sonmeli. Bu satirin yoklugu, pompaliyla tek atis
                    // yapip birakan oyuncunun ekraninda yedi cizginin ASILI KALMASI
                    // demek olurdu.
                    HidePelletTracers();
                }
            }

            if (_hitMarkerRemaining > 0f) _hitMarkerRemaining -= dt;
        }
    }
}
