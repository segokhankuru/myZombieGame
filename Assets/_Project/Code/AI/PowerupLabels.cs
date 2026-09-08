using System.Collections.Generic;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Yerdeki eşyaların <b>adını</b> yazar. 2026-09-07.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"yere düşen dropları ikonla ne anlama
    /// geldiğini belirtirsen daha anlamlı olur."</i> Siluet
    /// (<see cref="PowerupPickup"/>) uzaktan "orada bir şey var" der; yazı yakından
    /// "koşmaya değer mi" sorusunu cevaplar. İkisi birlikte çalışır — yalnızca yazı
    /// olsaydı kalabalıkta okunmazdı, yalnızca şekil olsaydı ilk oyunda
    /// öğrenilemezdi.</para>
    ///
    /// <para><b>Yazı ÖMÜR SAYACIYLA birlikte:</b> eşya 25 saniye sonra kayboluyor ve
    /// son üç saniyede yanıp sönüyor. Sönmeye başladığında saniye de yazılır —
    /// "yetişir miyim" bir tahmin olmaktan çıkar.</para>
    ///
    /// <para><b>Tek çizici, eşya başına bir <c>OnGUI</c> değil</b> — gerekçe
    /// <see cref="ZombieHealthLabels"/>'daki ile aynı.</para>
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PowerupLabels : MonoBehaviour
    {
        /// <summary>Bu mesafeden uzaktaki eşyanın adı yazılmaz.</summary>
        private const float LabelDistanceMeters = 18f;

        private static PowerupLabels _instance;

        private Camera _camera;
        private float _lastCameraSearch = -99f;

        private GUIStyle _style;

        /// <summary>Çiziciyi sahnede garantiler. İlk eşya düştüğünde çağrılır.</summary>
        internal static void EnsureInstalled()
        {
            if (_instance != null) return;

            var host = new GameObject("_PowerupLabels");
            _instance = host.AddComponent<PowerupLabels>();
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

            IReadOnlyList<PowerupPickup> pickups = PowerupPickup.AliveList;
            if (pickups.Count == 0) return;

            if (_camera == null) AcquireCamera();
            if (_camera == null) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                richText = false
            };

            Transform cameraTransform = _camera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 cameraForward = cameraTransform.forward;

            float maxDistanceSqr = LabelDistanceMeters * LabelDistanceMeters;

            for (int i = 0; i < pickups.Count; i++)
            {
                PowerupPickup pickup = pickups[i];
                if (pickup == null) continue;

                Vector3 position = pickup.transform.position + Vector3.up * 0.45f;
                Vector3 toPickup = position - cameraPosition;

                // Arkadaki esyaya yazi yazmak, ekranin dort bir yanina ters yansiyan
                // yazilar demektir - WorldToScreenPoint arkayi da dondurur.
                if (Vector3.Dot(toPickup, cameraForward) <= 0.1f) continue;
                if (toPickup.sqrMagnitude > maxDistanceSqr) continue;

                Vector3 screen = _camera.WorldToScreenPoint(position);
                if (screen.z <= 0f) continue;

                string text = pickup.Label;
                var rect = new Rect(screen.x - 90f, Screen.height - screen.y - 9f, 180f, 18f);

                Color color = pickup.LabelColor;

                _style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height),
                          text, _style);

                _style.normal.textColor = color;
                GUI.Label(rect, text, _style);
            }
        }

        /// <summary>
        /// Kamerayı bulur. <c>Camera.main</c> yalnızca "MainCamera" etiketli kamerayı
        /// döner; etiketsiz kalırsa yazılar hiç çizilmez. Arama saniyede bir.
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
