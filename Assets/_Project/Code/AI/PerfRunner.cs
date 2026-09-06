using System;
using System.Globalization;
using System.IO;
using System.Text;
using Bunker.Systems.Diagnostics;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Otomatik kare süresi ölçümü. ÇK-15: <i>"Kare süresi 40 zombi ile bütçede."</i>
    ///
    /// <para><b>Neden var:</b> ÇK-15 M-01'in çıkış kriterlerinden biri ve hiç
    /// ölçülmedi. <c>PERF-BUDGET.md</c> açık kalemler listesinde "gerçek harita
    /// NavMesh'i" ve "build (editör değil)" ikisi de M-01'e yazılmış. Bu koşu ikisini
    /// birden kapatır ve <b>kimsenin oynamasını gerektirmez</b>.</para>
    ///
    /// <para><b>Yalnızca komut satırından açılır.</b> <c>-perfRun</c> argümanı yoksa bu
    /// bileşen hiçbir şey yapmaz — normal oyuna sıfır risk. Editörde de sessizdir.</para>
    ///
    /// <para><b>Yem hedef, oyuncu değil:</b> ölçüm için sahada sabit ~40 zombi gerekiyor.
    /// Yerde duran bir oyuncu dört vuruşta ölür, run biter ve ölçüm penceresi hiç
    /// açılmaz. Çözüm zombilerin hasar yolundan geliyor: <c>ZombieAgent</c> hasarı
    /// <b>beacon üzerinden</b> verir (<c>_target.ReceiveAttack</c>), yani
    /// <c>IDamageable</c>'ı olmayan bir hedef hiç hasar almaz. Bu koşu oyuncunun
    /// durduğu yere hasar almayan bir <b>yem beacon</b> koyar ve oyuncunun kendi
    /// beacon'ını kapatır. Zombiler normal şekilde kovalar, yol bulur, toplanır ve
    /// saldırır — kimse ölmez.</para>
    ///
    /// <para><b>Oyuncuya dokunulmuyor.</b> Konumunu yazmaya çalışmak
    /// <c>CharacterController</c> açıkken sessizce işe yaramazdı (BUG-005'in birebir
    /// aynısı) ve <c>PlayerController</c> <c>Bunker.Gameplay</c>'de olduğu için buradan
    /// kapatılamaz. Yem hedef bu iki sorunu da hiç doğurmuyor.</para>
    ///
    /// <para><b>Ölçümün kapsamadığı:</b> hasar uygulaması, oyuncunun ateş etmesi,
    /// zombi ölümü ve havuza dönüş. Bunlar seyrek olaylar; kare süresini süren şey
    /// sürekli koşan yol bulma ve çizimdir. Rapor bunu ayrıca yazar — ölçümün ne
    /// ölçmediğini söylemeyen bir rapor, güven vermez.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Perf Runner (komut satiri)")]
    public sealed class PerfRunner : MonoBehaviour
    {
        /// <summary>Bütçe: 60 FPS = 16 ms (PERF-BUDGET.md).</summary>
        private const float BudgetMilliseconds = 16f;

        /// <summary>Oyuncunun ag uzerinden dogmasi icin beklenecek azami sure.</summary>
        private const float PlayerWaitTimeoutSeconds = 30f;

        [Tooltip("Olcum oncesi isinma suresi. Ilk kareler shader derlemesi ve havuz " +
                 "isinmasi tasir; olcume katilirsa p99'u tek basina belirler.")]
        [SerializeField] private float warmupSeconds = 6f;

        [SerializeField] private ZombieDirector director;

        private FrameTimeRecorder _recorder;


        private bool _active;
        private int _targetRound = 12;
        private float _measureSeconds = 45f;
        private string _outPath;

        private float _elapsed;
        private float _waitElapsed;
        private bool _warmedUp;

        private int _aliveSum;
        private int _aliveSamples;
        private int _aliveMin = int.MaxValue;
        private int _aliveMax;

        private void Awake()
        {
            if (!HasFlag("-perfRun"))
            {
                // Normal oyun: bu bilesen hic uyanmaz.
                enabled = false;
                return;
            }

            _targetRound = IntArg("-perfRound", _targetRound);
            _measureSeconds = FloatArg("-perfSeconds", _measureSeconds);
            _outPath = StringArg("-perfOut", DefaultOutPath());

            if (director == null) director = FindFirstObjectByType<ZombieDirector>();

            if (director == null)
            {
                Debug.LogError("[Perf] ZombieDirector bulunamadi - olcum yapilamaz.");
                Quit(1);
                return;
            }

            // Tampon BUTUN olcum penceresini almali. Sabit 4096 ile 45 saniyelik bir
            // kosunun yalnizca son ~3 saniyesi tutuluyordu (halka tampon eskisinin
            // ustune yazar) - ve p99'un butun amaci SEYREK takilmalari yakalamak.
            // Uc saniyelik bir pencerede seyrek olan sey hic gorunmez.
            //
            // 2000 FPS ust siniri: bu makine gri kutuda ~1200 FPS uretiyor, pay birakildi.
            // 45 sn x 2000 = 90k ornek = 360 KB. Tampon bir kez ayrilir; Add hicbir sey
            // tahsis etmez (olcum araci olcumu bozmamali).
            int capacity = Mathf.Clamp((int)(_measureSeconds * 2000f), 4096, 500000);
            _recorder = new FrameTimeRecorder(capacity);

            Debug.Log($"[Perf] Olcum basliyor: tur {_targetRound}, " +
                      $"{warmupSeconds:F0} sn isinma + {_measureSeconds:F0} sn olcum. " +
                      $"Cikti: {_outPath}");
        }

        private void Update()
        {
            if (!enabled) return;

            // --- 1) Oyuncuyu bekle
            //
            // Oyuncu Mirror tarafindan AG BASLADIKTAN SONRA dogar, yani Start()
            // aninda sahnede YOKTUR. Ilk surum Start()'ta ariyordu ve "oyuncu
            // bulunamadi" deyip cikiyordu - olcum hic baslamadi.
            if (!_active)
            {
                _waitElapsed += Time.unscaledDeltaTime;

                if (!TrySetUpDecoy())
                {
                    if (_waitElapsed < PlayerWaitTimeoutSeconds) return;

                    Debug.LogError("[Perf] Oyuncu " + PlayerWaitTimeoutSeconds +
                                   " saniyede dogmadi - zombilerin kovalayacagi bir sey yok.");
                    Quit(1);
                    return;
                }

                director.JumpToRound(_targetRound);
                _active = true;
                return;
            }

            _elapsed += Time.unscaledDeltaTime;

            if (!_warmedUp)
            {
                if (_elapsed < warmupSeconds) return;

                _warmedUp = true;
                _elapsed = 0f;
                Debug.Log($"[Perf] Isinma bitti, olcum basladi. Sahada {director.AliveCount} zombi.");
                return;
            }

            _recorder.Add(Time.unscaledDeltaTime * 1000f);

            int alive = director.AliveCount;
            _aliveSum += alive;
            _aliveSamples++;
            if (alive < _aliveMin) _aliveMin = alive;
            if (alive > _aliveMax) _aliveMax = alive;

            if (_elapsed >= _measureSeconds) Finish();
        }

        private void Finish()
        {
            _active = false;

            FrameStats stats = _recorder.Snapshot();

            if (stats.IsEmpty)
            {
                Debug.LogError("[Perf] Hic ornek toplanmadi.");
                Quit(1);
                return;
            }

            float avgAlive = _aliveSamples > 0 ? (float)_aliveSum / _aliveSamples : 0f;

            Debug.Log($"[Perf] SONUC  p50 {stats.P50:F2} ms | p95 {stats.P95:F2} ms | " +
                      $"p99 {stats.P99:F2} ms | butce {BudgetMilliseconds:F0} ms | " +
                      $"ortalama {avgAlive:F1} zombi");

            WriteReport(stats, avgAlive);
            Quit(0);
        }

        /// <summary>
        /// Raporu diske yazar.
        ///
        /// <para><b>Bütçe oranı yazılır, mutlak FPS değil</b> (PERF-BUDGET.md):
        /// "geliştirme makinesindeki 600 FPS orta seviye bir işlemcide hiçbir şey
        /// ifade etmez; '40 agent bütçenin %1.2'sini yiyor' ölçeklenebilir bir
        /// cümledir."</para>
        /// </summary>
        private void WriteReport(in FrameStats stats, float avgAlive)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(768);

            sb.Append("{\n");
            sb.Append("  \"schema\": 1,\n");
            sb.Append("  \"runAtUtc\": \"")
              .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", c)).Append("\",\n");
            sb.Append("  \"editor\": ").Append(Application.isEditor ? "true" : "false").Append(",\n");
            sb.Append("  \"platform\": \"").Append(Application.platform).Append("\",\n");
            sb.Append("  \"resolution\": \"").Append(Screen.width).Append('x')
              .Append(Screen.height).Append("\",\n");
            sb.Append("  \"round\": ").Append(_targetRound.ToString(c)).Append(",\n");
            sb.Append("  \"measureSeconds\": ").Append(_measureSeconds.ToString("0.##", c)).Append(",\n");
            sb.Append("  \"frames\": ").Append(stats.SampleCount.ToString(c)).Append(",\n");

            // Tampon tastiysa yuzdelikler PENCERENIN TAMAMINI kapsamaz. Bunu
            // raporda soylemek sart: eksik oldugunu bilmedigin bir olcum, yanlis
            // bir olcumden daha tehlikelidir.
            sb.Append("  \"windowComplete\": ")
              .Append(stats.TotalSamples <= stats.SampleCount ? "true" : "false").Append(",\n");
            sb.Append("  \"framesSeen\": ").Append(stats.TotalSamples.ToString(c)).Append(",\n");
            sb.Append("  \"avgFps\": ")
              .Append((stats.TotalSamples / Math.Max(0.001f, _measureSeconds)).ToString("0.#", c))
              .Append(",\n");
            sb.Append("  \"zombiesAvg\": ").Append(avgAlive.ToString("0.#", c)).Append(",\n");
            sb.Append("  \"zombiesMin\": ").Append(_aliveMin == int.MaxValue ? 0 : _aliveMin).Append(",\n");
            sb.Append("  \"zombiesMax\": ").Append(_aliveMax.ToString(c)).Append(",\n");
            sb.Append("  \"budgetMs\": ").Append(BudgetMilliseconds.ToString("0.##", c)).Append(",\n");
            sb.Append("  \"p50Ms\": ").Append(stats.P50.ToString("0.###", c)).Append(",\n");
            sb.Append("  \"p95Ms\": ").Append(stats.P95.ToString("0.###", c)).Append(",\n");
            sb.Append("  \"p99Ms\": ").Append(stats.P99.ToString("0.###", c)).Append(",\n");
            sb.Append("  \"minMs\": ").Append(stats.Min.ToString("0.###", c)).Append(",\n");
            sb.Append("  \"maxMs\": ").Append(stats.Max.ToString("0.###", c)).Append(",\n");
            sb.Append("  \"p50BudgetPercent\": ")
              .Append((100f * stats.P50 / BudgetMilliseconds).ToString("0.#", c)).Append(",\n");
            sb.Append("  \"p99BudgetPercent\": ")
              .Append((100f * stats.P99 / BudgetMilliseconds).ToString("0.#", c)).Append(",\n");
            sb.Append("  \"withinBudget\": ")
              .Append(stats.P99 <= BudgetMilliseconds ? "true" : "false").Append(",\n");
            sb.Append("  \"excludes\": \"oyuncu atesi, hasar uygulamasi, zombi olumu ve havuza donus\"\n");
            sb.Append("}\n");

            try
            {
                string dir = Path.GetDirectoryName(_outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(_outPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[Perf] Rapor yazildi: {_outPath}");
            }
            catch (Exception e)
            {
                // Olcum yapildi ve loga basildi; yazamamak sonucu yok etmez.
                Debug.LogError($"[Perf] Rapor yazilamadi ({_outPath}): {e.Message}");
            }
        }

        /// <summary>
        /// Oyuncu doğduysa yem hedefi kurar.
        ///
        /// <para>Sıra önemli: <b>önce yem eklenir, sonra oyuncunun beacon'ı kapatılır.</b>
        /// Ters sırada bir kare boyunca hiç hedef kalmaz ve o karede zombiler
        /// "hedefsiz" durumuna düşer.</para>
        /// </summary>
        /// <returns>Kurulum tamamlandıysa <c>true</c>.</returns>
        private bool TrySetUpDecoy()
        {
            ZombieTargetBeacon player = ZombieTargets.Nearest(transform.position);
            if (player == null) return false;

            var decoyObject = new GameObject("_PerfDecoyTarget");
            decoyObject.transform.position = player.GroundPosition;

            // IDamageable YOK: beacon hasari IDamageable'a iletir, olmayinca hasar
            // hicbir yere gitmez. Olcumun kimseyi oldurmemesinin tek sebebi bu.
            decoyObject.AddComponent<ZombieTargetBeacon>();

            player.enabled = false;

            Debug.Log($"[Perf] Yem hedef kuruldu: {decoyObject.transform.position}. " +
                      "Oyuncunun beacon'i kapatildi, kimse hasar almayacak.");

            return true;
        }

        private static string DefaultOutPath() =>
            Path.Combine(Application.persistentDataPath, "perf", "frame-times.json");

        private static void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }

        // ------------------------------------------------------------- argumanlar

        private static bool HasFlag(string name)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name) return true;
            }

            return false;
        }

        private static string StringArg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }

            return fallback;
        }

        private static int IntArg(string name, int fallback) =>
            int.TryParse(StringArg(name, null), NumberStyles.Integer,
                         CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private static float FloatArg(string name, float fallback) =>
            float.TryParse(StringArg(name, null), NumberStyles.Float,
                           CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
