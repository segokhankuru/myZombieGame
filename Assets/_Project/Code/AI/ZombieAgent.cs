using System;
using Bunker.Audio;
using Bunker.Systems.Ai;
using Bunker.Systems.Cards;
using Bunker.Systems.Config;
using Bunker.Systems.Combat;
using Bunker.Systems.Pickups;
using Bunker.Systems.Rounds;
using Bunker.Systems.Telemetry;
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

        [Header("Govde parcalari (surunme icin)")]
        [Tooltip("Butun gorselin kokü. Zombi surunmeye dustugunde EGILEN ve ALCALAN sey " +
                 "bu - tek tek parcalar degil.")]
        [SerializeField] private Transform visualRig;

        [Tooltip("Sol bacak. Koptugunda gizlenir ve yere bir parca dusurulur.")]
        [SerializeField] private GameObject legLeft;

        [Tooltip("Sag bacak.")]
        [SerializeField] private GameObject legRight;

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

        // --- suru cephesi (2026-09-05)
        // Bu zombinin serit numarasi. Dogum sirasindan gelir ve HAVUZDAN CIKARKEN
        // yeniden verilir: sabit kalsaydi, ayni havuz nesnesi her hayatinda ayni
        // seritte kosar ve dagilim zamanla bozulurdu.
        private int _swarmIndex;
        private float _swarmLateralMeters;

        /// <summary>
        /// Bu zombinin savaş günlüğündeki adı ("Zombi#42"). <b>Doğumda bir kez
        /// üretilir</b>; vuruş başına string kurmak, kalabalık bir turda saniyede
        /// yüzlerce tahsis demekti (csharp-code.md).
        /// </summary>
        private string _logName = "Zombi";

        /// <summary>Günlük ve teşhis adı. <see cref="ZombieTargetBeacon"/> da okur.</summary>
        public string LogName => _logName;

        // Dondurma boyamasi (2026-09-08). Renderer listesi ve ORIJINAL renkleri
        // Awake'te bir kez toplanir: cozulen zombi beyaza degil, KENDI rengine doner.
        // Beyaza dondurmek, materyalindeki her renk ayarini sessizce silerdi.
        private Renderer[] _visualRenderers;
        private Color[] _visualBaseColors;
        private bool _freezeTintApplied;

        // Dogum sayaci. Serit atamasi RASTGELE degil SIRAYLA yapilir (SwarmFormation):
        // rastgele olsaydi arka arkaya dogan uc zombi ayni tarafa dusebilirdi.
        private static int _swarmCursor;

        // --- surunme (2026-09-05) ---
        // Bacak basina emilen hasar. Esigi asan bacak KOPAR ve zombi surunmeye duser.
        private float _legDamageLeft;
        private float _legDamageRight;
        private bool _legLostLeft;
        private bool _legLostRight;
        private bool _crawling;

        private CapsuleCollider _bodyCollider;
        private Vector3 _rigHomePosition;
        private Quaternion _rigHomeRotation;
        private float _standHeight;
        private Vector3 _standCenter;
        private float _agentStandHeight;
        private float _agentBaseRadius;

        // Ortam homurtusu: arkadan gelen zombinin DUYULMASI icin. Her zombi kendi
        // sayacini tutar, hepsi ayni anda inlemesin diye rastgele baslar.
        private float _groanTimer;

        private bool _dying;
        private float _deathTimer;
        private Quaternion _deathStartRotation;

        private Vector3 _netTargetPosition;
        private float _netTargetYaw;
        private bool _hasNetTarget;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // --- boss (2026-09-06)
        // Boss ayri bir prefab DEGIL: ayni zombi, farkli carpanlarla. Zorluk egrisi
        // tek yerde (rounds.json) kalsin diye.
        private float _damageMultiplier = 1f;
        private float _bodyScale = 1f;

        /// <summary>Bu zombi boss mu. Ekonomi ve arayuz okur.</summary>
        public bool IsBoss { get; private set; }

        // --- bolgesel isabet parlamasi (2026-09-05)
        //
        // Once yalnizca GOVDE parliyordu: sendeleme rengi bodyRenderer'a yaziliyordu ve
        // kafaya da bacaga da vursan beyazlayan yer aynidiydi. Yani vurus kutulari
        // vardi ama oyuncu NEREYE vurdugunu goremiyordu - nisan almanin odulu ekranda
        // hic gorunmuyordu.
        //
        // Artik her vurus kutusunun kendi gorseli, kendi sayaciyla parlar.
        private Renderer[] _partRenderers;
        private Color[] _partBaseColors;
        private float[] _partFlashTimers;

        /// <summary>
        /// İsabet parlamasının süresi. <b>Denge değil sunum sayısı</b>
        /// (csharp-code.md'nin mühendislik sabiti istisnası): 120 ms, bir vuruşu
        /// okunur kılacak kadar uzun, arka arkaya isabetleri tek bir beyaz lekeye
        /// çevirmeyecek kadar kısa.
        /// </summary>
        private const float PartFlashSeconds = 0.12f;

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

        /// <summary>Bu zombinin kendisi. Vurus kutulari da buraya isaret eder.</summary>
        public IDamageable DamageRoot => this;
        public float HealthFraction01 => _initialized ? _health.Fraction01 : 0f;

        /// <summary>
        /// Sürünüyor mu (bacağı koptu). <b>Yürüme animasyonu buna bakar</b>: gövde
        /// zaten öne yatırılmışken bacaklara adım yazmak, sürünürken tekme atan bir
        /// şey üretirdi.
        /// </summary>
        public bool IsCrawling => _crawling;

        /// <summary>
        /// Vuruş hazırlığının ilerlemesi (0..1); hazırlık yoksa 0. <b>Animasyon bunu
        /// okur</b> — kolların geriye çekilmesi telegrafın kendisidir (ai-code.md).
        /// </summary>
        public float WindupProgress01 =>
            _brain != null && _brain.State == ZombieState.WindingUp
                ? _brain.WindupProgress01
                : 0f;

        /// <summary>
        /// Vuruş <b>indi mi ve üstünden ne kadar geçti</b> (0 = tam şimdi, 1 = açıklık
        /// bitti). Kolların ileri savrulması bu eğriden geliyor.
        /// </summary>
        public float StrikeProgress01
        {
            get
            {
                if (_brain == null || _config == null) return 1f;

                if (_brain.State == ZombieState.Striking) return 0f;

                if (_brain.State != ZombieState.Recovering) return 1f;

                float recovery = _config.AttackRecoverySeconds;
                return recovery <= 0f
                    ? 1f
                    : Mathf.Clamp01(_brain.StateTimeSeconds / recovery);
            }
        }

        /// <summary>Kalan can. <b>Geliştirme ölçümü</b> — can barının yazısı buradan.</summary>
        public float Health => _initialized ? _health.Current : 0f;

        /// <summary>Bu doğumdaki en yüksek can (tur ölçeklemesi uygulanmış hâli).</summary>
        public float MaxHealth => _initialized ? _health.Max : 0f;

        /// <summary>
        /// Bu zombinin <b>tek vuruşta</b> oyuncudan götürdüğü can — tur çarpanı dahil.
        ///
        /// <para>Geliştirici (2026-09-07): <i>"kaç hasar vurabiliyorlar onu da yaz,
        /// testler için önemli bir detay"</i>. Sayı hiçbir yerde görünmüyordu; "15.
        /// turda tek mi yiyorum" sorusu ancak ölerek cevaplanabiliyordu. Değerin
        /// sahibi hâlâ <c>zombie.json</c>; burası yalnızca okuyup gösteriyor.</para>
        /// </summary>
        public float AttackDamage =>
            _initialized && _config != null ? _config.AttackDamage * _damageMultiplier : 0f;

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

            // Ayakta duran halin olculeri BIR KEZ okunur: surunmeden ayaga donerken
            // geri yazilacak degerler bunlar. Prefab'tan her seferinde okumak yerine
            // burada saklamak, havuzdan cikan zombinin onceki hayatinin egik govdesini
            // tasimasini imkansiz kilar (systems-code.md).
            _bodyCollider = GetComponent<CapsuleCollider>();

            if (_bodyCollider != null)
            {
                _standHeight = _bodyCollider.height;
                _standCenter = _bodyCollider.center;
            }

            _agentStandHeight = _navAgent.height;
            _agentBaseRadius = _navAgent.radius;

            if (visualRig != null)
            {
                _rigHomePosition = visualRig.localPosition;
                _rigHomeRotation = visualRig.localRotation;
            }

            // Her zombi kendi fazinda dusunur. Ayni karede dusunen 40 zombi, o karede
            // 40 yol istegi demektir; kaydirmak ayni is yukunu zamana yayar.
            _thinkPhaseOffset = UnityEngine.Random.value;

            BuildPartRenderers();

            // Dondurma boyamasinin hedefi: GORSEL model. Vurus kutulari (goze
            // gorunmez ilkel sekiller) degil - onlar zaten parlama sisteminin isi.
            // Bir kez toplanir; kare basina GetComponentsInChildren yasak.
            Transform tintRoot = visualRig != null ? visualRig : transform;
            _visualRenderers = tintRoot.GetComponentsInChildren<Renderer>(true);
            _visualBaseColors = new Color[_visualRenderers.Length];

            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                Material shared = _visualRenderers[i] != null
                    ? _visualRenderers[i].sharedMaterial
                    : null;

                // sharedMaterial OKUNUR, material DEGIL: ikincisi materyali klonlar ve
                // her zombi ayri cizim cagrisi olur (shader-graphics.md).
                _visualBaseColors[i] = shared != null && shared.HasProperty(BaseColorId)
                    ? shared.GetColor(BaseColorId)
                    : Color.white;
            }
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
            Spawn(config, maxHealth, speedMetersPerSecond, entryWindow, position,
                  bossScale: 1f, damageMultiplier: 1f);
        }

        /// <summary>
        /// Boss doğumu (2026-09-06): aynı zombi, farklı çarpanlarla.
        ///
        /// <para><b>Neden ayrı bir prefab değil:</b> boss ayrı bir varlık olsaydı zorluk
        /// eğrisi iki yerden yönetilirdi — turun canı <c>rounds.json</c>'da, boss'unki
        /// başka bir dosyada, ve ikisi zamanla ayrışırdı. Boss <b>turun zombisinin
        /// katı</b> olarak tanımlı; eğri tek yerde kalıyor.</para>
        ///
        /// <para><b>Ölçek havuzdan çıkarken SIFIRLANIR</b>: aksi hâlde bir boss öldükten
        /// sonra havuza dönen nesne, bir sonraki hayatında dev bir normal zombi olurdu
        /// (systems-code.md: havuzlanmış nesne önceki hayatından durum taşıyamaz).</para>
        /// </summary>
        public void Spawn(ZombieConfig config, float maxHealth, float speedMetersPerSecond,
                          WindowEntry entryWindow, Vector3 position,
                          float bossScale, float damageMultiplier)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            IsBoss = bossScale > 1.001f;
            _damageMultiplier = damageMultiplier <= 0f ? 1f : damageMultiplier;

            ApplyBodyScale(bossScale);

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

            // Suru cephesi: serit, hiz sapmasi ve kacinma onceligi (2026-09-05).
            // Ucu birlikte calisir - biri eksik olursa zombiler yine tek sira dizilir.
            _swarmIndex = _swarmCursor++;
            if (_swarmCursor > 100000) _swarmCursor = 0;   // tasma yok, sayac dolaninca basa

            // Gunluk adi dogumda bir kez kurulur (savas gunlugu, 2026-09-08).
            _logName = (IsBoss ? "Boss#" : "Zombi#") + _swarmIndex;

            _swarmLateralMeters = SwarmFormation.LateralOffsetMeters(
                _swarmIndex, _config.SwarmLateralSpreadMeters);

            _navAgent.avoidancePriority = SwarmFormation.AvoidancePriority(_swarmIndex);

            _baseSpeed = speedMetersPerSecond *
                         SwarmFormation.SpeedMultiplier(_swarmIndex, _config.SwarmSpeedJitter01);

            // Havuzdan cikan zombi onceki hayatinin yavaslatmasini TASIMAZ
            // (systems-code.md: pooled nesnenin sifirlama sozlesmesi). Bu satir
            // olmasaydi bir saat oynadiktan sonra ortaya cikan turden bir hata olurdu:
            // yeni dogan zombiler sebepsiz yavas.
            _cardSlowMultiplier = 1f;

            // Havuzdan cikan zombi onceki hayatinin beyaz parlamasini da tasimaz.
            ClearPartFlashes();

            // ...ne de onceki hayatinin buz rengini. Bayragi sifirlamak YETMEZ:
            // renderer'da onceki hayatin mavi blogu duruyor olabilir ve karsilastiran
            // bir kontrol "zaten dogru" diye gecerdi. Renk KOSULSUZ yazilir
            // (systems-code.md: havuzdan cikan nesne durum tasiyamaz).
            _freezeTintApplied = PowerupState.IsFreezeActive;
            ApplyFreezeTint(_freezeTintApplied);

            // Havuzdan cikan zombi ONCEKI HAYATININ KOPMUS BACAGIYLA dogamaz. Bu
            // sifirlama olmasaydi bir saat oynadiktan sonra havuzun tamami surunen
            // zombilerden olusurdu ve sebebi cok uzakta gorunurdu.
            RestoreLimbs();

            // Homurtu sayaci rastgele baslar: kirk zombinin ayni anda inlemesi tek bir
            // ugultu olur ve YON bilgisi kaybolur - sesin butun isi zaten o.
            _groanTimer = UnityEngine.Random.Range(0.5f, 4f);

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

            // Isabet parlamalari her kare soner - vekilde de, olurken de: parlamayi
            // dusunme adimina baglamak, 125 ms'lik beyaz lekeler uretirdi.
            TickPartFlashes(Time.deltaTime);

            // Dondurma GORUNUR olmali (2026-09-08). Vekil zombide de: dondurma
            // sahaya ait bir etki, sahnedeki her zombiyi ilgilendirir.
            TickFreezeTint();

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

            float gapToTarget = _target != null ? GapToTarget() : float.MaxValue;

            // Mutlak tavanin olcusu (2026-09-10): yaricaplar dusulmeden merkez mesafesi.
            float distanceToTarget = _target != null ? HorizontalDistanceToTarget() : float.MaxValue;

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
                gapToTargetMeters: gapToTarget,
                needsWindowEntry: needsWindow,
                distanceToWindowMeters: distanceToWindow,
                actualSpeedMetersPerSecond: actualSpeed,
                windowBlocked: windowBlocked,
                reachMultiplier: _bodyScale,
                distanceToTargetMeters: distanceToTarget,
                isBoss: IsBoss);

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
                // Surunme carpani da CARPILIR: bacagi kopmus ve ayrica yavaslatilmis
                // bir zombi iki etkiyi birden tasimali.
                // Esya etkisi (2026-09-07): yavaslatma/dondurma SAHAYA aittir, tek
                // sayacta durur (PowerupState) ve her zombi ona bakar. Zombi basina
                // sayac tutulsaydi, esya toplandiktan SONRA dogan zombi etkilenmez
                // ve kural ogrenilemezdi. Dondurmada carpan sifir: zombi durur.
                _navAgent.speed = _baseSpeed * _brain.SpeedMultiplier * _cardSlowMultiplier *
                                  PowerupState.ZombieSpeedMultiplier *
                                  (_crawling ? _config.CrawlSpeedMultiplier : 1f);
            }

            TickGroan(thinkDelta);
            DriveMovement(thinkDelta);
        }

        private void OnStateChanged(ZombieState from, ZombieState to)
        {
            switch (to)
            {
                case ZombieState.Vaulting:
                    BeginVault();
                    GameAudio.PlayAt(SfxId.ZombieVault, _transform.position);
                    break;

                case ZombieState.Stuck:
                    RecoverFromStuck();
                    break;

                case ZombieState.WindingUp:
                    // Telegrafın SESLİ yarısı. Ekrandaki renk değişimi yalnızca zombiye
                    // BAKAN oyuncuya bir şey söyler; arkadan gelen zombinin hazırlığı
                    // yalnızca sesle okunabilir (ai-code.md: her anlamlı eylemin
                    // okunabilir bir hazırlığı olmalı).
                    GameAudio.PlayAt(SfxId.ZombieAttack, _transform.position);
                    break;

                case ZombieState.Chasing when from == ZombieState.Emerging ||
                                              from == ZombieState.Vaulting:
                    GameAudio.PlayAt(SfxId.ZombieAlert, _transform.position);
                    break;
            }

            if (from == ZombieState.Vaulting) EndVault();
        }

        /// <summary>
        /// Aralıklı homurtu. <b>Oyunun en ucuz gerilim aracı ve M-01'in en büyük
        /// eksiği:</b> arkadan gelen zombi duyulmuyorsa oyuncunun arkasını dönmesi için
        /// bir sebep yoktur, yani harita bilgisi tek yönlü kalır.
        ///
        /// <para>Ses 3B: yön ve mesafe taşır. Aralık rastgele, çünkü düzenli aralıklı
        /// bir ses birkaç dakika sonra duyulmaz olur.</para>
        /// </summary>
        private void TickGroan(float dt)
        {
            _groanTimer -= dt;
            if (_groanTimer > 0f) return;

            _groanTimer = UnityEngine.Random.Range(3.5f, 8f);

            // Yalnizca binaya girmis ya da yaklasan zombi inler; henuz belirmekte olan
            // zombinin sesi, oyuncuya daha dogmadan yer bildirirdi.
            if (_brain.State == ZombieState.Emerging || _brain.State == ZombieState.Dead) return;

            // OLUM EKRANINDA SESSIZLIK (2026-09-08, gelistirici: "olum ekraninda
            // zombilerin ugultusunu kes"). Homurtu bir GERILIM araci: arkani donmen
            // icin bir sebep. Run bittikten sonra donulecek bir arka yok - sicil
            // ekranini okurken devam eden ugultu, gerilim degil gurultu ve oyuncunun
            // "bitti" hissini geciktiriyor. Zombiler sahnede duruyor, yalnizca
            // susuyorlar.
            if (RunSignals.IsRunOver) return;

            GameAudio.PlayAt(SfxId.ZombieGroan, _transform.position);
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
                ZombieMoveIntent.Player when _target != null => SpreadDestination(_target.GroundPosition),
                _ => _transform.position
            };

            // SetDestination asenkron yol hesabi tetikler; senkron yol hesabi yasak
            // (ai-code.md).
            _navAgent.SetDestination(destination);
        }

        /// <summary>
        /// Hedefi bu zombinin <b>şeridine</b> kaydırır (2026-09-05).
        ///
        /// <para>Kayma <b>yaklaşma yönüne dik</b>: sürü, oyuncuya doğru bir <i>cephe</i>
        /// hâlinde gelir, tek sıra bir konvoy hâlinde değil. Sabit bir dünya eksenine
        /// göre kaydırsaydık, oyuncu döndüğünde cephe anlamsız bir açıya düşerdi.</para>
        ///
        /// <para><b>Yakında şerit söner</b> (<see cref="SwarmFormation.SpreadWeight01"/>):
        /// sönmeseydi zombi oyuncunun yanına gidip orada durur, saldıramazdı.</para>
        ///
        /// <para><b>Kaydırılan nokta NavMesh'e oturtulur.</b> Duvarın içine düşen bir
        /// hedef, ajanı "ulaşılamaz hedef" durumuna sokar ve zombi olduğu yerde
        /// bekler — ai-code.md'nin dört navigasyon hatasından biri. Oturmuyorsa şerit
        /// bırakılır ve doğrudan oyuncuya gidilir; dar koridorda doğru davranış budur.</para>
        /// </summary>
        private Vector3 SpreadDestination(Vector3 targetGround)
        {
            if (Mathf.Approximately(_swarmLateralMeters, 0f)) return targetGround;

            Vector3 toTarget = targetGround - _transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance < 0.001f) return targetGround;

            float weight = SwarmFormation.SpreadWeight01(distance, _config.SwarmSpreadFadeDistanceMeters);
            if (weight <= 0f) return targetGround;

            Vector3 forward = toTarget / distance;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 shifted = targetGround + right * (_swarmLateralMeters * weight);

            // Yaricap KUCUK: genis bir ornekleme duvarin obur tarafina denk gelebilir
            // ve zombiyi odanin disina yollardi (RecoverFromStuck ile ayni ders).
            return NavMesh.SamplePosition(shifted, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
                ? hit.position
                : targetGround;
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

        /// <summary>
        /// Vuruş iner — <b>ama önce mesafe BİR KEZ DAHA ölçülür</b> (2026-09-06 hatası).
        ///
        /// <para><b>Bulunan hata:</b> beyin mesafeyi <i>düşünme adımında</i> ölçüyordu
        /// (saniyede 8 kez, yani 125 ms'de bir) ve vuruş o ölçüme göre iniyordu. Koşan
        /// bir oyuncu 125 ms'de 0,94 metre gidiyor; menzil toleransıyla (0,6 m) birlikte
        /// bu, <b>üç metre uzaktan yenen bir vuruş</b> demekti. Geliştiricinin
        /// <i>"mesafe varken bir şekilde hit yiyorum"</i> cümlesi tam olarak buydu.</para>
        ///
        /// <para><b>Boss'ta daha da beterdi:</b> boss 1,7 kat büyük ama menzili normal
        /// zombiyle aynıydı — yani gövdesi çok daha uzaktayken, merkezleri arasındaki
        /// mesafe hâlâ menzilin içindeydi. Menzil artık <b>gövde ölçeğiyle</b> birlikte
        /// büyüyor: büyük bir yaratığın uzun kolu olması okunabilir, "görünmeyen bir
        /// menzil" değil.</para>
        ///
        /// <para><b>Neden iki kontrol (beyinde ve burada):</b> beyindeki kontrol
        /// <i>niyeti</i> belirler — vuruşu başlatmaya değer mi. Buradaki kontrol
        /// <i>sonucu</i> belirler — hâlâ menzilde mi. Telegrafın bedeli budur: oyuncu
        /// hazırlığı görüp geri çekilirse vuruş ISKALAMALI (ai-code.md).</para>
        /// </summary>
        private void LandAttack()
        {
            if (_target == null || !_target.IsTargetable) return;

            // MUTLAK TAVAN (2026-09-10, gelistirici: "zombiler 1 m'den uzaktan vuramaz,
            // bosslarda 2 m"). Beyin ayni soruyu dusunme adiminda sordu; burada inis
            // karesinin GUNCEL konumuyla bir kez daha - iki kontrolun gerekcesi yukarida.
            // Olcu yaricapsiz merkez mesafesi: bir yaricap yanlis olculse bile tavan tutar.
            float hardCap = IsBoss
                ? _config.AttackBossMaxHitDistanceMeters
                : _config.AttackMaxHitDistanceMeters;

            if (HorizontalDistanceToTarget() > hardCap) return;

            // Menzil GOVDE OLCEGIYLE buyur: boss'un kolu da buyuk.
            float reach = (_config.AttackRangeMeters + _config.AttackRangeToleranceMeters)
                          * _bodyScale;

            if (GapToTarget() > reach)
            {
                // Iskaladi: oyuncu telegrafi okuyup cekildi. Sessiz kalmasi dogru -
                // "hicbir sey olmamasi" zaten iskalamanin geri bildirimi.
                return;
            }

            // ONDEKI ZOMBININ ARKASINDAN VURULMAZ (2026-09-06, oyun testi).
            //
            // <b>Bulgu:</b> <i>"yigin oldugu zaman canin fullken birden oluyorsun"</i>.
            // Sebep buydu: sira halinde dizilen zombilerin hepsi menzil icinde
            // sayiliyordu ve ayni karede vuruyorlardi. Oyuncu tek bir zombi goruyor,
            // arkasindaki ucunun vurusunu ayni anda yiyordu - okunamayan bir olum.
            //
            // <b>Neden gorus kontrolu, hasar tavani degil:</b> ikinci vurusu yutmak
            // sonucu gizler, sebebi degil - zombi hala vurmus olur ve oyuncu neden
            // hasar almadigini da anlamaz. Burada vurus HIC OLMAZ, cunku gercekten
            // olmamali: arada baska bir govde var.
            // DONMUS ZOMBI VURMAZ (2026-09-07). Hizi sifira inen bir zombinin yerinde
            // durup vurmaya devam etmesi, "dondurma" esyasinin vaadini yalanlar -
            // oyuncu esyayi alir ve yine hasar yer.
            if (PowerupState.IsFreezeActive) return;

            if (IsBlocked()) return;

            // Boss daha agir vurur: carpan dogum aninda verildi (rounds.json boss).
            _target.ReceiveAttack(_config.AttackDamage * _damageMultiplier, _transform.position,
                                  _logName);
        }

        /// <summary>
        /// Zombinin gövdesiyle hedefin gövdesi arasındaki <b>yatay boşluk</b>.
        ///
        /// <para><b>Merkez mesafesi değil</b>: iki yarıçap düşülür. Gerekçe
        /// <see cref="ZombieSenses.GapToTargetMeters"/>'te.</para>
        ///
        /// <para><b>Yükseklik farkı bir yere kadar yok sayılır</b> (2026-09-06, oyun
        /// testi). Rampada, eşikte ya da bir basamak üstünde duran oyuncu dokunulmaz
        /// olmamalı — o yüzden küçük fark önemsenmiyor. Ama sınırsız yok saymak,
        /// <b>alt kattaki zombinin üst kattaki oyuncuya duvarın içinden vurması</b>
        /// demekti; geliştirici tam olarak bunu yaşadı: <i>"üst kata yerleştim, duvar
        /// dibinde, alttan hasar yedim."</i></para>
        ///
        /// <para>Sınır zombinin <b>boyuyla</b> ölçülür — uzanabileceği yer kadar. Kat
        /// yüksekliği bunun üstünde olduğu için kat arası vuruş imkânsız, basamak
        /// farkı ise hâlâ serbest. Sabit bir metre yazmak, boss ölçeklendiğinde ya da
        /// kat yüksekliği değiştiğinde sessizce yanlış olurdu.</para>
        /// </summary>
        private float GapToTarget()
        {
            float distance = HorizontalDistanceToTarget();
            if (distance == float.MaxValue) return float.MaxValue;

            return distance
                   - _navAgent.radius              // zaten _bodyScale ile olceklenir
                   - _target.BodyRadiusMeters;
        }

        /// <summary>
        /// Zombinin merkeziyle hedefin merkezi arasındaki <b>yatay mesafe</b>; dikeyde
        /// uzanamayacağı kadar uzaksa <c>float.MaxValue</c>. 2026-09-10.
        ///
        /// <para>Gövde boşluğu (<see cref="GapToTarget"/>) ve mutlak tavan bu tek ölçüden
        /// türer — dikey sınır iki yerde ayrı yazılsaydı, biri değiştiğinde kat arası
        /// vuruş hatası (2026-09-06) sessizce geri gelirdi.</para>
        /// </summary>
        private float HorizontalDistanceToTarget()
        {
            Vector3 toTarget = _target.GroundPosition - _transform.position;

            // Dikey sinir: zombinin boyunun yarisi kadar uzanabilir. Asilirsa vurus
            // ISKALAR - "cok uzakta" demekle ayni sey, cunku gercekten oyle.
            float verticalReach = _navAgent.height * 0.5f;

            if (Mathf.Abs(toTarget.y) > verticalReach) return float.MaxValue;

            toTarget.y = 0f;
            return toTarget.magnitude;
        }

        /// <summary>
        /// Zombiyle hedefi arasında <b>herhangi bir şey</b> duruyor mu — başka bir
        /// zombi ya da <b>geometri</b>.
        ///
        /// <para><b>Yalnızca vuruş indiği karede çalışır</b>, her karede değil: saniyede
        /// en fazla bir kez, zombi başına (vuruş açıklığı 0,9 sn). 40 zombide bile
        /// ölçülebilir bir maliyeti yok (ai-code.md: algı bütçeye tabidir).</para>
        ///
        /// <para><b>2026-09-07: geometri de sayılır.</b> Önceki sürüm bilerek yalnızca
        /// gövdelere bakıyordu ve gerekçesi <i>"duvar zaten yolu keser, zombi oraya
        /// ulaşamaz"</i> idi. O gerekçe <b>düz zeminde doğru, rampada yanlıştı</b>:
        /// geliştirici rampanın üstünde dururken alttan hasar yedi. Zombi rampanın
        /// altından geçebiliyor, dikey fark bir zombi boyunun altında kalıyor ve
        /// aradaki rampa yüzeyi hiç sorulmuyordu. Aynı hata sınıfı daha önce kat
        /// arasında yaşanmış ve orada dikey sınırla kapatılmıştı; asıl soru "arada bir
        /// şey var mı" olduğu için doğru cevap burada.</para>
        ///
        /// <para><b>Katman maskesi yok, sahibi sorulur:</b> ışın her şeye çarpar, sonra
        /// çarptığı şeyin <i>kendisi</i> mi, <i>hedefi</i> mi olduğu sorulur. Maske
        /// olsaydı kural, sahne katman kurulumuna bağlı olurdu — yeni bir platform
        /// yanlış katmanda doğduğu gün, sebebi görünmeden geri gelen bir hata.</para>
        /// </summary>
        private bool IsBlocked()
        {
            Vector3 from = _transform.position;
            from.y += _navAgent.height * 0.5f;      // gogus hizasi

            Vector3 to = _target.Position;
            Vector3 delta = to - from;
            float distance = delta.magnitude;

            if (distance < 0.01f) return false;     // ic ice: engel olamaz

            Transform targetRoot = _target.transform.root;

            int count = Physics.RaycastNonAlloc(from, delta / distance, _blockHits,
                                                distance, Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _blockHits[i].collider;
                if (hit == null) continue;

                // Kendi carpisanlari sayilmaz - zombi kendi govdesinin arkasinda
                // duramaz.
                if (hit.GetComponentInParent<ZombieAgent>() == this) continue;

                // Hedefin kendi carpisanlari da sayilmaz: isin oyuncunun govdesine
                // carpiyor olmasi, "arada bir sey var" demek degil.
                if (hit.transform.IsChildOf(targetRoot)) continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Görüş kontrolünün tamponu. <b>Bir kez ayrılır</b>, her vuruşta yeniden değil
        /// (csharp-code.md: kare başına tahsis yok).
        ///
        /// <para>Sekiz yeterli: sonuç "arada bir gövde var mı" sorusunun evet/hayır
        /// cevabı. Tampon dolarsa zaten en az bir engel bulunmuş demektir.</para>
        /// </summary>
        private readonly RaycastHit[] _blockHits = new RaycastHit[8];

        public DamageResult ApplyDamage(in DamageInfo damage) =>
            ApplyDamageToPart(damage, ZombiePart.Body);

        /// <summary>
        /// Hasarı <b>nereye geldiğini bilerek</b> uygular. Vuruş kutusu çağırır
        /// (<see cref="ZombieHitbox"/>); silah hangi parçaya isabet ettiğini bilmez.
        ///
        /// <para><b>Bacak ayrı sayılır:</b> yeterince hasar emen bacak kopar ve zombi
        /// ölmeden sürünmeye düşer. Bu, nişan almanın ikinci ödülüdür — kafa bitirir,
        /// bacak yavaşlatır — ve kalabalığın içine ritim farkı koyar.</para>
        /// </summary>
        public DamageResult ApplyDamageToPart(in DamageInfo damage, ZombiePart part)
        {
            if (!_initialized) return default;

            DamageResult result = _health.ApplyDamage(damage);

            // SAVAS GUNLUGU - zombiye gelen her hasarin TEK cikis noktasi
            // (2026-09-08). Cagri noktalarina degil buraya yazilmasinin sebebi
            // DamageInfo.Source'ta anlatiliyor: bes ayri yere bes satir koymak,
            // birinin unutuldugu gun gunlugu sessizce eksik birakirdi.
            if (CombatLog.IsInstalled && result.Absorbed > 0f)
            {
                CombatLog.Damage(damage.Source ?? damage.Kind.ToString(), null,
                                 _logName, PartName(part), result.Absorbed,
                                 _health.Current, _health.Max,
                                 result.Killed, damage.Headshot);
            }

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
            float slow = RunModifiers.Total(CardStat.SlowOnHit);
            // Taban 0.15 (2026-09-05): "Don" gibi ust kademe kartlarin gercekten
            // DONDURABILMESI icin. 0.25 tabani, kartlarin toplami ne olursa olsun
            // zombiyi yuruyebilir birakiyordu ve ust kademe kart hissedilmiyordu.
            if (slow > 0f) _cardSlowMultiplier = Mathf.Clamp(1f - slow, 0.15f, 1f);

            // M1-13: vurusun bir karsiligi olmali. Sendeleme kararini beyin verir
            // (hazirlanan vurusu kesmek dahil), gorunur kismini burasi surer.
            _brain.NotifyHit(damage.Headshot);
            ApplyKnockback(damage);
            ApplyDebugColor();

            // Parlama durum renginden SONRA: tersi sirada durum rengi parlamayi ayni
            // karede silerdi.
            FlashPart(part);

            GameAudio.PlayAt(SfxId.ZombieHurt, _transform.position);

            AccumulateLimbDamage(part, result.Absorbed);

            return result;
        }

        /// <summary>
        /// Vuruş kutusunun günlükteki adı. <b>Sabit stringler</b> — <c>enum.ToString()</c>
        /// her çağrıda tahsis eder ve bu yol atış başına geçiliyor.
        /// </summary>
        private static string PartName(ZombiePart part) => part switch
        {
            ZombiePart.Head => "kafa",
            ZombiePart.LegLeft => "sol bacak",
            ZombiePart.LegRight => "sag bacak",
            _ => "govde"
        };

        // ---------------------------------------------------------------- surunme

        /// <summary>
        /// Bacağa gelen hasarı biriktirir ve eşiği aşınca bacağı koparır.
        ///
        /// <para><b>Eşik tur canına oranlıdır</b>, sabit bir sayı değil: 25 000 canlı
        /// bir tur 30 zombisinin bacağını iki mermide koparmak, geç turlarda sürünün
        /// tamamını yerde sürünen bir kalabalığa çevirirdi.</para>
        ///
        /// <para>Ölüm bunun önündedir: yeterli hasar zaten zombiyi öldürür. Yani bacak
        /// koparmak, <b>öldürmeye yetmeyen</b> isabetlerin ödülüdür.</para>
        /// </summary>
        private void AccumulateLimbDamage(ZombiePart part, float absorbed)
        {
            if (absorbed <= 0f) return;
            if (part != ZombiePart.LegLeft && part != ZombiePart.LegRight) return;

            float threshold = _health.Max * _config.CrawlLegBreakHealthFraction;
            if (threshold <= 0f) return;

            if (part == ZombiePart.LegLeft)
            {
                if (_legLostLeft) return;

                _legDamageLeft += absorbed;
                if (_legDamageLeft >= threshold) BreakLeg(true);
            }
            else
            {
                if (_legLostRight) return;

                _legDamageRight += absorbed;
                if (_legDamageRight >= threshold) BreakLeg(false);
            }
        }

        private void BreakLeg(bool left)
        {
            GameObject leg = left ? legLeft : legRight;

            if (left) _legLostLeft = true;
            else _legLostRight = true;

            if (leg != null)
            {
                DropSeveredLeg(leg);
                leg.SetActive(false);
            }

            GameAudio.PlayAt(SfxId.ZombieLegBreak, _transform.position);

            if (!_crawling) EnterCrawl();
        }

        /// <summary>
        /// Kopan bacağı yere düşürür. <b>Zombinin kendi bacağı kullanılmaz</b> —
        /// o havuzlanmış bir nesnenin parçasıdır ve sahneye bırakılırsa bir sonraki
        /// doğumda geri gelmez (systems-code.md: havuzun sıfırlama sözleşmesi).
        /// </summary>
        private void DropSeveredLeg(GameObject source)
        {
            var renderer = source.GetComponent<Renderer>();
            if (renderer == null) return;

            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            piece.name = "SeveredLeg";
            piece.transform.SetPositionAndRotation(source.transform.position,
                                                   source.transform.rotation);
            piece.transform.localScale = source.transform.lossyScale;

            var pieceRenderer = piece.GetComponent<Renderer>();
            pieceRenderer.sharedMaterial = renderer.sharedMaterial;
            pieceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Carpistirici kapali: yerdeki bir bacaga oyuncunun ya da baska zombilerin
            // takilmasi, okunabilirligi (PILLAR-04) bir goruntu ucuna satmak olurdu.
            Destroy(piece.GetComponent<Collider>());

            // Kisa omur: cesetler gibi bunlar da birikirse hem kare butcesi hem
            // okunabilirlik bozulur.
            Destroy(piece, 8f);
        }

        /// <summary>
        /// Zombiyi sürünmeye düşürür: gövde eğilir, alçalır, yavaşlar.
        ///
        /// <para><b>Çarpıştırıcı da alçalır.</b> Yalnızca görseli eğmek, yerde sürünen
        /// bir zombiyi havada duran görünmez bir kapsülden vurulur hâlde bırakırdı —
        /// oyuncunun "vuruyorum ama gitmiyor" diye okuyacağı cinsten bir hata.</para>
        /// </summary>
        private void EnterCrawl()
        {
            _crawling = true;

            float height = Mathf.Max(0.3f, _config.CrawlBodyHeightMeters);

            if (visualRig != null)
            {
                // Kok kalca hizasinda duruyor (prefab boyle kuruluyor), yani buradan
                // dondurmek govdeyi ONE ve ASAGI yatirir - ayaklarindan dondurmek
                // zombiyi bir metre one atardi.
                visualRig.localPosition = new Vector3(_rigHomePosition.x, height * 0.5f, _rigHomePosition.z);
                visualRig.localRotation = _rigHomeRotation * Quaternion.Euler(75f, 0f, 0f);
            }

            if (_bodyCollider != null)
            {
                _bodyCollider.height = height;
                _bodyCollider.center = new Vector3(_standCenter.x, height * 0.5f, _standCenter.z);
            }

            if (_navAgent != null) _navAgent.height = height;
        }

        /// <summary>Ayaklara ve ayakta duran gövdeye geri döner. Yalnızca doğumda.</summary>
        private void RestoreLimbs()
        {
            _legDamageLeft = 0f;
            _legDamageRight = 0f;
            _legLostLeft = false;
            _legLostRight = false;
            _crawling = false;

            if (legLeft != null) legLeft.SetActive(true);
            if (legRight != null) legRight.SetActive(true);

            if (visualRig != null)
            {
                visualRig.localPosition = _rigHomePosition;
                visualRig.localRotation = _rigHomeRotation;
            }

            if (_bodyCollider != null && _standHeight > 0f)
            {
                _bodyCollider.height = _standHeight;
                _bodyCollider.center = _standCenter;
            }

            if (_navAgent != null && _agentStandHeight > 0f) _navAgent.height = _agentStandHeight;
        }

        /// <summary>
        /// İsabetin yönünü <b>görünür</b> kılar: zombi merminin gittiği yöne itilir.
        ///
        /// <para><b>Vuruşun geldiği yerden itilir, zombinin baktığı yönden değil</b>
        /// (2026-09-06, oyun testi: <i>"zombilere arkadan vurunca kendisince geri
        /// sekiyor, vuruş yönümün tersine hareket etmiş oluyor"</i>). Önceki sürüm
        /// <c>-transform.forward</c> kullanıyordu ve gerekçesi <i>"zombi zaten oyuncuya
        /// döner"</i>ydi — barikat sökerken, başka bir oyuncuyu kovalarken ya da
        /// arkadan vurulduğunda bu doğru değil. O durumlarda zombi <b>ateş edene
        /// doğru</b> itiliyordu; mermi onu çekiyormuş gibi.</para>
        ///
        /// <para><b>Kaynak yoksa eski davranış:</b> patlama ve barikat hasarı yön
        /// taşımıyor. Yönsüz bir vuruşta "geri" hâlâ makul bir tahmin.</para>
        ///
        /// <para>İtme <see cref="NavMeshAgent.Move"/> ile yapılır, transform'a yazılarak
        /// değil: doğrudan yazmak ajanı NavMesh'in dışına taşıyıp "not on NavMesh"
        /// durumuna sokabilir ve zombi olduğu yerde donar.</para>
        /// </summary>
        private void ApplyKnockback(in DamageInfo damage)
        {
            float distance = _config.HitReactionKnockbackMeters;
            if (distance <= 0f) return;
            if (!_navAgent.enabled || !_navAgent.isOnNavMesh) return;

            Vector3 push = -_transform.forward;

            if (damage.HasSource)
            {
                Vector3 fromSource = _transform.position -
                                     new Vector3(damage.SourceX, _transform.position.y, damage.SourceZ);

                // Cok yakinsa yon guvenilmez (sifira bolme): eski davranisa dusulur.
                if (fromSource.sqrMagnitude > 0.01f) push = fromSource.normalized;
            }

            _navAgent.Move(push * distance);
        }

        private void Die(DamageKind kind, bool headshot)
        {
            _brain.Kill();

            if (_navAgent.enabled && _navAgent.isOnNavMesh) _navAgent.isStopped = true;
            _navAgent.enabled = false;

            // Ceset carpismasi kalirsa oyuncu ve diger zombiler olulere takilir.
            SetCollidersEnabled(false);

            GameAudio.PlayAt(SfxId.ZombieDeath, _transform.position);

            // Oldurme HEMEN bildirilir: puan ve tur sayaci beklemez. Cesedin sahnede
            // kalmasi gorsel bir mesele, oyun mantiginin degil.
            Killed?.Invoke(this, kind, headshot);

            // YIKIM kartlari: olen zombi patlar (M-03'un eksik kalan etiketi).
            // Oldurme bildiriminden SONRA: patlamanin oldurdukleri de kendi puanlarini
            // yazar ve zincirleme patlama mumkun olur - ama once bu olum sayilir.
            TryExplode();

            // M1-13: olumun bir ani olmali. Zombinin aninda yok olmasi, oldurmenin
            // SAYILMAMIS gibi hissettirdigi seydir - oyuncunun basardigi seyi gorecek
            // zamani olmaz.
            _deathTimer = 0f;
            _dying = _config.HitReactionDeathLingerSeconds > 0f;
            _deathStartRotation = _transform.rotation;

            if (!_dying) FinishDeath();
        }

        /// <summary>
        /// Ölen zombi patlar — <b>Yıkım</b> kartları (2026-09-05).
        ///
        /// <para><b>Neden burada, silahta değil:</b> patlamayı öldüren <i>şey</i>
        /// tetiklemez, <b>ölüm</b> tetikler. Silahta olsaydı bıçakla, patlamayla ya da
        /// barikatla ölen zombi patlamazdı ve kart "bazen çalışan" bir karta dönerdi —
        /// oyuncunun kuralı öğrenemediği en kötü tür.</para>
        ///
        /// <para><b>Zincirleme patlama serbest ve bilinçli:</b> patlamanın öldürdüğü
        /// zombi de patlar. Sürünün ortasında tek bir öldürmenin zinciri başlatması,
        /// kartın vaat ettiği andır. Sonsuz döngü riski yok: her zombi yalnızca bir kez
        /// ölür (<c>HealthPool</c> ölümü bir kez bildirir).</para>
        ///
        /// <para><b>Kendi kendini vurmaz:</b> patlayan zombi zaten ölü ve
        /// <c>IsAlive</c> false; kutuları da kapatıldı.</para>
        /// </summary>
        private void TryExplode()
        {
            float power = RunModifiers.Total(CardStat.ExplodeOnKill);
            if (power <= 0f) return;

            // BOSS DAHA BUYUK PATLAR (2026-09-06, gelistirici: "boss olurse onun
            // patlama efekti zombilere gore daha etkili olmali, sonucta boss bu").
            //
            // Hasar zaten olceklenıyordu - <c>_health.Max</c> boss'ta cok daha yuksek.
            // Ama YARICAP sabitti: alti kat hasar veren bir patlama, normal zombiyle
            // ayni daireye siginiyordu. Buyuk bir seyin patlamasi buyuk gorunmeli;
            // aksi halde odul ekranda hic okunmuyor.
            //
            // <b>Yeni bir denge sayisi YOK</b> (config-data.md: hesaplanan deger
            // saklanmaz): olcek zaten dogum aninda verilen <c>_bodyScale</c>.
            float radius = _config.CardsExplosionRadiusMeters * _bodyScale;
            if (radius <= 0f) return;

            float damage = _health.Max * power;
            if (damage <= 0f) return;

            // Parca sayisi da olceklenir. Sabit kalsaydi genis yaricapta parcalar
            // SEYRELIR ve boss patlamasi, buyudugu halde daha az sey vuran bir
            // patlamaya donusurdu - tam tersi bir his.
            int shrapnel = Mathf.RoundToInt(_config.CardsExplosionShrapnelCount * _bodyScale);
            if (shrapnel <= 0) return;

            GameAudio.PlayAt(SfxId.Explosion, _transform.position);
            ExplosionFlash.Show(_transform.position, radius);

            // Parca basina hasar: TOPLAM hasar parca sayisina bolunur. Yakindaki
            // zombi cok parca yer, koseden bakan bir tane - "isabet alan hasar alir".
            float perShrapnel = damage / shrapnel;

            Vector3 origin = _transform.position + Vector3.up * 0.9f;
            float selfFraction = _config.CardsExplosionSelfDamageFraction01;

            for (int i = 0; i < shrapnel; i++)
            {
                Vector3 direction = ShrapnelDirection(i, shrapnel);

                // HER PARCA BIR ISIN: duvar onu DURDURUR (2026-09-06 bulgusu -
                // "patlama yuzeyi asiyor ve arkasindakilere hasar veriyor"). Kure
                // sorgusu geometriyi hic gormuyordu; isin gorur.
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, radius,
                                     Bunker.Config.GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                var target = hit.collider.GetComponent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                IDamageable identity = target.DamageRoot ?? target;
                if (identity == (IDamageable)this) continue;

                // OYUNCU AYRI HESAPLANIR (2026-09-05 hatasi): ilk surum patlamayi
                // yaricaptaki HERKESE uyguluyordu ve oyuncu da bir IDamageable.
                // Sarapnel karti alan oyuncu, yanindaki zombiyi oldurdugu anda
                // aninda oluyordu. Oran zombie.json'da ve varsayilani SIFIR.
                if (!(identity is ZombieAgent))
                {
                    if (selfFraction <= 0f) continue;

                    target.ApplyDamage(new DamageInfo(perShrapnel * selfFraction,
                                                      DamageKind.Environment, false,
                                                      _transform.position.x,
                                                      _transform.position.z,
                                                      ShrapnelSource));
                    continue;
                }

                target.ApplyDamage(new DamageInfo(perShrapnel, DamageKind.Environment,
                                                  false, 0f, 0f, ShrapnelSource));
            }
        }

        /// <summary>Şarapnelin günlükteki adı. Sabit: atış başına tahsis yok.</summary>
        private const string ShrapnelSource = "Patlama";

        /// <summary>
        /// Gövde ölçeği: boss büyük görünür, normal zombi kendi boyunda.
        ///
        /// <para><b>Ölçek her doğumda yazılır</b>, yalnızca boss olurken değil: havuzdan
        /// çıkan nesne önceki hayatının boyunu taşırsa, bir boss öldükten sonra sıradan
        /// zombiler dev olarak doğar. Bir saat oynadıktan sonra ortaya çıkan türden bir
        /// hata (systems-code.md).</para>
        ///
        /// <para><b>NavMeshAgent'ın yarıçapı da ölçeklenir</b>: yalnızca görseli
        /// büyütmek, kapıdan geçebilen ama gövdesi duvara giren bir boss üretirdi.</para>
        /// </summary>
        private void ApplyBodyScale(float scale)
        {
            if (scale <= 0f) scale = 1f;
            if (Mathf.Approximately(_bodyScale, scale)) return;

            _bodyScale = scale;
            _transform.localScale = Vector3.one * scale;

            if (_navAgent != null)
            {
                // Ajanin yaricapi ve boyu OLCEKLE birlikte buyur; Unity bunu
                // transform'dan turetmez.
                _navAgent.radius = _agentBaseRadius * scale;
                _navAgent.height = _agentStandHeight * scale;
            }
        }

        /// <summary>
        /// Şarapnel parçalarının yönü. <b>Rastgele değil, küreye eşit dağıtılmış</b>
        /// (Fibonacci küresi).
        ///
        /// <para><b>Neden rastgele değil:</b> rastgele yönlerde on dört parça, kümelenir
        /// ve boşluk bırakır — aynı mesafedeki iki zombiden biri beş parça yerken
        /// diğeri hiç almayabilir. Oyuncu bunu "patlama bazen çalışıyor" diye okur.
        /// Eşit dağıtım, patlamanın <b>öğrenilebilir</b> olmasını sağlar (ai-code.md:
        /// tahmin edilebilir, optimal olandan iyidir).</para>
        ///
        /// <para>Dağılım biraz yukarı eğik: zemine giden parçalar boşa gider, zombiler
        /// ayakta durur.</para>
        /// </summary>
        private static Vector3 ShrapnelDirection(int index, int count)
        {
            // Altin aci: ardisik indisler kurenin en uzak noktalarina duser.
            const float goldenAngle = 2.399963f;

            float y = 1f - (index + 0.5f) * 2f / count;   // -1..1
            y = y * 0.6f + 0.15f;                          // zemine degil, govdeye

            float radiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = goldenAngle * index;

            return new Vector3(Mathf.Cos(theta) * radiusAtY, y, Mathf.Sin(theta) * radiusAtY)
                .normalized;
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
        /// <summary>
        /// Her vuruş kutusunun görselini bir kez toplar.
        ///
        /// <para><b>Vuruş kutusundan gidilir, isimden değil</b>: anatominin nerede
        /// olduğunu zaten <see cref="ZombieHitbox"/> biliyor. İsimle aramak, yeni bir
        /// zombi tipinde sessizce yanlış parçayı parlatırdı.</para>
        ///
        /// <para>Gövde ayrı: onun görseli durum rengini de taşıyan
        /// <see cref="bodyRenderer"/>.</para>
        /// </summary>
        private void BuildPartRenderers()
        {
            int partCount = Enum.GetValues(typeof(ZombiePart)).Length;

            _partRenderers = new Renderer[partCount];
            _partBaseColors = new Color[partCount];
            _partFlashTimers = new float[partCount];

            _partRenderers[(int)ZombiePart.Body] = bodyRenderer;

            ZombieHitbox[] hitboxes = GetComponentsInChildren<ZombieHitbox>(true);

            for (int i = 0; i < hitboxes.Length; i++)
            {
                int index = (int)hitboxes[i].Part;
                if (_partRenderers[index] != null) continue;

                _partRenderers[index] = hitboxes[i].GetComponent<Renderer>();
            }

            // Temel renk bir kez okunur. `sharedMaterial` OKUNUR, `material` DEGIL:
            // ikincisi materyali klonlar ve her zombi ayri cizim cagrisi olur
            // (shader-graphics.md).
            for (int i = 0; i < _partRenderers.Length; i++)
            {
                Renderer r = _partRenderers[i];
                if (r == null || r.sharedMaterial == null) continue;

                _partBaseColors[i] = r.sharedMaterial.HasProperty(BaseColorId)
                    ? r.sharedMaterial.GetColor(BaseColorId)
                    : Color.grey;
            }
        }

        /// <summary>
        /// Vurulan bölgeyi beyaza çakar. <b>Sadece o bölge</b> (2026-09-05) — kafaya
        /// vurunca kafa, bacağa vurunca bacak.
        /// </summary>
        private void FlashPart(ZombiePart part)
        {
            if (_partRenderers == null) return;

            int index = (int)part;
            Renderer r = _partRenderers[index];
            if (r == null) return;

            _partFlashTimers[index] = PartFlashSeconds;

            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, Color.white);
            r.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// Süresi dolan parlamaları geri alır.
        ///
        /// <para>Sayaç işlemeyen bir parça hiç dokunulmaz: kare başına 40 zombi × 4
        /// parça property block yazmak, hiçbir şey olmadığında bile bedel ödemek
        /// olurdu.</para>
        /// </summary>
        private void TickPartFlashes(float dt)
        {
            if (_partFlashTimers == null) return;

            for (int i = 0; i < _partFlashTimers.Length; i++)
            {
                if (_partFlashTimers[i] <= 0f) continue;

                _partFlashTimers[i] -= dt;
                if (_partFlashTimers[i] > 0f) continue;

                _partFlashTimers[i] = 0f;

                // Govde durum rengini tasir; digerleri kendi temel rengine doner.
                if (i == (int)ZombiePart.Body) ApplyDebugColor();
                else RestorePartColor(i);
            }
        }

        private void RestorePartColor(int index)
        {
            Renderer r = _partRenderers[index];
            if (r == null) return;

            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, _partBaseColors[index]);
            r.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>Bütün parlamaları anında söndürür (ölüm, havuza iade).</summary>
        private void ClearPartFlashes()
        {
            if (_partFlashTimers == null) return;

            for (int i = 0; i < _partFlashTimers.Length; i++)
            {
                if (_partFlashTimers[i] <= 0f) continue;

                _partFlashTimers[i] = 0f;
                if (i != (int)ZombiePart.Body) RestorePartColor(i);
            }
        }

        // ------------------------------------------------------------- dondurma

        /// <summary>Buz rengi. Dokuyu <b>çarpar</b> — modeli silmez, soğutur.</summary>
        private static readonly Color FreezeTint = new Color(0.52f, 0.76f, 1.00f);

        /// <summary>
        /// Dondurma eşyasının <b>görünür</b> karşılığı. 2026-09-08.
        ///
        /// <para><b>Neden gerekti</b> (geliştirici: <i>"dondurma drobunu alınca
        /// zombiler donuyor ama hasar da almıyorlar"</i>): dondurmanın tek görünür
        /// sonucu <b>hareketin durması</b> idi. Duran bir zombi ile ölmüş, takılmış ya
        /// da dokunulmaz bir zombi ekranda aynı görünür — yani oyuncu, vurduğu hasarın
        /// sayıldığından emin olamıyor. <b>Kodda dondurmanın hasarı engellediği bir yol
        /// yok</b> (hasar <see cref="ApplyDamageToPart"/>'tan geçer ve orada eşya
        /// durumu hiç okunmaz); eksik olan kanıttı. Artık donmuş zombi mavi ve her
        /// isabet savaş günlüğüne düşüyor — sayı tutmuyorsa dosya söyleyecek.</para>
        ///
        /// <para><b>Yalnızca DEĞİŞİMDE yazılır:</b> kare başına kırk zombinin bütün
        /// renderer'larına özellik bloğu yazmak, hiçbir şey olmadığında bile bedel
        /// ödemek olurdu (csharp-code.md).</para>
        /// </summary>
        private void TickFreezeTint()
        {
            bool frozen = PowerupState.IsFreezeActive && IsAlive;
            if (frozen == _freezeTintApplied) return;

            _freezeTintApplied = frozen;
            ApplyFreezeTint(frozen);
        }

        /// <summary>Rengi <b>koşulsuz</b> yazar. Doğum ve durum değişimi çağırır.</summary>
        private void ApplyFreezeTint(bool frozen)
        {
            if (_visualRenderers == null) return;

            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                Renderer r = _visualRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId,
                                        frozen ? FreezeTint : _visualBaseColors[i]);
                r.SetPropertyBlock(_propertyBlock);
            }
        }

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

            // Sendeleme beyazi ARTIK BURADA DEGIL (2026-09-05): govdeye yazildigi
            // surece, kafaya da bacaga da vursan beyazlayan yer govdeydi ve vurus
            // kutulari ekranda hicbir sey soylemiyordu. Parlama artik VURULAN
            // bolgeye iniyor (FlashPart); durum rengi burada saf kaliyor.
            //
            // Parlama surerken durum rengini geri yazmak, parlamayi tek karede
            // silerdi.
            if (_partFlashTimers != null && _partFlashTimers[(int)ZombiePart.Body] > 0f) return;

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
