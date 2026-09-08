using System.Text;
using Bunker.Gameplay;
using Bunker.Systems.Net;
using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Run sonu skor ekranı. M1-11.
    ///
    /// <para><b>Neden IMGUI:</b> <see cref="CombatHud"/> ile aynı gerekçe — bu gri kutu
    /// bir arayüz değil, bir ölçüm aracı. Gerçek ekran (UGUI, yerelleştirme anahtarları,
    /// denetleyici gezinmesi, güvenli alan) sanat aşamasının işi. Buradaki soru "güzel
    /// mi" değil, <b>"ne kadar iyi olduğumu okuyabiliyor muyum"</b>.</para>
    ///
    /// <para><b>Ekran suçlamaz, davet eder.</b> En büyük yazı ulaşılan turdur — oyuncunun
    /// gerçekten kovaladığı sayı odur. Hemen altında tek bir satır: R'ye bas. Ölüm ile
    /// bir sonraki denemenin başlangıcı arasındaki mesafe ne kadar kısaysa, ÇK-17'nin
    /// cevabı o kadar dürüst olur.</para>
    ///
    /// <para><b>Bağımlılık yönü:</b> UI yalnızca <c>Bunker.Systems</c>'i okur. Turu
    /// yürüten <c>ZombieDirector</c> <c>Bunker.AI</c>'da ve buradan <b>görünmez</b>;
    /// aradaki tek köprü <see cref="RunSignals"/>.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Game Over HUD (gecici)")]
    public sealed class GameOverHud : MonoBehaviour
    {
        [Tooltip("Skor ekrani acildiktan sonra R'nin dinlenmeye baslamasi icin gecen " +
                 "sure. Olurken basili tutulan bir tus, ekrani okumadan yeni run " +
                 "baslatmasin diye. Denge degeri degil - girdi korumasi.")]
        [SerializeField] private float restartLockoutSeconds = 0.75f;

        private readonly StringBuilder _text = new StringBuilder(256);

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private Texture2D _pixel;

        private RunSummary _summary;
        private bool _visible;
        private float _shownAt;

        // Ozet dondurulmustur ve degismez, dolayisiyla metin BIR KEZ kurulur.
        // OnGUI kare basina birden fazla kez cagrilir (Layout + Repaint); metni
        // orada kurmak, ekran acik kaldigi surece kare basina birkac tahsis demek
        // olurdu (ui-code.md: kare basina string tahsisi yok).
        private string _body = string.Empty;
        private string _title = string.Empty;

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
            if (_pixel != null) Destroy(_pixel);
        }

        private void OnEnable()
        {
            RunSignals.RunEnded += OnRunEnded;
            RunSignals.RunRestarted += OnRunRestarted;

            // Bu nesne run bittikten SONRA acilmis olabilir (sahne gecisi, gec
            // etkinlestirme). Olayi kacirmis olmak, ekranin hic gelmemesi demek
            // olurdu - o yuzden mevcut durum da okunur.
            if (RunSignals.IsRunOver) OnRunEnded(RunSignals.LastSummary);
        }

        private void OnDisable()
        {
            // Statik yayin noktasina abone olan herkes OnDisable'da birakir
            // (RunSignals'in iki kuralindan biri).
            RunSignals.RunEnded -= OnRunEnded;
            RunSignals.RunRestarted -= OnRunRestarted;
        }

        private void Update()
        {
            if (!_visible) return;

            // IMLECI BU EKRAN KENDI ACAR (2026-09-06, oyun testi: "olunce gelen menude
            // cikis yapamiyor").
            //
            // Onceki halde imleci PlayerController serbest birakiyordu - ama yalnizca
            // 'isLocalPlayer' ise, yalnizca RunEnded olayi ona ulastiysa ve yalnizca o
            // an etkinse. Uc kosuldan biri tutmadiginda ekran aciliyor, dugmeler
            // ciziliyor ve imlec KILITLI kaldigi icin hicbirine tiklanamiyor. Bir
            // ekranin tiklanabilir olmasi, baska bir nesnenin durumuna emanet
            // edilemez.
            //
            // Her karede yazilmasinin sebebi: baska bir sistem (duraklatma, yeniden
            // dogus) imleci geri kilitlerse ekran yine tiklanabilir kalmali.
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Time.unscaledTime - _shownAt < restartLockoutSeconds) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // KLAVYE YEDEGI. Fare yolu herhangi bir sebeple bozuksa (imlec kilidi,
            // ustte cizen baska bir IMGUI, denetleyiciyle oynama) oyuncu yine de
            // oturumdan cikabilmeli. Cikisi olmayan bir ekran, oyuncuya Alt+F4'u
            // ogretir.
            if (keyboard.mKey.wasPressedThisFrame)
            {
                SessionSignals.RequestLeave();
                return;
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                AppExit.Quit();
                return;
            }

            // TODO(netcode-programmer, M-02): yeniden baslatma burada DOGRUDAN
            // cagriliyor. Solo host'ta dogru; uzak istemci geldiginde bu bir Command
            // olmali, cunku run'i baslatmak kalici sonucu olan bir karardir ve
            // otorite host'tadir (ADR-0004). Tek satirlik degisiklik, ama M-01'de
            // yazilmazsa M-02'de aranacak olan tam da budur.
            if (keyboard.rKey.wasPressedThisFrame) RunSignals.RequestRestart();
        }

        private void OnRunEnded(RunSummary summary)
        {
            _summary = summary;
            _visible = true;

            BuildText();

            // unscaledTime: run sonunda zaman olcegi degistirilirse (M-01'de
            // degismiyor, sonra degisebilir) kilit suresi bozulmasin.
            _shownAt = Time.unscaledTime;
        }

        private void OnRunRestarted() => _visible = false;

        private void OnGUI()
        {
            if (!_visible) return;

            // EN USTTE CIZILIR VE GIRDIYI ONCE BU ALIR. IMGUI'de kucuk depth ustte
            // demektir. Ayni nesne uzerinde alti ayri OnGUI kosuyor (CombatHud,
            // StatsHud, PerfHud, DamageNumbersHud, ShopHud, PauseMenu) ve varsayilan
            // depth'te siralama tanimsiz - olum ekraninin dugmelerinin bir digerinin
            // altinda kalmasi buna birakilamaz.
            GUI.depth = -1000;

            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                alignment = TextAnchor.MiddleCenter,
                richText = false
            };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };

            _hintStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                richText = false
            };

            // Karartma: sahne gorunur kalir ama okunmaz olur. Tam siyah bir ekran,
            // oyuncunun nerede oldugunu unutturur ve yeni run'i yabancilastirir.
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.5f - 170f;

            GUI.Label(new Rect(cx - 300f, top, 600f, 60f), "OLDUN", _titleStyle);

            // En buyuk sayi ulasilan turdur: oyuncunun gercekten kovaladigi sayi odur.
            GUI.Label(new Rect(cx - 300f, top + 56f, 600f, 60f), _title, _titleStyle);

            GUI.Label(new Rect(cx - 150f, top + 140f, 320f, 180f), _body, _bodyStyle);

            // SENI NE OLDURDU (2026-09-05). Iki oturumdur "birden oldum" cumlesi
            // tahmine dayaniyordu; oldureni yazmak o tahmini bitirir. 30 hasar bir
            // zombi vurusu, 300 hasar bambaska bir sey demektir.
            if (CombatFeedback.LastLethalAmount > 0f)
            {
                string kind = CombatFeedback.LastLethalKind switch
                {
                    DamageKind.Melee => "zombi vurusu",
                    DamageKind.Environment => "patlama / cevre",
                    _ => "mermi"
                };

                GUI.color = new Color(1f, 0.5f, 0.4f);
                GUI.Label(new Rect(cx - 300f, top + 290f, 600f, 26f),
                          $"son vurus: {Mathf.RoundToInt(CombatFeedback.LastLethalAmount)} hasar  ({kind})",
                          _hintStyle);
                GUI.color = Color.white;
            }

            // ÖLÜM EKRANI BIR CIKIS KAPISI DA OLMALI (2026-09-06, geliştirici):
            // yalnızca "R" yazan bir ekran, oyuncuyu ya yeniden başlamaya ya da
            // Alt+F4'e zorlar. Ölüm, oturumu bitirmenin en doğal anı — menüye dönüş
            // orada olmazsa hiçbir yerde yok demektir.
            const float w = 240f;
            const float h = 38f;
            float y = top + 315f;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "YENIDEN BASLA   (R)"))
            {
                RunSignals.RequestRestart();
            }

            y += h + 8f;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "ANA MENU   (M)"))
            {
                SessionSignals.RequestLeave();
            }

            y += h + 8f;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "CIKIS   (Q)"))
            {
                AppExit.Quit();
            }

            // Imleci Update her karede serbest tutuyor - dugmeler tiklanabilir.
        }

        /// <summary>Ekranın metnini bir kez kurar. Yalnızca run bitiminde çağrılır.</summary>
        private void BuildText()
        {
            _text.Clear();
            _text.Append("TUR ").Append(_summary.RoundReached);
            _title = _text.ToString();

            _text.Clear();

            int minutes = (int)(_summary.DurationSeconds / 60f);
            int seconds = (int)(_summary.DurationSeconds % 60f);

            _text.Append("SURE        ").Append(minutes).Append(':')
                 .Append(seconds < 10 ? "0" : string.Empty).Append(seconds).Append('\n');

            _text.Append("OLDURME     ").Append(_summary.Kills).Append('\n');

            _text.Append("KAFA        ").Append(_summary.HeadshotKills)
                 .Append("  (%").Append(Mathf.RoundToInt(_summary.HeadshotRatio01 * 100f))
                 .Append(")\n");

            _text.Append("BICAK       ").Append(_summary.MeleeKills).Append('\n');

            // Kazanilan TOPLAM gosterilir, kalan bakiye degil (SYS-01): kapi acan
            // oyuncu skor kaybetmis gibi gorunmemeli - PILLAR-02.
            _text.Append("PUAN        ").Append(_summary.PointsEarned);

            _body = _text.ToString();
        }
    }
}
