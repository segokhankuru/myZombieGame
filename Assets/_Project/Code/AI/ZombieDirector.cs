using System;
using System.Collections.Generic;
using Bunker.Config;
using Bunker.Systems.Ai;
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

            _pool.Prewarm(prewarmCount);

            if (startRound > 1) _runner.JumpToRound(startRound);

            _ready = true;
        }

        private void OnDestroy()
        {
            // Awake'in kurdugunu geri al: her zombinin olay aboneligi burada kopar,
            // yoksa sahne degisirken olu nesneler uzerinden olay tetiklenir.
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] != null) _active[i].Killed -= OnZombieKilled;
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

            TickRound(Time.deltaTime);
        }

        /// <summary>
        /// Tur akışını bir adım ilerletir. <c>autoRun</c> kapalıyken dışarıdan
        /// çağrılabilir — ölçüm ve otomatik oyun testi için.
        /// </summary>
        public void TickRound(float deltaTime)
        {
            int budget = _runner.Tick(deltaTime, _active.Count);

            if (_runner.RoundStartedThisTick) RoundStarted?.Invoke(_runner.Round);
            if (_runner.RoundClearedThisTick) RoundCleared?.Invoke(_runner.Round);

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
            WindowEntry window = NextOpenWindow();
            if (window == null) return false;

            // Dogum noktasi pencerenin DISINDA: zombinin gorunur sekilde hiclikten
            // belirmesi PILLAR-04'u cigner (LVL-01 spec'i).
            Vector3 wanted = window.OutsidePoint + window.transform.forward * 2f;

            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[Zombi/Yonetmen] '{window.name}' disinda NavMesh yok. " +
                                 "Bina cevresindeki serit uretilmemis ya da bake " +
                                 "edilmemis olabilir.", window);
                return false;
            }

            ZombieAgent zombie = _pool.Rent();
            if (zombie == null) return false;   // havuz sinirinda - tanimli durum

            zombie.transform.SetPositionAndRotation(hit.position, Quaternion.identity);
            zombie.NetId = _nextId++;
            if (_nextId == 0) _nextId = 1;      // 0 "yok" anlamina gelir

            zombie.Spawn(_zombieRuntimeConfig,
                         _scaling.HealthForRound(_runner.Round),
                         _scaling.SpeedForRound(_runner.Round),
                         window);

            zombie.SetSimulated(_authoritative);

            // Abonelik her kiralamada yeniden kurulur: ZombieAgent devre disi kalirken
            // dinleyicilerini temizler, boylece havuzdan cikan zombi eski bir
            // dinleyiciyi tasimaz.
            zombie.Killed += OnZombieKilled;

            _active.Add(zombie);
            _byId[zombie.NetId] = zombie;
            return true;
        }

        private WindowEntry NextOpenWindow()
        {
            for (int i = 0; i < _windows.Count; i++)
            {
                _windowCursor = (_windowCursor + 1) % _windows.Count;
                WindowEntry candidate = _windows[_windowCursor];
                if (candidate != null && candidate.IsOpen) return candidate;
            }

            return null;
        }

        private void OnZombieKilled(ZombieAgent zombie, DamageKind kind, bool headshot)
        {
            ZombieKilled?.Invoke(zombie, kind, headshot);
            Release(zombie);
        }

        private void Release(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                _active.RemoveAll(z => z == null);
                return;
            }

            zombie.Killed -= OnZombieKilled;
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
            zombie.Spawn(_zombieRuntimeConfig, 1f, 0f, null);
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
