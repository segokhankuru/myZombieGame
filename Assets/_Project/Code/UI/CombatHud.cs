using System.Text;
using Bunker.Gameplay;
using Bunker.Systems.Combat;
using Bunker.Systems.Pickups;
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
        /// <summary>
        /// Tam çömelmişken açıklığın kaça ineceği. <b>Denge sayısı değil</b>, bir
        /// okunabilirlik ayarı — bu yüzden config'de değil burada (config-data.md
        /// config'i denge için ayırır).
        /// </summary>
        private const float CrouchGapMultiplier = 0.45f;

        [Header("Nisangah")]
        [SerializeField] private float crosshairSizePixels = 10f;

        [Tooltip("AYAKTAYKEN acikligi. Comelince CrouchGapMultiplier kadar daralir.")]
        [SerializeField] private float crosshairGapPixels = 6f;
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
        private PlayerMelee _melee;
        private PlayerPowerups _powerups;
        private PlayerDownState _down;
        private PlayerReady _ready;
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
                _melee = weapons[i].GetComponent<PlayerMelee>();
                _powerups = weapons[i].GetComponent<PlayerPowerups>();
                _down = weapons[i].GetComponent<PlayerDownState>();
                _ready = weapons[i].GetComponent<PlayerReady>();
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
            DrawPowerup();
            DrawRoundThreat();
            DrawSlotBar();
            DrawBleedOut();
            DrawReadyPrompt();
        }

        /// <summary>
        /// Yerdeyken <b>ölüme kalan süre</b> (2026-09-09).
        ///
        /// <para><b>Görünmeyen bir geri sayım, kurtarma kararını tahmine bırakır.</b>
        /// Arkadaşının yetişip yetişemeyeceğini bilmeden yerde yatmak, oyuncunun
        /// yapabileceği hiçbir şey olmadığı anlamına gelir — oysa on saniye,
        /// bağırmaya ve arkadaşının koşmaya karar vermesine yeter. Sayaç o kararın
        /// zeminidir.</para>
        /// </summary>
        private void DrawBleedOut()
        {
            PlayerDownState down = _down;
            if (down == null || !down.IsDowned) return;

            float remaining = down.BleedOutRemainingSeconds;

            // SON UC SANIYE YANIP SONER: aciliyeti sayidan once renk soyler
            // (esyanin son uc saniyesiyle ayni dil).
            bool urgent = remaining <= 3f;
            bool blink = !urgent || Mathf.Repeat(remaining, 0.4f) > 0.2f;

            GUI.color = urgent
                ? new Color(1f, 0.25f, 0.2f, blink ? 0.95f : 0.45f)
                : new Color(1f, 0.55f, 0.35f, 0.95f);

            GUI.Label(new UnityEngine.Rect(Screen.width * 0.5f - 200f,
                                           Screen.height * 0.5f + 70f, 400f, 30f),
                      $"YERDESIN  -  {remaining:0.0} sn", _promptStyle);

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new UnityEngine.Rect(Screen.width * 0.5f - 200f,
                                           Screen.height * 0.5f + 98f, 400f, 24f),
                      down.ReviveProgress01 > 0f
                          ? $"kaldiriliyorsun  %{Mathf.RoundToInt(down.ReviveProgress01 * 100f)}"
                          : "bir arkadasin gelmeli", _promptStyle);

            GUI.color = Color.white;
        }

        /// <summary>
        /// Tur arasında <b>hazır</b> ipucu ve sayaç (2026-09-09).
        ///
        /// <para><b>Yalnızca molada çizilir:</b> tur içinde F hiçbir şey yapmıyor ve
        /// işlevsiz bir ipucu, oyuncuya çalışmayan bir tuş öğretir.</para>
        ///
        /// <para><b>Kaç kişi hazır YAZILIYOR</b>, yalnızca kendi durumun değil: co-op'ta
        /// "neden hâlâ başlamadı" sorusunun cevabı bu sayı. Onsuz oyuncular birbirine
        /// sesli olarak sormak zorunda kalır.</para>
        /// </summary>
        private void DrawReadyPrompt()
        {
            if (!RoundSignals.IsBreather) return;

            PlayerReady ready = _ready;
            if (ready == null || !ready.CanReady) return;

            int total = 0;
            int done = 0;

            var players = PlayerReady.Players;

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == null || !players[i].CanReady) continue;

                total++;
                if (players[i].IsReady) done++;
            }

            GUI.color = ready.IsReady
                ? new Color(0.55f, 0.95f, 0.55f, 0.95f)
                : new Color(1f, 1f, 1f, 0.80f);

            // Yazi yalnizca DURUM DEGISINCE kurulur. Mola 30 saniye surer ve OnGUI
            // kare basina birden fazla kez cagrilir - her cagride bir string ayirmak,
            // saniyede yuzlerce tahsis eder (ui-code.md).
            if (done != _lastReadyDone || total != _lastReadyTotal ||
                ready.IsReady != _lastReadySelf)
            {
                _lastReadyDone = done;
                _lastReadyTotal = total;
                _lastReadySelf = ready.IsReady;

                // Solo'da sayaci yazmak gurultu: "1/1 hazir" hicbir sey soylemez.
                _readyText = total > 1
                    ? (ready.IsReady ? $"HAZIRSIN   {done}/{total}"
                                     : $"F  -  hazir   {done}/{total}")
                    : (ready.IsReady ? "HAZIRSIN" : "F  -  turu baslat");
            }

            GUI.Label(new UnityEngine.Rect(Screen.width * 0.5f - 200f,
                                           Screen.height - 96f, 400f, 24f),
                      _readyText, _promptStyle);

            GUI.color = Color.white;
        }

        /// <summary>
        /// <b>Ekranın solunda</b> bu turun tehdit sayıları: zombinin vuruş hasarı ve
        /// hızı (2026-09-09, geliştirici isteği).
        ///
        /// <para><b>Neden can barının yanından buraya taşındı:</b> orada iki sorunu
        /// vardı. Kalabalıkta kırk kere tekrar ediyordu — PILLAR-04'ün (kaosta
        /// okunabilirlik) tam tersi — ve <i>ekranda zombi yokken hiç görünmüyordu</i>,
        /// yani "bu tur ne kadar sert" sorusunun sorulduğu tek anda, hazırlık
        /// molasında, cevap ekranda değildi.</para>
        ///
        /// <para><b>Hız kademesinin ADI da yazılıyor</b>, çıplak sayı değil: 3.1 m/s'nin
        /// oyuncunun 5 m/s'sine göre ne demek olduğunu söyleyen şey kademedir.
        /// Kaçabildiğin bir turla kaçamadığın tur arasındaki fark bir eşiktir, bir
        /// ondalık değil.</para>
        ///
        /// <para><b>Tur 0'da çizilmez:</b> hiçbir şey söylemeyen bir panel, ekranda yer
        /// kaplamaktan başka bir şey yapmaz (koşu göstergesiyle aynı kural).</para>
        /// </summary>
        private void DrawRoundThreat()
        {
            if (RoundThreat.Round <= 0) return;

            _threatStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };

            // Yazi yalnizca TUR DEGISINCE kurulur (ui-code.md: kare basina string
            // tahsisi yok). OnGUI kare basina birden fazla kez cagrilir.
            if (RoundThreat.Round != _lastThreatRound)
            {
                _lastThreatRound = RoundThreat.Round;

                _text.Clear();
                _text.Append("TUR ").Append(RoundThreat.Round).Append('\n');

                _text.Append("hasar    ")
                     .Append(Mathf.RoundToInt(RoundThreat.ZombieDamage)).Append('\n');

                _text.Append("hiz      ")
                     .Append(RoundThreat.ZombieSpeedMetersPerSecond.ToString("0.0"))
                     .Append(" m/s  ")
                     .Append(TierName(RoundThreat.SpeedTier));

                if (RoundThreat.IsBossRound)
                {
                    // Boss SAYISI da yaziyor (2026-09-11): artik turdan tura
                    // degisiyor ve "kac tane geliyor" hazirligin ilk sorusu.
                    _text.Append("\nBOSS x")
                         .Append(RoundThreat.BossCount)
                         .Append("   ")
                         .Append(Mathf.RoundToInt(RoundThreat.BossDamage))
                         .Append(" hasar");
                }

                _threatText = _text.ToString();
            }

            const float width = 210f;
            float x = 24f;
            float y = Screen.height * 0.5f - 40f;

            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            Rect(x - 8f, y - 6f, width, RoundThreat.IsBossRound ? 84f : 66f);

            // Boss turunda panel UYARI rengine doner: boss bir surpriz degil, bir
            // hazirlik sebebi olmali.
            GUI.color = RoundThreat.IsBossRound
                ? new Color(1f, 0.72f, 0.35f, 0.95f)
                : new Color(1f, 1f, 1f, 0.82f);

            GUI.Label(new UnityEngine.Rect(x, y, width, 80f), _threatText, _threatStyle);
            GUI.color = Color.white;
        }

        private static string TierName(ZombieSpeedTier tier) => tier switch
        {
            ZombieSpeedTier.Walk => "YURUME",
            ZombieSpeedTier.Jog => "TEMPOLU",
            _ => "KOSU"
        };

        // Tehdit panelinin onbellegi: yalnizca tur degisince yeniden kurulur.
        private GUIStyle _threatStyle;
        private string _threatText = string.Empty;
        private int _lastThreatRound = -1;

        /// <summary>
        /// <b>Slot çubuğu</b>: 1 bıçak, 2-5 ateşli silahlar, 6-0 eşyalar (2026-09-09).
        ///
        /// <para><b>Neden şart:</b> bıçak bu sürümde ayrı bir tuştan (V) bir slota
        /// taşındı ve eşyalar artık cepte birikiyor. İkisi de <i>ekranda görünmeyen</i>
        /// bir envanter üretti — hangi tuşta ne olduğunu ezberlemek zorunda kalan
        /// oyuncu, sürünün ortasında yanlış tuşa basar ve bunu oyunun hatası olarak
        /// okur.</para>
        ///
        /// <para><b>Dolu olmayan eşya slotu da çiziliyor</b>, soluk hâlde: boş slotu
        /// gizlemek, oyuncunun "6'da ne vardı" sorusuna ancak eşya bulduğunda cevap
        /// vermek olurdu — yani kuralı öğrenmenin tek yolu şans olurdu. Silah slotları
        /// ise <b>sahip olunmadıkça çizilmiyor</b>: satın alınmamış bir silahın yeri,
        /// öğrenilecek bir kural değil sadece gürültü.</para>
        ///
        /// <para>Bilgi renkle tek başına taşınmıyor (ui-code.md): seçili slotun
        /// çerçevesi <b>ve</b> yazı parlaklığı aynı şeyi söylüyor.</para>
        /// </summary>
        private void DrawSlotBar()
        {
            _slotStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperCenter,
                richText = false
            };

            _slotKeyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };

            const float slotWidth = 74f;
            const float slotHeight = 40f;
            const float gap = 4f;

            int gunCount = _weapon.OwnedCount;
            int weaponSlots = 1 + gunCount;                      // bicak + silahlar
            int itemSlots = PowerupInventory.SlotCount;           // 6-0
            int total = weaponSlots + itemSlots;

            float totalWidth = total * slotWidth + (total - 1) * gap;
            float x = (Screen.width - totalWidth) * 0.5f;
            float y = Screen.height - 46f;

            int active = _weapon.ActiveSlotNumber;

            // --- 1: bicak
            DrawSlot(x, y, slotWidth, slotHeight, 1,
                     _melee != null && _melee.Current.IsValid ? _melee.Current.DisplayName : "BICAK",
                     null, active == 1, true);
            x += slotWidth + gap;

            // --- 2..5: ates li silahlar
            for (int i = 0; i < gunCount; i++)
            {
                DrawSlot(x, y, slotWidth, slotHeight, i + 2, _weapon.OwnedName(i),
                         null, active == i + 2, true);
                x += slotWidth + gap;
            }

            // --- 6..0: esyalar
            PowerupInventory inventory = _powerups != null ? _powerups.Inventory : null;

            for (int slot = 0; slot < itemSlots; slot++)
            {
                int count = inventory != null ? inventory.CountAt(slot) : 0;

                DrawSlot(x, y, slotWidth, slotHeight,
                         PowerupInventory.KeyNumberFor(slot),
                         ItemName(PowerupInventory.KindAt(slot)),
                         count > 0 ? count : (int?)null,
                         false, count > 0);

                x += slotWidth + gap;
            }
        }

        private void DrawSlot(float x, float y, float width, float height,
                              int keyNumber, string label, int? count,
                              bool selected, bool owned)
        {
            GUI.color = selected
                ? new Color(0.95f, 0.85f, 0.35f, 0.30f)
                : new Color(0f, 0f, 0f, 0.45f);
            Rect(x, y, width, height);

            // Secili slotun CERCEVESI: renk tek basina bilgi tasimiyor (ui-code.md).
            if (selected)
            {
                GUI.color = new Color(0.98f, 0.88f, 0.40f, 0.95f);
                Rect(x, y, width, 2f);
                Rect(x, y + height - 2f, width, 2f);
                Rect(x, y, 2f, height);
                Rect(x + width - 2f, y, 2f, height);
            }

            GUI.color = selected
                ? new Color(1f, 0.95f, 0.6f)
                : (owned ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.28f));

            GUI.Label(new UnityEngine.Rect(x + 5f, y + 2f, 20f, 14f),
                      keyNumber.ToString(), _slotKeyStyle);

            GUI.Label(new UnityEngine.Rect(x, y + 15f, width, 16f), label, _slotStyle);

            // Yigin sayisi yalnizca DOLU slotta: "x0" yazmak, bos slotu dolu gibi
            // okutur.
            if (count.HasValue)
            {
                GUI.color = new Color(0.98f, 0.88f, 0.40f);
                GUI.Label(new UnityEngine.Rect(x, y + 26f, width - 6f, 14f),
                          "x" + count.Value, _slotCountStyle ??= new GUIStyle(GUI.skin.label)
                          {
                              fontSize = 12,
                              alignment = TextAnchor.UpperRight,
                              richText = false
                          });
            }

            GUI.color = Color.white;
        }

        private static string ItemName(PowerupKind kind) => kind switch
        {
            PowerupKind.Health => "CAN",
            PowerupKind.Ammo => "MERMI",
            PowerupKind.Slow => "YAVAS",
            PowerupKind.Freeze => "DONDUR",
            _ => "NUKE"
        };

        // Hazir yazisinin onbellegi: yalnizca durum degisince yeniden kurulur.
        private string _readyText = string.Empty;
        private int _lastReadyDone = -1;
        private int _lastReadyTotal = -1;
        private bool _lastReadySelf;

        private GUIStyle _slotStyle;
        private GUIStyle _slotKeyStyle;
        private GUIStyle _slotCountStyle;

        /// <summary>
        /// Yerden toplanan süreli eşyanın sayacı (2026-09-07).
        ///
        /// <para><b>Süresi görünmeyen bir etki, olmayan bir etkidir:</b> zombilerin
        /// neden yavaşladığını ve ne zaman hızlanacağını göremeyen oyuncu, kalan
        /// süreye göre plan yapamaz — eşya bir ödül değil, açıklanamayan bir olay olur.
        /// Bilgi renkle tek başına taşınmıyor; yazı da aynı şeyi söylüyor
        /// (ui-code.md).</para>
        /// </summary>
        private void DrawPowerup()
        {
            string label = PowerupState.ActiveLabel();
            if (label == null) return;

            float remaining = PowerupState.ActiveRemainingSeconds;

            GUI.color = PowerupState.IsFreezeActive
                ? new Color(0.65f, 0.95f, 1f, 0.95f)
                : new Color(0.45f, 0.78f, 1f, 0.9f);

            GUI.Label(new Rect(Screen.width * 0.5f - 100f, 74f, 200f, 24f),
                      $"{label}  {remaining:0.0} sn", _promptStyle);

            GUI.color = Color.white;
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

            // HAM SAYI BARIN USTUNDE (2026-09-06, gelistirici: "kendi canimin sayisal
            // degerini can barinda goster").
            //
            // Bar orani soyler, sayi KAC VURUS DAYANDIGINI soyler - ve kart alan
            // oyuncunun tavani degistigi icin "%60 can" her turda baska bir sey
            // demek. 30 hasarlik bir zombi vurusu karsisinda "72/120" ile "%60"
            // arasindaki fark, karar verebilmekle tahmin etmek arasindaki fark.
            //
            // Metin BIR KEZ kurulur ve yalnizca DEGISTIGINDE (ui-code.md: kare basina
            // string tahsisi yok). OnGUI kare basina birden fazla kez cagrilir.
            int current = Mathf.CeilToInt(_health.CurrentPoints);
            int max = Mathf.RoundToInt(_health.MaxPoints);

            if (current != _lastHealthShown || max != _lastHealthMaxShown)
            {
                _lastHealthShown = current;
                _lastHealthMaxShown = max;
                _healthText = $"{current} / {max}";
            }

            _healthStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.LowerLeft,
                richText = false
            };

            GUI.color = _health.IsLow ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new UnityEngine.Rect(x, y - 20f, barWidth, 18f), _healthText, _healthStyle);
            GUI.color = Color.white;
        }

        // Can yazisinin onbellegi: yalnizca sayi degistiginde yeniden kurulur.
        private string _healthText = string.Empty;
        private int _lastHealthShown = -1;
        private int _lastHealthMaxShown = -1;
        private GUIStyle _healthStyle;

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

        /// <summary>
        /// <b>Dürbün görüntüsü</b> (2026-09-09, geliştirici: <i>"gerçekten scope'tan
        /// bakıyormuşuz gibi göster, sadece yakınlaşmasın"</i>).
        /// </summary>
        /// <returns>Dürbün çizildiyse <c>true</c> — nişangâh o zaman çizilmez.</returns>
        private bool DrawScope()
        {
            if (!_weapon.IsScoped) return false;

            ScopeStyle style = _weapon.ScopeStyle;
            if (style == ScopeStyle.None) return false;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (style == ScopeStyle.LongRange) DrawLongRangeScope(cx, cy);
            else DrawPrismScope(cx, cy);

            return true;
        }

        /// <summary>
        /// Nişancı dürbünü (M107): <b>ekran kararır, ortada yuvarlak bir alan kalır.</b>
        ///
        /// <para><b>Çevreni feda edersin</b> ve bu, uzun menzilli gücün bedeli. Kararan
        /// alan bir süs değil: dürbündeyken yanından gelen zombiyi görememek, M107'yi
        /// kalabalıkta kullanılamaz yapan şeyin ta kendisi.</para>
        ///
        /// <para><b>Neden dikdörtgenlerle çiziliyor, bir doku ile değil:</b> proje hâlâ
        /// gri kutu ve IMGUI kullanıyor; bir dürbün dokusu, sanat yönü kilitlenmeden
        /// yapılmış atılacak iş olurdu. Dört kenar bandı + köşe blokları, yuvarlak bir
        /// alanın <i>okunabilir</i> karşılığını bedavaya yakın üretiyor.</para>
        /// </summary>
        private void DrawLongRangeScope(float cx, float cy)
        {
            // Gorus alaninin yaricapi: ekranin kisa kenarina bagli, yani her en-boy
            // oraninda ayni buyuklukte gorunur (ui-code.md: sabit piksel yerlesim yok).
            float radius = Mathf.Min(Screen.width, Screen.height) * 0.42f;

            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.97f);

            // Dort kenar bandi: yuvarlak alanin disinda kalan her sey.
            Rect(0f, 0f, Screen.width, cy - radius);
            Rect(0f, cy + radius, Screen.width, Screen.height - (cy + radius));
            Rect(0f, cy - radius, cx - radius, radius * 2f);
            Rect(cx + radius, cy - radius, Screen.width - (cx + radius), radius * 2f);

            // Koseleri yuvarlatan basamaklar: kare bir "delik", durbunden cok bir
            // pencere gibi okunurdu.
            const int steps = 10;

            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float y = radius * t;
                float x = radius * Mathf.Sqrt(1f - t * t);
                float h = radius / steps + 1f;

                Rect(cx - radius, cy - y - h * 0.5f, radius - x, h);
                Rect(cx + x, cy - y - h * 0.5f, radius - x, h);
                Rect(cx - radius, cy + y - h * 0.5f, radius - x, h);
                Rect(cx + x, cy + y - h * 0.5f, radius - x, h);
            }

            // Ince artı: kilı çapraz degil DUZ - mil noktalari onun uzerinde oturur.
            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            Rect(cx - radius, cy - 0.5f, radius * 2f, 1f);
            Rect(cx - 0.5f, cy - radius, 1f, radius * 2f);

            // MIL NOKTALARI: mesafe okumanin gorsel dili. Islevleri yok (menzil
            // dususu yok) ama durbunun NE OLDUGUNU soyleyen sey bunlar.
            for (int i = 1; i <= 4; i++)
            {
                float d = radius * 0.16f * i;

                Rect(cx - d - 1.5f, cy - 1.5f, 3f, 3f);
                Rect(cx + d - 1.5f, cy - 1.5f, 3f, 3f);
                Rect(cx - 1.5f, cy + d - 1.5f, 3f, 3f);
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// Prizmalı optik (M4/ELCAN, gerçekteki HAMR sınıfı): <b>gövde görünür kalır,
        /// ekran kararmaz.</b>
        ///
        /// <para><b>Fark bir süs değil</b> (geliştirici: <i>"HAMR scope zoomu farklı
        /// görünüyor, gerçekteki gibi uygula"</i>): prizmalı bir optikte iki gözün de
        /// açık kalabilmesi ve çevreyi görebilmek o optiğin <i>varlık sebebi</i>. Bu
        /// yüzden burada tam ekran karartma yok — yalnızca kalın bir gövde halkası ve
        /// dışarıda hafif bir sönme.</para>
        ///
        /// <para>Nişangâh <b>kırmızı bir chevron</b> (V), artı değil: prizmalı
        /// optiklerin imzası bu ve M107'nin ince artısından bir bakışta ayrılıyor.</para>
        /// </summary>
        private void DrawPrismScope(float cx, float cy)
        {
            float radius = Mathf.Min(Screen.width, Screen.height) * 0.30f;

            // DISARISI SONER ama KARARMAZ: cevreyi gormeye devam edersin.
            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.40f);
            Rect(0f, 0f, Screen.width, cy - radius);
            Rect(0f, cy + radius, Screen.width, Screen.height - (cy + radius));
            Rect(0f, cy - radius, cx - radius, radius * 2f);
            Rect(cx + radius, cy - radius, Screen.width - (cx + radius), radius * 2f);

            // Optigin GOVDESI: kalin, koyu bir halka. Prizmali optigi tanimlayan sey.
            GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.92f);

            const int steps = 12;
            float ring = radius * 0.14f;

            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float y = radius * t;
                float x = radius * Mathf.Sqrt(1f - t * t);
                float h = radius / steps + 1f;

                Rect(cx - x - ring, cy - y - h * 0.5f, ring, h);
                Rect(cx + x, cy - y - h * 0.5f, ring, h);
                Rect(cx - x - ring, cy + y - h * 0.5f, ring, h);
                Rect(cx + x, cy + y - h * 0.5f, ring, h);
            }

            // CHEVRON: asagi bakan bir V. Ucu nisan noktasi.
            GUI.color = new Color(0.95f, 0.22f, 0.18f, 0.95f);

            const float arm = 11f;
            const float thickness = 2f;

            for (int i = 0; i < (int)arm; i++)
            {
                float dx = i;
                float dy = i;

                Rect(cx - dx - thickness * 0.5f, cy - dy, thickness, thickness);
                Rect(cx + dx - thickness * 0.5f, cy - dy, thickness, thickness);
            }

            GUI.color = Color.white;
        }

        private void DrawCrosshair()
        {
            // DURBUN VARSA NISANGAH YOK: iki nisan isareti ust uste, hangisinin
            // gectigini okunmaz kilar (2026-09-09).
            if (DrawScope()) return;

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

            // NISANGAH COMELINCE TOPLANIR (2026-09-07, gelistirici istegi:
            // "ayaktayken bir tik daha ayrik, yere comelince daha yakin kalsin").
            //
            // <b>Neden doğru:</b> çömelmek zaten atışı toparlıyor
            // (<c>PlayerController.CrouchSpreadMultiplier</c>) — nişangâh o kazancı
            // <i>göstermiyordu</i>. Sabit bir nişangâh, oyuncuya "çömelmek işe
            // yarıyor" bilgisini yalnızca istatistikle öğretir; açıklık daralınca
            // aynı bilgi ilk çömelişte okunur.
            //
            // <b>Açıklık dağılımın kendisiyle değil, çömelme oranıyla ölçülüyor:</b>
            // gerçek koni açısını piksele çevirmek görüş açısına ve ekran boyuna bağlı
            // bir hesap olurdu ve dar ekranda yalan söylerdi. Burada nişangâh bir
            // ölçü aleti değil, bir <i>durum göstergesi</i>.
            float crouchGap = _controller != null
                ? Mathf.Lerp(crosshairGapPixels, crosshairGapPixels * CrouchGapMultiplier,
                             _controller.Crouch01)
                : crosshairGapPixels;

            float gap = crouchGap + (hit ? 3f : 0f);
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

                // YEDEK TAVANI GORUNUR (2026-09-10, gelistirici: "yedek mermi konusu
                // duzgun calismiyor hissi veriyor"). Tur sonu ikmali tavana kadar
                // doluyor, satin alinan mermi tavani asiyor - ama tavan hicbir yerde
                // yazmiyordu, yani oyuncu "120 mermim vardi, neden 60 geldi" sorusunun
                // cevabini goremiyordu. Gorunmeyen bir kural ogrenilemez.
                _text.Append("  (").Append(_weapon.ReserveCapacity).Append(')');

                if (_weapon.RoundsInMagazine == 0) _text.Append("   BOS - R");
            }

            // Sag alt: mermi. Ekranin ortasindan uzak, ama goz ucuyla okunacak yerde.
            // Genislik tavan eklenince 200 -> 300: "30 / 480  (480)   BOS - R" sigmali.
            GUI.Label(new UnityEngine.Rect(Screen.width - 320f, Screen.height - 60f, 300f, 40f),
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
