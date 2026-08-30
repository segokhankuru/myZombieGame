using Bunker.Systems.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

namespace Bunker.AI
{
    /// <summary>
    /// M0-04 yük testi: N tane NavMesh agent'ını sahaya salar ve rastgele hedeflere
    /// yürütür. **Bunlar zombi değil.** Model yok, saldırı yok, algı yok, oyun mantığı
    /// yok. Ölçülen tek şey NavMesh yol bulma ve hareket maliyeti.
    ///
    /// <para>Cevapladığı soru ÇK-5: <i>40 agent host'un işlemcisini boğuyor mu?</i>
    /// Bu, M-01'e girmeden önce cevaplanması gereken tek performans sorusu — çünkü
    /// cevap "evet" ise zombi sayısı veya AI karmaşıklığı, AI mimarisi yazılmadan önce
    /// düşmeli.</para>
    ///
    /// <para><b>Zamanlama gerçekçi tutuldu.</b> Agent'lar her kare yeniden yol istemez;
    /// bu ölçümü olduğundan kötü gösterirdi. Hiç istemeseler iyi gösterirdi. İkisi de
    /// yanlış cevap verir. Bunun yerine ai-code.md'nin kuralı uygulandı: yol istekleri
    /// kısıtlı ve <b>kareler arasına yayılmış</b>. Hepsinin aynı karede yol istemesi,
    /// gerçek oyunlarda ani kare düşüşlerinin ana sebebidir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Agent Load Test")]
    public sealed class AgentLoadTest : MonoBehaviour
    {
        [Header("Yuk")]
        [Tooltip("Hedef agent sayisi. Calisma aninda F3/F4 ile ya da bu alani " +
                 "dogrudan degistirerek ayarlanabilir.")]
        [SerializeField] private int agentCount = 40;
        [SerializeField] private int stepSize = 10;
        [SerializeField] private int maxAgents = 200;

        [Header("Davranis")]
        [Tooltip("Bir agent kac saniyede bir yeni hedef ister. Zombi kovalamasinin " +
                 "gercekci araligi 0.25-0.5 sn.")]
        [SerializeField] private float repathIntervalSeconds = 0.4f;
        [SerializeField] private float wanderRadiusMeters = 25f;
        [SerializeField] private float agentSpeedMetersPerSecond = 3.5f;

        [Header("Olcum kontrolu")]
        [Tooltip("Kapatilirsa agent'lar gorunmez olur. AI maliyetini cizim maliyetinden " +
                 "ayirmak icin: once acik olcup sonra kapali olcersin, fark cizimdir.")]
        [SerializeField] private bool showRenderers = true;

        [Header("Otomatik tarama (F5)")]
        [Tooltip("Taramanin sirayla olcecegi agent sayilari.")]
        [SerializeField] private int[] sweepCounts = { 0, 40, 100, 200 };
        [Tooltip("Sayi degistikten sonra yol bulmanin oturmasi icin beklenen sure.")]
        [SerializeField] private float sweepSettleSeconds = 3f;
        [Tooltip("Her adimda kac saniye olculur.")]
        [SerializeField] private float sweepMeasureSeconds = 15f;

        private NavMeshAgent[] _agents;
        private GameObject[] _objects;
        private int _activeCount;
        private int _repathCursor;
        private float _repathBudgetPerFrame;
        private float _repathAccumulator;

        private enum SweepPhase { Idle, Settling, Measuring }

        private SweepPhase _sweepPhase = SweepPhase.Idle;
        private int _sweepIndex;
        private float _sweepTimer;
        private FrameTimeRecorder _sweepRecorder;
        private FrameStats[] _sweepResults;
        private bool _sweepRenderersWereVisible;

        private void Start()
        {
            _agents = new NavMeshAgent[maxAgents];
            _objects = new GameObject[maxAgents];

            for (int i = 0; i < maxAgents; i++)
            {
                _objects[i] = CreateAgent(i);
                _agents[i] = _objects[i].GetComponent<NavMeshAgent>();
                _objects[i].SetActive(false);
            }

            SetActiveCount(agentCount);
        }

        private void OnDestroy()
        {
            DiagnosticCounters.Reset();
        }

        private GameObject CreateAgent(int index)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"LoadAgent_{index:D3}";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);

