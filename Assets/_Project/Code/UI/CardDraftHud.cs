using System.Text;
using Bunker.Gameplay;
using Bunker.Systems.Cards;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Tur arası kart seçim ekranı. SYS-02 §7b.
    ///
    /// <para><b>Tezgâh burada DEĞİL</b> (geliştirici, 2026-09-04): <i>"tezgâh bence
    /// kartlardan bağımsız, mermi doldurma yeri gibi duvarda olmalı; E'ye basınca
    /// menüsü gelmeli."</i> Doğru ayrım: kart seçimi turun <b>ödülü</b> ve zorunlu bir
    /// andır; tezgâh bir <b>harcama</b> ve oyuncunun gitmeyi seçtiği bir yerdir.
    /// Aynı ekranda olmaları ikincisini birincisinin eklentisi gibi gösteriyordu.</para>
    ///
    /// <para><b>Fareyle</b> (geliştirici, 2026-09-04): <i>"Kartları fareyle seçeyim,
    /// tuşlar iyi olmuyor."</i> Ekran açıkken imleç serbest bırakılır ve oyuncunun
    /// bakışı kilitlenir — yoksa kart seçmeye çalışırken kamera dönerdi.</para>
    ///
    /// <para><b>Neden IMGUI:</b> diğer HUD'larla aynı gerekçe — bu bir arayüz değil,
    /// bir ölçüm aracı. Slot makinesi dönüşü, dört oyuncunun yuvaları ve nadirlik
    /// gösterimi gerçek arayüzün işi.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Card Draft HUD (gecici)")]
    public sealed class CardDraftHud : MonoBehaviour
    {
        [SerializeField] private CardDraftController controller;

        [Tooltip("Ekran acildiktan sonra secimin KILITLI kaldigi sure. Turun son " +
                 "zombisini oldururken basili tutulan fare, ekran acilir acilmaz kart " +
                 "seciyordu - oyuncunun hic gormedigi bir secim.")]
        [SerializeField] private float pickLockSeconds = 0.8f;

        private readonly StringBuilder _text = new StringBuilder(256);

        private GUIStyle _titleStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private Texture2D _pixel;

        private CardDraft _draft;
        private PlayerScore _score;
        private float _searchTimer;
        private bool _cursorWasLocked;

        // Secim kilidi (2026-09-07). Iki kosul birden aranir: sure dolmali VE fare
        // tusu bir kez BIRAKILMIS olmali - suresi dolan bir kilit, hala basili duran
        // parmagin altinda kendiliginden acilirdi.
        private float _openedAt;
        private bool _mouseReleasedSinceOpen;

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

        private void OnEnable()
        {
            CardSignals.DraftOpened += OnDraftOpened;
            CardSignals.DraftClosed += OnDraftClosed;

            if (CardSignals.IsDraftOpen) OnDraftOpened(CardSignals.Draft);
        }

        private void OnDisable()
        {
            CardSignals.DraftOpened -= OnDraftOpened;
            CardSignals.DraftClosed -= OnDraftClosed;

            // Ekran kapanmadan bilesen kapanirsa imlec kilitli kalirdi ve oyuncu
            // fare kullanamazdi.
            if (_draft != null) ReleaseCursor();
        }

        private void OnDraftOpened(CardDraft draft)
        {
            _draft = draft;

            // Kilit her acilista bastan kurulur: ekran, oyuncunun ATES ETTIGI anda
            // aciliyor (turun son zombisi) ve o an fare basili.
            _openedAt = Time.unscaledTime;
            _mouseReleasedSinceOpen = false;

            _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDraftClosed(CardDefinition picked)
        {
            _draft = null;
            ReleaseCursor();
        }

        /// <summary>İmleci ekran açılmadan önceki hâline döndürür.</summary>
        private void ReleaseCursor()
        {
            if (!_cursorWasLocked) return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Seçim açıldı mı. <b>Süre VE fare bırakılmış olmalı</b> (2026-09-07).
        ///
        /// <para><b>Bulgu:</b> <i>"turu tamamladığın son zombiyi öldürünce anında kart
        /// seçimi geliyor, farkında olmadan seçim yapma riski oluyor."</i> Ekran, tam
        /// oyuncunun ateş ettiği karede açılıyor; imleç ekranın ortasında beliriyor ve
        /// hâlâ basılı duran tuş, ortadaki kartı seçiyordu — oyuncunun hiç görmediği,
        /// geri alınamayan bir karar.</para>
        ///
        /// <para><b>Neden yalnızca süre yetmez:</b> otomatik ateşte tuş saniyelerce
        /// basılı kalır; süreli bir kilit, parmağın altında kendiliğinden açılır ve
        /// aynı kaza bir saniye gecikmeyle olurdu.</para>
        /// </summary>
        private bool PickUnlocked =>
            _mouseReleasedSinceOpen && Time.unscaledTime - _openedAt >= pickLockSeconds;

        private void Update()
        {
            if (_draft != null && !_mouseReleasedSinceOpen)
            {
                Mouse mouse = Mouse.current;

                // Fare yoksa (pad, dokunmatik) kilit yalnizca sureye kalir - aksi
                // halde ekran hic acilmaz.
                if (mouse == null || !mouse.leftButton.isPressed) _mouseReleasedSinceOpen = true;
            }

            if (_draft == null || _score != null) return;

            // Yerel oyuncu ag tarafindan gec gelir; her kare aramak yerine saniyede
            // iki kez bakilir (csharp-code.md: kare basina Find yasak).
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
            if (_draft == null) return;

            EnsureStyles();

            GUI.color = new Color(0f, 0f, 0f, 0.80f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.5f - 260f;

            GUI.Label(new Rect(cx - 300f, top, 600f, 44f), "KART SEC", _titleStyle);

            // Kilitliyken sebebi GORUNUR: tiklamayi yutup sessiz kalmak, oyuncuya
            // arayuzun bozuk oldugunu soyler (ui-code.md).
            if (!PickUnlocked)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.Label(new Rect(cx - 300f, top + 34f, 600f, 20f),
                          "kartlari oku - secim birazdan acilacak", _hintStyle);
                GUI.color = Color.white;
            }

            const float cardWidth = 250f;
            const float cardHeight = 190f;
            const float gap = 22f;

            float totalWidth = _draft.SlotCount * cardWidth + (_draft.SlotCount - 1) * gap;
            float x = cx - totalWidth / 2f;
            float y = top + 52f;

            for (int i = 0; i < _draft.SlotCount; i++)
            {
                DrawSlot(i, new Rect(x + i * (cardWidth + gap), y, cardWidth, cardHeight));

                // KART SECILDIYSE EKRAN KAPANDI (2026-09-06, ikinci deneme).
                //
                // Ilk duzeltme DrawSlot'un icinde donuyordu ama YETMEDI: donus bu
                // donguye geliyor ve dongunun kosulu _draft'i tekrar okuyor. Ayni
                // NullReferenceException, bir satir yukarida.
                //
                // Ders: senkron kapanan bir ekranda, kapanmadan SONRAKI her okuma
                // kontrol edilmeli - tek bir erken donus yetmez.
                if (_draft == null) return;
            }

            DrawLoadout(cx, y + cardHeight + 16f);
        }

        // ---------------------------------------------------------------- kart

        private void DrawSlot(int index, Rect rect)
        {
            CardDefinition card = _draft.Slot(index);

            if (!card.IsValid)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.10f);
                GUI.DrawTexture(rect, _pixel);
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.Label(rect, "\n\n  (havuz tukendi)", _bodyStyle);
                GUI.color = Color.white;
                return;
            }

            // Kartin GOVDESI bir dugme, ama ALT SERIT haric: yenileme dugmesi orada
            // duruyor ve IMGUI'de once cizilen kontrol tiklamayi yutar. Ilk surumde
            // kartin tamami dugmeydi ve "yenile"ye basmak karti SECIYORDU - oyun
            // testinde ilk bulunan sey bu oldu.
            const float rerollStrip = 40f;
            var body = new Rect(rect.x, rect.y, rect.width, rect.height - rerollStrip);

            // KILITLIYKEN DUGME KAPALI: karti gormeden secmeyi imkansiz kilar. Sonmus
            // govde, kilidin gorunur karsiligi - ustteki yazi da sebebini soyluyor.
            bool unlocked = PickUnlocked;

            // KART ARTIK SAYDAM DEGIL (2026-09-08, gelistirici: "kartlarin
            // arkaplanindaki opakligi kaldir, daha belirgin hale gelsin").
            //
            // Onceki hali %12 beyazdi: arkasindaki dunya kartin icinden goruyordu ve
            // kart bir YUZEY degil, ekranin uzerinde bir leke gibi okunuyordu. Kartin
            // isi bir SECIM sunmak; secilecek sey once bir nesne gibi durmali.
            //
            // Renk KOYU, beyaz degil: yazi acik renk ve arka plan da acik olsaydi
            // kontrast ikinci bir sorun olurdu. Kilitliyken bir tik daha koyu -
            // "henuz degil" bilgisi rengin YOKLUGUYLA degil, kararmasiyla tasiniyor
            // (ui-code.md: bilgi tek basina renkle tasinmaz; ustteki yazi da soyluyor).
            GUI.color = unlocked
                ? new Color(0.13f, 0.13f, 0.15f, 1f)
                : new Color(0.09f, 0.09f, 0.10f, 1f);

            GUI.DrawTexture(body, _pixel);

            GUI.color = Color.white;
            GUI.enabled = unlocked;
            bool picked = GUI.Button(body, GUIContent.none, _buttonStyle);
            GUI.enabled = true;
            GUI.color = Color.white;

            if (picked)
            {
                controller.Pick(index);

                // HEMEN DON (2026-09-06). Pick, draft'i AYNI CAGRI ICINDE kapatiyor
                // (CardSignals.DraftClosed -> OnDraftClosed -> _draft = null) ve bu
                // fonksiyonun devami _draft'i okumaya devam ediyordu: her turda bir
                // NullReferenceException, tam kart secildigi anda. Log'da gorunen
                // hata buydu.
                //
                // Istisna, o karenin OnGUI'sinin geri kalanini da iptal ediyordu -
                // yani gorunmeyen bir maliyeti vardi. Kapanmis bir ekranin cizecek
                // bir seyi yok; dogru olan devam etmemek.
                return;
            }

            GUI.color = TagColor(card.Tag);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4f), _pixel);
            GUI.color = Color.white;

            var inner = new Rect(rect.x + 14f, rect.y + 16f, rect.width - 28f, rect.height - 28f);

            GUI.Label(new Rect(inner.x, inner.y, inner.width, 26f), card.DisplayName, _nameStyle);
            GUI.Label(new Rect(inner.x, inner.y + 32f, inner.width, 80f), card.Description, _bodyStyle);

            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.Label(new Rect(inner.x, rect.yMax - 52f, inner.width, 18f), TagName(card.Tag), _bodyStyle);
            GUI.color = Color.white;

            // Yenileme dugmesi kartin ALTINDA ve kucuk: varsayilan eylem secmek.
            RerollState state = _draft.Reroll(index);

            if (state == RerollState.Exhausted) return;

            // FIYAT DUGMENIN USTUNDE YAZAR (2026-09-05). Onceki hali yalnizca
            // "yenile (puanli)" diyordu: oyuncu kac puan gidecegini ancak bastiktan
            // sonra ogreniyordu - ve o sirada yenileme aslinda hicbir sey almiyordu.
            int cost = controller != null ? controller.RerollCost(index) : 0;

            string label = cost > 0 ? $"yenile ({cost} puan)" : "yenile (ucretsiz)";
            var button = new Rect(rect.x + 14f, rect.yMax - 32f, rect.width - 28f, 24f);

            // Puani yetmiyorsa dugme KAPALI ve sebebi gorunur: basip hicbir sey
            // olmamasi, oyuncuya arayuzun bozuk oldugunu soyler.
            bool affordable = cost <= 0 || (_score != null && _score.Spendable >= cost);

            // Yenileme de kilide TABI: puan harcayan bir dugmenin, oyuncunun gormedigi
            // bir karede basilmasi secimden de kotu - hem karti degistirir hem puan alir.
            GUI.enabled = affordable && unlocked;
            if (GUI.Button(button, label)) controller.RerollSlot(index, _score);
            GUI.enabled = true;
        }

        private void DrawLoadout(float cx, float y)
        {
            CardLoadout loadout = CardSignals.Loadout;

            _text.Clear();
            _text.Append("ELINDE ").Append(loadout.Count).Append(" kart");

            AppendTag(loadout, CardTag.Ballistics, "Balistik");

            // YIKIM SAYACI EKSIKTI (2026-09-07): etiket vardi, karti vardi, sayacta
            // yoktu - oyuncu kac patlama karti aldigini hicbir yerde goremiyordu.
            AppendTag(loadout, CardTag.Demolition, "Yikim");
            AppendTag(loadout, CardTag.Blood, "Kan");
            AppendTag(loadout, CardTag.Tempo, "Tempo");
            AppendTag(loadout, CardTag.Loot, "Ganimet");

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(cx - 400f, y, 800f, 22f), _text.ToString(), _hintStyle);
            GUI.color = Color.white;
        }

        private void AppendTag(CardLoadout loadout, CardTag tag, string label)
        {
            int count = loadout.TagCount(tag);
            if (count == 0) return;

            _text.Append("   ").Append(label).Append(' ').Append(count);
            if (loadout.HasTagBonus(tag)) _text.Append(" *BONUS*");
        }

        // ---------------------------------------------------------------- yardimci

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
            { fontSize = 32, alignment = TextAnchor.MiddleCenter, richText = false };

            _nameStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.UpperLeft, richText = false };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = false };

            _hintStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = false };

            // Kartin tamamini kaplayan seffaf dugme: cerceve cizmez, yalnizca tiklamayi
            // yakalar. Uzerine kartin icerigi ayrica ciziliyor.
            _buttonStyle ??= new GUIStyle(GUI.skin.box);
        }
    }
}
