using Bunker.Gameplay;
using Bunker.Systems.Cards;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Duvardaki tezgâhın menüsü. <b>E</b> ile açılır, <b>E</b> ya da <b>Esc</b> ile
    /// kapanır. SYS-02 §7e.
    ///
    /// <para><b>Kart ekranından ayrı</b> (geliştirici, 2026-09-04). Kart seçimi turun
    /// zorunlu ödülü; tezgâh oyuncunun gitmeyi seçtiği bir harcama noktası. Aynı
    /// ekranda olmaları ikincisini birincisinin eklentisi gibi gösteriyordu.</para>
    ///
    /// <para><b>Turu durdurmaz.</b> Draft açıkken tur ilerlemiyor, ama tezgâh
    /// oyuncunun kendi kararı — molayı onunla harcamak bir bedel olmalı. Mola biterse
    /// menü açıkken tur başlar; kapatıp koşmak oyuncunun işi.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Shop HUD (gecici)")]
    public sealed class ShopHud : MonoBehaviour
    {
        [SerializeField] private ShopController shop;

        private static readonly ShopLine[] Lines =
        {
            ShopLine.Health, ShopLine.Damage, ShopLine.FireRate, ShopLine.Magazine
        };

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private Texture2D _pixel;

        private PlayerScore _score;
        private float _searchTimer;
        private bool _cursorWasLocked;
        private bool _open;

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        private void OnEnable() => CardSignals.ShopVisibilityChanged += OnVisibilityChanged;

        private void OnDisable()
        {
            CardSignals.ShopVisibilityChanged -= OnVisibilityChanged;

            // Menu acikken bilesen kapanirsa imlec kilitli kalirdi.
            if (_open) RestoreCursor();
        }

        private void OnVisibilityChanged(bool open)
        {
            _open = open;

            if (open)
            {
                _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            RestoreCursor();
        }

        private void RestoreCursor()
        {
            if (!_cursorWasLocked) return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!_open) return;

            FindLocalScore();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // E ile acildi, E ile kapanir - ayni tus, ogrenilecek tek kural.
            // Esc de kapatir cunku her menuden Esc'le cikilmasi beklenir.
            if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                CardSignals.SetShopOpen(false);
            }
        }

        private void FindLocalScore()
        {
            if (_score != null) return;

            _searchTimer -= Time.unscaledDeltaTime;
            if (_searchTimer > 0f) return;
            _searchTimer = 0.5f;

            foreach (PlayerScore candidate in FindObjectsByType<PlayerScore>(FindObjectsSortMode.None))
            {
                if (candidate.isLocalPlayer) { _score = candidate; break; }
            }
        }

        private void OnGUI()
        {
            if (!_open || shop == null || shop.Shop == null) return;

            EnsureStyles();

            GUI.color = new Color(0f, 0f, 0f, 0.80f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.5f - 170f;

            GUI.Label(new Rect(cx - 300f, top, 600f, 44f), "TEZGAH", _titleStyle);

            int points = _score != null ? _score.Spendable : 0;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(cx - 300f, top + 44f, 600f, 24f), $"puan: {points}", _hintStyle);
            GUI.color = Color.white;

            const float lineWidth = 210f;
            const float lineHeight = 108f;
            const float gap = 14f;

            float totalWidth = Lines.Length * lineWidth + (Lines.Length - 1) * gap;
            float x = cx - totalWidth / 2f;
            float y = top + 84f;

            for (int i = 0; i < Lines.Length; i++)
            {
                DrawLine(Lines[i], new Rect(x + i * (lineWidth + gap), y, lineWidth, lineHeight));
            }

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(cx - 300f, y + lineHeight + 20f, 600f, 24f),
                      "E ya da Esc  -  kapat", _hintStyle);
            GUI.color = Color.white;
        }

        private void DrawLine(ShopLine line, Rect rect)
        {
            ShopState state = shop.Shop;

            int tier = state.Tier(line);
            int max = state.MaxTier(line);

            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(rect, _pixel);
            GUI.color = Color.white;

            var inner = new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 22f);

            GUI.Label(inner, ShopState.DisplayName(line), _bodyStyle);

            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(inner.x, inner.y + 20f, inner.width, 20f),
                      ShopState.Description(line), _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.y + 38f, inner.width, 20f),
                      $"kademe {tier}/{max}", _bodyStyle);
            GUI.color = Color.white;

            var button = new Rect(rect.x + 12f, rect.yMax - 32f, rect.width - 24f, 24f);

            if (state.IsMaxed(line))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(button, "   tavanda", _bodyStyle);
                GUI.color = Color.white;
                return;
            }

            // Puan yetmiyorsa dugme kapali. Fiyat yaninda yaziyor, yani bilgi renkle
            // TEK BASINA tasinmiyor (ui-code.md).
            bool wasEnabled = GUI.enabled;
            GUI.enabled = shop.CanAfford(line, _score);

            if (GUI.Button(button, $"{state.CostFor(line)} puan")) shop.Buy(line, _score);

            GUI.enabled = wasEnabled;
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 32, alignment = TextAnchor.MiddleCenter, richText = false };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = false };

            _hintStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = false };
        }
    }
}
