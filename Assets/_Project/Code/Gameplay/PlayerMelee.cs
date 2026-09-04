using System;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using Bunker.Systems.Config;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Bıçak. M1-07.
    ///
    /// <para><b>Neden var:</b> mermi harcamayan, en yakın ve en riskli öldürme yolu.
    /// Ekonomi de en yüksek puanı ona verir (SYS-ekonomi). Erken turlarda mermi
    /// biriktirmenin, geç turlarda son çarenin adı.</para>
    ///
    /// <para><b>Risk zamanlamada:</b> bekleme süresi boyunca oyuncu savunmasızdır ve
    /// zombinin saldırı menzili (1.6 m) bıçağın erişiminin (2.2 m) hemen içindedir —
    /// yani bıçak kullanmak, vurulma menziline <i>bilerek</i> girmek demektir.</para>
    ///
    /// <para><b>Hasar host'ta</b> (ADR-0004), hız sınırı sunucuda ayrıca sayılır. Yerel
    /// oyuncu savuruşu aynı karede görür; onay beklemez.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Melee")]
    public sealed class PlayerMelee : NetworkBehaviour
    {
        [Header("Ayar")]
        [Tooltip("config/balance/knife.json'dan uretilen varlik.")]
        [SerializeField] private KnifeConfigAsset knifeConfig;

        [Header("Referanslar")]
        [SerializeField] private Camera playerCamera;

        private KnifeConfig _config;

        private float _localCooldown;
        private float _pendingSwingDelay;
        private bool _swingPending;

        private ActionRateLimiter _serverLimiter;

        // Kure sorgusu tamponu bir kez ayrilir: kare basina tahsis yasak
        // (csharp-code.md).
        private static readonly Collider[] SwingHits = new Collider[24];

        /// <summary>Bir bıçak öldürmesi onaylandı. Ekonomi buna bağlanır.</summary>
        public event Action<DamageKind, bool> KillConfirmed;

        /// <summary>Savuruş bekleme süresi (0 = hazır). HUD için.</summary>
        public float CooldownRemaining => _localCooldown;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);

            if (knifeConfig == null)
            {
                // Eksik ayar sessiz varsayilanla gecistirilmez (config-protocol.md).
                Debug.LogError("[Bicak] knife.asset atanmamis. 'Bunker/Config/Ice Aktar' " +
                               "ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            _config = knifeConfig.ToRuntime();

            // Ag payi silahtakiyle ayni gerekcede: komut bir kare sonra gelir
            // (BUG-002). Tolerans birikmez.
            _serverLimiter = new ActionRateLimiter(_config.SwingCooldownSeconds);
        }

        private void Update()
        {
            if (_config == null) return;

            float dt = Time.deltaTime;
            if (_localCooldown > 0f) _localCooldown -= dt;

            TickPendingSwing(dt);

            if (!isLocalPlayer) return;

            // Run bitti: girdi kesilir (M1-11, AC-3).
            // Run bitti YA DA tur arasi ekrani acik: girdi kesilir. Ekran acikken
            // ates etmek, bakis cevirmek ya da satin almak, fareyle kart secmeyi
            // imkansiz kilardi.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // V: fare tuslari silahta, bicak ayri bir tusta. Ayni tusa binmek,
            // sürünün icinde yanlislikla bicak cekmek demek olurdu.
            if (keyboard.vKey.wasPressedThisFrame) TrySwing();
        }

        private void TrySwing()
        {
            if (_localCooldown > 0f) return;
            if (_swingPending) return;

            _localCooldown = _config.SwingCooldownSeconds;

            // Hazirlik suresi bicagin AGIRLIGIDIR: hasar aninda degil, savurus
            // tamamlanınca iner. Sifir olsaydi bicak bir tusa basmaktan ibaret olurdu.
            _pendingSwingDelay = _config.SwingWindupSeconds;
            _swingPending = true;

            if (_pendingSwingDelay <= 0f) ReleaseSwing();
        }

        private void TickPendingSwing(float dt)
        {
            if (!_swingPending) return;

            _pendingSwingDelay -= dt;
            if (_pendingSwingDelay > 0f) return;

            ReleaseSwing();
        }

        private void ReleaseSwing()
        {
            _swingPending = false;

            if (!isLocalPlayer) return;

            Transform cam = playerCamera != null ? playerCamera.transform : transform;
            CmdSwing(cam.position, cam.forward);
        }

        /// <summary>
        /// Hasarın uygulandığı tek yer. <b>Her RPC bir güven sınırıdır</b> (netcode.md):
        /// savuruş hızı sunucuda ayrıca sayılır.
        /// </summary>
        [Command]
        private void CmdSwing(Vector3 origin, Vector3 forward)
        {
            if (!_serverLimiter.TryAccept(Time.time)) return;

            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();

            const float maxOriginDriftMeters = 3f;
            if ((origin - transform.position).sqrMagnitude >
                maxOriginDriftMeters * maxOriginDriftMeters)
            {
                return;
            }

            // Bicak bir isin degil bir KONI: kalabalikta savurmak ise yaramali, ama
            // arkani donup vurmak yaramamali.
            int count = Physics.OverlapSphereNonAlloc(origin, _config.SwingRangeMeters, SwingHits,
                                                      ~0, QueryTriggerInteraction.Ignore);
            if (count == 0) return;

            float cosLimit = Mathf.Cos(_config.SwingArcDegrees * 0.5f * Mathf.Deg2Rad);
            IDamageable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider c = SwingHits[i];
                if (c == null) continue;

                Vector3 toTarget = c.bounds.center - origin;
                float distance = toTarget.magnitude;
                if (distance < 0.001f) continue;

                if (Vector3.Dot(forward, toTarget / distance) < cosLimit) continue;

                var target = c.GetComponent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                // En yakini secilir: bir savurus bir hedef. Koni icindeki herkese
                // vurmak bicagi alan silahina cevirirdi ve riski silerdi.
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = target;
            }

            if (best == null) return;

            // Kafa kutusuna bicak carpani uygulanmaz: bicak zaten en yuksek puani
            // veriyor, ustune kafa carpani vermek silahi tamamen gereksiz kilardi.
            DamageResult result = best.ApplyDamage(
                new DamageInfo(_config.SwingDamage *
                               RunModifiers.Multiplier(CardStat.MeleeDamage),
                               DamageKind.Melee));

            if (result.Killed) KillConfirmed?.Invoke(DamageKind.Melee, false);
        }
    }
}
