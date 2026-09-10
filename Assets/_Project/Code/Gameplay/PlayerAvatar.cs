using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Oyuncunun <b>gövdesi</b>: model, animasyon ve onu süren durum (2026-09-09).
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"oynadığımız karakterin bir görseli vs
    /// yok şu an, sadece küp diye biliyorum."</i> Doğru — oyuncu bir kapsüldü. Solo'da
    /// bu neredeyse görünmez bir eksik (kendi gövdeni zaten görmezsin) ama iki yerde
    /// gerçek bir bedeli vardı: <b>gölgen</b> bir kapsül gölgesiydi, ve co-op'ta
    /// arkadaşının ne yaptığını —koşuyor mu, duruyor mu— okumanın hiçbir yolu yoktu.</para>
    ///
    /// <para><b>Yerel oyuncunun gövdesi YALNIZCA GÖLGE çizer.</b> Kamera kafanın
    /// içinde; gövdeyi normal çizmek, ekranın ortasında bir boyun kesiti göstermek
    /// olurdu. Nesneyi tamamen kapatmak ise gölgeyi de götürürdü ve gölge <i>bilgidir</i>:
    /// arkandan gelen zombiyi ilk oradan görürsün. <c>ShadowsOnly</c> ikisini birden
    /// çözüyor — el modeli (<see cref="PlayerViewmodel"/>) zaten ayrı yaşıyor.</para>
    ///
    /// <para><b>Animasyon durumu TAKIP EDER, durum animasyonu beklemez</b>
    /// (gameplay-code.md). Bu bileşen tek bir oyun kuralı içermez: hızı okur, iki
    /// parametre yazar. Hareketin kendisi <see cref="PlayerController"/>'da ve orada
    /// kalmalı — animasyonun karar verdiği bir hareket, animasyon yeniden
    /// zamanlandığında sessizce bozulur.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Avatar")]
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        [Tooltip("Karakter modelinin Animator'u. Bunker > Oyuncu > Karakteri Kur baglar.")]
        [SerializeField] private Animator animator;

        [Tooltip("Modelin kok nesnesi. Yerel oyuncuda yalnizca GOLGE cizer.")]
        [SerializeField] private GameObject model;

        private PlayerController _player;
        private CharacterController _body;

        // Animator parametre id'leri: string ile aramak kare basina bir hash hesabi
        // demektir (csharp-code.md). Bir kez cozulur.
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int StrafeHash = Animator.StringToHash("Strafe");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");

        /// <summary>
        /// Animasyon parametresinin yumuşatılması. <b>Ham hızı doğrudan yazmak</b>,
        /// yön değiştiren oyuncuda animasyonun tek karede zıplaması demek — koşudan
        /// durmaya geçiş bir eşik değil, bir eğri.
        /// </summary>
        private const float BlendSmoothing = 12f;

        private float _speed;
        private float _strafe;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _body = GetComponent<CharacterController>();

            if (animator == null && model != null)
            {
                animator = model.GetComponentInChildren<Animator>(true);
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            if (model == null) return;

            // KENDI GOVDEN YALNIZCA GOLGE. Kapatmak degil - gölge, arkandan gelen
            // zombiyi gordugun ilk yer (bkz. sinif notu).
            var renderers = model.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }

        private void Update()
        {
            if (animator == null || _body == null) return;

            float dt = Time.deltaTime;

            // Yatay hiz, KARAKTERIN KENDI eksenlerinde: ileri/geri ve saga/sola ayri
            // parametreler, yoksa geri geri yuruyen oyuncu one dogru yuruyor gorunur.
            Vector3 velocity = _body.velocity;
            velocity.y = 0f;

            Vector3 local = transform.InverseTransformDirection(velocity);

            // Kosu, yurumeden ayrilsin diye olcek hiz kademesine gore: blend tree
            // 0 = dur, 1 = yurume, 2 = kosu bekliyor.
            float sprintScale = _player != null && _player.IsSprinting ? 2f : 1f;
            float reference = Mathf.Max(0.1f, WalkReferenceSpeed);

            float targetSpeed = Mathf.Clamp(local.z / reference * sprintScale, -1f, 2f);
            float targetStrafe = Mathf.Clamp(local.x / reference, -1f, 1f);

            // Ussel yumusatma: dt'ye bagimli olmayan tek dogru bicim. Sabit bir lerp
            // katsayisi, kare hizina gore farkli hizda yumusatirdi.
            float k = 1f - Mathf.Exp(-BlendSmoothing * dt);

            _speed = Mathf.Lerp(_speed, targetSpeed, k);
            _strafe = Mathf.Lerp(_strafe, targetStrafe, k);

            animator.SetFloat(SpeedHash, _speed);
            animator.SetFloat(StrafeHash, _strafe);
            animator.SetBool(GroundedHash, _body.isGrounded);
        }

        /// <summary>
        /// Blend ağacının "1 = yürüme" karşılığı, metre/saniye. <b>Denge değeri
        /// değil</b>, animasyon klibinin kendi hızı — bu yüzden config'de değil burada
        /// (config-data.md config'i denge için ayırır). Paketin yürüme klibi bu hızda
        /// kaydedilmiş; sayı değişirse ayaklar yerde kayar.
        /// </summary>
        private const float WalkReferenceSpeed = 2.2f;
    }
}
