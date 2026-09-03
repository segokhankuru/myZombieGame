using System.Text;
using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.AI
{
    /// <summary>
    /// Oyun testi tezgâhı: turu ileri sar, sahayı temizle, ekranda ne olduğunu gör.
    ///
    /// <para><b>Doğum burada değil</b> (M1-05 <see cref="ZombieDirector"/>'e taşıdı),
    /// <b>silah da burada değil</b> (M1-06 gerçek silahı yazdı — geçici ışın kaldırıldı;
    /// aynı tuşta iki ateş kaynağı olması hem çift hasar hem teşhis zorluğu üretiyordu).
    /// Geriye yalnızca tur zıplatma ve tezgâh ekranı kaldı; skor ekranı (M1-11) gelince
    /// bu dosya silinir.</para>
    ///
    /// <code>
    /// F7/F8  tur -/+        F9  sahayi temizle
    /// </code>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Sandbox (gecici)")]
    public sealed class ZombieSandbox : MonoBehaviour
    {
        [Header("Referans")]
        [SerializeField] private ZombieDirector director;

        [Header("Ekran")]
        [SerializeField] private bool showHud = true;

        private DebugPlayerHealth _playerHealth;
        private int _killsThisRun;

        private readonly StringBuilder _hud = new StringBuilder(256);
        private GUIStyle _hudStyle;

        private void Awake()
        {
            if (director == null) director = FindFirstObjectByType<ZombieDirector>();

            if (director == null)
            {
                Debug.LogError("[Zombi/Tezgah] ZombieDirector bulunamadi. " +
                               "'Bunker/Zombi/Test Alanini Kur' calistir.", this);
                enabled = false;
                return;
            }

            director.ZombieKilled += OnZombieKilled;
            director.RoundStarted += OnRoundStarted;
        }

        private void OnDestroy()
        {
            // Awake'in kurdugunu OnDestroy geri alir (csharp-code.md).
            if (director == null) return;

            director.ZombieKilled -= OnZombieKilled;
            director.RoundStarted -= OnRoundStarted;
        }

        private void OnZombieKilled(ZombieAgent zombie, DamageKind kind, bool headshot)
        {
            _killsThisRun++;
        }

        private void OnRoundStarted(int round)
        {
            RoundScaling s = director.Scaling;

            Debug.Log($"[Zombi/Tezgah] TUR {round}: " +
                      $"{s.TotalZombiesForRound(round, 1)} zombi, " +
                      $"can {s.HealthForRound(round):F0}, " +
                      $"hiz {s.SpeedForRound(round):F1} m/s ({s.SpeedTierForRound(round)}), " +
                      $"dogum araligi {s.SpawnIntervalForRound(round):F2} sn");
        }

        private void Update()
        {
            ReadInput();
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f7Key.wasPressedThisFrame) director.JumpToRound(director.Round - 1);
                else if (keyboard.f8Key.wasPressedThisFrame) director.JumpToRound(director.Round + 1);
                else if (keyboard.f9Key.wasPressedThisFrame) director.KillAll();
            }

        }

        private void OnGUI()
        {
            if (!showHud || director == null) return;

            _hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };

            if (_playerHealth == null) _playerHealth = FindFirstObjectByType<DebugPlayerHealth>();

            _hud.Clear();
            _hud.Append("ZOMBI TEZGAHI (M1-05)\n");

            if (director.Phase == RoundPhase.Breather)
            {
                _hud.Append("MOLA  ").Append(director.BreatherRemainingSeconds.ToString("F1"))
                    .Append(" sn\n");
            }
            else
            {
                _hud.Append("TUR ").Append(director.Round)
                    .Append("  |  sahada ").Append(director.AliveCount)
                    .Append("  |  kalan dogum ").Append(director.RemainingToSpawn).Append('\n');
            }

            _hud.Append("oldurme (run) ").Append(_killsThisRun);

            if (_playerHealth != null)
            {
                _hud.Append("   oyuncu cani ").Append(_playerHealth.Current.ToString("F0"))
                    .Append('/').Append(_playerHealth.Max.ToString("F0"));
            }

            _hud.Append('\n').Append("F7/F8 tur atla   F9 sahayi temizle");

            GUI.Label(new Rect(12f, 120f, 520f, 120f), _hud.ToString(), _hudStyle);
        }
    }
}
