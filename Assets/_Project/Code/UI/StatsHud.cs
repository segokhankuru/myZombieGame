using System.Text;
using Bunker.Gameplay;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Durum paneli (<b>TAB</b>): oyuncunun bütün sayıları, tahmin değil kesin değer.
    ///
    /// <para><b>Neden gerekiyordu</b> (geliştirici, 2026-09-06): <i>"mevcut canımın
    /// hasarımın vs net bilgisini bilmeliyim."</i> Kartlar ve tezgâh her turda
    /// sayıları değiştiriyor; oyuncu neyi ne kadar değiştirdiğini göremeyince kart
    /// seçimi kumar oluyor. <b>Bir yığın kuran oyuncu, kurduğu şeyi görmeli.</b></para>
    ///
    /// <para><b>Basılı tutulur, açılıp kapanmaz:</b> bilgi paneli bir mod değil bir
    /// bakış. Aç/kapat olsaydı sürünün ortasında açık kalır ve oyuncuyu kör ederdi.</para>
    ///
    /// <para><b>Oyunu durdurmaz.</b> Duraklatma menüsünden farkı bu: TAB bir kaçış
    /// kapısı değil, koşarken de okunabilen bir tablo.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Stats HUD")]
    public sealed class StatsHud : MonoBehaviour
    {
        private PlayerWeapon _weapon;
        private PlayerHealth _health;
        private PlayerController _controller;
        private PlayerScore _score;
        private float _searchTimer;

        private readonly StringBuilder _text = new StringBuilder(512);

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
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
        }

        private void Update()
        {
            if (_weapon != null) return;

            // Yerel oyuncu ag tarafindan gec gelir; saniyede iki kez bakilir
            // (csharp-code.md: kare basina Find yasak).
            _searchTimer -= Time.deltaTime;
            if (_searchTimer > 0f) return;
            _searchTimer = 0.5f;

            foreach (PlayerWeapon candidate in
                     FindObjectsByType<PlayerWeapon>(FindObjectsSortMode.None))
            {
                if (!candidate.isLocalPlayer) continue;

                _weapon = candidate;
                _health = candidate.GetComponent<PlayerHealth>();
                _controller = candidate.GetComponent<PlayerController>();
                _score = candidate.GetComponent<PlayerScore>();
                return;
            }
        }

        private void OnGUI()
        {
            if (_weapon == null) return;
            if (RunSignals.IsRunOver) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tabKey.isPressed) return;

            EnsureStyles();

            const float width = 420f;
            const float height = 430f;

            float x = Screen.width * 0.5f - width * 0.5f;
            float y = Screen.height * 0.5f - height * 0.5f;

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, width, height), _pixel);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 16f, y + 10f, width - 32f, 30f), "DURUM", _titleStyle);

            BuildText();
            GUI.Label(new Rect(x + 16f, y + 46f, width - 32f, height - 60f),
                      _text.ToString(), _bodyStyle);
        }

        /// <summary>
        /// Paneli kurar. <b>Yalnızca TAB basılıyken</b> çağrılır, yani kare başına
        /// string üretmesi kabul edilebilir — panel kapalıyken hiçbir maliyeti yok
        /// (ui-code.md'nin "kare başına string yok" kuralının bilinçli istisnası,
        /// çünkü burada değişmeyen bir değer yok: can her karede değişiyor).
        /// </summary>
        private void BuildText()
        {
            _text.Clear();

            // --- can
            _text.Append("CAN\n");
            _text.Append("  ").Append(Mathf.RoundToInt(_health != null ? _health.CurrentPoints : 0f))
                 .Append(" / ").Append(Mathf.RoundToInt(_health != null ? _health.MaxPoints : 0f));

            float damageTaken = RunModifiers.DamageTakenMultiplier;

            if (damageTaken < 0.999f)
            {
                _text.Append("     alinan hasar %")
                     .Append(Mathf.RoundToInt(damageTaken * 100f));
            }

            _text.Append("\n\n");

            // --- silah
            WeaponDefinition weapon = _weapon.Current;

            _text.Append("SILAH   ").Append(weapon.DisplayName).Append('\n');

            float damage = weapon.Damage * (1f + RunModifiers.Total(CardStat.WeaponDamage));
            float rpm = weapon.RoundsPerMinute * (1f + RunModifiers.Total(CardStat.FireRate));
            float headshot = weapon.HeadshotMultiplier + RunModifiers.Total(CardStat.HeadshotMultiplier);
            float reload = weapon.ReloadSeconds / (1f + RunModifiers.Total(CardStat.ReloadSpeed));

            AppendStat("hasar", damage, weapon.Damage, weapon.PelletCount > 1
                ? $" x{weapon.PelletCount} sacma"
                : string.Empty);

            AppendStat("atis/dk", rpm, weapon.RoundsPerMinute,
                       $"   ({rpm / 60f:0.0} atis/sn)");

            _text.Append("  kafa carpani   ").Append(headshot.ToString("0.0")).Append("x\n");

            AppendStat("dolum (sn)", reload, weapon.ReloadSeconds, string.Empty);

            _text.Append("  sarjor          ").Append(_weapon.RoundsInMagazine)
                 .Append(" / ").Append(_weapon.MagazineCapacity)
                 .Append("     yedek ").Append(_weapon.Reserve).Append('\n');

            int penetration = Mathf.RoundToInt(RunModifiers.Total(CardStat.Penetration));
            if (penetration > 0) _text.Append("  delici          +").Append(penetration).Append('\n');

            _text.Append('\n');

            // --- hareket
            _text.Append("HAREKET\n");
            _text.Append("  hiz carpani     x")
                 .Append(RunModifiers.Multiplier(CardStat.MoveSpeed).ToString("0.00")).Append('\n');

            if (_controller != null)
            {
                _text.Append("  kosu            %")
                     .Append(Mathf.RoundToInt(_controller.SprintFraction01 * 100f)).Append('\n');
            }

            _text.Append('\n');

            // --- envanter
            _text.Append("SILAHLAR (1-4 / tekerlek)\n");

            for (int i = 0; i < _weapon.OwnedCount; i++)
            {
                _text.Append(i == _weapon.EquippedSlot ? "  > " : "    ")
                     .Append(i + 1).Append(". ").Append(_weapon.OwnedName(i)).Append('\n');
            }

            _text.Append('\n');

            // --- kartlar
            CardLoadout loadout = CardSignals.Loadout;
            _text.Append("KARTLAR   ").Append(loadout.Count).Append(" adet");

            if (_score != null) _text.Append("        PUAN ").Append(_score.Spendable);
        }

        /// <summary>
        /// Bir sayıyı <b>tabanıyla birlikte</b> yazar: "hasar 71 (taban 55)".
        ///
        /// <para>Kartın ne yaptığını gösteren tek şey bu karşılaştırma. Yalnızca son
        /// sayıyı göstermek, "bu kart işe yaradı mı" sorusunu cevapsız bırakır.</para>
        /// </summary>
        private void AppendStat(string label, float value, float baseValue, string suffix)
        {
            _text.Append("  ").Append(label.PadRight(16));
            _text.Append(value.ToString("0.#"));

            if (!Mathf.Approximately(value, baseValue))
            {
                _text.Append("  (taban ").Append(baseValue.ToString("0.#")).Append(')');
            }

            _text.Append(suffix).Append('\n');
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter, richText = false };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.UpperLeft, richText = false, wordWrap = false };
        }
    }
}
