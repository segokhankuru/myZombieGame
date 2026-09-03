using System;
using Bunker.Config;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Barikat tamiri: pencereye bak, <b>E</b>'yi basılı tut. M1-08.
    ///
    /// <para><b>Karar oyuncunun:</b> tamir sırasında bakışını pencereye vermiş ve
    /// savunmasızdır. "Şimdi mi tamir edeyim, önce mi temizleyeyim" sorusu bu türün
    /// temel gerilimlerinden biri — ve barikatın oyuna kattığı asıl şey o soru.</para>
    ///
    /// <para><b>Bağımlılık yönü:</b> barikat <c>Bunker.AI</c>'da (pencerenin üstünde)
    /// yaşıyor ve Gameplay AI'ya bağımlı olamaz. Işın neye çarptığını bilmez; çarptığı
    /// şey <see cref="IRepairable"/> olduğunu kendisi söyler.</para>
    ///
    /// <para><b>Puan sunucuda yazılır</b> (ADR-0004). İstemci "tamir ediyorum" der,
    /// tahtanın gerçekten takılıp takılmadığına host karar verir.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Repair")]
    public sealed class PlayerRepair : NetworkBehaviour
    {
        [Header("Ayar")]
        [Tooltip("config/balance/barricade.json'dan uretilen varlik.")]
        [SerializeField] private BarricadeConfigAsset barricadeConfig;

        [Header("Referanslar")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerScore score;
        [SerializeField] private PlayerInteract interact;

        private BarricadeConfig _config;

        /// <summary>Şu an bakılan tamir edilebilir şey (yerel oyuncu). HUD ipucu için.</summary>
        public bool HasRepairTarget { get; private set; }

        /// <summary>Bir tahta takıldı. HUD ve ses buna bağlanır.</summary>
        public event Action BoardRepaired;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
            if (score == null) score = GetComponent<PlayerScore>();
            if (interact == null) interact = GetComponent<PlayerInteract>();

            if (barricadeConfig == null)
            {
                Debug.LogError("[Tamir] barricade.asset atanmamis. " +
                               "'Bunker/Config/Ice Aktar' ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            _config = barricadeConfig.ToRuntime();
        }

        private void Update()
        {
            if (!isLocalPlayer || _config == null) return;

            // Satin alinabilir bir seye bakarken tamir calismaz: ayni tus iki is
            // yapiyor ve cakisma TANIMLI olmali (basmak satin alir, tutmak tamir eder).
            bool blockedByPurchase = interact != null && interact.HasTarget;

            IRepairable target = blockedByPurchase ? null : FindTarget();
            HasRepairTarget = target != null;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.isPressed) return;
            if (target == null) return;

            CmdRepair(Time.deltaTime);
        }

        /// <summary>
        /// Bakılan tamir edilebilir şeyi bulur. <b>Yalnızca görsel ipucu için</b> —
        /// gerçek tamir sunucuda tekrar aranır, çünkü istemcinin neye baktığı iddiası
        /// güvenilmez veridir.
        /// </summary>
        private IRepairable FindTarget()
        {
            Transform cam = playerCamera != null ? playerCamera.transform : transform;

            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit,
                                 _config.RepairRangeMeters, ~0, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            var repairable = hit.collider.GetComponentInParent<IRepairable>();
            return repairable != null && repairable.NeedsRepair ? repairable : null;
        }

        [Command]
        private void CmdRepair(float deltaTime)
        {
            // Istemcinin gonderdigi sure DOGRULANIR: bir karelik makul tavanla
            // sinirlanir, yoksa "bir saniye tamir ettim" diyerek barikat aninda
            // doldurulabilirdi (netcode.md - istemciden gelen sureye guvenilmez).
            const float maxFrameSeconds = 0.1f;
            if (deltaTime <= 0f) return;
            if (deltaTime > maxFrameSeconds) deltaTime = maxFrameSeconds;

            Transform cam = playerCamera != null ? playerCamera.transform : transform;

            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit,
                                 _config.RepairRangeMeters, ~0, QueryTriggerInteraction.Collide))
            {
                return;
            }

            var repairable = hit.collider.GetComponentInParent<IRepairable>();
            if (repairable == null || !repairable.NeedsRepair) return;

            if (!repairable.Repair(deltaTime)) return;

            // Puan tahtanin takildigi ANDA yazilir, tusa basili tutmaya degil.
            if (score != null) score.Award(Systems.Economy.PointEvent.BarricadeBoardRepair);

            TargetNotifyRepaired(connectionToClient);
        }

        [TargetRpc]
        private void TargetNotifyRepaired(NetworkConnection target)
        {
            BoardRepaired?.Invoke();
        }
    }
}
