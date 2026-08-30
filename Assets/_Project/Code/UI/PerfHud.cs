using System.Text;
using Bunker.Systems.Diagnostics;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// Kare süresi ölçüm ekranı. M-00'ın ÇK-11'i.
    ///
    /// <para><b>Bu aracın kendisi ölçümü bozmamalı.</b> Kare başına hiçbir tahsis
    /// yapmaz: tamponlar bir kez ayrılır, ekran metni saniyede dört kez yeniden yazılır.
    /// Yüzdelik hesabı <see cref="FrameTimeRecorder"/> içinde — saf C#, Unity'siz,
    /// test edilebilir ve `Bunker.AI`'nin otomatik taramasıyla ortak.</para>
    ///
    /// <para><b>Yetkili sayı Unity Profiler'dır.</b> Bu HUD oynarken canlı geri bildirim
    /// içindir; IMGUI'nin kendi maliyeti küçüktür ama sıfır değildir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Perf HUD")]
    public sealed class PerfHud : MonoBehaviour
    {
        private const int LiveCapacity = 256;      // canli p50/p99 penceresi
        private const int SessionCapacity = 32768; // elle olcum oturumu
        private const float RefreshIntervalSeconds = 0.25f;

        // Tuslar sabittir: F1 goster/gizle, F2 olcum oturumu baslat/bitir.
        // Inspector'a acilmadi -- calismayan bir ayar alani, olmayan bir ayardan kotudur.
        [Header("Kontroller")]
        [SerializeField] private bool visibleOnStart = true;

        [Header("Butce")]
        [Tooltip("PERF-BUDGET hedefi. p99 bunu asinca metin kirmiziya doner.")]
        [SerializeField] private float frameBudgetMilliseconds = 16f;

        private readonly FrameTimeRecorder _live = new FrameTimeRecorder(LiveCapacity);
        private readonly FrameTimeRecorder _session = new FrameTimeRecorder(SessionCapacity);
        private readonly StringBuilder _text = new StringBuilder(256);
        private readonly GUIStyle _style = new GUIStyle();

        private bool _sessionActive;
        private float _sessionStartTime;
        private float _refreshTimer;
        private string _display = string.Empty;
        private bool _visible;
        private bool _overBudget;

        private static int AgentCount => DiagnosticCounters.ActiveAgents;

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

            _live.Add(frameMilliseconds);
            if (_sessionActive) _session.Add(frameMilliseconds);

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
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                if (_sessionActive) StopSession();
                else StartSession();
            }
        }

        /// <summary>Elle ölçüm oturumu başlatır. Önceki örnekler atılır.</summary>
        public void StartSession()
        {
            _session.Reset();
            _sessionStartTime = Time.unscaledTime;
            _sessionActive = true;
            Debug.Log($"[PerfHud] Olcum basladi. Agent: {AgentCount}");
        }

        /// <summary>Ölçüm oturumunu bitirir ve özeti konsola yazar.</summary>
        public void StopSession()
        {
            _sessionActive = false;
            FrameStats s = _session.Snapshot();

            if (s.IsEmpty)
            {
                Debug.Log("[PerfHud] Oturum bos, ozet yok.");
                return;
            }

            float duration = Time.unscaledTime - _sessionStartTime;
            bool passed = s.P99 <= frameBudgetMilliseconds;

            // Ilk satir tek basina okunabilir olmali: Unity konsolu cok satirli
            // loglari katliyor ve yalnizca ilk satiri gosteriyor.
            Debug.Log(
                $"[PerfHud] {AgentCount} agent | p50 {s.P50:F2}ms | p95 {s.P95:F2}ms | " +
                $"p99 {s.P99:F2}ms | butce {frameBudgetMilliseconds:F0}ms -> " +
                $"{(passed ? "GECTI" : "ASILDI")}\n" +
                $"  sure       : {duration:F1} sn, {s.TotalSamples} kare " +
                $"(ortalama {s.TotalSamples / Mathf.Max(duration, 0.001f):F0} FPS)\n" +
                $"  ornek      : {s.SampleCount}" +
                (s.TotalSamples > s.SampleCount ? " (son N kare)" : "") + "\n" +
                $"  min / max  : {s.Min:F2} / {s.Max:F2} ms");
        }

        private void Rebuild(float currentMilliseconds)
        {
            FrameStats s = _live.Snapshot();
            _overBudget = s.P99 > frameBudgetMilliseconds;

            long managedBytes = System.GC.GetTotalMemory(false);

            _text.Clear();
            _text.Append("kare  ").Append(currentMilliseconds.ToString("F2")).Append(" ms   ")
                 .Append((1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)).ToString("F0")).Append(" FPS\n");
            _text.Append("p50   ").Append(s.P50.ToString("F2")).Append(" ms\n");
            _text.Append("p99   ").Append(s.P99.ToString("F2")).Append(" ms   (butce ")
                 .Append(frameBudgetMilliseconds.ToString("F0")).Append(" ms)\n");
            _text.Append("agent ").Append(AgentCount).Append('\n');
            _text.Append("heap  ").Append((managedBytes / 1048576f).ToString("F1")).Append(" MB\n");
            _text.Append(_sessionActive
                ? $"OLCUM ACIK  {Time.unscaledTime - _sessionStartTime:F0} sn  F2=bitir"
                : "F1=gizle  F2=olcum  F5=tarama");

            _display = _text.ToString();
        }

        private void OnGUI()
        {
            if (!_visible || _display.Length == 0) return;

            _style.normal.textColor = _overBudget ? new Color(1f, 0.4f, 0.4f) : Color.white;
            GUI.Label(new Rect(10f, 10f, 320f, 130f), _display, _style);
        }
    }
}
