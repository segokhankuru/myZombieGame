using System.Collections.Generic;
using System.Text;
using Bunker.Systems.Ai;
using Bunker.Systems.Config;
using Bunker.Systems.Combat;
using Bunker.Config;
using Bunker.Systems.Rounds;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Bunker.AI
{
    /// <summary>
    /// M1-04 deneme tezgâhı: zombiyi <b>oynanabilir</b> hâle getirir, tur sistemi
    /// bağlanmadan.
    ///
    /// <para><b>Bu M1-05 değil.</b> Gerçek doğum akışı (tur akışına bağlı, havuzlanmış,
    /// ağ-farkında) M1-05'in işidir ve bu sınıfı siler. Buradaki tek amaç, zombinin
    /// nasıl hissettirdiğine <i>bugün</i> bakabilmek — çünkü his oynayarak ayarlanır,
    /// kod okuyarak değil.</para>
    ///
    /// <para>Silah da yok (M1-06), o yüzden fare tuşu basit bir ışın atar. O ışının
    /// hissiyatı <b>ölçüt değildir</b>; yalnızca zombiyi öldürebilmek içindir.</para>
    ///
    /// <code>
    /// F6  dogum acik/kapali      F7/F8  tur -/+
    /// F9  hepsini oldur          Sol tik  hata ayiklama isini
    /// </code>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Sandbox (gecici)")]
    public sealed class ZombieSandbox : MonoBehaviour
    {
        [Header("Prefab ve ayarlar")]
        [SerializeField] private ZombieAgent zombiePrefab;

        [Tooltip("config/balance/rounds.json'dan uretilen varlik. " +
                 "'Bunker/Config/Ice Aktar' uretir, kurulum araci baglar.")]
        [SerializeField] private RoundsConfigAsset roundsConfig;

        [Tooltip("config/balance/zombie.json'dan uretilen varlik.")]
        [SerializeField] private ZombieConfigAsset zombieConfig;

        [Header("Tur")]
        [Tooltip("Baslangic turu. F7/F8 ile calisma aninda degistirilir.")]
        [SerializeField] private int round = 1;
        [SerializeField] private bool spawning = true;

        [Header("Hata ayiklama silahi (M1-06 bunu silecek)")]
        [Tooltip("Bir isinin hasari. Denge degeri DEGIL - tur 1 zombisini iki vuruşta " +
                 "dusurecek kadar, oyle ki olum akisi denenebilsin.")]
        [SerializeField] private float debugShotDamage = 80f;
        [SerializeField] private float debugShotRangeMeters = 60f;

        [Header("Ekran")]
        [SerializeField] private bool showHud = true;

        private RoundScaling _scaling;
        private ZombieConfig _zombieConfig;

        private readonly List<WindowEntry> _windows = new List<WindowEntry>(24);
        private readonly List<ZombieAgent> _alive = new List<ZombieAgent>(64);

        private float _spawnTimer;
        private int _spawnedThisRound;
        private int _windowCursor;
        private DebugPlayerHealth _playerHealth;

        private readonly StringBuilder _hud = new StringBuilder(256);
        private GUIStyle _hudStyle;

        private static readonly RaycastHit[] ShotHits = new RaycastHit[8];

        private void Awake()
        {
            // Ayarlar uretilen varliklardan gelir, C# varsayilanindan degil. Eksikse
            // BOOT'TA SESSIZ VARSAYILAN YOK: eksik ayar haftalarca gorunmeyen denge
            // hatalarinin kaynagidir (config-protocol.md), o yuzden yuksek sesle durur.
            if (roundsConfig == null || zombieConfig == null)
            {
                Debug.LogError("[Zombie/Sandbox] Config varliklari atanmamis " +
                               "(Assets/_Project/Config/rounds.asset, zombie.asset). " +
                               "'Bunker/Config/Ice Aktar' ile uret, sonra " +
                               "'Bunker/Zombi/Test Alanini Kur' ile bagla.", this);
                enabled = false;
                return;
            }

            // Bir kez cozulup asagi verilir; hicbir sistem config'i statikten cekmez.
            _scaling = new RoundScaling(roundsConfig.ToRuntime());
            _zombieConfig = zombieConfig.ToRuntime();

            // Boot'ta bir kez arama serbest; kare basina yasak (csharp-code.md).
            _windows.AddRange(FindObjectsByType<WindowEntry>(FindObjectsSortMode.None));

            if (_windows.Count == 0)
            {
                Debug.LogError("[Zombie/Sandbox] Sahnede hic WindowEntry yok. " +
                               "Menuden 'Bunker/Zombi/Test Alanini Kur' calistir - " +
                               "gri kutuyu pencere bilesenleriyle yeniden uretir.", this);
            }

            if (zombiePrefab == null)
            {
                Debug.LogError("[Zombie/Sandbox] Zombi prefab'i atanmamis. " +
                               "'Bunker/Zombi/Test Alanini Kur' bunu doldurur.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            ReadInput();
            TickSpawning();
            PruneDead();
        }

        // ---------------------------------------------------------------- girdi

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f6Key.wasPressedThisFrame)
                {
                    spawning = !spawning;
                    Debug.Log($"[Zombie/Sandbox] Dogum: {(spawning ? "acik" : "kapali")}");
                }
                else if (keyboard.f7Key.wasPressedThisFrame) SetRound(round - 1);
                else if (keyboard.f8Key.wasPressedThisFrame) SetRound(round + 1);
                else if (keyboard.f9Key.wasPressedThisFrame) KillAll();
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) DebugShot();
        }

        private void SetRound(int value)
        {
            round = value < 1 ? 1 : value;
            _spawnedThisRound = 0;

            Debug.Log($"[Zombie/Sandbox] Tur {round}: " +
                      $"{_scaling.TotalZombiesForRound(round, 1)} zombi, " +
                      $"can {_scaling.HealthForRound(round):F0}, " +
                      $"hiz {_scaling.SpeedForRound(round):F1} m/s " +
                      $"({_scaling.SpeedTierForRound(round)}), " +
                      $"aralik {_scaling.SpawnIntervalForRound(round):F2} sn");
        }

        private void KillAll()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] != null && _alive[i].IsAlive)
                {
                    _alive[i].ApplyDamage(new DamageInfo(float.MaxValue, DamageKind.Environment));
                }
            }

            _alive.Clear();
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

            // NonAlloc: kare basina cop uretmemek icin (csharp-code.md). Tek atista
            // onemsiz gorunur ama otomatik ates gelince aynen kalacak kod bu.
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

            DamageResult result = hitbox.ApplyDamage(new DamageInfo(debugShotDamage, DamageKind.Bullet));

            if (result.Killed)
            {
                Debug.Log($"[Zombie/Sandbox] Oldurme{(hitbox.IsHead ? " (KAFA)" : "")}.");
            }
        }

        // ---------------------------------------------------------------- dogum

        private void TickSpawning()
        {
            if (!spawning || _windows.Count == 0) return;

            int total = _scaling.TotalZombiesForRound(round, 1);
            if (_spawnedThisRound >= total) return;
            if (_alive.Count >= _scaling.MaxConcurrent) return;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < _scaling.SpawnIntervalForRound(round)) return;
            _spawnTimer = 0f;

            SpawnOne();
        }

        private void SpawnOne()
        {
            WindowEntry window = NextOpenWindow();
            if (window == null) return;

            // Dogum noktasi pencerenin DISINDA: zombinin gorunur sekilde hiclikten
            // belirmesi PILLAR-04'u cigner (LVL-01 spec'i).
            Vector3 spawnPoint = window.OutsidePoint + window.transform.forward * 2f;

            if (!NavMesh.SamplePosition(spawnPoint, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[Zombie/Sandbox] '{window.name}' penceresinin disinda " +
                                 "NavMesh yok. Bina disinda yurunecek zemin (apron) " +
                                 "uretilmemis ya da bake edilmemis olabilir - " +
                                 "'Bunker/Zombi/Test Alanini Kur' bunu duzeltir.", window);
                return;
            }

            ZombieAgent zombie = Instantiate(zombiePrefab, hit.position, Quaternion.identity);
            zombie.name = $"Zombie_{_spawnedThisRound:D3}";
            zombie.Spawn(_zombieConfig,
                         _scaling.HealthForRound(round),
                         _scaling.SpeedForRound(round),
                         window);

            _alive.Add(zombie);
            _spawnedThisRound++;
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

        private void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                ZombieAgent z = _alive[i];
                if (z != null && z.IsAlive) continue;

                if (z != null) Destroy(z.gameObject);
                _alive.RemoveAt(i);
            }
        }

        // ---------------------------------------------------------------- ekran

        private void OnGUI()
        {
            if (!showHud) return;

            _hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };

            if (_playerHealth == null) _playerHealth = FindFirstObjectByType<DebugPlayerHealth>();

            _hud.Clear();
            _hud.Append("ZOMBI TEZGAHI (M1-04)\n")
                .Append("tur ").Append(round)
                .Append("  |  canli ").Append(_alive.Count)
                .Append('/').Append(_scaling.TotalZombiesForRound(round, 1))
                .Append("  |  dogum ").Append(spawning ? "acik" : "kapali").Append('\n')
                .Append("zombi cani ").Append(_scaling.HealthForRound(round).ToString("F0"))
                .Append("  hiz ").Append(_scaling.SpeedForRound(round).ToString("F1"))
                .Append(" m/s (").Append(_scaling.SpeedTierForRound(round).ToString()).Append(")\n");

            if (_playerHealth != null)
            {
                _hud.Append("oyuncu cani ").Append(_playerHealth.Current.ToString("F0"))
                    .Append('/').Append(_playerHealth.Max.ToString("F0")).Append('\n');
            }

            _hud.Append("F6 dogum  F7/F8 tur  F9 hepsini oldur  sol tik ates");

            GUI.Label(new Rect(12f, 120f, 520f, 120f), _hud.ToString(), _hudStyle);
        }
    }
}
