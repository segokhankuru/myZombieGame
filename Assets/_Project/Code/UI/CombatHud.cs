using System.Text;
using Bunker.Gameplay;
using Bunker.Systems.Rounds;
using Bunker.Systems.Settings;
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

        [Header("Can (M1-11)")]
        [Tooltip("Dusuk canda ekran kenarinda yanan uyarinin kalinligi, piksel. " +
                 "Denge degeri degil - okunabilirlik ayari.")]
        [SerializeField] private float lowHealthVignettePixels = 90f;

        private PlayerWeapon _weapon;
        private PlayerScore _score;
        private PlayerHealth _health;
        private PlayerRepair _repair;
        private PlayerInteract _interact;
        private PlayerController _controller;
        private float _searchTimer;

        private readonly StringBuilder _text = new StringBuilder(128);
        private GUIStyle _style;
        private GUIStyle _promptStyle;
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
                _health = weapons[i].GetComponent<PlayerHealth>();
                _repair = weapons[i].GetComponent<PlayerRepair>();
                _interact = weapons[i].GetComponent<PlayerInteract>();
                _controller = weapons[i].GetComponent<PlayerController>();
                return;
            }
        }

        private void OnGUI()
        {
            if (_weapon == null) return;

            // Run bitti: sahne HUD'u susar, ekran skor ekranina birakilir. Ust uste
            // iki arayuz, hangisinin canli oldugunu okunamaz yapar.
            if (RunSignals.IsRunOver) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, richText = false };
            _promptStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                richText = false
            };

            // Can uyarisi EN ALTTA cizilir: nisangahin ve yazinin ustune binmemeli.
            // Bilgiyi kapatan bir uyari, uyardigi seyi kotu gosterir.
            DrawHealth();
            DrawSprint();
            DrawCrosshair();
            DrawReadout();
        }

        /// <summary>
        /// Can barı ve düşük can uyarısı (AC-7).
        ///
        /// <para><b>Ölüm sürpriz olmamalı.</b> Oyuncu canının azaldığını hasar aldığı
        /// anda görmüş olmalı; yoksa ölüm haksızlık gibi okunur ve ÇK-17'nin cevabı
        /// "hayır" olur. Bilgi renkle <b>tek başına</b> taşınmıyor: barın uzunluğu da
        /// aynı şeyi söylüyor (ui-code.md).</para>
        /// </summary>
        private void DrawHealth()
        {
            if (_health == null) return;

            float fraction = _health.Fraction01;

            if (_health.IsLow)
            {
                // Ekran kenari uyarisi: dort kenarda ince bir kirmizi bant. Tam ekran
                // bir kaplama, nisan almayi zorlastirir - uyari oyunu oynanamaz
                // yapmamali.
                GUI.color = new Color(0.8f, 0.05f, 0.05f, 0.35f);

                float t = lowHealthVignettePixels;
                Rect(0f, 0f, Screen.width, t);
                Rect(0f, Screen.height - t, Screen.width, t);
                Rect(0f, 0f, t, Screen.height);
                Rect(Screen.width - t, 0f, t, Screen.height);

                GUI.color = Color.white;
            }

            // Can bari: sol altta, puanin hemen ustunde.
            const float barWidth = 220f;
            const float barHeight = 14f;
            float x = 24f;
            float y = Screen.height - 90f;

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            Rect(x, y, barWidth, barHeight);

            GUI.color = _health.IsLow ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.85f, 0.85f, 0.85f);
            Rect(x, y, barWidth * fraction, barHeight);

            GUI.color = Color.white;
        }

        /// <summary>
        /// Koşu göstergesi: can barının hemen altında ince bir çubuk (2026-09-05).
        ///
        /// <para><b>Neden bir gösterge şart:</b> koşu 4,5 saniyelik <i>sınırlı</i> bir
        /// kaynak. Göstergesi olmadan oyuncu ne zaman koşabileceğini tahmin etmek
        /// zorunda kalır ve tam kaçması gereken anda Shift'in çalışmadığını görür —
        /// oyuncunun "oyun beni yüzüstü bıraktı" diye okuduğu şey tam olarak budur.</para>
        ///
        /// <para><b>Dolu ve koşulmuyorken çizilmez</b>: hiçbir şey söylemeyen bir
        /// gösterge, ekranda yer kaplamaktan başka bir şey yapmaz.</para>
        /// </summary>
        private void DrawSprint()
        {
            if (_controller == null) return;

            float fraction = _controller.SprintFraction01;
            if (fraction >= 1f && !_controller.IsSprinting) return;

            const float barWidth = 220f;
            const float barHeight = 6f;
            float x = 24f;
            float y = Screen.height - 70f;

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            Rect(x, y, barWidth, barHeight);

            // Bilgi renkle TEK BASINA tasinmiyor (ui-code.md): cubugun uzunlugu da
            // ayni seyi soyluyor. Renk yalnizca "su an kosuyorsun"u ekliyor.
            GUI.color = _controller.IsSprinting
                ? new Color(0.45f, 0.80f, 0.95f)
                : new Color(0.45f, 0.55f, 0.60f);

            Rect(x, y, barWidth * fraction, barHeight);

            GUI.color = Color.white;
        }

        private void DrawCrosshair()
        {
            // Nisangah kapatilabilir (M-04): bazi oyuncular temiz ekran ister ve
            // silahin dogal dogrulugu zaten merkezden gecer.
            if (!GameSettings.ShowCrosshair && _weapon.HitMarkerRemaining <= 0f) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            bool hit = GameSettings.ShowHitMarker && _weapon.HitMarkerRemaining > 0f;

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

            // Tamir ipucu: ekranin ortasinin biraz altinda, cunku nisangaha bakan goz
            // onu goz ucuyla yakalar. Ipucu YALNIZCA tamir edilebilir bir sey varken
            // cikar - surekli duran bir tus hatirlatmasi gurultudur.
            if (_interact != null && _interact.HasTarget)
            {
                // Satin alma ipucu: puan yetmiyorsa soluk. Fiyati okumadan da "bu
                // simdilik alinmaz" bilgisi gecsin - ama fiyat da yaninda duruyor,
                // yani bilgi renkle TEK BASINA tasinmiyor.
                Color previous = GUI.color;
                GUI.color = _interact.CanAfford
                    ? new Color(1f, 1f, 1f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.45f);

                GUI.Label(new UnityEngine.Rect(Screen.width * 0.5f - 150f,
                                               Screen.height * 0.5f + 40f, 300f, 30f),
                          $"E    {_interact.CurrentPrompt}", _promptStyle);

                GUI.color = previous;
            }
            else if (_repair != null && _repair.HasRepairTarget)
            {
                GUI.Label(new UnityEngine.Rect(Screen.width * 0.5f - 150f,
                                               Screen.height * 0.5f + 40f, 300f, 30f),
                          "E    barikati tamir et", _promptStyle);
            }

            if (_score == null) return;

            _text.Clear();
            _text.Append("PUAN ").Append(_score.Spendable);

            GUI.Label(new UnityEngine.Rect(24f, Screen.height - 60f, 260f, 40f),
                      _text.ToString(), _style);
        }
    }
}
