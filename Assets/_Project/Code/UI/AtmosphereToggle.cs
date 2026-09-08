using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Atmosfer katmanını (post-processing + sis) tek tuşla kapatır. <b>F10.</b>
    ///
    /// <para><b>Neden kapatılabilir olması şart:</b> M-01'in çıkış kriteri ÇK-17 tam
    /// olarak şunu soruyor — <i>"gri kutuda, sanatsız bir tur döngüsü 20 dakika sonra
    /// tekrar oynatıyor mu?"</i> Atmosfer açıkken alınan bir "evet", döngünün mü yoksa
    /// görselliğin mi taşıdığını söylemez. Bu tuş, ölçümü istendiğinde temiz gri kutuda
    /// tekrarlayabilmek içindir.</para>
    ///
    /// <para><b>Okunabilirlik bu tuşa bağlı değildir.</b> Renk ve materyal dili
    /// (zemin/duvar/kapı/barikat ayrımı) her iki durumda da açıktır — o bir cila değil,
    /// PILLAR-04'ün tasarım kısıtı. Kapanan yalnızca <i>atmosfer</i>: bloom, vinyet,
    /// renk derecelendirme ve sis.</para>
    ///
    /// <para><b>Volume tipine dokunmaz</b>, nesneyi açıp kapatır. Böylece
    /// <c>Bunker.UI</c>'nin render pipeline paketine bağımlı olması gerekmez.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Atmosphere Toggle (F10)")]
    public sealed class AtmosphereToggle : MonoBehaviour
    {
        [Tooltip("Kapatilacak atmosfer nesnesi (global Volume'u tasiyan). Kurulum " +
                 "araci baglar.")]
        [SerializeField] private GameObject atmosphere;

        [Tooltip("Sis de kapansin mi. Sis atmosferin parcasi; okunabilirligin degil.")]
        [SerializeField] private bool alsoToggleFog = true;

        [Tooltip("Ekranin kosesinde durumu goster. Kapaliyken hangi modda oldugunu " +
                 "bilmeden alinan bir olcum, olcum degildir.")]
        [SerializeField] private bool showState = true;

        private bool _on = true;
        private bool _fogAtStart;

        /// <summary>Dis hava sistemi. F10 onu da kapatmak zorunda (2026-09-07).</summary>
        private Bunker.Gameplay.OutdoorWeather _weather;
        private GUIStyle _style;

        private void Awake()
        {
            // Sahnenin acilistaki sis durumu saklanir: F10 ile geri acildiginda
            // sahnenin kendi ayarina donulur, uydurma bir varsayilana degil.
            _fogAtStart = RenderSettings.fog;
        }

        private void OnDestroy()
        {
            // Awake'in okudugunu OnDestroy geri yazar. Sis global bir ayardir ve
            // kapali birakilirsa BIR SONRAKI Play oturumu sissiz acilir - editorde
            // fark edilmesi zor, sessiz bir yan etki.
            RenderSettings.fog = _fogAtStart;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!keyboard.f10Key.wasPressedThisFrame) return;

            _on = !_on;

            if (atmosphere != null) atmosphere.SetActive(_on);

            // HAVA SISTEMI DE KAPANIR (2026-09-07). OutdoorWeather sisi HER KAREDE
            // yeniden yaziyor ve yagmuru surduruyor; yalnizca RenderSettings'e
            // dokunmak, F10'un bir kare sonra geri alinmasi demek olurdu - yani
            // calismayan bir anahtar. Kapatma isteginin sahibine soylenmesi gerekiyor.
            if (_weather == null) _weather = FindFirstObjectByType<Bunker.Gameplay.OutdoorWeather>();
            if (_weather != null) _weather.SetEnabled(_on);

            if (alsoToggleFog) RenderSettings.fog = _on && _fogAtStart;
        }

        private void OnGUI()
        {
            if (!showState || _on) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };

            // Yalnizca KAPALIYKEN yazi cikar: acik hal normal durumdur ve surekli
            // duran bir etiket gurultudur (ui-code.md).
            GUI.Label(new Rect(24f, 24f, 320f, 24f), "ATMOSFER KAPALI  (F10)", _style);
        }
    }
}
