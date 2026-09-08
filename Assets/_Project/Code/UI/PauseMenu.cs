using Bunker.Systems.Cards;
using Bunker.Systems.Net;
using Bunker.Systems.Rounds;
using Bunker.Systems.Ui;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Oyun içi duraklatma menüsü (ESC). M-04.
    ///
    /// <para><b>Solo'da dünya durur, co-op'ta durmaz.</b> Dört kişilik bir oturumda bir
    /// oyuncunun menü açması diğerlerinin oyununu donduramaz — bu yüzden co-op'ta menü
    /// yalnızca bir ekran, oyuncu <b>savunmasız</b> kalır ve ekran bunu söyler.
    /// Söylemeseydi, tezgâhın 14. turda yaptığı şeyi bu menü tekrarlardı: menünün
    /// arkasında ölmek.</para>
    ///
    /// <para><b>Zamanı durdurmak <c>Time.timeScale</c> ile</b> ve yalnızca solo'da:
    /// host bir oturumda timeScale sıfır, ağ paketlerini de durdurur ve bağlantı
    /// zaman aşımına düşer.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Pause Menu")]
    public sealed class PauseMenu : MonoBehaviour
    {
        private bool _settingsOpen;
        private bool _cursorWasLocked;


        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private Texture2D _pixel;

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);

            // Bilesen menü açıkken yok edilirse zaman DONMUS kalirdi ve oyun bir daha
            // hic akmazdi - sebebi bulunmasi en zor hata turu.
            RestoreTime();
        }

        private void OnDisable()
        {
            if (MenuSignals.IsPauseOpen) Close();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            // OLUM EKRANI ACIKKEN DURAKLATMA YOK (2026-09-06). Skor ekraninin
            // ustune ikinci bir menu acmak, oyuncunun hangi "ANA MENU" dugmesine
            // bastigini belirsiz kilar ve solo'da Time.timeScale'i sifirlar - yeniden
            // baslatma o noktadan sonra donmus bir dunyaya doner.
            if (RunSignals.IsRunOver) return;

            // Kart ekrani acikken ESC duraklatmaz: kart secimi turun zorunlu bir
            // adimi ve arkasinda ikinci bir menu acmak, hangi ekranin canli oldugunu
            // okunmaz yapardi.
            if (CardSignals.IsDraftOpen) return;

            if (MenuSignals.IsPauseOpen) Close();
            else Open();
        }

        private void Open()
        {
            // Tezgah acikken ESC'e basmak once tezgahi kapatir: ic ice iki menu,
            // "geri" tusunun ne yapacagini belirsiz kilar.
            CardSignals.SetShopOpen(false);

            MenuSignals.SetPauseOpen(true);

            _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // ZAMANIN TEK SAHIBI WorldClock (2026-09-07): tezgahlar da durduruyor
            // ve iki sahip olsaydi tezgahi kapatmak, hala acik olan menunun
            // arkasinda dunyayi yeniden akitirdi.
            WorldClock.Set(WorldClock.Reason.PauseMenu, true);
        }

        private void Close()
        {
            _settingsOpen = false;
            SettingsPanel.Close();

            MenuSignals.SetPauseOpen(false);
            RestoreTime();

            if (!_cursorWasLocked) return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Bu menünün zaman üzerindeki hakkını bırakır. <b>Saati doğrudan yazmaz</b>
        /// (2026-09-07): tezgâh hâlâ açıksa dünya durmaya devam etmeli.
        /// </summary>
        private void RestoreTime() => WorldClock.Set(WorldClock.Reason.PauseMenu, false);

        private void OnGUI()
        {
            if (!MenuSignals.IsPauseOpen) return;

            EnsureStyles();

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float cx = Screen.width * 0.5f;
            float y = Screen.height * 0.5f - 200f;

            GUI.Label(new Rect(cx - 300f, y, 600f, 44f), "DURAKLATILDI", _titleStyle);
            y += 60f;

            if (!WorldClock.IsFrozen)
            {
                // Co-op: dunya donmuyor. Bunu SOYLEMEK sart - menunun arkasinda
                // olmek, oyuncunun haksizliga ugradigini hissettigi olum turudur.
                GUI.color = new Color(0.95f, 0.65f, 0.25f);
                GUI.Label(new Rect(cx - 300f, y, 600f, 40f),
                          "Oyun DEVAM EDIYOR - ortakli oturumda dunya durmaz.\n" +
                          "Menu acikken savunmasizsin.", _labelStyle);
                GUI.color = Color.white;
                y += 46f;
            }

            const float w = 300f;
            const float h = 40f;
            const float gap = 10f;

            if (_settingsOpen)
            {
                SettingsPanel.Draw(new Rect(cx - w / 2f, y, w, SettingsPanel.Height), _labelStyle);
                y += SettingsPanel.Height;

                if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "GERI"))
                {
                    SettingsPanel.Close();
                    _settingsOpen = false;
                }

                return;
            }

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "DEVAM ET")) Close();
            y += h + gap;

            // Davet YALNIZCA gercekten mumkunse cizilir (ADR-0007): Steam kapaliysa
            // ya da bu makine host degilse dugme hic gorunmez. Calismayan bir dugme,
            // olmayan bir ozelligi varmis gibi gosterir.
            if (SessionSignals.CanInvite)
            {
                if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "ARKADAS DAVET ET (Steam)"))
                {
                    SessionSignals.RequestInvite();
                }

                y += h + gap;

                // Kod, davetin YEDEGI: davet penceresi yalnizca oyun Steam uzerinden
                // baslatildiginda guvenilir calisir, kod her kosulda calisir.
                GUI.Label(new Rect(cx - w / 2f, y, w, 22f),
                          $"Katilma kodu: {SessionSignals.JoinCode}", _labelStyle);

                if (GUI.Button(new Rect(cx + w / 2f - 90f, y - 2f, 90f, 24f), "kopyala"))
                {
                    GUIUtility.systemCopyBuffer = SessionSignals.JoinCode;
                }

                y += 30f + gap;
            }

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "AYARLAR")) _settingsOpen = true;
            y += h + gap;

            if (GUI.Button(new Rect(cx - w / 2f, y, w, h), "ANA MENUYE DON"))
            {
                // Once menuyu kapat: zaman geri akmadan sahne degistirmek, yeni
                // sahnenin donmus zamanla acilmasi demek olurdu.
                Close();
                SessionSignals.RequestLeave();
            }
        }

        // Solo kararini artik WorldClock veriyor (2026-09-07): zamani durdurmaya kim
        // karar veriyorsa "durdurulabilir mi" sorusunu da o cevaplamali.

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 30, alignment = TextAnchor.MiddleCenter, richText = false };

            _labelStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = false };
        }
    }
}
