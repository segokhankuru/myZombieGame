using Bunker.Audio;
using Bunker.Systems.Combat;
using Bunker.Systems.Economy;
using Bunker.Systems.Cards;
using Bunker.Systems.Rounds;
using Bunker.Systems.Ui;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Satın alma etkileşimi: bak, <b>E</b>'ye bas. Kapı (M1-09) ve duvar silahı
    /// (M1-10) aynı yoldan geçer.
    ///
    /// <para><b>Tek etkileşim noktası:</b> yeni bir satın alınabilir eklemek burayı
    /// değiştirmeyi gerektirmez — bakılan şey fiyatını ve ne olacağını kendisi söyler
    /// (<see cref="IPurchasable"/>).</para>
    ///
    /// <para><b>Basmak satın alır, tutmak tamir eder.</b> Aynı tuş iki iş yapıyor ve
    /// çakışma tanımlı: satın alınabilir bir şeye bakarken tamir çalışmaz. Bu ikisi
    /// pratikte aynı anda görüş alanında olmaz, ama "olmaz" varsaymak yerine kuralı
    /// yazmak gerekir.</para>
    ///
    /// <para><b>Otorite host'ta</b> (ADR-0004): puanı düşüren ve satın almayı onaylayan
    /// sunucudur. İstemcinin "aldım" demesi bir istektir, sonuç değil.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Interact")]
    public sealed class PlayerInteract : NetworkBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerScore score;
        [SerializeField] private PlayerWeapon weapon;

        [Tooltip("Yakin dovus. Tezgahtan kilic/balta alimi buradan gecer (2026-09-08).")]
        [SerializeField] private PlayerMelee melee;

        [Tooltip("Etkilesim menzili. Denge degeri degil ergonomi: oyuncunun 'buna " +
                 "bakiyorum' dedigi mesafe.")]
        [SerializeField] private float rangeMeters = 3f;

        /// <summary>Şu an bakılan satın alınabilir şeyin metni; yoksa boş. HUD okur.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        /// <summary>Bakılan şey alınabilir mi (puan yetiyor mu). HUD rengi için.</summary>
        public bool CanAfford { get; private set; }

        /// <summary>Satın alınabilir bir şeye bakılıyor mu — tamir bu sırada durur.</summary>
        public bool HasTarget => CurrentPrompt.Length > 0;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
            if (score == null) score = GetComponent<PlayerScore>();
            if (weapon == null) weapon = GetComponent<PlayerWeapon>();
            if (melee == null) melee = GetComponent<PlayerMelee>();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            // Run bitti: girdi kesilir (M1-11, AC-3). Olu bir oyuncunun kapi satin
            // almasi, yeniden baslatmada silinecek bir harcamadir.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen)
            {
                // HasTarget bu ikisinden turetilir; ayrica yazilmaz.
                CurrentPrompt = string.Empty;
                CanAfford = false;
                return;
            }

            // MENU BU KAREDE KAPANDIYSA E YUTULUR (2026-09-09). Menuyu kapatan tus
            // basisi, ayni karede menuyu yeniden acmamali - "E ile cikamiyorum"
            // sikayetinin sebebi buydu. Bilesen sirasi tanimsiz oldugu icin cozum
            // burada degil ORTAK yerde (MenuSignals.LastMenuClosedFrame).
            if (MenuSignals.ClosedThisFrame(Time.frameCount))
            {
                CurrentPrompt = string.Empty;
                CanAfford = false;
                return;
            }

            // Tezgah istasyonu satin alinabilir bir sey DEGIL, bir menu acar. Ayni
            // tusu paylasiyor: mermi almayi ogrenmis oyuncuya ikinci bir kural
            // dayatmak yerine ayni "bak ve bas" aliskanligini kullaniyor.
            ShopStation station = FindStation();

            if (station != null)
            {
                CurrentPrompt = station.Prompt;
                CanAfford = true;   // menu acmak bedava; fiyatlar iceride

                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    station.Open();
                }

                return;
            }

            // Silah tezgahi (2026-09-06). Tezgah istasyonuyla ayni kalip ve ayni tus:
            // ucuncu bir etkilesim kurali yok.
            WeaponStation weaponStation = LookedAt<WeaponStation>();

            if (weaponStation != null)
            {
                CurrentPrompt = weaponStation.Prompt;
                CanAfford = true;

                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    weaponStation.Open();
                }

                return;
            }

            IPurchasable target = FindTarget();

            if (target == null)
            {
                CurrentPrompt = string.Empty;
                CanAfford = false;
                return;
            }

            CurrentPrompt = target.Prompt;
            CanAfford = score != null && score.Spendable >= target.Cost;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;

            CmdPurchase();
        }

        /// <summary>
        /// Bakılan satın alınabilir şey. <b>Yalnızca ipucu için</b> — sunucu kendi
        /// ışınını atar, çünkü istemcinin neye baktığı iddiası güvenilmez veridir.
        /// </summary>
        private IPurchasable FindTarget()
        {
            Transform cam = playerCamera != null ? playerCamera.transform : transform;

            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, rangeMeters,
                                 Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            var purchasable = hit.collider.GetComponentInParent<IPurchasable>();
            return purchasable != null && purchasable.IsAvailable ? purchasable : null;
        }

        /// <summary>Bakilan tezgah istasyonu, yoksa null.</summary>
        private ShopStation FindStation() => LookedAt<ShopStation>();

        /// <summary>Bakılan nesnedeki <typeparamref name="T"/>; yoksa <c>null</c>.</summary>
        private T LookedAt<T>() where T : Component
        {
            Transform cam = playerCamera != null ? playerCamera.transform : transform;

            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, rangeMeters,
                                 Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<T>();
        }

        // ------------------------------------------------------- silah tezgahi

        /// <summary>
        /// Tezgâhtan silah ya da mermi ister. <b>Arayüz çağırır, sunucu karar
        /// verir.</b>
        /// </summary>
        public void RequestBuyWeapon(string weaponId)
        {
            if (!isLocalPlayer || string.IsNullOrEmpty(weaponId)) return;

            CmdBuyWeapon(weaponId);
        }

        /// <summary>
        /// Tezgâh alımı. <b>Her RPC bir güven sınırıdır</b> (netcode.md) — burada
        /// doğrulanan üç şey var ve üçü de gerekli:
        ///
        /// <list type="number">
        /// <item>İstemci gerçekten bir <see cref="WeaponStation"/>'ın <b>yanında</b> mı.
        /// Işın yerine yarıçap kullanılıyor: menü açıkken oyuncu başka yöne bakıyor
        /// olabilir, ama tezgâhtan uzaklaşmış olamaz.</item>
        /// <item>Tezgâh <b>açılabilir durumda</b> mı (mola). İstemcinin menüyü açık
        /// tutup tur ortasında satın alması engellenir.</item>
        /// <item>İstenen id <b>katalogda</b> var mı. Uydurulan bir id bulunamaz ve
        /// istek sessizce düşer.</item>
        /// </list>
        /// </summary>
        [Command]
        private void CmdBuyWeapon(string weaponId)
        {
            // SESSIZ REDDETME YOK (2026-09-06). Onceki surum uc ayri sebeple hicbir
            // sey soylemeden donuyordu ve gelistirici "yedek mermi gelmiyor" diye
            // okudu - hangi sebep oldugunu kimse bilemedi. Sunucuda kalan bir uyari,
            // bir sonraki oyun testini tahmin olmaktan cikarir.
            if (weapon == null || score == null)
            {
                Debug.LogWarning("[Tezgah] Alim reddedildi: oyuncuda silah ya da puan " +
                                 "bileseni yok.", this);
                return;
            }

            if (!IsNearOpenWeaponStation())
            {
                Debug.LogWarning("[Tezgah] Alim reddedildi: acik bir silah tezgahinin " +
                                 "yaninda degilsin (mola bitmis olabilir).", this);
                return;
            }

            WeaponDefinition definition = weapon.FindInCatalog(weaponId);

            if (!definition.IsValid)
            {
                Debug.LogWarning($"[Tezgah] Alim reddedildi: '{weaponId}' katalogda yok. " +
                                 "'Bunker/Config/Silahlari Ice Aktar' calistir.", this);
                return;
            }

            // UC DURUM (2026-09-09), taşıma sınırı geldikten sonra:
            //
            //  1) HIC ALINMAMIS  -> satin alinir (fiyat), ele gecer
            //  2) ALINMIS ama UZERINDE DEGIL -> BEDELSIZ ele gecer
            //  3) ALINMIS ve UZERINDE -> mermi satar (eski davranis)
            //
            // Ikinci durum yeni ve geliştiricinin istegi: "satin alinmis silahlara
            // puan harcamadan degistirebilme olayimiz olsun". Gerekce basit: o silahi
            // zaten odedin. Ikinci kez ucret almak, tasima sinirini bir CEZAYA
            // cevirirdi - oysa sinirin isi bir SECIM urettirmek.
            bool owned = weapon.Owns(definition.Id);
            bool carrying = weapon.IsCarrying(definition.Id);

            int cost;

            if (!owned) cost = definition.Price;
            else if (!carrying) cost = 0;
            else cost = definition.AmmoPrice > 0 ? definition.AmmoPrice : definition.Price;

            // BEDAVA ELE ALIS CUZDANA UGRAMAZ (2026-09-10, oyun testi: "satin
            // aldigimi ELE AL dedigimde degismiyor").
            //
            // Burada duran eski yorum "TrySpend(0) basarili doner" diyordu. Donmuyor:
            // PlayerWallet.TryPurchase sifir maliyeti InvalidCost sayar - ve haklidir,
            // cunku bedava bir "SATIN AL" dugmesi baska yerde bir hatanin belirtisi.
            // Sonuc: cantadaki silaha gecis SESSIZCE reddediliyordu. Dugme calisiyor,
            // komut gidiyor, sunucu geri donuyor ve oyuncuya gorunen tek sey "hicbir
            // sey olmadi".
            //
            // Ders (2026-09-09'un aynisi): bir yorumun davranisi tarif etmesi, kodun
            // oyle davrandigi anlamina gelmiyor. Bicak tarafi ayni durumu zaten ayri
            // bir yolla cozuyordu; iki yol artik ayni sekli tutuyor.
            if (cost > 0 && score.TrySpend(cost) != PurchaseResult.Success)
            {
                TargetReportPurchase(connectionToClient, false);
                return;
            }

            if (owned && carrying)
            {
                // Mermi SATIN ALINAN SILAHA yazilir, eldekine degil: katalogdan
                // pompali mermisi alan oyuncunun elinde tabanca olabilir.
                weapon.ServerAddReserveTo(definition.Id, AmmoPerPurchase(definition));
            }
            else
            {
                // Hem satin alma hem bedelsiz gecis ayni yoldan: PlayerWeapon yer
                // varsa ekler, yoksa ELDEKININ yerine koyar. Karar orada, tek yerde.
                weapon.ServerGrantWeapon(definition, equip: true);
            }

            TargetReportPurchase(connectionToClient, true);
        }

        // -------------------------------------------------- tezgah: yakin dovus

        /// <summary>
        /// Tezgâhtan kılıç ya da balta ister. <b>Arayüz çağırır, sunucu karar
        /// verir.</b> 2026-09-08.
        /// </summary>
        public void RequestBuyMelee(string meleeId)
        {
            if (!isLocalPlayer || string.IsNullOrEmpty(meleeId)) return;

            CmdBuyMelee(meleeId);
        }

        /// <summary>
        /// Bıçak alımı. Doğrulama <see cref="CmdBuyWeapon"/> ile <b>aynı üç adım</b>:
        /// tezgâhın yanında mısın, tezgâh açık mı, id katalogda var mı.
        ///
        /// <para><b>Sahip olunan bıçak yeniden satılmaz.</b> Silahta ikinci alım mermi
        /// getiriyor; bıçağın mermisi yok, yani ikinci alımın verecek bir şeyi de yok.
        /// Düğme zaten "elinde" yazacak — burası o kuralın sunucu tarafı, çünkü
        /// arayüze güvenilmez (netcode.md).</para>
        /// </summary>
        [Command]
        private void CmdBuyMelee(string meleeId)
        {
            // SESSIZ REDDETME YOK (CmdBuyWeapon ile ayni ders): reddin sebebi
            // sunucuda yazili kalmali, yoksa "para gitti bicak gelmedi" tahmin olur.
            if (melee == null || score == null)
            {
                Debug.LogWarning("[Tezgah] Bicak alimi reddedildi: oyuncuda bicak ya da " +
                                 "puan bileseni yok.", this);
                return;
            }

            if (!IsNearOpenWeaponStation())
            {
                Debug.LogWarning("[Tezgah] Bicak alimi reddedildi: acik bir silah " +
                                 "tezgahinin yaninda degilsin (mola bitmis olabilir).", this);
                return;
            }

            MeleeDefinition definition = melee.FindInCatalog(meleeId);

            if (!definition.IsValid)
            {
                Debug.LogWarning($"[Tezgah] Bicak alimi reddedildi: '{meleeId}' katalogda " +
                                 "yok. 'Bunker/Config/Yakin Dovus Ice Aktar' calistir.", this);
                return;
            }

            if (melee.Owns(definition.Id))
            {
                // Elinde zaten var: ELINE AL, puan alma. Tezgahta iki bicagi olan
                // oyuncunun aralarinda gecis yapabilmesi gerek ve buradan ucuz.
                melee.ServerEquipOwned(definition.Id);
                TargetReportPurchase(connectionToClient, true);
                return;
            }

            if (definition.Price <= 0)
            {
                Debug.LogWarning($"[Tezgah] '{meleeId}' bedava ama envanterde yok - " +
                                 "baslangic bicagi kurulmamis olabilir.", this);
                return;
            }

            if (score.TrySpend(definition.Price) != PurchaseResult.Success)
            {
                TargetReportPurchase(connectionToClient, false);
                return;
            }

            melee.ServerGrantMelee(definition, equip: true);
            TargetReportPurchase(connectionToClient, true);
        }

        /// <summary>
        /// Bir alışta gelen yedek mermi: <b>şarjörün üç katı</b>.
        ///
        /// <para><b>Neden şarjöre bağlı, sabit bir sayı değil</b> (config-data.md):
        /// pompalıya 60, tabancaya 60 mermi vermek birine cömert, diğerine cimri
        /// olurdu. Şarjör kapasitesi silahın ritmini zaten taşıyor.</para>
        ///
        /// <para><b>Neden üç</b>: pompalıda 18 mermi (üç dolum), tabancada 36. Bir
        /// alış, oyuncuyu bir sonraki molaya kadar taşımalı; taşımıyorsa tezgâh bir
        /// musluk değil bir kuyruk olur.</para>
        /// </summary>
        private static int AmmoPerPurchase(in WeaponDefinition definition) =>
            definition.MagazineCapacity * 3;

        /// <summary>
        /// Sunucunun konum doğrulaması: açık bir silah tezgâhının yanında mıyız.
        ///
        /// <para><c>FindObjectsByType</c> burada kabul edilebilir çünkü <b>kare başına
        /// değil</b>, yalnızca bir satın alma komutunda çalışır (csharp-code.md'nin
        /// yasağı sıcak yollar içindir).</para>
        /// </summary>
        private bool IsNearOpenWeaponStation()
        {
            const float maxDistanceMeters = 5f;

            Vector3 position = transform.position;

            foreach (WeaponStation candidate in
                     FindObjectsByType<WeaponStation>(FindObjectsSortMode.None))
            {
                if (!candidate.CanOpen) continue;

                if ((candidate.transform.position - position).sqrMagnitude <=
                    maxDistanceMeters * maxDistanceMeters)
                {
                    return true;
                }
            }

            return false;
        }

        [Command]
        private void CmdPurchase()
        {
            Transform cam = playerCamera != null ? playerCamera.transform : transform;

            // Sunucu kendi isinini atar. Mesafeye biraz pay birakilir: istemcinin
            // konumu bir kare eski olabilir (BUG-002'nin dersi - esitlik degil
            // makuliyet).
            const float serverRangeSlackMeters = 1f;

            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit,
                                 rangeMeters + serverRangeSlackMeters,
                                 Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Collide))
            {
                return;
            }

            var purchasable = hit.collider.GetComponentInParent<IPurchasable>();
            if (purchasable == null || !purchasable.IsAvailable) return;
            if (score == null) return;

            // Duvar silahi mermiyi ALICIYA yazar; kim aldiysa o.
            if (purchasable is WallWeaponPurchase wallWeapon) wallWeapon.SetBuyer(weapon);

            // Once odeme, sonra etki. Ters sirada bir hata, bedava kapi demektir.
            if (score.TrySpend(purchasable.Cost) != PurchaseResult.Success)
            {
                // Yetmeyen puan SESSIZ kalmaz: hicbir sey olmamasi, tusun
                // calismadigi gibi okunur (game-ux: her etkilesimin bir cevabi olmali).
                TargetReportPurchase(connectionToClient, false);
                return;
            }

            purchasable.OnPurchased();
            TargetReportPurchase(connectionToClient, true);
        }

        /// <summary>Alimin sonucu yalnizca ALANA gider - kisisel bir bilgi.</summary>
        [TargetRpc]
        private void TargetReportPurchase(NetworkConnection target, bool success)
        {
            GameAudio.Play(success ? SfxId.Purchase : SfxId.Denied);
        }
    }
}
