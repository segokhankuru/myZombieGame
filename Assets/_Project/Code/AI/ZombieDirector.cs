using System;
using System.Collections.Generic;
using Bunker.Audio;
using Bunker.Config;
using Bunker.Systems.Ai;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using UnityEngine;
using UnityEngine.AI;

namespace Bunker.AI
{
    /// <summary>
    /// Zombi doğumunu tur akışına bağlar: kim, ne zaman, nereden doğar; öldüğünde
    /// havuza döner. M1-05.
    ///
    /// <para><b>Otorite (ADR-0004):</b> bu bileşen yalnızca <b>host'ta</b> düşünür.
    /// Kararı kendisi vermez — <c>Bunker.Net</c> katmanı
    /// <see cref="SetAuthoritative"/> ile söyler. Bu ters çevirme bilinçli:
    /// <c>Bunker.AI</c> ağı bilmez ve Mirror'a bağımlı değildir, ama yine de ağ-farkında
    /// çalışır.</para>
    ///
    /// <para><b>Zombide <c>NetworkTransform</c> yoktur</b> (M-01 zorunlu kısıtı). Konum
    /// senkronu tek bir seam'den geçer: <c>ZombieNetworkRelay</c> buradaki
    /// <see cref="Active"/> listesini okur ve tek bir paket hâlinde yayınlar.</para>
    ///
    /// <para><b>Kimlik:</b> her zombi <see cref="ZombieAgent.NetId"/> alır. İstemci
    /// tarafı bu id ile hangi vekilin (proxy) hangi zombi olduğunu bilir — nesne
    /// referansı değil, id gönderilir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Director")]
    public sealed class ZombieDirector : MonoBehaviour
    {
        [Header("Prefab ve ayarlar")]
        [SerializeField] private ZombieAgent zombiePrefab;

        [Tooltip("config/balance/rounds.json'dan uretilen varlik.")]
        [SerializeField] private RoundsConfigAsset roundsConfig;

        [Tooltip("config/balance/zombie.json'dan uretilen varlik.")]
        [SerializeField] private ZombieConfigAsset zombieConfig;

        [Header("Akis")]
        [Tooltip("Acikken tur akisi kendiliginden isler. Kapaliyken yalnizca elle " +
                 "cagrildiginda dogurur - oyun testi ve olcum icin.")]
        [SerializeField] private bool autoRun = true;

        [Tooltip("Baslangic turu. 1'den buyuk bir deger vermek, o turu oynamadan " +
                 "gormeyi saglar - denge turunun tamami budur.")]
        [SerializeField] private int startRound = 1;

        [Tooltip("Havuzda bastan hazirlanacak zombi sayisi. Ilk turun ilk saniyesinde " +
                 "hepsini birden yaratmak, oyunun ilk izlenimini takilmayla acar.")]
        [SerializeField] private int prewarmCount = 8;

        // --- calisma ani ---

        private RoundScaling _scaling;
        private RoundRunner _runner;
        private ZombieConfig _zombieRuntimeConfig;
        private ZombiePool _pool;

        private readonly List<WindowEntry> _windows = new List<WindowEntry>(24);
        private readonly List<ZombieAgent> _active = new List<ZombieAgent>(64);
        private readonly Dictionary<ushort, ZombieAgent> _byId = new Dictionary<ushort, ZombieAgent>(64);

        private int _windowCursor;
        private ushort _nextId = 1;
        private bool _authoritative = true;
        private bool _ready;
        private const float WindowReachabilityRefreshSeconds = 1.5f;

        private float[] _windowCheckedAt;
        private bool[] _windowReachable;
        private NavMeshPath _reachabilityPath;

        // Bir turda TEK boss: iki boss bir savas degil bir kusatma olurdu.
        private bool _bossSpawnedThisRound;

        private bool _warnedNoWindow;
        private bool _warnedInsideSpawn;

