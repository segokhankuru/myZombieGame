using Bunker.Config;
using Bunker.Systems.Combat;
using Bunker.Systems.Economy;
using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Oyuncunun cüzdanı: öldürme puanı burada yazılır, kapı ve silah burada harcanır.
    /// M1-02'nin mantığını M1-06'nın silahına bağlar.
    ///
    /// <para><b>Puan iki ayrı sayıdır</b> (SYS-01): harcanabilir bakiye ve kazanılan
    /// toplam. Tek sayı olsaydı kapı açan oyuncu skor kaybederdi ve hiçbir şey almayan
    /// tabloda birinci olurdu — PILLAR-02 ihlali.</para>
    ///
    /// <para><b>Otorite host'tadır</b> (ADR-0004): puan kalıcı sonucu olan bir şeydir,
    /// istemci kendi puanını yazamaz. Yazan taraf sunucu, gösteren taraf istemci.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Score")]
    public sealed class PlayerScore : NetworkBehaviour
    {
        [Tooltip("config/balance/economy.json'dan uretilen varlik.")]
        [SerializeField] private EconomyConfigAsset economyConfig;

        [SerializeField] private PlayerWeapon weapon;

        private PlayerWallet _wallet;

        /// <summary>Harcanabilir bakiye. İstemcide gösterilir, sunucuda yazılır.</summary>
        [SyncVar] private int _spendable;

        /// <summary>Run boyunca kazanılan toplam — harcamak bunu düşürmez.</summary>
        [SyncVar] private int _earned;

        public int Spendable => _spendable;
        public int Earned => _earned;

        private void Awake()
        {
            if (weapon == null) weapon = GetComponent<PlayerWeapon>();

            if (economyConfig == null)
            {
                Debug.LogError("[Puan] economy.asset atanmamis. 'Bunker/Config/Ice Aktar' " +
                               "ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            _wallet = new PlayerWallet(economyConfig.ToRuntime());
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (weapon != null) weapon.KillConfirmed += OnKillConfirmed;
        }

        public override void OnStopServer()
        {
            // OnStartServer'in kurdugunu OnStopServer bozar (csharp-code.md).
            if (weapon != null) weapon.KillConfirmed -= OnKillConfirmed;

            base.OnStopServer();
        }

        private void OnKillConfirmed(DamageKind kind, bool headshot)
        {
            PointEvent pointEvent = kind switch
            {
                DamageKind.Melee => PointEvent.MeleeKill,
                _ => headshot ? PointEvent.HeadshotKill : PointEvent.BodyKill
            };

            Award(pointEvent);
        }

        /// <summary>Puan yazar. <b>Yalnızca sunucuda çağrılmalı.</b></summary>
        [Server]
        public void Award(PointEvent pointEvent, int times = 1)
        {
            _wallet.Award(pointEvent, times);
            _spendable = _wallet.SpendablePoints;
            _earned = _wallet.TotalEarned;
        }

        /// <summary>Harcama denemesi. Kapı ve duvar silahı buradan geçer (M1-09, M1-10).</summary>
        [Server]
        public PurchaseResult TrySpend(int cost)
        {
            PurchaseResult result = _wallet.TryPurchase(cost);

            if (result == PurchaseResult.Success)
            {
                _spendable = _wallet.SpendablePoints;
            }

            return result;
        }
    }
}
