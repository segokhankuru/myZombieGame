using System.Collections.Generic;
using Bunker.Audio;
using Bunker.Systems.Net;
using Bunker.Systems.Settings;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// Ana menü. M-04.
    ///
    /// <para><b>Neden bir menü gerekiyordu</b> (geliştirici, 2026-09-05): <i>"oyuna
    /// menü ekleyelim, açınca hemen seansın içine almasın."</i> Doğrudan oyuna düşen
    /// bir açılış, ayar değiştirmeyi ve arkadaş davet etmeyi imkânsız kılıyordu —
    /// oyunun kendisinden önce gelen her şeyin yeri burası.</para>
    ///
    /// <para><b>Ağ tiplerini bilmez.</b> Menü yalnızca <b>niyet</b> bildirir
    /// (<see cref="SessionSignals"/>); ne olacağına <c>Bunker.Net</c> karar verir.
    /// Steam daveti geldiğinde (ADR-0007) bu dosya değişmez: davet de bir "katıl"
    /// niyetidir.</para>
    ///
    /// <para><b>Neden IMGUI:</b> HUD'larla aynı gerekçe — bu bir arayüz değil, çalışan
    /// bir iskele. Gerçek menü UI Toolkit ile, kontrolcü navigasyonu ve yerelleştirme
    /// anahtarlarıyla gelecek.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Main Menu")]
    public sealed class MainMenu : MonoBehaviour
    {
        private enum Page
        {
            Root,
            Join,
            Settings
        }

        [Tooltip("Baslik. M-03'te yerellestirme anahtari olur.")]
        [SerializeField] private string title = "BUNKER";

        private Page _page = Page.Root;
        private string _joinAddress = "localhost";

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;
        private Texture2D _pixel;

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            _joinAddress = BunkerNetworkAddress();

            // Ses ayari menude de gecerli: oyuncunun kistigi ses, oyuna girene kadar
            // acik kalmamali.
            GameAudio.MasterVolume = GameSettings.MasterVolume;
        }

        private void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        private void OnEnable()
        {
            // Menudeyken imlec serbest. Oyundan menuye donuldugunde imlec kilitli
            // kalirsa oyuncu hicbir seye tiklayamaz - bir kez yasandi (M1-11).
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.color = new Color(0.04f, 0.04f, 0.05f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.5f - 220f;

            GUI.Label(new Rect(cx - 300f, top, 600f, 60f), title, _titleStyle);

            // Lobi HER ZAMAN oncelikli: oda acildiysa ya da bir odaya katildiysan,
            // ekranda gormen gereken sey odur - hangi sayfada oldugun degil.
            if (SessionSignals.IsInLobby)
            {
                DrawLobby(cx, top + 80f);
                DrawStatus(cx);
                return;
            }

            switch (_page)
            {
                case Page.Join: DrawJoin(cx, top + 90f); break;
                case Page.Settings: DrawSettings(cx, top + 90f); break;
                default: DrawRoot(cx, top + 90f); break;
            }

            DrawStatus(cx);
        }

        // ---------------------------------------------------------------- sayfalar

        private void DrawRoot(float cx, float y)
        {
            const float w = 320f;
            const float h = 42f;
            const float gap = 12f;

            bool busy = SessionSignals.Status == SessionStatus.Connecting;
            GUI.enabled = !busy;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "TEK KISI OYNA"))
            {
                SessionSignals.RequestSolo();
            }

            y += h + gap;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "ODA AC  (arkadaslarin katilir)"))
            {
                SessionSignals.RequestHost();
            }

            y += h + gap;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "ODAYA KATIL"))
            {
                _joinAddress = BunkerNetworkAddress();
                _page = Page.Join;
            }

            y += h + gap;
            GUI.enabled = true;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "AYARLAR")) _page = Page.Settings;

            y += h + gap;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "CIKIS")) SessionSignals.RequestQuit();
        }

        /// <summary>
        /// Lobi: oda kuruldu, oyun henüz başlamadı.
        ///
        /// <para><b>Bu ekranın tek işi beklemeyi anlamlı kılmak:</b> kim katıldı, nasıl
        /// davet edilir, ne zaman başlar. Üçünü de göstermeyen bir lobi, oyuncuya
        /// "takıldı mı?" dedirtir.</para>
        /// </summary>
        private void DrawLobby(float cx, float y)
        {
            const float w = 420f;

            GUI.Label(new Rect(cx - w / 2f, y, w, 28f),
                      SessionSignals.IsHost ? "ODAN HAZIR" : "ODADASIN", _statusStyle);
            y += 34f;

            // --- oyuncu listesi
            IReadOnlyList<string> players = SessionSignals.LobbyPlayers;

            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(new Rect(cx - w / 2f, y, w, 120f), _pixel);
            GUI.color = Color.white;

            for (int i = 0; i < 4; i++)
            {
                string label = i < players.Count ? $"{i + 1}.  {players[i]}" : $"{i + 1}.  (bos)";

                GUI.color = i < players.Count ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(new Rect(cx - w / 2f + 14f, y + 8f + i * 26f, w - 28f, 24f),
                          label, _labelStyle);
            }

            GUI.color = Color.white;
            y += 132f;

            // --- davet
            if (SessionSignals.CanInvite)
            {
                if (GUI.Button(new Rect(cx - w / 2f, y, w, 38f), "ARKADAS DAVET ET (Steam)"))
                {
                    SessionSignals.RequestInvite();
                }

                y += 44f;

                GUI.Label(new Rect(cx - w / 2f, y, w - 100f, 22f),
                          $"Katilma kodu: {SessionSignals.JoinCode}", _labelStyle);

                if (GUI.Button(new Rect(cx + w / 2f - 96f, y - 4f, 96f, 26f), "kopyala"))
                {
                    GUIUtility.systemCopyBuffer = SessionSignals.JoinCode;
                }

                y += 32f;
            }
            else if (SessionSignals.IsHost)
            {
                // Steam yoksa davet yok - ama SEBEBI yazili olsun, yoksa oyuncu
                // ozelligin bozuk oldugunu dusunur.
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(cx - w / 2f, y, w, 40f),
                          "Steam kapali: davet penceresi yok. Arkadasin ayni agdaysa " +
                          "senin yerel IP'nle katilabilir.", _labelStyle);
                GUI.color = Color.white;
                y += 46f;
            }

            y += 10f;

            // --- baslat / ayril
            if (SessionSignals.IsHost)
            {
                if (GUI.Button(new Rect(cx - w / 2f, y, w, 44f), "OYUNU BASLAT"))
                {
                    SessionSignals.RequestStartGame();
                }

                y += 50f;
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(new Rect(cx - w / 2f, y, w, 28f),
                          "Odayi acan kisinin baslatmasi bekleniyor...", _statusStyle);
                GUI.color = Color.white;
                y += 34f;
            }

            if (GUI.Button(new Rect(cx - w / 2f, y, w, 34f), "ODADAN AYRIL"))
            {
                SessionSignals.RequestLeave();
                _page = Page.Root;
            }
        }

        private void DrawJoin(float cx, float y)
        {
            const float w = 380f;

            GUI.Label(new Rect(cx - w / 2f, y, w, 24f), "ODA ADRESI", _labelStyle);
            y += 28f;

            _joinAddress = GUI.TextField(new Rect(cx - w / 2f, y, w, 30f), _joinAddress, 64);
            y += 38f;

            // Ayni kutu IKI SEYI birden kabul eder: bir IP (KCP tasimasi) ya da bir
            // Steam katilma kodu. Iki ayri kutu olsaydi oyuncu hangisini nereye
            // yazacagini bilmek zorunda kalirdi - bilmesi gereken bir sey degil.
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(cx - w / 2f, y, w, 44f),
                      "Steam davetini kabul ettiysen buraya bir sey yazman gerekmez.\n" +
                      "Elle: arkadasinin verdigi katilma kodu, ya da ayni agdaysaniz IP.",
                      _labelStyle);
            GUI.color = Color.white;

            y += 52f;

            if (GUI.Button(new Rect(cx - w / 2f, y, 180f, 38f), "KATIL"))
            {
                SessionSignals.RequestJoin(_joinAddress);
            }

            if (GUI.Button(new Rect(cx + w / 2f - 180f, y, 180f, 38f), "GERI"))
            {
                _page = Page.Root;
            }
        }

        private void DrawSettings(float cx, float y)
        {
            const float w = 380f;

            SettingsPanel.Draw(new Rect(cx - w / 2f, y, w, SettingsPanel.Height), _labelStyle);

            if (GUI.Button(new Rect(cx - w / 2f, y + SettingsPanel.Height, 180f, 38f), "GERI"))
            {
                SettingsPanel.Close();
                _page = Page.Root;
            }
        }

        private void DrawStatus(float cx)
        {
            string text = SessionSignals.Status switch
            {
                SessionStatus.Connecting => "BAGLANILIYOR...",
                SessionStatus.Failed => SessionSignals.LastError,
                _ => string.Empty
            };

            if (text.Length == 0) return;

            GUI.color = SessionSignals.Status == SessionStatus.Failed
                ? new Color(0.95f, 0.45f, 0.35f)
                : new Color(1f, 1f, 1f, 0.75f);

            GUI.Label(new Rect(cx - 400f, Screen.height - 90f, 800f, 60f), text, _statusStyle);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- yardimci

        /// <summary>
        /// Son kullanılan adres. <c>Bunker.Net</c>'i görmeden okunur: menü ağ
        /// tiplerine bağlı değil (bkz. sınıf açıklaması).
        /// </summary>
        private static string BunkerNetworkAddress() =>
            PlayerPrefs.GetString("bunker.session.lastAddress", "localhost");

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 46, alignment = TextAnchor.MiddleCenter, richText = false };

            _labelStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = false };

            _statusStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
        }
    }
}
