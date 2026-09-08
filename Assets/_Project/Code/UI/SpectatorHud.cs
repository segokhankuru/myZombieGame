using Bunker.Gameplay;
using Bunker.Systems.Rounds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.UI
{
    /// <summary>
    /// Yere düştüğünde ya da öldüğünde <b>arkadaşını izleme</b>. 2026-09-07.
    ///
    /// <para><b>Neden gerekli</b> (geliştirici): <i>"tur boyunca da arkadaşlarını
    /// spectate edebilmeli."</i> Bu bir konfor özelliği değil, cezanın <b>tahammül
    /// edilebilir</b> olmasının şartı: yere düşüp bir turluk beklemek, ekranda hiçbir
    /// şey olmadan geçerse oyuncu alt+tab yapar ve bir daha dönmez. İzlemek, öldüğün
    /// turu hâlâ <i>yaşamanı</i> sağlar — ve arkadaşının nasıl oynadığını görmek
    /// öğretir.</para>
    ///
    /// <para><b>Kamera taşınmaz, ÖDÜNÇ alınır:</b> düşen oyuncunun kendi kamerası
    /// arkadaşının omzuna gider ve dirilince kendi yerine döner. İkinci bir kamera
    /// yaratmak, iki dinleyici (AudioListener) ve iki render yolu demek olurdu —
    /// birinci oturumda kimsenin fark etmeyeceği, üçüncüsünde sesi bozan türden bir
    /// hata.</para>
    ///
    /// <para><b>Boşluk tuşu izlenen kişiyi değiştirir</b>: dört kişilik bir oturumda
    /// tek bir kişiyi izlemeye mahkûm olmak, izlemeyi yine bir bekleme ekranına
    /// çevirir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Spectator HUD")]
    public sealed class SpectatorHud : MonoBehaviour
    {
        /// <summary>İzlenen oyuncunun ne kadar arkasında/üstünde durulacağı.</summary>
        private static readonly Vector3 ShoulderOffset = new Vector3(0.5f, 0.55f, -2.4f);

        private PlayerDownState _local;
        private PlayerDownState _watching;
        private Camera _camera;

        private Vector3 _cameraHomePosition;
        private Quaternion _cameraHomeRotation;
        private Transform _cameraHomeParent;
        private bool _borrowed;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private Texture2D _pixel;

        private float _lastSearch = -99f;

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);

            // Kamera odunc alinmisken bilesen yok edilirse, oyuncu bir daha kendi
            // gozunden bakamazdi (csharp-code.md: Awake'in kurdugunu OnDestroy bozar).
            ReturnCamera();
        }

        private void LateUpdate()
        {
            FindLocal();

            if (_local == null)
            {
                ReturnCamera();
                return;
            }

            if (_local.IsAlive || RunSignals.IsRunOver)
            {
                ReturnCamera();
                return;
            }

            BorrowCamera();
            TickTarget();
            FollowTarget();
        }

        private void FindLocal()
        {
            if (_local != null) return;
            if (Time.unscaledTime - _lastSearch < 0.5f) return;

            _lastSearch = Time.unscaledTime;

            // Kare basina Find yasak (csharp-code.md); saniyede iki kez, ve yalnizca
            // henuz bulunmadiysa.
            foreach (PlayerDownState candidate in PlayerDownState.Players)
            {
                if (candidate == null || !candidate.isLocalPlayer) continue;

                _local = candidate;
                _camera = candidate.GetComponentInChildren<Camera>(true);
                return;
            }
        }

        private void TickTarget()
        {
            bool switchRequested = Keyboard.current != null
                                   && Keyboard.current.spaceKey.wasPressedThisFrame;

            if (_watching != null && _watching.IsAlive && !switchRequested) return;

            _watching = PlayerDownState.AnyAliveOther(_local);
        }

        private void FollowTarget()
        {
            if (_camera == null || _watching == null) return;

            Transform target = _watching.transform;
            Vector3 wanted = target.TransformPoint(ShoulderOffset);

            // Yumusak takip: izlenen oyuncu sicradiginda kameranin da sicramasi
            // izleyeni yorar - izlemek edilgen bir istir, sarsintiyi hak etmez.
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, wanted,
                                                      1f - Mathf.Exp(-10f * Time.deltaTime));

            Vector3 lookAt = target.position + Vector3.up * 1.4f;
            _camera.transform.rotation = Quaternion.Slerp(
                _camera.transform.rotation,
                Quaternion.LookRotation(lookAt - _camera.transform.position, Vector3.up),
                1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void BorrowCamera()
        {
            if (_borrowed || _camera == null) return;

            Transform cameraTransform = _camera.transform;

            _cameraHomeParent = cameraTransform.parent;
            _cameraHomePosition = cameraTransform.localPosition;
            _cameraHomeRotation = cameraTransform.localRotation;

            cameraTransform.SetParent(null, true);
            _borrowed = true;
        }

        private void ReturnCamera()
        {
            if (!_borrowed || _camera == null)
            {
                _borrowed = false;
                return;
            }

            Transform cameraTransform = _camera.transform;

            cameraTransform.SetParent(_cameraHomeParent, false);
            cameraTransform.localPosition = _cameraHomePosition;
            cameraTransform.localRotation = _cameraHomeRotation;

            _borrowed = false;
            _watching = null;
        }

        private void OnGUI()
        {
            if (_local == null || _local.IsAlive || RunSignals.IsRunOver) return;

            EnsureStyles();

            float cx = Screen.width * 0.5f;

            // Ust bant: neden oynamiyorsun ve ne zaman doneceksin.
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 84f), _pixel);
            GUI.color = Color.white;

            string title = _local.IsDowned ? "YERDESIN" : "OLDUN";
            GUI.Label(new Rect(cx - 300f, 12f, 600f, 32f), title, _titleStyle);

            string body = _local.IsDowned
                ? "Bir arkadasin yanina gelip E'yi basili tutarsa kalkarsin."
                : "Bir sonraki turda dirileceksin.";

            if (_watching != null) body += "   [Bosluk] baska birini izle";
            else body += "   (izlenecek ayakta kimse yok)";

            GUI.Label(new Rect(cx - 300f, 46f, 600f, 24f), body, _bodyStyle);

            // Yerdeyken kaldirilma cubugu: ilerlemeyi GORMEK, arkadasin gelip
            // gelmediginden emin olmayi saglar.
            if (_local.IsDowned && _local.ReviveProgress01 > 0.001f)
            {
                DrawReviveBar(cx, _local.ReviveProgress01);
            }
        }

        private void DrawReviveBar(float cx, float progress01)
        {
            const float width = 260f;
            const float height = 14f;

            float y = Screen.height * 0.62f;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(cx - width * 0.5f, y, width, height), _pixel);

            GUI.color = new Color(0.35f, 0.85f, 0.4f);
            GUI.DrawTexture(new Rect(cx - width * 0.5f, y, width * progress01, height), _pixel);

            GUI.color = Color.white;
            GUI.Label(new Rect(cx - width * 0.5f, y - 22f, width, 20f),
                      "KALDIRILIYOR", _bodyStyle);
        }

        private void EnsureStyles()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(1f, 0.45f, 0.35f) }
            };

            _bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
                normal = { textColor = new Color(0.92f, 0.92f, 0.90f) }
            };
        }
    }
}
