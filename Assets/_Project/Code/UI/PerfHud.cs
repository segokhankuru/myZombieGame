using System;
using System.Text;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// Kare süresi ölçüm ekranı. M-00'ın ÇK-11'i ve M0-04'teki ÇK-5 ölçümünün kanıt
    /// üreteci.
    ///
    /// <para><b>Bu aracın kendisi ölçümü bozmamalı.</b> Kare başına hiçbir tahsis
    /// yapmaz: halka tamponu, sıralama tamponu ve metin oluşturucu bir kez ayrılır,
    /// ekran metni saniyede dört kez yeniden yazılır. Sıralama yerinde yapılır
    /// (<c>Array.Sort</c>), kopya üretmez.</para>
    ///
    /// <para><b>Yetkili sayı Unity Profiler'dır.</b> Bu HUD oynarken canlı geri bildirim
    /// içindir; ÇK-5'in kanıtı Profiler ekran görüntüsüdür. IMGUI'nin kendi maliyeti
    /// küçüktür ama sıfır değildir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Perf HUD")]
    public sealed class PerfHud : MonoBehaviour
    {
        private const int SampleCapacity = 256;      // canli p50/p99 penceresi
        private const int SessionCapacity = 8192;    // olcum oturumu (~136 sn @ 60fps)
        private const float RefreshIntervalSeconds = 0.25f;

        // Tuslar sabittir: F1 goster/gizle, F2 olcum oturumu baslat/bitir.
        // Inspector'a acilmadi -- calismayan bir ayar alani, olmayan bir ayardan kotudur.
        [Header("Kontroller")]
        [SerializeField] private bool visibleOnStart = true;

        [Header("Butce")]
        [Tooltip("PERF-BUDGET hedefi. Kare suresi bunu asinca metin kirmiziya doner.")]
        [SerializeField] private float frameBudgetMilliseconds = 16f;

        /// <summary>
        /// Ölçülen varlık sayısı. M0-04'te agent spawner bunu doldurur; başka bir şey
        /// bilmesine gerek yok.
        /// </summary>
        public int TrackedAgentCount { get; set; }

        private readonly float[] _samples = new float[SampleCapacity];
        private readonly float[] _sortBuffer = new float[SampleCapacity];
        private readonly float[] _sessionSamples = new float[SessionCapacity];
        private readonly StringBuilder _text = new StringBuilder(256);
        private readonly GUIStyle _style = new GUIStyle();

        private int _sampleCount;
        private int _sampleCursor;
        private int _sessionCount;
        private bool _sessionActive;
        private float _refreshTimer;
        private string _display = string.Empty;
        private bool _visible;
        private bool _overBudget;

        private void Awake()
        {
            _visible = visibleOnStart;
            _style.fontSize = 14;
            _style.normal.textColor = Color.white;
        }

        private void Update()
        {
            // unscaledDeltaTime: Time.timeScale ile oynanirsa olcum bozulmasin.
            float frameMilliseconds = Time.unscaledDeltaTime * 1000f;

            _samples[_sampleCursor] = frameMilliseconds;
            _sampleCursor = (_sampleCursor + 1) % SampleCapacity;
            if (_sampleCount < SampleCapacity) _sampleCount++;

            if (_sessionActive && _sessionCount < SessionCapacity)
            {
                _sessionSamples[_sessionCount++] = frameMilliseconds;
                if (_sessionCount == SessionCapacity)
                {
                    Debug.LogWarning("[PerfHud] Oturum tamponu doldu, olcum durduruldu.");
                    StopSession();
                }
            }

            ReadInput();

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= RefreshIntervalSeconds)
            {
                _refreshTimer = 0f;
                Rebuild(frameMilliseconds);
            }
        }

        private void ReadInput()
        {
            // Eski Input sinifi bu projede devre disi (activeInputHandler = 1).
            // Klavye okumasi icin yeni Input System kullaniliyor.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                if (_sessionActive) StopSession();
                else StartSession();
            }
        }

        /// <summary>Ölçüm oturumu başlatır. Önceki örnekler atılır.</summary>
        public void StartSession()
        {
            _sessionCount = 0;
            _sessionActive = true;
            Debug.Log($"[PerfHud] Olcum oturumu basladi. Agent: {TrackedAgentCount}");
        }

        /// <summary>Ölçüm oturumunu bitirir ve özeti konsola yazar.</summary>
        public void StopSession()
        {
            _sessionActive = false;
            if (_sessionCount == 0)
            {
                Debug.Log("[PerfHud] Oturum bos, ozet yok.");
                return;
            }

            // Oturum tamponunu yerinde sirala. Kopya uretmez.
            Array.Sort(_sessionSamples, 0, _sessionCount);

            float p50 = Percentile(_sessionSamples, _sessionCount, 0.50f);
            float p99 = Percentile(_sessionSamples, _sessionCount, 0.99f);
            float min = _sessionSamples[0];
            float max = _sessionSamples[_sessionCount - 1];

            Debug.Log(
                "[PerfHud] OLCUM OZETI\n" +
                $"  ornek      : {_sessionCount}\n" +
                $"  agent      : {TrackedAgentCount}\n" +
                $"  p50        : {p50:F2} ms  ({1000f / Mathf.Max(p50, 0.001f):F0} FPS)\n" +
                $"  p99        : {p99:F2} ms  ({1000f / Mathf.Max(p99, 0.001f):F0} FPS)\n" +
                $"  min / max  : {min:F2} / {max:F2} ms\n" +
                $"  butce      : {frameBudgetMilliseconds:F1} ms -> " +
                $"{(p99 <= frameBudgetMilliseconds ? "GECTI" : "ASILDI")}");
        }

        private void Rebuild(float currentMilliseconds)
        {
            Array.Copy(_samples, _sortBuffer, _sampleCount);
            Array.Sort(_sortBuffer, 0, _sampleCount);

            float p50 = Percentile(_sortBuffer, _sampleCount, 0.50f);
            float p99 = Percentile(_sortBuffer, _sampleCount, 0.99f);
            _overBudget = p99 > frameBudgetMilliseconds;

            long managedBytes = GC.GetTotalMemory(false);

            _text.Clear();
            _text.Append("kare  ").Append(currentMilliseconds.ToString("F2")).Append(" ms   ")
                 .Append((1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)).ToString("F0")).Append(" FPS\n");
            _text.Append("p50   ").Append(p50.ToString("F2")).Append(" ms\n");
            _text.Append("p99   ").Append(p99.ToString("F2")).Append(" ms   (butce ")
                 .Append(frameBudgetMilliseconds.ToString("F0")).Append(" ms)\n");
            _text.Append("agent ").Append(TrackedAgentCount).Append('\n');
            _text.Append("heap  ").Append((managedBytes / 1048576f).ToString("F1")).Append(" MB\n");
            _text.Append(_sessionActive
                ? $"OLCUM ACIK  ({_sessionCount} ornek)  F2=bitir"
                : "F1=gizle  F2=olcum baslat");

            _display = _text.ToString();
        }

        private static float Percentile(float[] sortedAscending, int count, float fraction)
        {
            if (count <= 0) return 0f;
            int index = Mathf.Clamp(Mathf.RoundToInt(fraction * (count - 1)), 0, count - 1);
            return sortedAscending[index];
        }

        private void OnGUI()
        {
            if (!_visible || _display.Length == 0) return;

            _style.normal.textColor = _overBudget ? new Color(1f, 0.4f, 0.4f) : Color.white;
            GUI.Label(new Rect(10f, 10f, 320f, 130f), _display, _style);
        }
    }
}
