using System;
using System.Collections.Generic;
using Bunker.Audio;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using Bunker.Systems.Config;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Yakın dövüş: <b>hançer, kılıç, balta</b>. M1-07, 2026-09-08'de üçe çıktı.
    ///
    /// <para><b>Neden var:</b> mermi harcamayan, en yakın ve en riskli öldürme yolu.
    /// Ekonomi de en yüksek puanı ona verir (SYS-ekonomi). Erken turlarda mermi
    /// biriktirmenin, geç turlarda son çarenin adı.</para>
    ///
    /// <para><b>Neden üç silah</b> (geliştirici, 2026-09-08): <i>"kılıç dagger'ın 1.5
    /// katı hasar, axe 2.5 katı ama savurması da 2x uzun sürsün."</i> Üçü aynı bıçağın
    /// güçlüsü değil, <b>üç ayrı ritim</b>: hançer hızlı ve affedici, balta ağır ve
    /// taahhütlü. Bıçağın riski zamanlamada olduğu için, savuruş süresini uzatmak
    /// hasarı artırmanın gerçek bedelidir — silah tezgâhındaki dört silahın DPS'lerinin
    /// bilerek yakın tutulmasıyla aynı tasarım.</para>
    ///
    /// <para><b>Risk zamanlamada:</b> bekleme süresi boyunca oyuncu savunmasızdır ve
    /// zombinin saldırı menzili bıçağın erişiminin hemen içindedir — yani bıçak
    /// kullanmak, vurulma menziline <i>bilerek</i> girmek demektir.</para>
    ///
    /// <para><b>Sayılar burada değil:</b> taban savuruş <c>knife.json</c>'da, silah
    /// başına çarpanlar <c>config/content/melee.json</c>'da (config-data.md).</para>
    ///
    /// <para><b>Hasar host'ta</b> (ADR-0004), hız sınırı sunucuda ayrıca sayılır. Yerel
    /// oyuncu savuruşu aynı karede görür; onay beklemez.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Melee")]
    public sealed class PlayerMelee : NetworkBehaviour
    {
        [Header("Ayar")]
        [Tooltip("config/balance/knife.json'dan uretilen varlik. TABAN savurus.")]
        [SerializeField] private KnifeConfigAsset knifeConfig;

        [Tooltip("config/content/melee.json'dan uretilen katalog. Bos birakilirsa " +
                 "yalnizca taban bicak calisir.")]
        [SerializeField] private MeleeCatalogAsset meleeCatalog;

        [Header("Referanslar")]
        [SerializeField] private Camera playerCamera;

        /// <summary>Oyuncunun her run'a başladığı bıçak. Katalogdaki id ile aynı.</summary>
        private const string StarterMeleeId = "melee.dagger";

        private KnifeConfig _base;

        private List<MeleeDefinition> _catalog;
        private readonly List<MeleeDefinition> _owned = new List<MeleeDefinition>(3);

        private MeleeDefinition _current;

        /// <summary>Elde ne oldugunu bilen taraf (PlayerWeapon.IsMeleeActive).</summary>
        private PlayerWeapon _hands;

        /// <summary>Savaş günlüğünde vuranın adı ("Oyuncu[BALTA]"). Silah değişince kurulur.</summary>
        private string _logSource = "Oyuncu";

        private float _localCooldown;
        private float _pendingSwingDelay;
        private bool _swingPending;

        private ActionRateLimiter _serverLimiter;

        // Kure sorgusu tamponu bir kez ayrilir: kare basina tahsis yasak
        // (csharp-code.md).
        private static readonly Collider[] SwingHits = new Collider[24];

        // Nisan yolundaki isabetler icin ayri tampon. Bicak once BAKTIGIN yere bakar
        // (2026-09-05); koni taramasi yalnizca yedek.
        private static readonly RaycastHit[] SwingRayHits = new RaycastHit[16];

        /// <summary>
        /// Savuruş ışınının kalınlığı. Bıçak bir iğne değil: tam ortayı tutturmayı
        /// zorunlu kılmak, oyuncunun "vurdum ama saymadı" diye okuduğu şeydir.
        /// </summary>
        private const float SwingProbeRadiusMeters = 0.18f;

        /// <summary>
        /// Savuruş başladı. <b>Yerel, aynı karede</b> — el modeli ve ses buna bağlanır.
        /// Hazırlık süresi boyunca bıçağın hareket ediyor olması, bekleme süresini
        /// bir gecikme değil bir AGIRLIK olarak okutur (gameplay-code.md).
        /// </summary>
        public event Action SwingStarted;

        /// <summary>
        /// Elindeki bıçak değişti. <b>El modeli buna bağlanır</b> — balta aldığın hâlde
        /// elinde hançer görmek, satın almanın karşılığını görünmez kılar.
        /// </summary>
        public event Action<MeleeDefinition> Equipped;

        /// <summary>Bir bıçak öldürmesi onaylandı. Ekonomi buna bağlanır.</summary>
        public event Action<DamageKind, bool> KillConfirmed;

        /// <summary>Savuruş bekleme süresi (0 = hazır). HUD için.</summary>
        public float CooldownRemaining => _localCooldown;

        /// <summary>Elindeki bıçak. Arayüz ve el modeli okur.</summary>
        public MeleeDefinition Current => _current;

        /// <summary>Satılabilecek bütün bıçaklar. <b>Tezgâh bunu listeler.</b></summary>
        public IReadOnlyList<MeleeDefinition> Catalog => _catalog;

        /// <summary>Bu bıçak envanterde var mı.</summary>
        public bool Owns(string meleeId)
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i].Id == meleeId) return true;
            }

            return false;
        }

        /// <summary>Katalogdan bir tanım; bulunamazsa geçersiz tanım döner.</summary>
        public MeleeDefinition FindInCatalog(string meleeId)
        {
            if (_catalog == null) return default;

            for (int i = 0; i < _catalog.Count; i++)
            {
                if (_catalog[i].Id == meleeId) return _catalog[i];
            }

            return default;
        }

        // ---------------------------------------------------------------- kurulum

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);

            // "Elimde ne var" sorusunun sahibi (bkz. Update). Awake'te bir kez
            // cozulur - kare basina GetComponent yasak (csharp-code.md).
            _hands = GetComponent<PlayerWeapon>();

            if (_hands == null)
            {
                // Sessiz kalmaz: bicak SESSIZCE calismaz hâle gelirdi ve oyun
                // testinde "sol tik bicagi sallamiyor" diye okunurdu - teshisi en
                // pahali hata turu.
                Debug.LogError("[Bicak] Ayni nesnede PlayerWeapon yok. Bicak 1 numarali " +
                               "SLOT oldugu icin hangi elin aktif oldugunu ondan okuyor; " +
                               "onsuz hic sallanmaz.", this);
            }

            if (knifeConfig == null)
            {
                // Eksik ayar sessiz varsayilanla gecistirilmez (config-protocol.md).
                Debug.LogError("[Bicak] knife.asset atanmamis. 'Bunker/Config/Ice Aktar' " +
                               "ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            _base = knifeConfig.ToRuntime();

            // Katalog yoksa oyun TABAN bicakla calisir - eksik bir katalog oyunu
            // durdurmamali ama SESSIZ de kalmamali (silah katalogu ile ayni kural).
            if (meleeCatalog != null && meleeCatalog.Count > 0)
            {
                _catalog = meleeCatalog.ToRuntime(_base);
            }
            else
            {
                Debug.LogWarning("[Bicak] melee.asset atanmamis ya da bos - yalnizca " +
                                 "taban bicak calisacak. 'Bunker/Config/Yakin Dovus " +
                                 "Ice Aktar' calistir.", this);

                _catalog = new List<MeleeDefinition>(1)
                {
                    new MeleeDefinition(StarterMeleeId, "BICAK", string.Empty,
                                        _base.SwingDamage, _base.SwingRangeMeters,
                                        _base.SwingArcDegrees, _base.SwingCooldownSeconds,
                                        _base.SwingWindupSeconds, 0)
                };
            }

            MeleeDefinition starter = FindInCatalog(StarterMeleeId);
            if (!starter.IsValid) starter = _catalog[0];

            AddMelee(starter);
            Equip(starter);
        }

        /// <summary>
        /// Bir bıçağı envantere ekler.
        /// </summary>
        /// <returns>Yeni eklendiyse <c>true</c>; zaten varsa <c>false</c>.</returns>
        private bool AddMelee(in MeleeDefinition definition)
        {
            if (!definition.IsValid || Owns(definition.Id)) return false;

            _owned.Add(definition);
            return true;
        }

        /// <summary>
        /// Eldeki bıçağı değiştirir.
        ///
        /// <para><b>Hız sınırı da yeniden kurulur:</b> baltanın bekleme süresi
        /// hançerinkinin iki katı ve sunucu sınırı eskisiyle kalsaydı, balta hançer
        /// hızında savrulabilirdi — istemcinin dayattığı bir ritim, tam olarak
        /// <c>ActionRateLimiter</c>'in engellemek için var olduğu şey (netcode.md).</para>
        /// </summary>
        private void Equip(in MeleeDefinition definition)
        {
            if (!definition.IsValid) return;

            _current = definition;
            _logSource = "Oyuncu[" + definition.DisplayName + "]";

            // Ag payi silahtakiyle ayni gerekcede: komut bir kare sonra gelir
            // (BUG-002). Tolerans birikmez.
            _serverLimiter = new ActionRateLimiter(definition.CooldownSeconds);

            // Bekleyen savurus IPTAL: hancerin hazirligiyla baslayip baltanin
            // hasarini indiren bir savurus, iki silahin ritmini birden yalanlardi.
            _swingPending = false;
            _pendingSwingDelay = 0f;

            Equipped?.Invoke(definition);
        }

        /// <summary>
        /// Tezgâh alımı: bıçağı verir ve eline koyar. <b>Yalnızca sunucu.</b>
        /// </summary>
        [Server]
        public void ServerGrantMelee(MeleeDefinition definition, bool equip)
        {
            if (!AddMelee(definition)) return;

            TargetGrantMelee(connectionToClient, definition.Id, equip);

            if (equip) Equip(definition);
        }

        /// <summary>
        /// Zaten sahip olunan bir bıçağa geçer (tezgâhta ikinci kez tıklamak).
        /// <b>Yalnızca sunucu.</b>
        /// </summary>
        [Server]
        public void ServerEquipOwned(string meleeId)
        {
            if (!Owns(meleeId)) return;

            MeleeDefinition definition = FindInCatalog(meleeId);
            if (!definition.IsValid) return;

            Equip(definition);
            TargetEquipMelee(connectionToClient, meleeId);
        }

        /// <summary>Geçişi istemcide de uygular — el modeli orada yaşıyor.</summary>
        [TargetRpc]
        private void TargetEquipMelee(NetworkConnection target, string meleeId)
        {
            if (isServer) return;

            MeleeDefinition definition = FindInCatalog(meleeId);
            if (definition.IsValid && Owns(meleeId)) Equip(definition);
        }

        /// <summary>
        /// Bıçağı <b>istemcide de</b> envantere ekler.
        ///
        /// <para>Envanter iki tarafta da yaşar: sunucu hasarın, istemci elindeki
        /// modelin sahibi. Yalnızca sunucuda eklenseydi, satın alan oyuncu bıçağı kendi
        /// ekranında hiç görmezdi (<c>PlayerWeapon.TargetGrantWeapon</c> ile aynı
        /// gerekçe).</para>
        /// </summary>
        [TargetRpc]
        private void TargetGrantMelee(NetworkConnection target, string meleeId, bool equip)
        {
            if (isServer) return;   // host: sunucu tarafi zaten ekledi

            MeleeDefinition definition = FindInCatalog(meleeId);
            if (!definition.IsValid) return;

            if (!AddMelee(definition)) return;
            if (equip) Equip(definition);
        }

        private void OnEnable() => RunSignals.RunRestarted += OnRunRestarted;

        private void OnDisable() => RunSignals.RunRestarted -= OnRunRestarted;

        /// <summary>
        /// Yeni run: envanter <b>başlangıç bıçağına döner</b>.
        ///
        /// <para>Puanla alınmış bir balta yeni run'a taşınsaydı, ikinci run'un ilk
        /// turu birincinin sonundaki güçle başlardı — <c>CardLoadout.Reset</c> ve
        /// <c>ShopState</c> ile aynı kural, aynı sebep.</para>
        /// </summary>
        private void OnRunRestarted()
        {
            _owned.Clear();

            MeleeDefinition starter = FindInCatalog(StarterMeleeId);
            if (!starter.IsValid && _catalog != null && _catalog.Count > 0) starter = _catalog[0];

            AddMelee(starter);
            Equip(starter);

            _localCooldown = 0f;
        }

        // ---------------------------------------------------------------- girdi

        private void Update()
        {
            if (_base == null) return;

            float dt = Time.deltaTime;
            if (_localCooldown > 0f) _localCooldown -= dt;

            TickPendingSwing(dt);

            if (!isLocalPlayer) return;

            // Run bitti YA DA tur arasi ekrani acik: girdi kesilir. Ekran acikken
            // ates etmek, bakis cevirmek ya da satin almak, fareyle kart secmeyi
            // imkansiz kilardi.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen) return;

            // BICAK ARTIK BIR SLOT (2026-09-09, gelistirici: "melee atagi V'ye
            // basarak yapiyorduk, bunu degistiriyoruz ve silah gibi slota
            // yerlestiriyoruz").
            //
            // Eski hâlde bicak ayri bir tustaydi (V) ve bu onu bir SILAH degil bir
            // KISAYOL yapiyordu: elindeki silahi birakmadan bicak sallayabilmek,
            // bicagin bedelini (o sirada ates edememek) sifira indiriyordu. Artik
            // bicak 1 numarali slotta ve sol tikla sallaniyor - yani her silah gibi.
            //
            // "Elimde ne var" sorusunun TEK cevabi PlayerWeapon'da (IsMeleeActive).
            // Burada ikinci bir bayrak tutulsaydi, ikisinin ayristigi bir karede
            // oyuncu hem ates eder hem bicak sallardi.
            if (_hands == null || !_hands.IsMeleeActive) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame) TrySwing();
        }

        private void TrySwing()
        {
            if (_localCooldown > 0f) return;
            if (_swingPending) return;
            if (!_current.IsValid) return;

            _localCooldown = _current.CooldownSeconds;

            // Hazirlik suresi bicagin AGIRLIGIDIR: hasar aninda degil, savurus
            // tamamlanınca iner. Sifir olsaydi bicak bir tusa basmaktan ibaret olurdu.
            _pendingSwingDelay = _current.WindupSeconds;
            _swingPending = true;

            SwingStarted?.Invoke();
            GameAudio.Play(SfxId.KnifeSwing);

            if (_pendingSwingDelay <= 0f) ReleaseSwing();
        }

        private void TickPendingSwing(float dt)
        {
            if (!_swingPending) return;

            _pendingSwingDelay -= dt;
            if (_pendingSwingDelay > 0f) return;

            ReleaseSwing();
        }

        private void ReleaseSwing()
        {
            _swingPending = false;

            if (!isLocalPlayer) return;

            Transform cam = playerCamera != null ? playerCamera.transform : transform;
            CmdSwing(cam.position, cam.forward);
        }

        /// <summary>Bıçak bir şeye değdi. Savuruşun boşa gitmediğini söyleyen tek şey.</summary>
        [TargetRpc]
        private void TargetReportSwingHit(NetworkConnection target, Vector3 hitPoint, float damage, bool killed)
        {
            GameAudio.Play(SfxId.KnifeHit);

            // Bicagin hasar sayisi da gorunur (2026-09-05): bicak en yuksek puani
            // veren ve en riskli oldurme yolu - ne kadar vurdugunu gormeden o riski
            // almaya deger mi bilinmez.
            CombatFeedback.RaiseDamageDealt(hitPoint, damage, false, killed);
        }

        /// <summary>
        /// Hasarın uygulandığı tek yer. <b>Her RPC bir güven sınırıdır</b> (netcode.md):
        /// savuruş hızı sunucuda ayrıca sayılır.
        /// </summary>
        [Command]
        private void CmdSwing(Vector3 origin, Vector3 forward)
        {
            if (_serverLimiter == null || !_serverLimiter.TryAccept(Time.time)) return;
            if (!_current.IsValid) return;

            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();

            const float maxOriginDriftMeters = 3f;
            if ((origin - transform.position).sqrMagnitude >
                maxOriginDriftMeters * maxOriginDriftMeters)
            {
                return;
            }

            // ONCE NISAN YOLU (2026-09-05): baktigin yere hangi VURUS KUTUSU denk
            // geliyorsa oraya iner - kafaya nisan alip savurmak kafayi, bacaga nisan
            // alip savurmak bacagi vurur. Onceki surumde yalnizca koni taramasi vardi
            // ve her zaman merkeze EN YAKIN kutuyu seciyordu, yani bicak nereye
            // baktigindan bagimsiz olarak hep govdeye iniyordu.
            IDamageable aimed = FindAimedTarget(origin, forward);

            if (aimed != null)
            {
                Strike(aimed);
                return;
            }

            // Bicak bir isin degil bir KONI: kalabalikta savurmak ise yaramali, ama
            // arkani donup vurmak yaramamali. Isin bosa gittiginde koni yedege gecer -
            // yoksa kalabaligin ortasinda savurmak bosa dusebilirdi.
            int count = Physics.OverlapSphereNonAlloc(origin, _current.RangeMeters, SwingHits,
                                                      Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            if (count == 0) return;

            float cosLimit = Mathf.Cos(_current.ArcDegrees * 0.5f * Mathf.Deg2Rad);
            IDamageable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider c = SwingHits[i];
                if (c == null) continue;

                Vector3 toTarget = c.bounds.center - origin;
                float distance = toTarget.magnitude;
                if (distance < 0.001f) continue;

                if (Vector3.Dot(forward, toTarget / distance) < cosLimit) continue;

                var target = c.GetComponent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                // En yakini secilir: bir savurus bir hedef. Koni icindeki herkese
                // vurmak bicagi alan silahina cevirirdi ve riski silerdi.
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = target;
            }

            if (best == null) return;

            Strike(best);
        }

        /// <summary>
        /// Nişan yolundaki ilk canlı vuruş kutusu.
        ///
        /// <para><b>Küre taraması, ince ışın değil:</b> bıçak bir iğne değil. Tam
        /// ortayı tutturmayı zorunlu kılmak, oyuncunun "vurdum ama saymadı" diye
        /// okuduğu şeydir.</para>
        ///
        /// <para>Duvar, tahta ve zemin de ışını kesebilir — ilk çarpılan şey canlı bir
        /// hedef değilse savuruş oraya iner ve boşa gider. Bu doğru: duvarın arkasından
        /// bıçaklamak, mermiyle duvardan geçmekle aynı şey olurdu.</para>
        /// </summary>
        private IDamageable FindAimedTarget(Vector3 origin, Vector3 forward)
        {
            int count = Physics.SphereCastNonAlloc(origin, SwingProbeRadiusMeters, forward,
                                                   SwingRayHits, _current.RangeMeters,
                                                   Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            if (count == 0) return null;

            IDamageable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider c = SwingRayHits[i].collider;
                if (c == null) continue;

                // OYUNCU GOVDESI YOK SAYILIR - kendisi de, takim arkadasi da (2026-09-10).
                // Oyunda PvP yok (CONTEXT). Ilk surum yalnizca savuranin kendi
                // carpistiricilarini eliyordu; bicak bir takim arkadasina hasar
                // yazabiliyordu. Yolu da KESMEZ: arkadasin arkasindaki zombiye gecer.
                if (c.GetComponentInParent<PlayerHealth>() != null) continue;

                float distance = SwingRayHits[i].distance;
                if (distance >= bestDistance) continue;

                var target = c.GetComponent<IDamageable>();

                // Canli bir hedef degilse yine de YOLU KESER: arkasindaki zombiye
                // gecmemeli. Bu yuzden 'best' null birakilip mesafe guncelleniyor.
                bestDistance = distance;
                best = target != null && target.IsAlive ? target : null;
            }

            return best;
        }

        /// <summary>
        /// Hasar sayısının ekranda görüneceği nokta.
        ///
        /// <para>Bıçak bir ışın değil bir koni; tek bir "çarpma noktası" yok. Vurulan
        /// şeyin kendi konumu, sayıyı doğru zombinin üstüne koymaya yeter.</para>
        /// </summary>
        private static Vector3 HitPointOf(IDamageable target)
        {
            return target is Component component
                ? component.transform.position + Vector3.up
                : Vector3.zero;
        }

        /// <summary>Hasarı uygular ve geri bildirimi yollar. <b>Yalnızca sunucuda.</b></summary>
        private void Strike(IDamageable target)
        {
            // Kafa kutusuna bicak carpani uygulanmaz: bicak zaten en yuksek puani
            // veriyor, ustune kafa carpani vermek silahi tamamen gereksiz kilardi.
            // Kaynak konumu: itme yonu bicakta da savuranin YONUNDE olmali
            // (2026-09-06). Yon tasinmasaydi zombi bicaklandiginda kendi baktigi
            // yonun tersine, yani oyuncuya DOGRU itilirdi.
            Vector3 from = transform.position;

            DamageResult result = target.ApplyDamage(
                new DamageInfo(_current.Damage *
                               RunModifiers.Multiplier(CardStat.MeleeDamage),
                               DamageKind.Melee, headshot: false,
                               sourceX: from.x, sourceZ: from.z,
                               source: _logSource));

            // Isabet geri bildirimi YALNIZCA savurana gider: kisisel bir bilgidir
            // (silahtaki TargetReportHit ile ayni gerekce).
            TargetReportSwingHit(connectionToClient, HitPointOf(target), result.Absorbed, result.Killed);

            if (result.Killed) KillConfirmed?.Invoke(DamageKind.Melee, false);
        }
    }
}
