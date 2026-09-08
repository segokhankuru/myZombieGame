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
        private GUIStyle _damageStyle;

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
            Vector3 cameraRight = cameraTransform.right;

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

                // --- vurdugu hasar: barin SOL tarafinda.
                //
                // Sol kenar ekranda nerede? Barin sol ucunu dunyada bulup ayrica
                // yansitiyoruz: sabit bir piksel payi, iki metredeki zombide barin
                // icine, yirmi metredekinde ekranin yarisina duserdi.
                Vector3 leftEdgeWorld = barPosition - cameraRight * bar.BarHalfWidthMeters;
                Vector3 leftEdge = _camera.WorldToScreenPoint(leftEdgeWorld);

                // YALNIZCA SAYI (2026-09-08, gelistirici: "yanindaki 'vurus hasari'
                // yazisini sil, sadece gucu yazsin"). Etiket ilk surumde eklenmisti
                // cunku iki ciplak sayi birbirine karisiyordu; artik can sayisi "CAN"
                // ile etiketli, yani ayrimi zaten o tasiyor. Renk de ayri (turuncu).
                //
                // <b>Sayi HER ZAMAN GUNCEL:</b> agent.AttackDamage bir onbellek degil,
                // config x boss carpani hesabinin kendisi - ekranda o karedeki gercek
                // deger duruyor. Sayinin turlar boyunca degismemesi bir gosterim
                // hatasi DEGIL: zombie.json'da attack.damage sabit 30 ve tur
                // olceklemesi (RoundScaling) yalnizca CAN, HIZ ve ADET uretiyor. Tek
                // degisen sey boss (rounds.json boss.damageMultiplier = 2), o da
                // burada carpilmis hâlde gorunuyor.
                _text.Clear();
                _text.Append(Mathf.CeilToInt(agent.AttackDamage));

                var damageRect = new Rect(leftEdge.x - 122f, Screen.height - leftEdge.y - 9f,
                                          114f, 18f);
                Draw(damageRect, _text.ToString(), _damageStyle);

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

            // Hasar sayisi TURUNCU ve saga dayali: canla ayni renkte olsaydi hangi
            // sayinin hangi soruyu cevapladigi okunmazdi.
            _damageStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleRight,
                richText = false,
                normal = { textColor = new Color(1f, 0.62f, 0.25f) }
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
