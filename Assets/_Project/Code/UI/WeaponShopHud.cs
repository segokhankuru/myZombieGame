using System.Collections.Generic;
using Bunker.Gameplay;
using Bunker.Systems.Combat;
using Bunker.Systems.Ui;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Silah tezgâhının kataloğu. <b>E</b> ile açılır, <b>E</b> ya da <b>Esc</b> ile
    /// kapanır. 2026-09-06.
    ///
    /// <para><b>Neden bir liste, üç duvar değil</b> (geliştirici): silah seçmek bir
    /// karar ve karar ancak seçenekler <b>yan yana</b> durursa verilebilir. Üç ayrı
    /// duvara dağılmış silah, oyuncunun kafasında karşılaştırılamayan silahtır.</para>
    ///
    /// <para><b>Her satır silahın karakterini söyler, gücünü değil:</b> hasar, atış
    /// hızı ve menzil birlikte gösterilir çünkü seçimi belirleyen şey <i>ritim</i>
    /// olmalı (weapons.json'un tasarım notu: DPS'ler bilerek birbirine yakın). Yalnızca
    /// hasarı göstermek, tüfeği tek doğru cevap gibi okuturdu.</para>
    ///
    /// <para><b>Sahip olunan silah mermi satar</b> — duvardaki musluğun aynı kuralı.
    /// Oyuncu iki farklı kural öğrenmez.</para>
    ///
    /// <para><b>IMGUI, gerekçesi <see cref="ShopHud"/> ile aynı:</b> bu gri kutu bir
    /// arayüz değil, bir ölçüm aracı. Gerçek ekran sanat aşamasının işi.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Weapon Shop HUD (gecici)")]
    public sealed class WeaponShopHud : MonoBehaviour
    {
        private GUIStyle _titleStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private Texture2D _pixel;

        private PlayerScore _score;
        private PlayerWeapon _weapon;
        private PlayerMelee _melee;
        private PlayerInteract _interact;

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

        private void OnEnable() => MenuSignals.WeaponShopVisibilityChanged += OnVisibilityChanged;

        private void OnDisable()
        {
            MenuSignals.WeaponShopVisibilityChanged -= OnVisibilityChanged;

            // Menu acikken bilesen kapanirsa imlec kilitli kalirdi.
            if (_open) RestoreCursor();

            // ...ve dunya DONMUS kalirdi. Bilesenin yok olmasi, oyunun bir daha hic
            // akmamasi demek olamaz.
            WorldClock.Set(WorldClock.Reason.WeaponShop, false);
        }

        private void OnVisibilityChanged(bool open)
        {
            _open = open;

            // TEZGAH ACIKKEN ZAMAN DURUR (2026-09-07, gelistirici istegi). Gerekce
            // yukseltme tezgahiyla ayni: dort silah yan yana bir KARAR, ve karari
            // okumak cezalandirilmamali. Sahibi WorldClock - iki menu ayni degiskeni
            // yazsaydi, birini kapatmak digerinin arkasinda dunyayi akitirdi.
            WorldClock.Set(WorldClock.Reason.WeaponShop, open);

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

            FindLocalPlayer();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // E ile acildi, E ile kapanir - tezgahla ayni tek kural.
            if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                // KAPANDIGI KARE DAMGALANIR (2026-09-09). Bu satir olmadan E hicbir
                // sey yapmiyor gibi gorunuyordu: bu bilesen menuyu kapatiyor, ayni
                // karede PlayerInteract ayni tus basisini gorup yeniden aciyordu.
                // Bilesen sirasi tanimsiz oldugu icin hata bazen "calisiyor" bile
                // goruluyordu - teshisi zorlastiran tam olarak buydu.
                MenuSignals.NoteMenuClosed(Time.frameCount);
                MenuSignals.SetWeaponShopOpen(false);
            }
        }

        /// <summary>
        /// Yerel oyuncunun bileşenleri. <b>Yarım saniyede bir aranır</b>, her karede
        /// değil — oyuncu nesnesi ağdan gelir ve ilk karede henüz yoktur.
        /// </summary>
        private void FindLocalPlayer()
        {
            if (_score != null && _weapon != null && _interact != null) return;

            _searchTimer -= Time.unscaledDeltaTime;
            if (_searchTimer > 0f) return;
            _searchTimer = 0.5f;

            foreach (PlayerScore candidate in
                     FindObjectsByType<PlayerScore>(FindObjectsSortMode.None))
            {
                if (!candidate.isLocalPlayer) continue;

                _score = candidate;
                _weapon = candidate.GetComponent<PlayerWeapon>();
                _melee = candidate.GetComponent<PlayerMelee>();
                _interact = candidate.GetComponent<PlayerInteract>();
                break;
            }
        }

        private void OnGUI()
        {
            if (!_open) return;
            if (_weapon == null || _weapon.Catalog == null) return;

            EnsureStyles();

            GUI.color = new Color(0f, 0f, 0f, 0.80f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            IReadOnlyList<WeaponDefinition> catalog = _weapon.Catalog;

            const float rowHeight = 150f;
            const float gap = 14f;

            // GENISLIK EKRANA UYAR (2026-09-09). Sabit 230 piksellik sutunlar, silah
            // sayisi dortten ALTIYA cikinca 1450 piksel ediyordu ve 1280x800'de
            // (Steam Deck - ui-code.md'nin guvenli alan listesinde) ekrandan tasiyordu.
            //
            // Kenarda 48 piksel pay birakilir; kalan genislik sutunlara bolunur ve
            // 230'u ASMAZ - genis ekranda sutunlari sismek, panelin okunmasini
            // kolaylastirmaz.
            const float maxRowWidth = 230f;
            const float sideMargin = 48f;

            // SATIRLARA SARAR (2026-09-10). Silah Atolyesi'nden silah eklenebildigi
            // icin sayi artik sabit degil. Tek satira sigdirmak 12 silahta sutunu 86
            // piksele indirip "SATIN AL - 2200 puan" yazisini kesiyordu. Sutun bu
            // genisligin altina inmez; sigmayan silah bir alt satira gecer.
            const float minRowWidth = 170f;

            int count = Mathf.Max(1, catalog.Count);
            float usable = Screen.width - sideMargin * 2f;

            int columns = Mathf.Clamp(Mathf.FloorToInt((usable + gap) / (minRowWidth + gap)), 1, count);
            int rows = Mathf.CeilToInt(count / (float)columns);

            float rowWidth = Mathf.Min(maxRowWidth, (usable - (columns - 1) * gap) / columns);

            float cx = Screen.width * 0.5f;
            float totalWidth = columns * rowWidth + Mathf.Max(0, columns - 1) * gap;
            float gridHeight = rows * rowHeight + Mathf.Max(0, rows - 1) * gap;

            // Bicak siri eklendi (2026-09-08): panel asagi dogru ~145 piksel uzadi,
            // yani baslangic noktasi da yukari kaymali - yoksa "kapat" ipucu 1080p'de
            // ekranin altindan tasar. Her ek silah satiri paneli yarim satir daha
            // yukari tasir, ama ekranin ustunden cikarmaz.
            float top = Mathf.Max(8f, Screen.height * 0.5f - 230f - (gridHeight - rowHeight) * 0.5f);

            GUI.Label(new Rect(cx - 300f, top, 600f, 44f), "SILAH TEZGAHI", _titleStyle);

            int points = _score != null ? _score.Spendable : 0;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(cx - 300f, top + 44f, 600f, 24f), $"puan: {points}", _hintStyle);
            GUI.color = Color.white;

            float x = cx - totalWidth / 2f;
            float y = top + 84f;

            for (int i = 0; i < catalog.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;

                DrawWeapon(catalog[i], new Rect(x + column * (rowWidth + gap), y + row * (rowHeight + gap),
                                                rowWidth, rowHeight));
            }

            float meleeBottom = DrawMeleeRow(cx, y + gridHeight + 20f, rowWidth, gap);

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(cx - 300f, meleeBottom + 14f, 600f, 24f),
                      "2 / 3  -  elindekini degistir       E ya da Esc  -  kapat",
                      _hintStyle);
            GUI.color = Color.white;
        }

        /// <summary>
        /// Bıçak sırası: <b>hançer, kılıç, balta</b>. 2026-09-08.
        ///
        /// <para><b>Neden aynı tezgâhta, ayrı bir istasyonda değil</b> (geliştirici:
        /// <i>"tezgâha kılıç ve axe'yi de ekle"</i>): bıçak seçmek de bir karar ve
        /// karar ancak seçenekler <b>yan yana</b> durursa verilebilir — ateşli silah
        /// sırasının varlık sebebiyle birebir aynı. Ayrı bir sıra olarak duruyor çünkü
        /// bıçak ateşli silahın alternatifi değil, <i>tamamlayıcısı</i>; ikisini tek
        /// listede karıştırmak "hangisini alayım" sorusunu yanlış sorar.</para>
        ///
        /// <para><b>Satır yoksa hiç çizilmez</b> ve yer de kaplamaz: katalog eksikse
        /// boş bir başlık, arayüzün bozuk olduğunu söyler.</para>
        /// </summary>
        /// <returns>Çizilen bölgenin alt kenarı.</returns>
        private float DrawMeleeRow(float cx, float y, float rowWidth, float gap)
        {
            if (_melee == null || _melee.Catalog == null || _melee.Catalog.Count == 0) return y;

            IReadOnlyList<MeleeDefinition> catalog = _melee.Catalog;

            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(cx - 300f, y, 600f, 22f), "YAKIN DOVUS", _hintStyle);
            GUI.color = Color.white;

            const float meleeHeight = 118f;

            float totalWidth = catalog.Count * rowWidth + Mathf.Max(0, catalog.Count - 1) * gap;
            float x = cx - totalWidth / 2f;
            float top = y + 26f;

            for (int i = 0; i < catalog.Count; i++)
            {
                DrawMelee(catalog[i], new Rect(x + i * (rowWidth + gap), top, rowWidth, meleeHeight));
            }

            return top + meleeHeight;
        }

        private void DrawMelee(in MeleeDefinition definition, Rect rect)
        {
            bool owned = _melee.Owns(definition.Id);
            bool equipped = owned && _melee.Current.Id == definition.Id;

            GUI.color = new Color(1f, 1f, 1f, equipped ? 0.18f : 0.10f);
            GUI.DrawTexture(rect, _pixel);
            GUI.color = Color.white;

            var inner = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 22f);

            GUI.Label(inner, definition.DisplayName, _nameStyle);

            // Karakteri soyleyen ucu: hasar, savurus suresi, erisim. Yalnizca hasari
            // gostermek baltayi tek dogru cevap gibi okuturdu - agirligi tasiyan sey
            // sure.
            GUI.color = new Color(1f, 1f, 1f, 0.72f);
            GUI.Label(new Rect(inner.x, inner.y + 22f, inner.width, 18f),
                      $"{Mathf.RoundToInt(definition.Damage)} hasar", _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.y + 38f, inner.width, 18f),
                      $"savurus {definition.CooldownSeconds:0.00} sn", _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.y + 54f, inner.width, 18f),
                      $"erisim {definition.RangeMeters:0.0} m", _bodyStyle);
            GUI.color = Color.white;

            var button = new Rect(rect.x + 12f, rect.yMax - 30f, rect.width - 24f, 24f);

            if (equipped)
            {
                GUI.color = new Color(0.6f, 0.9f, 0.6f);
                GUI.Label(button, "   elinde  (1 tusu)", _bodyStyle);
                GUI.color = Color.white;
                return;
            }

            // SAHIP OLUNAN BICAK BEDAVA ELE ALINIR: bicagin mermisi yok, yani ikinci
            // alimin verecek bir seyi de yok. Ucretsiz gecis, iki bicagi olan
            // oyuncunun ritim secebilmesi demek.
            string label = owned ? "ELE AL" : $"SATIN AL  -  {definition.Price} puan";

            bool wasEnabled = GUI.enabled;
            GUI.enabled = _interact != null &&
                          (owned || (_score != null && _score.Spendable >= definition.Price));

            if (GUI.Button(button, label)) _interact.RequestBuyMelee(definition.Id);

            GUI.enabled = wasEnabled;
        }

        private void DrawWeapon(in WeaponDefinition definition, Rect rect)
        {
            // UC DURUM (2026-09-09): alinmamis / alinmis ama uzerinde degil / uzerinde.
            // Ortadaki yeni ve BEDELSIZ - ekranin bunu soylememesi, oyuncunun
            // "yeniden para odeyecegim" sanip hic denememesi demek olurdu.
            bool owned = _weapon.Owns(definition.Id);
            bool carrying = _weapon.IsCarrying(definition.Id);
            bool equipped = carrying && _weapon.Current.Id == definition.Id;

            GUI.color = new Color(1f, 1f, 1f, equipped ? 0.18f : 0.10f);
            GUI.DrawTexture(rect, _pixel);
            GUI.color = Color.white;

            var inner = new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 22f);

            GUI.Label(inner, definition.DisplayName, _nameStyle);

            // Karakteri soyleyen ucu: hasar, ritim, menzil. Sacmali silahta tek atisin
            // toplami gosteriliyor - "24 hasar" yaziyor olsaydi pompali listenin en
            // zayif silahi gibi okunurdu.
            int perShot = Mathf.RoundToInt(definition.Damage * Mathf.Max(1, definition.PelletCount));

            string shotText = definition.PelletCount > 1
                ? $"{perShot} hasar ({definition.PelletCount} sacma)"
                : $"{perShot} hasar";

            GUI.color = new Color(1f, 1f, 1f, 0.72f);
            GUI.Label(new Rect(inner.x, inner.y + 24f, inner.width, 18f), shotText, _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.y + 40f, inner.width, 18f),
                      $"{Mathf.RoundToInt(definition.RoundsPerMinute)} atis/dk", _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.y + 56f, inner.width, 18f),
                      $"menzil {Mathf.RoundToInt(definition.RangeMeters)} m", _bodyStyle);
            GUI.Label(new Rect(inner.x, inner.y + 72f, inner.width, 18f),
                      $"sarjor {definition.MagazineCapacity}", _bodyStyle);
            GUI.color = Color.white;

            if (equipped || (owned && !carrying))
            {
                GUI.color = equipped
                    ? new Color(0.6f, 0.9f, 0.6f)
                    : new Color(0.95f, 0.85f, 0.45f);

                GUI.Label(new Rect(inner.x, inner.y + 90f, inner.width, 18f),
                          equipped ? "elinde" : "cantada", _bodyStyle);
                GUI.color = Color.white;
            }
            else if (!carrying && _weapon.IsCarryFull)
            {
                // KURALI EKRAN SOYLER (2026-09-10): slotlar doluyken yeni silah
                // ELDEKININ yerine gecer. Yazmayan bir kural, oyuncu icin "tabancam
                // hic gitmiyor" diye okunuyordu - kurali degistirmeye gerek yoktu,
                // gorunur kilmaya vardi. Tezgah acikken 2/3 tuslari calisiyor, yani
                // cumlenin tarif ettigi secim buradan yapilabiliyor.
                GUI.color = new Color(0.95f, 0.85f, 0.45f);
                GUI.Label(new Rect(inner.x, inner.y + 90f, inner.width, 18f),
                          $"{_weapon.Current.DisplayName} yerine", _bodyStyle);
                GUI.color = Color.white;
            }

            var button = new Rect(rect.x + 12f, rect.yMax - 32f, rect.width - 24f, 24f);

            // Uzerinde olan silah MERMI satar; cantadaki BEDELSIZ ele gecer; hic
            // alinmamis olan fiyatini ister.
            int cost;
            string label;

            if (!owned)
            {
                cost = definition.Price;
                label = $"SATIN AL  -  {cost} puan";
            }
            else if (!carrying)
            {
                cost = 0;
                label = "ELE AL  -  bedava";
            }
            else
            {
                cost = definition.AmmoPrice > 0 ? definition.AmmoPrice : definition.Price;
                label = $"MERMI  -  {cost} puan";
            }

            // Bedava baslangic silahi satilmaz: fiyati sifir olan bir "SATIN AL"
            // dugmesi, oyuncuya ne oldugu belirsiz bir sey vaat eder.
            if (!owned && cost <= 0)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(button, "   baslangic silahi", _bodyStyle);
                GUI.color = Color.white;
                return;
            }

            // ELDEKI SILAH DEGISTIRILEMEZ: kendisiyle takas anlamsiz ve dugme "MERMI"
            // yaziyorsa zaten mermi satiyor - o durumda dugme acik kalir.
            if (equipped && cost <= 0)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(button, "   zaten elinde", _bodyStyle);
                GUI.color = Color.white;
                return;
            }

            // Puan yetmiyorsa dugme kapali. Fiyat yaninda yaziyor, yani bilgi renkle
            // TEK BASINA tasinmiyor (ui-code.md).
            bool wasEnabled = GUI.enabled;
            GUI.enabled = _score != null && _score.Spendable >= cost && _interact != null;

            if (GUI.Button(button, label)) _interact.RequestBuyWeapon(definition.Id);

            GUI.enabled = wasEnabled;
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 32, alignment = TextAnchor.MiddleCenter, richText = false };

            _nameStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.UpperLeft, richText = false };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 12, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = false };

            _hintStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = false };
        }
    }
}
