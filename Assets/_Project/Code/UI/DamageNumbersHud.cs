using System.Text;
using Bunker.Gameplay;
using Bunker.Systems.Rounds;
using Bunker.Systems.Settings;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// Hasar sayıları ve <b>hasar yönü göstergesi</b>. 2026-09-05.
    ///
    /// <para><b>İki ayrı soruyu cevaplar.</b> Verdiğin hasar: <i>"silahım hâlâ işe
    /// yarıyor mu?"</i> — 15. turda zombinin 3000 canı varken 55 hasar vurduğunu
    /// görmezsen bunu ancak hissedersin, bilemezsin. Aldığın hasar: <i>"tek mi yedim,
    /// dört mü, nereden?"</i> — geliştiricinin "turlar ilerleyince tek yiyorum sanırım"
    /// cümlesi tam olarak bu bilginin yokluğuydu.</para>
    ///
    /// <para><b>Havuzlu ve tahsissiz:</b> sayılar sabit bir dizide yaşar, yeni bir
    /// isabet en eskisinin üstüne yazar. Vuruş başına nesne yaratmak, sürünün içinde
    /// saniyede onlarca tahsis demek olurdu (csharp-code.md).</para>
    ///
    /// <para><b>Kapatılabilir</b> (<c>GameSettings.ShowDamageNumbers</c>): ekranı temiz
    /// isteyen oyuncu için. Kapalıyken hiç çizilmez, hesaplanmaz bile.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Damage Numbers HUD")]
    public sealed class DamageNumbersHud : MonoBehaviour
    {
        /// <summary>Aynı anda ekranda durabilecek en fazla sayı.</summary>
        private const int PoolSize = 24;

        /// <summary>Bir sayının ömrü. Kısa: ekranı doldurmadan okunmalı.</summary>
        private const float LifeSeconds = 0.9f;

        /// <summary>Sayının yukarı süzülme hızı (metre/saniye).</summary>
        private const float FloatSpeed = 0.8f;

        /// <summary>Hasar yönü okunun ömrü.</summary>
        private const float DirectionLifeSeconds = 1.2f;

        private struct Popup
        {
            public Vector3 WorldPosition;
            public float Amount;
            public bool Headshot;
            public bool Killed;
            public float Remaining;
        }

        private readonly Popup[] _popups = new Popup[PoolSize];
        private int _next;

        private readonly StringBuilder _text = new StringBuilder(16);

        // Alinan hasarin yonu: kaynagin dunya konumu ve kalan sure.
        private Vector3 _damageFrom;
        private float _damageFromRemaining;
        private float _damageTakenAmount;
        private int _damageTakenHits;

        private GUIStyle _numberStyle;
        private GUIStyle _takenStyle;
        private Texture2D _pixel;
        private Camera _camera;
        private float _cameraSearchTimer;

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
            CombatFeedback.DamageDealt += OnDamageDealt;
            CombatFeedback.DamageTaken += OnDamageTaken;
        }

        private void OnDisable()
        {
            CombatFeedback.DamageDealt -= OnDamageDealt;
            CombatFeedback.DamageTaken -= OnDamageTaken;
        }

        // ---------------------------------------------------------------- girdi

        private void OnDamageDealt(Vector3 worldPosition, float amount, bool headshot, bool killed)
        {
            if (!GameSettings.ShowDamageNumbers) return;

            // En eskinin uzerine yaz: havuz dolu diye yeni isabeti YUTMAK, en yogun
            // anda - yani sayinin en cok gerektigi anda - gostergeyi susturmak olurdu.
            _popups[_next] = new Popup
            {
                WorldPosition = worldPosition,
                Amount = amount,
                Headshot = headshot,
                Killed = killed,
                Remaining = LifeSeconds
            };

            _next = (_next + 1) % PoolSize;
        }

        /// <summary>
        /// Alınan hasar. <b>Gösterge açıkken gelen vuruşlar TOPLANIR ve sayılır</b>
        /// (2026-09-07).
        ///
        /// <para><b>Neden gerekti:</b> önceki sürüm her vuruşta sayının üstüne yazıyordu
        /// — yani sürünün içinde art arda dört vuruş yiyen oyuncu ekranda yalnızca son
        /// vuruşu görüyordu. "Bir zombi bana 30 vuruyor ama canım 120 gitti" diye
        /// okunuyor ve zombinin hasarı yanlış sanılıyordu (geliştirici,
        /// 2026-09-07: <i>"-30 vurur bilgisi varken 250 küsür vurdu"</i>).</para>
        ///
        /// <para>Artık gösterge <b>bir pencere</b>: içinde ne kadar can gittiğini ve
        /// <i>kaç vuruştan</i> geldiğini yazıyor. "Birden öldüm" cümlesini tahmin
        /// olmaktan çıkaran şey, tek sayı değil bu ikisi.</para>
        /// </summary>
        private void OnDamageTaken(float amount, Vector3 sourcePosition)
        {
            _damageFrom = sourcePosition;

            // Gosterge sonmusse yeni bir pencere baslar; hala aciksa uzerine eklenir.
            if (_damageFromRemaining <= 0f)
            {
                _damageTakenAmount = 0f;
                _damageTakenHits = 0;
            }

            _damageFromRemaining = DirectionLifeSeconds;
            _damageTakenAmount += amount;
            _damageTakenHits++;
        }

        // ---------------------------------------------------------------- dongu

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < _popups.Length; i++)
            {
                if (_popups[i].Remaining <= 0f) continue;

                _popups[i].Remaining -= dt;
                _popups[i].WorldPosition += Vector3.up * (FloatSpeed * dt);
            }

            if (_damageFromRemaining > 0f) _damageFromRemaining -= dt;

            if (_camera != null) return;

            // Kamera ag tarafindan gec gelir; her kare aramak yerine saniyede iki kez
            // (csharp-code.md: kare basina Find yasak).
            _cameraSearchTimer -= dt;
            if (_cameraSearchTimer > 0f) return;

            _cameraSearchTimer = 0.5f;
            _camera = Camera.main;
        }

        private void OnGUI()
        {
            if (_camera == null) return;
            if (RunSignals.IsRunOver) return;

            EnsureStyles();

            DrawDealtNumbers();
            DrawTakenIndicator();
        }

        // ---------------------------------------------------------------- cizim

        private void DrawDealtNumbers()
        {
            for (int i = 0; i < _popups.Length; i++)
            {
                Popup popup = _popups[i];
                if (popup.Remaining <= 0f) continue;

                Vector3 screen = _camera.WorldToScreenPoint(popup.WorldPosition);

                // Kameranin ARKASINDAKI nokta ekranda ONDE gibi gorunur: z kontrolu
                // olmadan arkanda oldurdugun zombinin sayisi ekranin ortasinda belirir.
                if (screen.z <= 0f) continue;

                float fade = Mathf.Clamp01(popup.Remaining / LifeSeconds);

                _text.Clear();
                _text.Append(Mathf.RoundToInt(popup.Amount));
                if (popup.Headshot) _text.Append('!');

                // Renk BILGI tasiyor ama tek basina degil (ui-code.md): kafa vurusunun
                // ayrica bir unlem isareti var, oldurmenin sayisi daha buyuk yazilir.
                Color color = popup.Killed
                    ? new Color(1f, 0.35f, 0.25f)
                    : popup.Headshot
                        ? new Color(1f, 0.85f, 0.25f)
                        : new Color(0.95f, 0.95f, 0.95f);

                color.a = fade;

                _numberStyle.fontSize = popup.Killed ? 26 : popup.Headshot ? 22 : 18;

                GUI.color = color;
                GUI.Label(new Rect(screen.x - 60f, Screen.height - screen.y - 20f, 120f, 30f),
                          _text.ToString(), _numberStyle);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Aldığın hasar: miktar ve <b>geldiği yön</b>.
        ///
        /// <para>Yön, ekranın ortasından dışa doğru bir çizgi olarak çiziliyor — bir ok
        /// ya da yazı değil, çünkü sürünün içinde okunacak süre yarım saniyedir.</para>
        /// </summary>
        private void DrawTakenIndicator()
        {
            if (_damageFromRemaining <= 0f) return;
            if (!GameSettings.ShowDamageDirection) return;

            float fade = Mathf.Clamp01(_damageFromRemaining / DirectionLifeSeconds);
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            // Miktar nisangahin biraz ustunde, kirmizi: kendi canindan giden sayi.
            GUI.color = new Color(1f, 0.35f, 0.3f, fade);
            _takenStyle.fontSize = 22;
            // Vurus sayisi yalnizca BIRDEN FAZLAYSA yazilir: tek vuruslarda "x1"
            // her seferinde ekranda duran ve hicbir sey soylemeyen bir isaret olurdu.
            string taken = _damageTakenHits > 1
                ? $"-{Mathf.RoundToInt(_damageTakenAmount)}   {_damageTakenHits} vurus"
                : $"-{Mathf.RoundToInt(_damageTakenAmount)}";

            GUI.Label(new Rect(cx - 140f, cy - 90f, 280f, 30f), taken, _takenStyle);

            Vector3 toSource = _damageFrom - _camera.transform.position;
            toSource.y = 0f;

            if (toSource.sqrMagnitude > 0.01f)
            {
                Vector3 forward = _camera.transform.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, forward);

                toSource.Normalize();

                // Kameraya GORE aci: oyuncu dondugunde gosterge de doner, yani ok
                // her zaman gercek yonu isaret eder.
                float angle = Mathf.Atan2(Vector3.Dot(toSource, right),
                                          Vector3.Dot(toSource, forward));

                float radius = 110f;
                float x = cx + Mathf.Sin(angle) * radius;
                float y = cy - Mathf.Cos(angle) * radius;

                GUI.DrawTexture(new Rect(x - 14f, y - 3f, 28f, 6f), _pixel);
            }

            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            _numberStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = false };

            _takenStyle ??= new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = false };
        }
    }
}
