using Bunker.Systems.Economy;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Satın alma etkileşimi: bak, <b>E</b>'ye bas. Kapı (M1-09) ve duvar silahı
    /// (M1-10) aynı yoldan geçer.
    ///
    /// <para><b>Tek etkileşim noktası:</b> yeni bir satın alınabilir eklemek burayı
    /// değiştirmeyi gerektirmez — bakılan şey fiyatını ve ne olacağını kendisi söyler
    /// (<see cref="IPurchasable"/>).</para>
    ///
    /// <para><b>Basmak satın alır, tutmak tamir eder.</b> Aynı tuş iki iş yapıyor ve
    /// çakışma tanımlı: satın alınabilir bir şeye bakarken tamir çalışmaz. Bu ikisi
    /// pratikte aynı anda görüş alanında olmaz, ama "olmaz" varsaymak yerine kuralı
    /// yazmak gerekir.</para>
    ///
    /// <para><b>Otorite host'ta</b> (ADR-0004): puanı düşüren ve satın almayı onaylayan
    /// sunucudur. İstemcinin "aldım" demesi bir istektir, sonuç değil.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Interact")]
    public sealed class PlayerInteract : NetworkBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerScore score;
        [SerializeField] private PlayerWeapon weapon;

        [Tooltip("Etkilesim menzili. Denge degeri degil ergonomi: oyuncunun 'buna " +
                 "bakiyorum' dedigi mesafe.")]
        [SerializeField] private float rangeMeters = 3f;

        /// <summary>Şu an bakılan satın alınabilir şeyin metni; yoksa boş. HUD okur.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        /// <summary>Bakılan şey alınabilir mi (puan yetiyor mu). HUD rengi için.</summary>
        public bool CanAfford { get; private set; }

        /// <summary>Satın alınabilir bir şeye bakılıyor mu — tamir bu sırada durur.</summary>
        public bool HasTarget => CurrentPrompt.Length > 0;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
            if (score == null) score = GetComponent<PlayerScore>();
            if (weapon == null) weapon = GetComponent<PlayerWeapon>();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            IPurchasable target = FindTarget();

            if (target == null)
            {
                CurrentPrompt = string.Empty;
                CanAfford = false;
                return;
            }

            CurrentPrompt = target.Prompt;
            CanAfford = score != null && score.Spendable >= target.Cost;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;

            CmdPurchase();
        }

        /// <summary>
        /// Bakılan satın alınabilir şey. <b>Yalnızca ipucu için</b> — sunucu kendi
        /// ışınını atar, çünkü istemcinin neye baktığı iddiası güvenilmez veridir.
        /// </summary>
        private IPurchasable FindTarget()
        {
            Transform cam = playerCamera != null ? playerCamera.transform : transform;

            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, rangeMeters,
                                 ~0, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            var purchasable = hit.collider.GetComponentInParent<IPurchasable>();
            return purchasable != null && purchasable.IsAvailable ? purchasable : null;
        }

        [Command]
        private void CmdPurchase()
        {
            Transform cam = playerCamera != null ? playerCamera.transform : transform;

            // Sunucu kendi isinini atar. Mesafeye biraz pay birakilir: istemcinin
            // konumu bir kare eski olabilir (BUG-002'nin dersi - esitlik degil
            // makuliyet).
            const float serverRangeSlackMeters = 1f;

            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit,
                                 rangeMeters + serverRangeSlackMeters,
                                 ~0, QueryTriggerInteraction.Collide))
            {
                return;
            }

            var purchasable = hit.collider.GetComponentInParent<IPurchasable>();
            if (purchasable == null || !purchasable.IsAvailable) return;
            if (score == null) return;

            // Duvar silahi mermiyi ALICIYA yazar; kim aldiysa o.
            if (purchasable is WallWeaponPurchase wallWeapon) wallWeapon.SetBuyer(weapon);

            // Once odeme, sonra etki. Ters sirada bir hata, bedava kapi demektir.
            if (score.TrySpend(purchasable.Cost) != PurchaseResult.Success) return;

            purchasable.OnPurchased();
        }
    }
}