            // Collider birakiliyor: gercek zombilerin de collider'i olacak (hitscan
            // onlara isabet edecek). Cikarmak olcumu gercekten daha ucuz gosterirdi.
            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.speed = agentSpeedMetersPerSecond;
            agent.angularSpeed = 720f;
            agent.acceleration = 20f;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.autoBraking = false;

            return go;
        }

        private void SetActiveCount(int requested)
        {
            int target = Mathf.Clamp(requested, 0, maxAgents);

            for (int i = 0; i < maxAgents; i++)
            {
                bool shouldBeActive = i < target;
                if (_objects[i].activeSelf == shouldBeActive) continue;

                if (shouldBeActive && TrySampleNavMeshPoint(out Vector3 spawn))
                {
                    _objects[i].transform.position = spawn;
                    _objects[i].SetActive(true);
                    if (_agents[i].isOnNavMesh) _agents[i].Warp(spawn);
                }
                else
                {
                    _objects[i].SetActive(shouldBeActive);
                }
            }

            _activeCount = target;
            agentCount = target;
            DiagnosticCounters.ActiveAgents = target;

            // Yol istekleri kareler arasina yayilir. Ornek: 40 agent, 0.4 sn araliksa
            // ve 60 FPS'te 24 kare varsa, kare basina ~1.7 agent yol ister.
            _repathBudgetPerFrame = repathIntervalSeconds > 0f
                ? target / (repathIntervalSeconds * 60f)
                : target;

            SetRenderers(showRenderers);
        }

        private void SetRenderers(bool visible)
        {
            for (int i = 0; i < maxAgents; i++)
            {
                var renderer = _objects[i].GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = visible;
            }
        }

        private void Update()
        {
            ReadInput();
            TickSweep();

            // Inspector'daki alan oynatilirsa calisma aninda uygulanir. Tus
            // kombinasyonu ile ugrasmak istemeyen icin en dogrudan yol.
            if (agentCount != _activeCount)
            {
                SetActiveCount(agentCount);
                Debug.Log($"[AgentLoadTest] Agent: {_activeCount}");
            }

            if (_activeCount == 0) return;

            // Kare basina yalnizca butce kadar agent yeni hedef ister.
            // Hepsini ayni karede tetiklemek, gercek oyunlardaki ani kare
            // dususlerinin ana sebebidir (ai-code.md).
            _repathAccumulator += _repathBudgetPerFrame;
            int toProcess = Mathf.FloorToInt(_repathAccumulator);
            if (toProcess <= 0) return;

            _repathAccumulator -= toProcess;

            for (int n = 0; n < toProcess; n++)
            {
                _repathCursor = (_repathCursor + 1) % _activeCount;
                NavMeshAgent agent = _agents[_repathCursor];

                if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;

                if (TrySampleNavMeshPoint(out Vector3 destination))
                {
                    // SetDestination asenkron yol hesabi tetikler; Update icinde
                    // senkron yol hesabi asla yapilmaz (ai-code.md).
                    agent.SetDestination(destination);
                }
            }
        }

        private void ReadInput()
        {
            // F3/F4 secildi cunku fonksiyon tuslari klavye duzeninden bagimsiz.
            // Kose parantez Turkce Q'da AltGr gerektiriyor ve Input System fiziksel
            // tus konumuna baktigi icin yanlis tusa denk geliyor.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                SetActiveCount(_activeCount - stepSize);
                Debug.Log($"[AgentLoadTest] Agent: {_activeCount}");
            }
            else if (keyboard.f4Key.wasPressedThisFrame)
            {
                SetActiveCount(_activeCount + stepSize);
                Debug.Log($"[AgentLoadTest] Agent: {_activeCount}");
            }
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                showRenderers = !showRenderers;
                SetRenderers(showRenderers);
                Debug.Log($"[AgentLoadTest] Cizim: {(showRenderers ? "acik" : "kapali")}");
            }
            else if (keyboard.f5Key.wasPressedThisFrame)
            {
                if (_sweepPhase == SweepPhase.Idle) StartSweep();
                else CancelSweep();
            }
        }

        // ---------------------------------------------------------------- otomatik tarama

        /// <summary>
        /// Tum tarama adimlarini sirayla olcup tek bir tablo basar. Elle dort ayri
        /// olcum koordine etmek hataya acik -- olcumu durdurmayi unutmak ya da sayiyi
        /// olcum ortasinda degistirmek veriyi sessizce kullanilamaz hale getirir.
        /// </summary>
        public void StartSweep()
        {
            if (sweepCounts == null || sweepCounts.Length == 0)
            {
                Debug.LogWarning("[AgentLoadTest] Tarama listesi bos.");
                return;
            }

            // Ornek sayisi: en yuksek makul FPS x en uzun adim. Halka tampon oldugu
            // icin tasarsa da en son ornekleri tutar.
            _sweepRecorder ??= new FrameTimeRecorder(32768);
            _sweepResults = new FrameStats[sweepCounts.Length];
            _sweepRenderersWereVisible = showRenderers;

            _sweepIndex = 0;
            BeginSweepStep();

            Debug.Log($"[AgentLoadTest] TARAMA BASLADI - {sweepCounts.Length} adim, " +
                      $"adim basina {sweepSettleSeconds:F0}+{sweepMeasureSeconds:F0} sn, " +
                      $"cizim {(showRenderers ? "ACIK" : "KAPALI")}. Iptal icin F5.");
        }

        public void CancelSweep()
        {
            if (_sweepPhase == SweepPhase.Idle) return;
            _sweepPhase = SweepPhase.Idle;
            Debug.Log("[AgentLoadTest] Tarama iptal edildi.");
        }

        private void BeginSweepStep()
        {
            SetActiveCount(sweepCounts[_sweepIndex]);
            _sweepRecorder.Reset();
            _sweepTimer = 0f;
            _sweepPhase = SweepPhase.Settling;
        }

        private void TickSweep()
        {
            if (_sweepPhase == SweepPhase.Idle) return;

            float dt = Time.unscaledDeltaTime;
            _sweepTimer += dt;

            if (_sweepPhase == SweepPhase.Settling)
            {
                if (_sweepTimer >= sweepSettleSeconds)
                {
                    _sweepTimer = 0f;
                    _sweepRecorder.Reset();
                    _sweepPhase = SweepPhase.Measuring;
                }
                return;
            }

            _sweepRecorder.Add(dt * 1000f);

            if (_sweepTimer < sweepMeasureSeconds) return;

            _sweepResults[_sweepIndex] = _sweepRecorder.Snapshot();
            _sweepIndex++;

            if (_sweepIndex < sweepCounts.Length)
            {
                BeginSweepStep();
            }
            else
            {
                _sweepPhase = SweepPhase.Idle;
                ReportSweep();
            }
        }

        private void ReportSweep()
        {
            var report = new System.Text.StringBuilder(512);
            report.Append("[AgentLoadTest] TARAMA SONUCU  (cizim ")
                  .Append(_sweepRenderersWereVisible ? "ACIK" : "KAPALI")
                  .Append(")\n");
            report.Append("  agent |    p50 |    p95 |    p99 |    max |  agent basi (p50)\n");
            report.Append("  ------+--------+--------+--------+--------+------------------\n");

            float baseline = _sweepResults.Length > 0 && sweepCounts[0] == 0
                ? _sweepResults[0].P50
                : 0f;

            for (int i = 0; i < sweepCounts.Length; i++)
            {
                FrameStats s = _sweepResults[i];
                int count = sweepCounts[i];

                string perAgent = count > 0 && baseline > 0f
                    ? $"{(s.P50 - baseline) / count * 1000f:F1} us"
                    : "-";

                report.Append("  ")
                      .Append(count.ToString().PadLeft(5)).Append(" | ")
                      .Append(s.P50.ToString("F2").PadLeft(6)).Append(" | ")
                      .Append(s.P95.ToString("F2").PadLeft(6)).Append(" | ")
                      .Append(s.P99.ToString("F2").PadLeft(6)).Append(" | ")
                      .Append(s.Max.ToString("F2").PadLeft(6)).Append(" | ")
                      .Append(perAgent).Append('\n');
            }

            report.Append("  (butun degerler milisaniye)");
            Debug.Log(report.ToString());
        }

        private bool TrySampleNavMeshPoint(out Vector3 point)
        {
            Vector3 origin = transform.position + Random.insideUnitSphere * wanderRadiusMeters;
            origin.y = transform.position.y;

            if (NavMesh.SamplePosition(origin, out NavMeshHit hit, wanderRadiusMeters, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }

            point = transform.position;
            return false;
        }
    }
}