        /// <summary>Sahadaki zombiler. Ağ seam'i bu listeyi okur.</summary>
        public IReadOnlyList<ZombieAgent> Active => _active;

        public int AliveCount => _active.Count;
        public int Round => _runner?.Round ?? 0;
        public RoundPhase Phase => _runner?.Phase ?? RoundPhase.Breather;
        public float BreatherRemainingSeconds => _runner?.BreatherRemainingSeconds ?? 0f;
        public int RemainingToSpawn => _runner?.RemainingToSpawn ?? 0;
        public RoundScaling Scaling => _scaling;

        /// <summary>Yeni tur başladı (tur numarası).</summary>
        public event Action<int> RoundStarted;

        /// <summary>Tur temizlendi (tur numarası).</summary>
        public event Action<int> RoundCleared;

        /// <summary>Bir zombi öldü. Ekonomi ve telemetri buna bağlanır.</summary>
        public event Action<ZombieAgent, DamageKind, bool> ZombieKilled;

        // ---------------------------------------------------------------- kurulum

        private void Awake()
        {
            if (zombiePrefab == null || roundsConfig == null || zombieConfig == null)
            {
                // Eksik ayar sessiz varsayilanla gecistirilmez (config-protocol.md).
                Debug.LogError("[Zombi/Yonetmen] Prefab ya da config varliklari atanmamis. " +
                               "'Bunker/Zombi/Test Alanini Kur' bunlari baglar.", this);
                enabled = false;
                return;
            }

            _scaling = new RoundScaling(roundsConfig.ToRuntime());
            _zombieRuntimeConfig = zombieConfig.ToRuntime();
            _runner = new RoundRunner(_scaling);
            _pool = new ZombiePool(zombiePrefab, transform, _scaling.MaxConcurrent);

            // Boot'ta bir kez arama serbest; kare basina yasak (csharp-code.md).
            _windows.AddRange(FindObjectsByType<WindowEntry>(FindObjectsSortMode.None));

            if (_windows.Count == 0)
            {
                Debug.LogError("[Zombi/Yonetmen] Sahnede hic WindowEntry yok - zombilerin " +
                               "girecek yeri yok. 'Bunker/Zombi/Test Alanini Kur' calistir.", this);
                enabled = false;
                return;
            }

            // Pencere basina ulasilabilirlik onbellegi (kapali kapi arkasi dogum yapmaz).
            _windowCheckedAt = new float[_windows.Count];
            _windowReachable = new bool[_windows.Count];
            for (int i = 0; i < _windowCheckedAt.Length; i++) _windowCheckedAt[i] = float.NegativeInfinity;

            _pool.Prewarm(prewarmCount);

            if (startRound > 1) _runner.JumpToRound(startRound);

            _ready = true;
        }

        private void OnEnable()
        {
            // Statik yayin noktasina abone olan herkes OnDisable'da birakir
            // (RunSignals'in iki kuralindan biri).
            RunSignals.RunRestarted += OnRunRestarted;
        }

        private void OnDisable()
        {
            RunSignals.RunRestarted -= OnRunRestarted;
        }

        /// <summary>
        /// Yeni run: saha temizlenir, tur sayacı başa döner (AC-5).
        ///
        /// <para><see cref="JumpToRound"/> değil <see cref="RoundRunner.Reset"/>:
        /// yeni run <b>molayla</b> başlamalı. Doğrudan tur 1'e atlamak, oyuncuyu
        /// yeni run'ın ilk saniyesinde zombiyle karşılaştırır ve toparlanma anını
        /// hiç vermez.</para>
        /// </summary>
        private void OnRunRestarted()
        {
            // Otorite kontrolu Update'teki ile ayni olmali: yetkisiz bir yonetmen
            // (M-02'de uzak istemci) sahayi kendi basina temizlemez ve tur sayacini
            // kendi basina sifirlamaz.
            if (!_ready || !_authoritative) return;

            KillAll();
            _runner.Reset();
        }

