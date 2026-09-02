using System.Text;
using Bunker.Gameplay;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// Gri kutu savaş HUD'u: nişangâh, isabet işareti, şarjör ve puan. M1-06.
    ///
    /// <para><b>Neden IMGUI:</b> bu geçici bir HUD. Gerçek arayüz (UGUI/UI Toolkit,
    /// yerelleştirme anahtarları, denetleyici gezinmesi, güvenli alan) M1-11'in ve
    /// <c>design/ux/</c>'nin işi. Şu anki tek amaç, silahın hissiyatını yargılayabilmek
    /// için gereken minimum bilgiyi ekrana koymak — <b>nişangâhsız bir nişancı oyunu
    /// test edilemez.</b></para>
    ///
    /// <para><b>İsabet işareti pazarlık konusu değil</b> (M1-06 kapsamı): isabetin ilk
    /// işi bir şeyin olduğunu söylemektir. O işaret olmadan sünger hissi kaçınılmazdır
    /// ve silah ne kadar iyi ayarlanırsa ayarlansın kötü hissettirir.</para>
    ///
    /// <para><b>Bağımlılık yönü:</b> UI Gameplay'i okur; Gameplay UI'ı bilmez. Hiçbir
    /// şey <c>Bunker.UI</c>'ye bağımlı olamaz (ARCHITECTURE.md).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Combat HUD (gecici)")]
    public sealed class CombatHud : MonoBehaviour
    {
        [Header("Nisangah")]
        [SerializeField] private float crosshairSizePixels = 10f;
        [SerializeField] private float crosshairGapPixels = 4f;
        [SerializeField] private float crosshairThicknessPixels = 2f;

        private PlayerWeapon _weapon;
        private PlayerScore _score;
        private float _searchTimer;

        private readonly StringBuilder _text = new StringBuilder(128);
        private GUIStyle _style;
        private Texture2D _pixel;

        private void Awake()
        {
            // Tek piksellik doku bir kez yaratilir: OnGUI icinde doku yaratmak kare
            // basina tahsis demektir (csharp-code.md).
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            // Awake'in yarattigini OnDestroy yok eder, yoksa her sahne gecisinde bir
            // doku sizar.
            if (_pixel != null) Destroy(_pixel);
        }

        private void Update()
        {
            if (_weapon != null && _score != null) return;

            // Yerel oyuncu ag tarafindan gec gelir; her kare aramak yerine saniyede
            // iki kez bakilir (csharp-code.md: kare basina Find yasak).
            _searchTimer -= Time.deltaTime;
            if (_searchTimer > 0f) return;
            _searchTimer = 0.5f;

            FindLocalPlayer();
        }

        private void FindLocalPlayer()
        {
            PlayerWeapon[] weapons = FindObjectsByType<PlayerWeapon>(FindObjectsSortMode.None);

            for (int i = 0; i < weapons.Length; i++)
            {
                if (!weapons[i].isLocalPlayer) continue;

                _weapon = weapons[i];
                _score = weapons[i].GetComponent<PlayerScore>();
                return;
            }
        }

        private void OnGUI()
        {
            if (_weapon == null) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = false };

            DrawCrosshair();
            DrawReadout();
        }

        private void DrawCrosshair()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            bool hit = _weapon.HitMarkerRemaining > 0f;

            // Isabet isareti nisangahin KENDISINI degistirir: ayri bir sekil ciziip
            // ustune bindirmek, kaosun icinde iki ayri sey okumaya zorlar (PILLAR-04).
            Color color = hit
                ? (_weapon.LastHitWasHeadshot ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.4f, 0.3f))
                : new Color(1f, 1f, 1f, 0.75f);

            float gap = crosshairGapPixels + (hit ? 3f : 0f);
            float len = crosshairSizePixels;
            float t = crosshairThicknessPixels;

            GUI.color = color;

            Rect(cx - t * 0.5f, cy - gap - len, t, len);   // ust
            Rect(cx - t * 0.5f, cy + gap, t, len);         // alt
            Rect(cx - gap - len, cy - t * 0.5f, len, t);   // sol
            Rect(cx + gap, cy - t * 0.5f, len, t);         // sag

            GUI.color = Color.white;
        }

        private void Rect(float x, float y, float w, float h)
        {
            GUI.DrawTexture(new UnityEngine.Rect(x, y, w, h), _pixel);
        }

        private void DrawReadout()
        {
            _text.Clear();

            if (_weapon.IsReloading)
            {
                _text.Append("DOLUM  ")
                     .Append(Mathf.RoundToInt(_weapon.ReloadProgress01 * 100f)).Append('%');
            }
            else
            {
                _text.Append(_weapon.RoundsInMagazine).Append(" / ").Append(_weapon.Reserve);

                if (_weapon.RoundsInMagazine == 0) _text.Append("   BOS - R");
            }

            // Sag alt: mermi. Ekranin ortasindan uzak, ama goz ucuyla okunacak yerde.
            GUI.Label(new UnityEngine.Rect(Screen.width - 220f, Screen.height - 60f, 200f, 40f),
                      _text.ToString(), _style);

            if (_score == null) return;

            _text.Clear();
            _text.Append("PUAN ").Append(_score.Spendable);

            GUI.Label(new UnityEngine.Rect(24f, Screen.height - 60f, 260f, 40f),
                      _text.ToString(), _style);
        }
    }
}
