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
    /// <para><b>Doğum artık burada değil</b> — M1-05 ile <see cref="ZombieDirector"/>'e
    /// taşındı ve tur akışına bağlandı. Burada kalan tek şey, henüz sistemi olmayan iki
    /// şeyin geçici karşılığı: <b>silah</b> (M1-06) ve <b>skor ekranı</b> (M1-11).
    /// İkisi geldiğinde bu dosya silinir.</para>
    ///
    /// <code>
    /// F7/F8  tur -/+        F9  sahayi temizle
    /// Sol tik  hata ayiklama isini (M1-06 bunu silecek)
    /// </code>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Sandbox (gecici)")]
    public sealed class ZombieSandbox : MonoBehaviour
    {
        [Header("Referans")]
        [SerializeField] private ZombieDirector director;

        [Header("Hata ayiklama silahi (M1-06 bunu silecek)")]
        [Tooltip("Bir isinin hasari. Denge degeri DEGIL - tur 1 zombisini iki vurusta " +
                 "dusurecek kadar, oyle ki olum akisi denenebilsin.")]
        [SerializeField] private float debugShotDamage = 80f;
        [SerializeField] private float debugShotRangeMeters = 60f;

        [Header("Ekran")]
        [SerializeField] private bool showHud = true;

        private DebugPlayerHealth _playerHealth;
        private int _killsThisRun;

        private readonly StringBuilder _hud = new StringBuilder(256);
        private GUIStyle _hudStyle;

        private static readonly RaycastHit[] ShotHits = new RaycastHit[8];

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

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) DebugShot();
        }

        /// <summary>
        /// M1-06 gelene kadarki en basit ışın. <b>Hissiyat ölçütü değildir:</b> geri
        /// tepme yok, yayılım yok, isabet geri bildirimi yok. Yalnızca zombiyi
        /// öldürebilmek için var.
        /// </summary>
        private void DebugShot()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var ray = new Ray(cam.transform.position, cam.transform.forward);

            // NonAlloc: kare basina cop uretmemek icin (csharp-code.md).
            int count = Physics.RaycastNonAlloc(ray, ShotHits, debugShotRangeMeters);
            if (count == 0) return;

            int nearest = -1;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (ShotHits[i].distance >= nearestDistance) continue;
                nearestDistance = ShotHits[i].distance;
                nearest = i;
            }

            if (nearest < 0) return;

            var hitbox = ShotHits[nearest].collider.GetComponent<ZombieHitbox>();
            if (hitbox == null) return;

            hitbox.ApplyDamage(new DamageInfo(debugShotDamage, DamageKind.Bullet));
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

            _hud.Append('\n').Append("F7/F8 tur  F9 sahayi temizle  sol tik ates");

            GUI.Label(new Rect(12f, 120f, 520f, 120f), _hud.ToString(), _hudStyle);
        }
    }
}