        private void OnDestroy()
        {
            // Awake'in kurdugunu geri al: her zombinin olay aboneligi burada kopar,
            // yoksa sahne degisirken olu nesneler uzerinden olay tetiklenir.
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] == null) continue;
                _active[i].Killed -= OnZombieKilled;
                _active[i].Despawned -= OnZombieDespawned;
            }

            _active.Clear();
            _byId.Clear();
        }

        /// <summary>
        /// Host mu, istemci mi. <c>Bunker.Net</c> katmanı çağırır; tek başına oynanan
        /// sahnede varsayılan <c>true</c>'dur (solo = uzak istemcisiz host).
        /// </summary>
        public void SetAuthoritative(bool value)
        {
            _authoritative = value;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] != null) _active[i].SetSimulated(value);
            }
        }

        // ---------------------------------------------------------------- akis

        private void Update()
        {
            if (!_ready || !_authoritative || !autoRun) return;

            // Draft acikken tur ilerlemez: oyuncu kart secerken bir sonraki turun
            // baslamasi, secim ekraninin arkasindan surunun gelmesi demek olurdu.
            if (CardSignals.IsDraftOpen) return;

            // Run bitti: tur ilerlemez, zombi dogmaz (AC-3). Skor ekrani acikken
            // arkada bir sonraki turun baslamasi, ekrani kapatan oyuncuyu surunun
            // ortasinda birakirdi.
            if (RunSignals.IsRunOver) return;

            // Sureyi turu yuruten taraf ilerletir: run'in suresi, oynanan surenin
            // kendisidir - menude gecen zaman degil.
            RunSignals.Current.Tick(Time.deltaTime);

            TickRound(Time.deltaTime);
        }

        /// <summary>
        /// Tur akışını bir adım ilerletir. <c>autoRun</c> kapalıyken dışarıdan
        /// çağrılabilir — ölçüm ve otomatik oyun testi için.
        /// </summary>
        public void TickRound(float deltaTime)
        {
            int budget = _runner.Tick(deltaTime, _active.Count);

            if (_runner.RoundStartedThisTick)
            {
                RoundStarted?.Invoke(_runner.Round);

                // Assembly sinirini asan yayin: silah ve barikat Bunker.Gameplay ile
                // Bunker.AI'da yasiyor ve birbirlerini goremiyor (RoundSignals).
                RoundSignals.RaiseRoundStarted(_runner.Round);

                // Skor ekraninin "ulasilan tur" sayisi (M1-11).
                RunSignals.Current.NoteRound(_runner.Round);

                // Yeni tur, yeni boss hakki.
                _bossSpawnedThisRound = false;
            }

            if (_runner.RoundClearedThisTick)
            {
                RoundCleared?.Invoke(_runner.Round);
                RoundSignals.RaiseRoundCleared(_runner.Round);

                // Kismi yenilenme (2026-09-05): mermi ve barikat kendiliginden bir
                // miktar geri gelir. Oranlarin tek sahibi rounds.json; silah ve
                // barikat onu okumaz, burasi okuyup yayinlar.
                RoundSignals.RaiseRoundEndRestock(
                    _scaling.RoundEndReserveAmmoFraction01,
                    _scaling.RoundEndBarricadeBoardsFraction01);
            }

            if (budget <= 0) return;

            int spawned = 0;
            for (int i = 0; i < budget; i++)
            {
                if (TrySpawnOne()) spawned++;
            }

            // Gerceklesen bildirilir, niyet degil: dogurulamayan zombi sayilirsa tur
            // hic dogmamis zombileri bekleyerek acik kalir.
            _runner.ReportSpawned(spawned);
        }

        /// <summary>Turu doğrudan bir numaraya taşır. Oyun testi ve denge turu için.</summary>
        public void JumpToRound(int round)
        {
            if (!_ready) return;

            KillAll();
            _runner.JumpToRound(round);
            RoundStarted?.Invoke(_runner.Round);
            RoundSignals.RaiseRoundStarted(_runner.Round);
        }

        public void KillAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ZombieAgent zombie = _active[i];
                if (zombie != null && zombie.IsAlive)
                {
                    zombie.ApplyDamage(new DamageInfo(float.MaxValue, DamageKind.Environment));
                }
                else
                {
                    Release(zombie);
                }
            }
        }

        // ---------------------------------------------------------------- dogum

        private bool TrySpawnOne()
        {
            WindowEntry window = NextSpawnWindow();

            if (window == null)
            {
                // Hicbir dogum noktasi bulunamamasi SESSIZ kalamaz: tur akisi doner,
                // sayaclar isler, ama sahada hicbir sey olmaz. Teshis edilmesi en zor
                // hata turu budur (BUG-001'in ve BUG-003'un ortak dersi).
                if (!_warnedNoWindow)
                {
                    _warnedNoWindow = true;
                    Debug.LogError("[Zombi/Yonetmen] Dogum icin uygun pencere yok - hicbir " +
                                   "zombi dogamayacak. Sahnedeki WindowEntry'lerin 'open' " +
                                   "alani kapali olabilir.", this);
                }

                return false;
            }

            // Dogum noktasi pencerenin COK DISINDA: zombinin gorunur sekilde hiclikten
            // belirmesi PILLAR-04'u cigner (LVL-01 spec'i), ve barikatin DIBINDE
            // belirmesi disarisini savunmayi anlamsiz kilar (gelistirici, 2026-09-04).
            //
            // Mesafe artik pencerenin kendisinden geliyor (BlockoutSettings ->
            // WindowEntry.SpawnPoint). Onceki surumde burada hesaplaniyordu ve
            // uretecin sahneye koydugu Spawn_XX isaretleri baska bir yeri
            // gosteriyordu - iki ayri dogru, biri yalan.
            Vector3 wanted = window.SpawnPoint;

            // Yaricap KUCUK tutuluyor. SamplePosition duvarlari umursamaz: genis bir
            // yaricapla, disarida NavMesh bulunamayan bir noktadan ICERIDEKI zemine
            // yapisabilir - zombi o zaman barikati hic gormeden binanin icinde belirir.
            // Oyun testinde "barikati yikmadan giriyorlar, icerde spawn oluyor
            // olabilirler" diye okundu; tam olarak buydu.
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[Zombi/Yonetmen] '{window.name}' disinda NavMesh yok. " +
                                 "Bina cevresindeki serit uretilmemis ya da bake " +
                                 "edilmemis olabilir.", window);
                return false;
            }

            // Ikinci korkuluk: dogum noktasi pencerenin DIS tarafinda mi? Yaricap
            // kucultuldu ama geometriden bagimsiz bir garanti daha ucuz: noktanin
            // pencerenin disa bakan yonunde olmasi. Icerideyse dogum atlanir.
            if (Vector3.Dot(hit.position - window.transform.position,
                            window.transform.forward) <= 0f)
            {
                if (!_warnedInsideSpawn)
                {
                    _warnedInsideSpawn = true;
                    Debug.LogWarning($"[Zombi/Yonetmen] '{window.name}' icin bulunan dogum " +
                                     "noktasi binanin ICINDE kaldi; dogum atlandi. " +
                                     "Bina cevresindeki serit (apron) eksik olabilir.", window);
                }

                return false;
            }

            ZombieAgent zombie = _pool.Rent();
            if (zombie == null) return false;   // havuz sinirinda - tanimli durum

            zombie.NetId = _nextId++;
            if (_nextId == 0) _nextId = 1;      // 0 "yok" anlamina gelir

            // Konumu ZombieAgent.Spawn yazar - acik bir NavMeshAgent transform
            // yazmasini yok sayar (havuzdan cikan zombi eski olum yerine geri
            // cekiliyordu).
            // BOSS: tur boss turuysa, o turun ILK zombisi boss olur (2026-09-06).
            //
            // <b>İlk olması bilinçli:</b> boss turun sonunda gelseydi oyuncu turun
            // tamamını "acaba şimdi mi" diye oynardı; başta gelmesi turun geri kalanını
            // <i>onunla birlikte</i> hayatta kalma problemine çevirir. Ve tek: iki boss
            // bir savaş değil bir kuşatma olurdu.
            bool boss = !_bossSpawnedThisRound && _scaling.IsBossRound(_runner.Round);

            if (boss)
            {
                _bossSpawnedThisRound = true;

                zombie.Spawn(_zombieRuntimeConfig,
                             _scaling.BossHealthForRound(_runner.Round),
                             _scaling.BossSpeedForRound(_runner.Round),
                             window,
                             hit.position,
                             _scaling.BossScaleMultiplier,
                             _scaling.BossDamageMultiplier);

                GameAudio.PlayAt(SfxId.RoundStart, hit.position, 1.2f);
            }
            else
            {
                zombie.Spawn(_zombieRuntimeConfig,
                             _scaling.HealthForRound(_runner.Round),
                             _scaling.SpeedForRound(_runner.Round),
                             window,
                             hit.position);
            }

            zombie.SetSimulated(_authoritative);

            // Abonelik her kiralamada yeniden kurulur: ZombieAgent devre disi kalirken
            // dinleyicilerini temizler, boylece havuzdan cikan zombi eski bir
            // dinleyiciyi tasimaz.
            zombie.Killed += OnZombieKilled;
            zombie.Despawned += OnZombieDespawned;

            _active.Add(zombie);
            _byId[zombie.NetId] = zombie;
            return true;
        }

        /// <summary>
        /// Sıradaki doğum penceresi. <b>Barikatlı olması engel değildir</b> — zombi
        /// barikatlı pencerede doğar ve onu söker. Bir zamanlar bu metot "açık"
        /// pencere arıyordu ve barikatlar gelince hiçbir zombi doğamadı (BUG-003).
        /// </summary>
        private WindowEntry NextSpawnWindow()
        {
            for (int i = 0; i < _windows.Count; i++)
            {
                _windowCursor = (_windowCursor + 1) % _windows.Count;
                WindowEntry candidate = _windows[_windowCursor];

                if (candidate == null || !candidate.IsOpen) continue;
                if (!IsWindowInPlayableArea(_windowCursor, candidate)) continue;

                return candidate;
            }

            return null;
        }

        /// <summary>
        /// Bu pencere <b>oyuncunun ulaşabildiği alanda mı</b>.
        ///
        /// <para>Klasik tur döngüsünün temel kuralı: <b>yalnızca açık bölgelerde zombi
        /// doğar.</b> Bu olmadan kapalı kapının ardındaki pencerelerden zombi doğuyordu;
        /// barikatı söküp içeri giriyor, sonra oyuncuya yolu kapalı olduğu için
        /// hareket edemiyor ve <i>sıkışmış</i> (mavi) hâlde bekliyordu. Oyuncu tarafında
        /// bu, "gelmeyen zombiler yüzünden tur bitmiyor" olarak görünür — turun
        /// kilitlenmesinin en sinsi hâli.</para>
        ///
        /// <para><b>Bölge tanımına gerek yok:</b> kapı kapalıyken NavMesh'i kesiyor
        /// (M1-09), yani "oyuncuya yol var mı" sorusu bölge sorusunun tam karşılığı.
        /// Kapı açılınca o bölgenin pencereleri kendiliğinden devreye girer.</para>
        ///
        /// <para>Sonuç önbelleğe alınır: yol hesabı ucuz değil ve bir saniyede birden
        /// fazla değişmez.</para>
        /// </summary>
        private bool IsWindowInPlayableArea(int index, WindowEntry window)
        {
            if (Time.time - _windowCheckedAt[index] < WindowReachabilityRefreshSeconds)
            {
                return _windowReachable[index];
            }

            _windowCheckedAt[index] = Time.time;

            ZombieTargetBeacon target = ZombieTargets.Nearest(window.InsidePoint);

            if (target == null)
            {
                // Hedef yoksa kisitlamanin anlami da yok: oyuncu daha dogmamis olabilir.
                _windowReachable[index] = true;
                return true;
            }

            _reachabilityPath ??= new NavMeshPath();

            bool reachable =
                NavMesh.SamplePosition(window.InsidePoint, out NavMeshHit inside, 2f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(target.GroundPosition, out NavMeshHit player, 2f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(inside.position, player.position, NavMesh.AllAreas, _reachabilityPath) &&
                _reachabilityPath.status == NavMeshPathStatus.PathComplete;

            _windowReachable[index] = reachable;
            return reachable;
        }

        private void OnZombieKilled(ZombieAgent zombie, DamageKind kind, bool headshot)
        {
            // BOSS ODULU (2026-09-06): puani ekonomi yazar, ama "bu bir bossdu"
            // bilgisi yalnizca burada var - silah hangi zombiyi vurdugunu bilmez.
            // TODO(netcode-programmer, M-02): co-op'ta odul OLDURENE gitmeli;
            // su an solo host oldugu icin tek oyuncuya gidiyor.
            if (zombie != null && zombie.IsBoss)
            {
                RoundSignals.RaiseBossKilled(_scaling.BossPointsMultiplier);
            }

            ZombieKilled?.Invoke(zombie, kind, headshot);

            // Olen zombi HEMEN sahadan sayilmaz olur: tur "hepsi oldu mu" sorusunu
            // cesetleri bekleyerek cevaplamamali. Nesnenin kendisi hala gorunur
            // (yikilma ani, M1-13) ve havuza iadesi Despawned ile gelir.
            if (zombie == null) return;

            zombie.Killed -= OnZombieKilled;
            _active.Remove(zombie);
            _byId.Remove(zombie.NetId);
        }

        private void OnZombieDespawned(ZombieAgent zombie)
        {
            if (zombie == null) return;

            zombie.Despawned -= OnZombieDespawned;
            _pool.Return(zombie);
        }

        /// <summary>Zombiyi sahadan ve havuzdan tek adımda çeker (temizlik yolu).</summary>
        private void Release(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                _active.RemoveAll(z => z == null);
                return;
            }

            zombie.Killed -= OnZombieKilled;
            zombie.Despawned -= OnZombieDespawned;
            _active.Remove(zombie);
            _byId.Remove(zombie.NetId);
            _pool.Return(zombie);
        }

        // ---------------------------------------------------------------- ag seam'i icin

        /// <summary>İstemci tarafı: bu id'li vekil yoksa havuzdan bir tane açar.</summary>
        public ZombieAgent EnsureProxy(ushort id)
        {
            if (id == 0) return null;
            if (_byId.TryGetValue(id, out ZombieAgent existing)) return existing;

            ZombieAgent zombie = _pool.Rent();
            if (zombie == null) return null;

            zombie.NetId = id;
            zombie.Spawn(_zombieRuntimeConfig, 1f, 0f, null, zombie.transform.position);
            zombie.SetSimulated(false);   // vekil dusunmez, yalnizca konuma uyar

            _active.Add(zombie);
            _byId[id] = zombie;
            return zombie;
        }

        /// <summary>İstemci tarafı: sunucunun artık göndermediği vekili kaldırır.</summary>
        public void RemoveProxy(ushort id)
        {
            if (!_byId.TryGetValue(id, out ZombieAgent zombie)) return;
            Release(zombie);
        }

        public bool TryGet(ushort id, out ZombieAgent zombie) => _byId.TryGetValue(id, out zombie);
    }
}
