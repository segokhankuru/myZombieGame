using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Can barlarının <b>sayıları</b>. 2026-09-07.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"zombilerin can barında kaç HP'si
    /// olduğunu yaz ve can barının sol tarafına kaç hasar vurabiliyorlar onu da yaz,
    /// testler için önemli bir detay, hasar takibi için."</i> Bar bir <i>oran</i>
    /// gösterir; oran denge ayarlamaz. 15. turda barın yarısı 90 can da olabilir 1500
    /// de, ve tur ölçeklemesinin doğru çalışıp çalışmadığı ancak sayıyla görülür.</para>
    ///
    /// <para><b>Neden tek çizici, zombi başına bir bileşen değil:</b> <c>OnGUI</c>
    /// bileşen başına kare başına iki çağrıdır (Layout + Repaint). 40 zombide bu,
    /// ölçmek istediğimiz kare süresini bozan bir ölçüm aracı demek olurdu — projedeki
    /// "aracın kendisi ölçümü kirletiyor" dersinin aynısı. Burada tek bir <c>OnGUI</c>
    /// kayıt defterini dolaşır.</para>
    ///
    /// <para><b>Kendi kendini kurar</b> (<see cref="EnsureInstalled"/>): sahneye elle
    /// eklenmesi gereken bir teşhis aracı, eklenmeyi unutulduğu gün sessizce yok olur —
    /// bu projedeki hataların en sık türü. İlk can barı doğduğunda kurulur.</para>
    ///
    /// <para><b>Yayın öncesi kalkacak</b>, tıpkı <see cref="ZombieHealthBar"/> gibi:
    /// oyuncu zombinin canını sayıdan değil davranışından okumalı (PILLAR-04).</para>
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ZombieHealthLabels : MonoBehaviour
    {
        /// <summary>Bu mesafeden uzaktaki zombilerin sayısı yazılmaz — ekran dolmasın.</summary>
        private const float LabelDistanceMeters = 22f;

        /// <summary>Aynı karede en fazla kaç etikete yazı yazılır.</summary>
        private const int MaxLabelsPerFrame = 24;

        private static ZombieHealthLabels _instance;

        private Camera _camera;
        private float _lastCameraSearch = -99f;

        private GUIStyle _healthStyle;

        private readonly StringBuilder _text = new StringBuilder(24);

        /// <summary>
        /// Çiziciyi sahnede garantiler. İlk <see cref="ZombieHealthBar"/> çağırır.
        /// </summary>
        internal static void EnsureInstalled()
        {
            if (_instance != null) return;

            var host = new GameObject("_ZombieHealthLabels");
            _instance = host.AddComponent<ZombieHealthLabels>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            List<ZombieHealthBar> bars = ZombieHealthBar.Registry;
            if (bars.Count == 0) return;

            if (_camera == null) AcquireCamera();
            if (_camera == null) return;

            EnsureStyles();

            Transform cameraTransform = _camera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 cameraForward = cameraTransform.forward;

            float maxDistanceSqr = LabelDistanceMeters * LabelDistanceMeters;
            int drawn = 0;

            for (int i = 0; i < bars.Count && drawn < MaxLabelsPerFrame; i++)
            {
                ZombieHealthBar bar = bars[i];
                if (bar == null || !bar.IsShowing) continue;

                ZombieAgent agent = bar.Agent;
                if (agent == null || !agent.IsAlive) continue;

                Vector3 barPosition = bar.BarWorldPosition;
                Vector3 toBar = barPosition - cameraPosition;

                // Arkadaki zombiye yazi yazmak, ekranin dort bir yanina ters
                // yansiyan sayilar demektir - WorldToScreenPoint arkayi da dondurur.
                if (Vector3.Dot(toBar, cameraForward) <= 0.1f) continue;
                if (toBar.sqrMagnitude > maxDistanceSqr) continue;

                Vector3 center = _camera.WorldToScreenPoint(barPosition);
                if (center.z <= 0f) continue;

                // --- can: barin USTUNDE, "CAN kalan/en yuksek"
                //
                // ETIKETLI (2026-09-07): ilk surum iki ciplak sayi yaziyordu ve
                // geliştirici ikisini birbirine karistirdi ("-30 vurur diyor ama 250
                // vurdu"). Yanindaki sari hasar sayilariyla ayni gorsel dilde duran
                // etiketsiz bir sayi, hangi soruyu cevapladigini soylemez.
                _text.Clear();
                _text.Append("CAN ");
                _text.Append(Mathf.CeilToInt(agent.Health));
                _text.Append('/');
                _text.Append(Mathf.CeilToInt(agent.MaxHealth));

                float y = Screen.height - center.y;

                var healthRect = new Rect(center.x - 70f, y - 34f, 140f, 18f);
                Draw(healthRect, _text.ToString(), _healthStyle);

                // --- vurdugu hasar: ARTIK BURADA DEGIL (2026-09-09).
                //
                // Gelistirici: "zombilerin hasarini ... zombilerin can barinin yaninda
                // degil de ekranin solunda belirt". Sayi CombatHud'un sol tehdit
                // panelinde (RoundThreat) ve orada olmasinin iki sebebi var:
                //
                // (1) Burada KIRK KERE TEKRAR EDIYORDU. Ayni sabit sayinin ekrandaki
                //     her zombinin yaninda yazmasi, PILLAR-04'un (kaosta okunabilirlik)
                //     tam tersi - gozun ayirt etmesi gereken sey zombinin KENDISI iken,
                //     etraf ayni sayiyla doluyordu.
                //
                // (2) Zombi ekranda yokken HIC GORUNMUYORDU. Oysa "bu tur ne kadar
                //     sert" sorusu tam da hazirlik molasinda soruluyor - yani sayinin
                //     en gerekli oldugu anda ekranda degildi.
                //
                // Boss'un iki katli hasari da panelde ayrica yaziyor, yani zombiye
                // gore degisen tek bilgi de kaybolmadi.

                drawn++;
            }
        }

        /// <summary>
        /// Yazıyı önce koyu bir gölgeyle çizer. <b>Kontur değil, tek kaydırma:</b>
        /// dört yönlü kontur dört kat çizim demek; bir piksel gölge, aydınlık bir
        /// duvarın önündeki beyaz yazıyı okunur kılmaya yetiyor.
        /// </summary>
        private static void Draw(Rect rect, string value, GUIStyle style)
        {
            Color previous = style.normal.textColor;

            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), value, style);

            style.normal.textColor = previous;
            GUI.Label(rect, value, style);
        }

        private void EnsureStyles()
        {
            _healthStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(0.95f, 0.95f, 0.92f) }
            };
        }

        /// <summary>
        /// Kamerayı bulur. <c>Camera.main</c> yalnızca "MainCamera" etiketli kamerayı
        /// döner ve oyuncu kamerası etiketsizse <c>null</c> gelir — can barlarında bu
        /// hata bir kez yaşandı (bar göründü, hiç güncellenmedi). Arama saniyede bir.
        /// </summary>
        private void AcquireCamera()
        {
            if (Time.unscaledTime - _lastCameraSearch < 1f) return;
            _lastCameraSearch = Time.unscaledTime;

            _camera = Camera.main;
            if (_camera == null) _camera = FindFirstObjectByType<Camera>();
        }
    }
}
