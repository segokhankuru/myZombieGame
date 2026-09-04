using System.Text;
using Bunker.Gameplay;
using Bunker.Systems.Cards;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Gri kutu kart draft ekranı. SYS-02 §7b, `design/ux/draft-ekrani.md`.
    ///
    /// <para><b>Neden IMGUI:</b> <see cref="CombatHud"/> ve <see cref="GameOverHud"/>
    /// ile aynı gerekçe — bu bir arayüz değil, bir ölçüm aracı. Slot makinesi dönüşü,
    /// dört oyuncunun yan yana yuvaları ve nadirlik gösterimi gerçek arayüzün işi.
    /// Buradaki soru "güzel mi" değil, <b>"seçim bir karar gibi mi hissettiriyor"</b>.</para>
    ///
    /// <para><b>Varsayılan eylem seçmek, yenilemek değil</b> (draft-ekrani.md): yenileme
    /// düğmeleri kartlardan görsel olarak geride durur. Aksi hâlde her tur altı ek
    /// karar, PILLAR-03'ün "kesintisiz tur" sözünü sıklık üzerinden aşındırır.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Card Draft HUD (gecici)")]
    public sealed class CardDraftHud : MonoBehaviour
    {
        [SerializeField] private CardDraftController controller;

        private readonly StringBuilder _text = new StringBuilder(256);

        private GUIStyle _titleStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private Texture2D _pixel;

        private CardDraft _draft;

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
            CardSignals.DraftOpened += OnDraftOpened;
            CardSignals.DraftClosed += OnDraftClosed;

            // Bu nesne draft acildiktan SONRA etkinlesmis olabilir; olayi kacirmak
            // ekranin hic gelmemesi demek olurdu.
            if (CardSignals.IsDraftOpen) _draft = CardSignals.Draft;
        }

        private void OnDisable()
        {
            // Statik yayin noktasina abone olan herkes OnDisable'da birakir.
            CardSignals.DraftOpened -= OnDraftOpened;
            CardSignals.DraftClosed -= OnDraftClosed;
        }

        private void OnDraftOpened(CardDraft draft) => _draft = draft;

        private void OnDraftClosed(CardDefinition picked) => _draft = null;

        private void Update()
        {
            if (_draft == null || controller == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 1/2/3 secer, Q/W/E yeniler. Fare gerektirmez: imlec oyun sirasinda
            // kilitli ve draft icin acip kapamak kare kaybettirir.
            if (keyboard.digit1Key.wasPressedThisFrame) controller.Pick(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) controller.Pick(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) controller.Pick(2);
            else if (keyboard.qKey.wasPressedThisFrame) controller.RerollSlot(0, null);
            else if (keyboard.wKey.wasPressedThisFrame) controller.RerollSlot(1, null);
            else if (keyboard.eKey.wasPressedThisFrame) controller.RerollSlot(2, null);
        }

        private void OnGUI()
        {
            if (_draft == null) return;

            EnsureStyles();

            // Karartma: sahne gorunur kalir ama okunmaz olur.
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.5f - 190f;

            GUI.Label(new Rect(cx - 300f, top, 600f, 50f), "KART SEC", _titleStyle);

            const float cardWidth = 240f;
            const float cardHeight = 200f;
            const float gap = 24f;

            float totalWidth = _draft.SlotCount * cardWidth + (_draft.SlotCount - 1) * gap;
            float x = cx - totalWidth / 2f;
            float y = top + 60f;

            for (int i = 0; i < _draft.SlotCount; i++)
            {
                DrawSlot(i, new Rect(x + i * (cardWidth + gap), y, cardWidth, cardHeight));
            }

            GUI.Label(new Rect(cx - 300f, y + cardHeight + 26f, 600f, 24f),
                      "1 / 2 / 3  sec        Q / W / E  yenile", _hintStyle);

            DrawLoadout(cx, y + cardHeight + 60f);
        }

        private void DrawSlot(int index, Rect rect)
        {
            CardDefinition card = _draft.Slot(index);

            if (!card.IsValid)
            {
                // Havuz tukendi. Bos yuvayi GIZLEMEK, uc secenek varmis gibi
                // gostermekten daha durust.
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(rect, _pixel);
                GUI.color = Color.white;
                GUI.Label(rect, "\n\n(havuz tukendi)", _bodyStyle);
                return;
            }

            GUI.color = TagColor(card.Tag);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4f), _pixel);
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 4f, rect.width, rect.height - 4f), _pixel);
            GUI.color = Color.white;

            var inner = new Rect(rect.x + 14f, rect.y + 16f, rect.width - 28f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width, 26f),
                      $"{index + 1}.  {card.DisplayName}", _nameStyle);

            GUI.Label(new Rect(inner.x, inner.y + 34f, inner.width, 90f), card.Description, _bodyStyle);

            // Etiket ve yenileme durumu, kartin altinda ve SOLUK: varsayilan eylem
            // secmek, yenilemek degil.
            string reroll = _draft.Reroll(index) switch
            {
                RerollState.FreeAvailable => "yenile: ucretsiz",
                RerollState.PaidAvailable => "yenile: puanli",
                _ => "yenileme bitti"
            };

            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(inner.x, rect.yMax - 46f, inner.width, 20f),
                      TagName(card.Tag), _bodyStyle);
            GUI.Label(new Rect(inner.x, rect.yMax - 28f, inner.width, 20f), reroll, _bodyStyle);
            GUI.color = Color.white;
        }

        private void DrawLoadout(float cx, float y)
        {
            CardLoadout loadout = CardSignals.Loadout;

            _text.Clear();
            _text.Append("ELINDE  ").Append(loadout.Count).Append(" kart");

            AppendTag(loadout, CardTag.Ballistics, "Balistik");
            AppendTag(loadout, CardTag.Demolition, "Yikim");
            AppendTag(loadout, CardTag.Blood, "Kan");
            AppendTag(loadout, CardTag.Tempo, "Tempo");
            AppendTag(loadout, CardTag.Loot, "Ganimet");

            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(cx - 300f, y, 600f, 24f), _text.ToString(), _hintStyle);
            GUI.color = Color.white;
        }

        private void AppendTag(CardLoadout loadout, CardTag tag, string label)
        {
            int count = loadout.TagCount(tag);
            if (count == 0) return;

            _text.Append("   ").Append(label).Append(' ').Append(count);

            // Etiket bonusu build kimliginin gorunur oldugu an - isaretlenmeli.
            if (loadout.HasTagBonus(tag)) _text.Append(" *BONUS*");
        }

        private static string TagName(CardTag tag) => tag switch
        {
            CardTag.Ballistics => "BALISTIK",
            CardTag.Demolition => "YIKIM",
            CardTag.Blood => "KAN",
            CardTag.Tempo => "TEMPO",
            CardTag.Loot => "GANIMET",
            _ => "TAKIM"
        };

        /// <summary>
        /// Etiket rengi. <b>Bilgi renkle tek başına taşınmıyor</b> (ui-code.md):
        /// etiketin adı da kartın altında yazıyor.
        /// </summary>
        private static Color TagColor(CardTag tag) => tag switch
        {
            CardTag.Ballistics => new Color(0.95f, 0.75f, 0.25f),
            CardTag.Demolition => new Color(0.90f, 0.40f, 0.20f),
            CardTag.Blood => new Color(0.85f, 0.25f, 0.30f),
            CardTag.Tempo => new Color(0.35f, 0.75f, 0.90f),
            CardTag.Loot => new Color(0.45f, 0.80f, 0.45f),
            _ => new Color(0.75f, 0.55f, 0.95f)
        };

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 34, alignment = TextAnchor.MiddleCenter, richText = false
            };

            _nameStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, alignment = TextAnchor.UpperLeft, richText = false
            };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = false
            };

            _hintStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter, richText = false
            };
        }
    }
}
