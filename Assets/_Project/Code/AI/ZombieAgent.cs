using System;
using Bunker.Systems.Ai;
using Bunker.Systems.Cards;
using Bunker.Systems.Config;
using Bunker.Systems.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Bunker.AI
{
    /// <summary>
    /// Bir zombinin motor tarafı: kararı <see cref="ZombieBrain"/> verir, bu sınıf onu
    /// NavMesh, çarpışma ve görsel geri bildirime çevirir. M1-04.
    ///
    /// <para><b>İş bölümü neden böyle:</b> karar mantığı saf C# olduğu için Unity
    /// açmadan test edilir (ÇK-16). Burada test edilemeyen tek şey kalır — motoru
    /// sürmek. Bir kural buraya sızarsa test edilemez hâle gelir; sızmışsa beyne
    /// taşınmalıdır.</para>
    ///
    /// <para><b>Otorite (ADR-0004, M-01 kısıtı):</b> zombi mantığı yalnızca host'ta
    /// çalışır. Bu sınıf Mirror'a bağlı değil — bilerek: <c>Bunker.AI</c> ağı bilmez.
    /// M1-05'te gelen ağ seam'i <see cref="SetSimulated"/> ile istemcilerde simülasyonu
    /// kapatacak ve konumu tek noktadan yayınlayacak. <b>Zombide NetworkTransform
    /// yoktur</b> ve bu kural burada başlar: bu prefab'a ağ bileşeni eklenmez.</para>
    ///
    /// <para><b>Bütçe (PERF-BUDGET, M0-04 ölçümü):</b> zombiler her kare düşünmez —
    /// saniyede <c>thinkHz</c> kez, ve fazları birbirine göre kaydırılmış olarak. 40
    /// zombinin aynı karede yol istemesi, gerçek oyunlardaki ani kare düşüşlerinin ana
    /// sebebidir. Uzaktaki zombilerin animatörü tamamen kapatılır — ölçüm darboğazın
    /// NavMesh değil animatör olduğunu söyledi, o yüzden bu <b>baştan</b> var.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Agent")]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ZombieAgent : MonoBehaviour, IDamageable
    {
        [Header("Referanslar")]
        [Tooltip("Govde gorseli. Gri kutuda durum rengi buradan gosterilir.")]
        [SerializeField] private Renderer bodyRenderer;
        [Tooltip("Varsa animator. M1-04'te yok; alan simdiden var cunku uzaklik " +
                 "kesmesi (culling) sonradan degil bastan kurulmali.")]
        [SerializeField] private Animator animator;

        [Header("Hata ayiklama")]
        [Tooltip("Durum rengi ve gizmo. Ilk oyun testinde masrafini cikarir (ai-code.md).")]
        [SerializeField] private bool debugVisuals = true;

        // --- calisma ani ---

        private NavMeshAgent _navAgent;
        private Transform _transform;
        private Collider[] _colliders;
        private MaterialPropertyBlock _propertyBlock;

        private ZombieConfig _config;
        private ZombieBrain _brain;
        private HealthPool _health;

        private ZombieTargetBeacon _target;
        private WindowEntry _window;
        private WindowBarricade _barricade;
        private bool _hasEnteredBuilding;

        private float _thinkAccumulator;
        private float _thinkPhaseOffset;
        private float _repathTimer;
        private float _vaultTimer;
        private Vector3 _vaultFrom;

        private bool _simulated = true;
        private bool _initialized;

        // Kart kaynakli yavaslatma. Havuzdan cikan zombi bunu TASIMAMALI -
        // Reset'te sifirlanir (systems-code.md: havuzlanmis nesne onceki hayatindan
        // durum tasiyamaz).
        private float _cardSlowMultiplier = 1f;

        private float _baseSpeed;
        private bool _dying;
        private float _deathTimer;
        private Quaternion _deathStartRotation;

        private Vector3 _netTargetPosition;
        private float _netTargetYaw;
        private bool _hasNetTarget;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Zombi öldüğünde bir kez tetiklenir. Ekonomi ve tur sayacı buna bağlanır.</summary>
        public event Action<ZombieAgent, DamageKind, bool> Killed;

        /// <summary>
        /// Ceset sahneden kalktı — <b>havuza iade zamanı</b>. Ölümden ayrı bir olay,
        /// çünkü ölüm anında puan yazılmalı ama nesne hâlâ görünürdür (M1-13).
        /// </summary>
        public event Action<ZombieAgent> Despawned;

        /// <summary>
        /// Ağdaki kimliği. <b>Nesne referansı değil id gönderilir</b> — istemci
        /// tarafı hangi vekilin hangi zombi olduğunu bununla bilir (ADR-0004 seam'i).
        /// 0 "kimlik verilmemiş" demektir.
        /// </summary>
        public ushort NetId { get; set; }

        public ZombieState State => _brain?.State ?? ZombieState.Dead;
        public bool IsAlive => _initialized && _health.IsAlive;

        /// <summary>Govde. Kafa kutusu ayri bir bilesendir (ZombieHitbox).</summary>
        public bool CountsAsHeadshot => false;
        public float HealthFraction01 => _initialized ? _health.Fraction01 : 0f;

        // ---------------------------------------------------------------- kurulum

        private void Awake()
        {
            // Referanslar bir kez cozulur (csharp-code.md).
            _transform = transform;
            _navAgent = GetComponent<NavMeshAgent>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _propertyBlock = new MaterialPropertyBlock();

            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            // Her zombi kendi fazinda dusunur. Ayni karede dusunen 40 zombi, o karede
            // 40 yol istegi demektir; kaydirmak ayni is yukunu zamana yayar.
            _thinkPhaseOffset = UnityEngine.Random.value;
        }

        /// <summary>
        /// Zombiyi oyuna sokar. Havuzdan çıkan bir zombi için de <b>her seferinde</b>
        /// çağrılır — havuzlanmış nesnenin önceki hayatından durum taşıması yasaktır
        /// (systems-code.md).
        /// </summary>
        /// <param name="config">Davranış ayarları (`config/balance/zombie.json`).</param>
        /// <param name="maxHealth">Turun canı (<c>RoundScaling.HealthForRound</c>).</param>
        /// <param name="speedMetersPerSecond">Turun hızı (<c>RoundScaling.SpeedForRound</c>).</param>
        /// <param name="entryWindow">Girilecek pencere. <c>null</c> ise zombi zaten içeridedir.</param>
        /// <param name="position">
        /// Doğum noktası. <b>Konumu bu metot yazar, çağıran taraf değil.</b>
        ///
        /// <para>Sebep somut: <c>NavMeshAgent</c> açıkken <c>transform.position</c>'a
        /// yazmak işe yaramaz — ajan kendi iç konumunu korur ve nesneyi oraya geri
        /// çeker. Havuzdan çıkan bir zombi böylece <b>bir önceki hayatında öldüğü
        /// yere</b> ışınlanıyordu; oyuncu bunu "zombiler doğrudan içeride beliriyor ve
        /// mavi yanıp sönüyor" olarak gördü. Doğrusu ajanı kapatıp konumu yazmak, sonra
        /// açıp <c>Warp</c> ile iç konumu da hizalamaktır.</para>
        /// </param>
        public void Spawn(ZombieConfig config, float maxHealth, float speedMetersPerSecond,
                          WindowEntry entryWindow, Vector3 position)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (_brain == null) _brain = new ZombieBrain(_config);
            else _brain.Reset();

            if (_health == null) _health = new HealthPool(maxHealth);
            else _health.ResetTo(maxHealth);

            _window = entryWindow;
            _barricade = entryWindow != null ? entryWindow.GetComponent<WindowBarricade>() : null;
            _hasEnteredBuilding = entryWindow == null;

            _thinkAccumulator = 0f;
            _repathTimer = 0f;
            _vaultTimer = 0f;
            _target = null;
            _hasNetTarget = false;
            _dying = false;
            _deathTimer = 0f;

            SetCollidersEnabled(true);

            _baseSpeed = speedMetersPerSecond;

            // Havuzdan cikan zombi onceki hayatinin yavaslatmasini TASIMAZ
            // (systems-code.md: pooled nesnenin sifirlama sozlesmesi). Bu satir
            // olmasaydi bir saat oynadiktan sonra ortaya cikan turden bir hata olurdu:
            // yeni dogan zombiler sebepsiz yavas.
            _cardSlowMultiplier = 1f;

            // Once ajani KAPAT, sonra konumu yaz: acik bir ajan transform yazmasini
            // yok sayar ve nesneyi kendi ic konumuna geri ceker.
            _navAgent.enabled = false;
            _transform.SetPositionAndRotation(position, Quaternion.identity);

            _navAgent.enabled = true;
            _navAgent.speed = speedMetersPerSecond;
            _navAgent.isStopped = false;

            // Warp ic konumu da hizalar. Bu satir olmadan ajan, acildigi andaki eski
            // konumunu "dogru" kabul edebilir.
            _navAgent.Warp(position);

            if (_navAgent.isOnNavMesh) _navAgent.ResetPath();

            _initialized = true;
            ApplyDebugColor();
        }

        /// <summary>
        /// Simülasyonu açar/kapatır. <b>M1-05'in ağ seam'i için:</b> istemcilerde zombi
        /// düşünmez, yalnızca host'un yayınladığı konuma uyar (ADR-0004).
        /// </summary>
        public void SetSimulated(bool value)
        {
            _simulated = value;
            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = !value;
            }
        }

        private void OnDisable()
        {
            // Awake'in kurdugunu OnDestroy/OnDisable bozar (csharp-code.md). Burada
            // bozulacak abonelik yok; olay dinleyicileri temizleniyor ki havuzdan
            // cikan zombi eski dinleyiciyi tasimasin.
            Killed = null;
            Despawned = null;
        }

        // ---------------------------------------------------------------- kare dongusu

        /// <summary>
        /// İstemci tarafı: host'un yayınladığı konumu uygular. <b>Anında atamaz,
        /// yumuşatır</b> — 10 Hz gelen bir konuma kare kare zıplamak, gecikmenin
        /// kendisinden daha kötü görünür (netcode.md).
        /// </summary>
        public void ApplyNetworkState(Vector3 position, float yawDegrees)
        {
            _netTargetPosition = position;
            _netTargetYaw = yawDegrees;
            _hasNetTarget = true;
        }

        private void TickNetworkProxy(float dt)
        {
            if (!_hasNetTarget) return;

            // Isinlanma esigi: cok uzaktaki bir duzeltmeyi yumusatmak, zombiyi
            // haritanin icinden gecirerek suruklemek demek olurdu.
            const float snapDistanceMeters = 4f;
            const float followSpeed = 12f;

            Vector3 current = _transform.position;

            if ((current - _netTargetPosition).sqrMagnitude > snapDistanceMeters * snapDistanceMeters)
            {
                _transform.position = _netTargetPosition;
            }
            else
            {
                _transform.position = Vector3.Lerp(current, _netTargetPosition, followSpeed * dt);
            }

            _transform.rotation = Quaternion.RotateTowards(
                _transform.rotation, Quaternion.Euler(0f, _netTargetYaw, 0f), 720f * dt);
        }

        private void Update()
        {
            if (!_initialized) return;

            // Yikilma ani her kare surulur ve baska hicbir sey calismaz: olu zombi
            // dusunmez, yol bulmaz, sendelemez.
            if (_dying)
            {
                TickDying(Time.deltaTime);
                return;
            }

            if (!_simulated)
            {
                // Vekil dusunmez, yol bulmaz, saldirmaz. Yalnizca host'un soyledigi
                // yere gider - tek seam kurali budur.
                TickNetworkProxy(Time.deltaTime);
                return;
            }

            if (_brain.State == ZombieState.Dead) return;

            float dt = Time.deltaTime;

            // Tirmanis kare basina surulur: beynin dusunme adimi 125 ms'dir ve
            // tirmanis o cozunurlukte kesik gorunur.
            if (_brain.State == ZombieState.Vaulting)
            {
                TickVault(dt);
            }

            TickThinking(dt);
            ApplyDebugColor();
        }

        private void TickThinking(float dt)
        {
            float interval = _config.BudgetThinkHz <= 0f ? 0.125f : 1f / _config.BudgetThinkHz;

            // Uzaktaki zombi daha seyrek dusunur ve animatoru tamamen kapanir.
            bool far = _target != null &&
                       (_target.GroundPosition - _transform.position).sqrMagnitude >
                       _config.BudgetAnimatorCullDistanceMeters * _config.BudgetAnimatorCullDistanceMeters;

            if (far) interval *= 4f;
            ApplyAnimatorCulling(far);

            // Faz kaydirmasi yalnizca ilk adimda uygulanir; sonrasi duzenli aralik.
            _thinkAccumulator += dt + _thinkPhaseOffset * interval;
            _thinkPhaseOffset = 0f;

            if (_thinkAccumulator < interval) return;

            float thinkDelta = _thinkAccumulator;
            _thinkAccumulator = 0f;

            Think(thinkDelta);
        }

        private void Think(float thinkDelta)
        {
            _target = ZombieTargets.Nearest(_transform.position);

            bool needsWindow = !_hasEnteredBuilding && _window != null && _window.IsOpen;

            float distanceToTarget = _target != null
                ? Vector3.Distance(_transform.position, _target.GroundPosition)
                : float.MaxValue;

            float distanceToWindow = needsWindow
                ? Vector3.Distance(_transform.position, _window.OutsidePoint)
                : 0f;

            float actualSpeed = _navAgent.enabled && _navAgent.isOnNavMesh
                ? _navAgent.velocity.magnitude
                : 0f;

            // Barikat pencerenin uzerinde yasar; zombi yalnizca "gecebilir miyim"
            // sorusunu sorar (M1-08).
            bool windowBlocked = needsWindow && _barricade != null && !_barricade.AllowsEntry;

            var senses = new ZombieSenses(
                hasTarget: _target != null,
                distanceToTargetMeters: distanceToTarget,
                needsWindowEntry: needsWindow,
                distanceToWindowMeters: distanceToWindow,
                actualSpeedMetersPerSecond: actualSpeed,
                windowBlocked: windowBlocked);

            ZombieState before = _brain.State;
            _brain.Tick(thinkDelta, senses);

            if (before != _brain.State) OnStateChanged(before, _brain.State);

            if (_brain.AttackLandedThisTick) LandAttack();

            // Sokme: beyin "sokuyorum" der, tahtayi dusuren burasi.
            if (_brain.State == ZombieState.Tearing && _barricade != null)
            {
                _barricade.Tear(thinkDelta);
            }

            // Sendeleme hizi (M1-13). Taban hiz turdan gelir; carpani beyin verir.
            if (_navAgent.enabled)
            {
                // Kart etkisi: "Yavaslatma" karti vurulan zombiyi yavaslatir (M-03).
                // Beynin sendeleme carpaniyla CARPILIR, toplanmaz: ikisi ayri
                // katman (SYS-02 §3.1) ve toplansalardi sendeleme sirasinda
                // yavaslatma etkisiz kalirdi.
                _navAgent.speed = _baseSpeed * _brain.SpeedMultiplier * _cardSlowMultiplier;
            }

            DriveMovement(thinkDelta);
        }

        private void OnStateChanged(ZombieState from, ZombieState to)
        {
            switch (to)
            {
                case ZombieState.Vaulting:
                    BeginVault();
                    break;

                case ZombieState.Stuck:
                    RecoverFromStuck();
                    break;
            }

            if (from == ZombieState.Vaulting) EndVault();
        }

        // ---------------------------------------------------------------- hareket

        private void DriveMovement(float thinkDelta)
        {
            if (!_navAgent.enabled || !_navAgent.isOnNavMesh) return;

            bool shouldMove = _brain.WantsMovement;
            _navAgent.isStopped = !shouldMove;

            if (!shouldMove)
            {
                // Vurus ve tirmanis sirasinda zombi hedefe DONER ama yurumez.
                // Donmeye devam etmesi telegrafi okunabilir kilar.
                FaceTarget(thinkDelta);
                return;
            }

            _repathTimer += thinkDelta;
            if (_repathTimer < _config.NavigationRepathIntervalSeconds) return;
            _repathTimer = 0f;

            Vector3 destination = _brain.MoveIntent switch
            {
                ZombieMoveIntent.Window when _window != null => _window.OutsidePoint,
                ZombieMoveIntent.Player when _target != null => _target.GroundPosition,
                _ => _transform.position
            };

            // SetDestination asenkron yol hesabi tetikler; senkron yol hesabi yasak
            // (ai-code.md).
            _navAgent.SetDestination(destination);
        }

        private void FaceTarget(float dt)
        {
            if (_target == null) return;

            Vector3 flat = _target.GroundPosition - _transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            _transform.rotation = Quaternion.RotateTowards(
                _transform.rotation,
                Quaternion.LookRotation(flat),
                _navAgent.angularSpeed * dt);
        }

        // ---------------------------------------------------------------- pencereden giris

        private void BeginVault()
        {
            // Emniyet agi: barikat hala geciyorsa tirmanis baslamamali. Beyin bunu
            // zaten kontrol ediyor; burasi, ayarin bozuk olmasi gibi sebeplerle
            // barikatin sessizce "acik" gorunmesi durumunu YAKALAR ve sessiz
            // kalmaz - oyun testinde "barikat oldugu halde gecti" diye okundu.
            if (_barricade != null && !_barricade.AllowsEntry)
            {
                Debug.LogWarning($"[Zombi] '{_window.name}' barikatli oldugu halde tirmanis " +
                                 "baslatildi. Barikat ayari eksik olabilir.", this);
            }

            _vaultTimer = 0f;
            _vaultFrom = _transform.position;

            // NavMesh ajani tirmanis boyunca kapali: yol bulma duvarin ustunden
            // gecemez, konumu bu sure boyunca biz suruyoruz.
            if (_navAgent.enabled && _navAgent.isOnNavMesh) _navAgent.ResetPath();
            _navAgent.enabled = false;
        }

        private void TickVault(float dt)
        {
            if (_window == null) return;

            _vaultTimer += dt;
            float t = _config.WindowEntryVaultSeconds <= 0f ? 1f : Mathf.Clamp01(_vaultTimer / _config.WindowEntryVaultSeconds);

            // Ikinci dereceden Bezier: disaridan pencere esigine, oradan iceriye.
            // Duz cizgi zombinin duvarin icinden gecmesi demek olurdu.
            Vector3 p = QuadraticBezier(_vaultFrom, _window.SillPoint, _window.InsidePoint, t);
            _transform.position = p;

            Vector3 facing = _window.InsidePoint - _window.SillPoint;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                _transform.rotation = Quaternion.LookRotation(facing);
            }
        }

        private void EndVault()
        {
            _hasEnteredBuilding = true;
            _navAgent.enabled = true;

            // Tirmanis bitisi NavMesh'in tam ustune denk gelmeyebilir; en yakin
            // gecerli noktaya oturtulur. Bu yapilmazsa ajan "not on NavMesh" olur ve
            // zombi oldugu yerde donar.
            if (NavMesh.SamplePosition(_transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                _navAgent.Warp(hit.position);
            }

            _repathTimer = float.MaxValue; // ilk dusunmede hemen yol istesin
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float inv = 1f - t;
            return inv * inv * a + 2f * inv * t * b + t * t * c;
        }

        // ---------------------------------------------------------------- sikisma

        /// <summary>
        /// Sıkışma kurtarması. <b>Her navigasyon hatasının tanımlı bir durumu olmalı</b>
        /// (ai-code.md): NavMesh dışına düşmek, ulaşılamaz hedef, geometri içinde kalmak.
        /// Hepsi olur.
        /// </summary>
        private void RecoverFromStuck()
        {
            if (!_navAgent.enabled) return;

            if (!_navAgent.isOnNavMesh)
            {
                // Yaricap KUCUK. Genis bir yaricapla SamplePosition duvarlari
                // umursamadan en yakin NavMesh noktasini bulur ve disarida sikismis
                // bir zombiyi binanin ICINE isinlayabilir - barikati hic gormeden.
                // Hala disaridaysa kendi penceresinin disina donmesi dogru olan.
                Vector3 anchor = !_hasEnteredBuilding && _window != null
                    ? _window.OutsidePoint
                    : _transform.position;

                if (NavMesh.SamplePosition(anchor, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    _navAgent.Warp(hit.position);
                }

                return;
            }

            _navAgent.ResetPath();
            _repathTimer = float.MaxValue;
        }

        // ---------------------------------------------------------------- saldiri ve hasar

        private void LandAttack()
        {
            if (_target == null || !_target.IsTargetable) return;
            _target.ReceiveAttack(_config.AttackDamage);
        }

        public DamageResult ApplyDamage(in DamageInfo damage)
        {
            if (!_initialized) return default;

            DamageResult result = _health.ApplyDamage(damage);

            if (result.Killed)
            {
                Die(damage.Kind, damage.Headshot);
                return result;
            }

            // Hasar emilmediyse (olu hedef) tepki de yok.
            if (result.Absorbed <= 0f) return result;

            // Kart etkisi: mermi degdikce zombi yavaslar. Carpan ISABET BASINA
            // yeniden hesaplanir, birikmez - biriken bir yavaslatma zombiyi durdurur
            // ve "Buz" karti (SYS-02) tam da o birikmeyi ayri bir kart olarak satar.
            float slow = CardSignals.Loadout.Total(CardStat.SlowOnHit);
            if (slow > 0f) _cardSlowMultiplier = Mathf.Clamp(1f - slow, 0.25f, 1f);

            // M1-13: vurusun bir karsiligi olmali. Sendeleme kararini beyin verir
            // (hazirlanan vurusu kesmek dahil), gorunur kismini burasi surer.
            _brain.NotifyHit(damage.Headshot);
            ApplyKnockback();
            ApplyDebugColor();

            return result;
        }

        /// <summary>
        /// İsabetin yönünü <b>görünür</b> kılar: zombi biraz geri iter.
        ///
        /// <para>Yön bilgisi taktiktir — oyuncu sürüyü bir hatta tutmayı bununla öğrenir.
        /// İtme <see cref="NavMeshAgent.Move"/> ile yapılır, transform'a yazılarak
        /// değil: doğrudan yazmak ajanı NavMesh'in dışına taşıyıp "not on NavMesh"
        /// durumuna sokabilir ve zombi olduğu yerde donar.</para>
        /// </summary>
        private void ApplyKnockback()
        {
            float distance = _config.HitReactionKnockbackMeters;
            if (distance <= 0f) return;
            if (!_navAgent.enabled || !_navAgent.isOnNavMesh) return;

            // Zombinin baktigi yonun tersi: oyuncu onu kovaladigi icin zaten oyuncuya
            // doner, yani "geri" pratikte atisin geldigi yondur.
            _navAgent.Move(-_transform.forward * distance);
        }

        private void Die(DamageKind kind, bool headshot)
        {
            _brain.Kill();

            if (_navAgent.enabled && _navAgent.isOnNavMesh) _navAgent.isStopped = true;
            _navAgent.enabled = false;

            // Ceset carpismasi kalirsa oyuncu ve diger zombiler olulere takilir.
            SetCollidersEnabled(false);

            // Oldurme HEMEN bildirilir: puan ve tur sayaci beklemez. Cesedin sahnede
            // kalmasi gorsel bir mesele, oyun mantiginin degil.
            Killed?.Invoke(this, kind, headshot);

            // M1-13: olumun bir ani olmali. Zombinin aninda yok olmasi, oldurmenin
            // SAYILMAMIS gibi hissettirdigi seydir - oyuncunun basardigi seyi gorecek
            // zamani olmaz.
            _deathTimer = 0f;
            _dying = _config.HitReactionDeathLingerSeconds > 0f;
            _deathStartRotation = _transform.rotation;

            if (!_dying) FinishDeath();
        }

        /// <summary>
        /// Yıkılma anı: zombi yana devrilir ve zemine gömülür. <b>Ragdoll değil</b> —
        /// gri kutuda fizik simülasyonu gereksiz; okunması gereken tek şey "bu artık
        /// ölü". Ceset görünümü sanat aşamasının işi.
        /// </summary>
        private void TickDying(float dt)
        {
            _deathTimer += dt;

            float duration = _config.HitReactionDeathLingerSeconds;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(_deathTimer / duration);

            // Once devril, sonra bat. Ikisi ayni anda olursa hangisinin oldugu okunmaz.
            _transform.rotation = Quaternion.Slerp(
                _deathStartRotation,
                _deathStartRotation * Quaternion.Euler(88f, 0f, 0f),
                Mathf.Clamp01(t * 2.5f));

            if (t > 0.6f)
            {
                float sink = (t - 0.6f) / 0.4f;
                Vector3 p = _transform.position;
                p.y = -1.2f * sink;
                _transform.position = p;
            }

            if (t >= 1f) FinishDeath();
        }

        private void FinishDeath()
        {
            _dying = false;
            Despawned?.Invoke(this);
            gameObject.SetActive(false);
        }

        private void SetCollidersEnabled(bool value)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null) _colliders[i].enabled = value;
            }
        }

        // ---------------------------------------------------------------- gorsel

        private void ApplyAnimatorCulling(bool far)
        {
            if (animator == null) return;

            // M0-04 olcumunun dogrudan sonucu: risk NavMesh'te degil animatorde.
            AnimatorCullingMode mode = far
                ? AnimatorCullingMode.CullCompletely
                : AnimatorCullingMode.CullUpdateTransforms;

            if (animator.cullingMode != mode) animator.cullingMode = mode;
        }

        /// <summary>
        /// Gri kutuda durumu <b>renkle</b> okutur. Sanat gelene kadar tek okunabilirlik
        /// aracımız bu; PILLAR-04 gri kutuda da geçerli. `renderer.material` <b>kullanılmaz</b>
        /// — o materyali klonlar ve her zombi ayrı çizim çağrısı olur (shader-graphics.md).
        /// </summary>
        private void ApplyDebugColor()
        {
            if (!debugVisuals || bodyRenderer == null) return;

            Color c = _brain == null ? Color.grey : _brain.State switch
            {
                ZombieState.Emerging => new Color(0.35f, 0.35f, 0.40f),
                ZombieState.ApproachingWindow => new Color(0.45f, 0.40f, 0.20f),
                ZombieState.Tearing => new Color(0.70f, 0.45f, 0.10f),
                ZombieState.Vaulting => new Color(0.85f, 0.65f, 0.10f),
                ZombieState.Chasing => new Color(0.45f, 0.20f, 0.20f),
                ZombieState.WindingUp => Color.Lerp(new Color(0.60f, 0.20f, 0.20f),
                                                    new Color(1.00f, 0.30f, 0.10f),
                                                    _brain.WindupProgress01),
                ZombieState.Striking => new Color(1.00f, 0.20f, 0.05f),
                ZombieState.Recovering => new Color(0.30f, 0.25f, 0.30f),
                ZombieState.Stuck => new Color(0.10f, 0.40f, 0.70f),
                _ => Color.black
            };

            // Sendeleme beyaza dogru bir parlama: durum renginin USTUNE binir, onun
            // yerine gecmez. Ayri bir renk olsaydi "vuruldu" ve "ne yapiyor" bilgisi
            // birbirini gizlerdi (PILLAR-04).
            if (_brain != null && _brain.IsFlinching)
            {
                c = Color.Lerp(c, Color.white, 0.65f);
            }

            bodyRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugVisuals || _brain == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _config?.AttackRangeMeters ?? 1.6f);

            if (_target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up, _target.Position);
            }

            if (_window != null && !_hasEnteredBuilding)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position + Vector3.up, _window.OutsidePoint);
            }
        }
    }
}
