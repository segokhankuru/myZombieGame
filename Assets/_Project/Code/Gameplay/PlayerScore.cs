using Bunker.Config;
using Bunker.Systems.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Economy;
using Bunker.Systems.Rounds;
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
        [SerializeField] private PlayerMelee melee;

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
            if (melee == null) melee = GetComponent<PlayerMelee>();

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
            if (melee != null) melee.KillConfirmed += OnKillConfirmed;

            RunSignals.RunRestarted += OnRunRestarted;
            CardSignals.LoadoutChanged += OnLoadoutChanged;
            RoundSignals.BossKilled += OnBossKilled;
            RoundSignals.FieldKills += OnFieldKills;
        }

        public override void OnStopServer()
        {
            // OnStartServer'in kurdugunu OnStopServer bozar (csharp-code.md).
            if (weapon != null) weapon.KillConfirmed -= OnKillConfirmed;
            if (melee != null) melee.KillConfirmed -= OnKillConfirmed;

            RunSignals.RunRestarted -= OnRunRestarted;
            CardSignals.LoadoutChanged -= OnLoadoutChanged;
            RoundSignals.BossKilled -= OnBossKilled;
            RoundSignals.FieldKills -= OnFieldKills;

            base.OnStopServer();
        }

        private PlayerHealth _health;

        private void OnKillConfirmed(DamageKind kind, bool headshot)
        {
            PointEvent pointEvent = kind switch
            {
                DamageKind.Melee => PointEvent.MeleeKill,
                _ => headshot ? PointEvent.HeadshotKill : PointEvent.BodyKill
            };

            // Skor ekraninin sayaci (M1-11). Puani Award yaziyor; buradaki bildirim
            // OLDURMENIN kendisi - kac zombi, kaci kafadan, kaci bicakla.
            RunSignals.Current.NoteKill(kind, headshot);

            Award(pointEvent);

            // KAN ve GANIMET kartlarinin oldurme odulleri (M-03, 2026-09-05).
            // Burada, cunku "oldurme" olayinin tek sahibi bu metot: silaha ve bicaga
            // ayri ayri eklemek, ikisinden birinin unutulmasi demekti.
            ApplyKillRewards();
        }

        /// <summary>
        /// Öldürmenin kart ödülleri: <b>can</b> ve <b>mermi</b>.
        ///
        /// <para><b>Neden bu iki ödül:</b> ikisi de sürünün içinde kalmayı bir <i>seçim</i>
        /// hâline getirir. Kaçmak yerine öldürmeye devam etmek, öldürdükçe hayatta
        /// kalmak — kartların vaat ettiği "kan" hissi budur (SYS-02 Kan etiketi).</para>
        ///
        /// <para><b>Yalnızca sunucuda</b> (ADR-0004): can ve mermi kalıcı sonucu olan
        /// kaynaklar.</para>
        /// </summary>
        private void ApplyKillRewards()
        {
            if (!isServer) return;

            float heal = RunModifiers.Total(CardStat.HealOnKill);

            // Referans bir kez cozulur: oldurme basina GetComponent, yogun bir turda
            // saniyede onlarca arama demek (csharp-code.md).
            if (heal > 0f)
            {
                if (_health == null) _health = GetComponent<PlayerHealth>();
                _health?.ServerHealFraction(heal);
            }

            int ammo = Mathf.RoundToInt(RunModifiers.Total(CardStat.AmmoOnKill));

            // OLDURME ODULU BEDAVA MERMIDIR (2026-09-10, gelistirici: "yedek mermi
            // kapasitesinin ustune sadece mermi satin alarak cikilir"). Tavana kadar
            // doldurur; tavanin ustune tasiyan tek sey satin alma.
            if (ammo > 0 && weapon != null) weapon.ServerAddFreeReserve(ammo);
        }

        /// <summary>
        /// Boss olduruldu: normal oldurme puani ZATEN yazildi, buraya FARK gelir.
        ///
        /// <para>Bossu oldurmek bir SECIM olmali - kacmak da mesru. Odul, o secimi
        /// cazip kilan sey: alti kat puan, tezgahta bir kademe demektir.</para>
        /// </summary>
        private void OnBossKilled(float pointsMultiplier)
        {
            if (!isServer) return;

            int extra = Mathf.RoundToInt(pointsMultiplier) - 1;
            if (extra <= 0) return;

            Award(PointEvent.BodyKill, extra);
        }

        /// <summary>
        /// Silahın vurmadığı öldürmeler: nuke eşyası, Yıkım kartının patlaması.
        /// 2026-09-08.
        ///
        /// <para><b>Gövde puanı verilir, kafa puanı değil:</b> kafa vuruşunun ödülü
        /// nişan almanın ödülüdür; bir alan silme eşyası onu hak etmez. Öldürme sayacı
        /// da (<see cref="RunRecorder"/>) burada işlenir — nuke'un sildiği otuz zombi
        /// skor ekranında görünmeliydi ve görünmüyordu.</para>
        ///
        /// <para><b>Kart ödülleri (can, mermi) BURADA UYGULANMAZ:</b> "öldürdükçe
        /// iyileş" kartının otuz zombilik bir nuke ile tam can vermesi, kartı bir
        /// eşyanın eklentisine çevirirdi. Kan etiketi <i>senin</i> öldürmelerini
        /// ödüllendirir.</para>
        /// </summary>
        private void OnFieldKills(int count)
        {
            if (!isServer || count <= 0) return;

            for (int i = 0; i < count; i++)
            {
                RunSignals.Current.NoteKill(DamageKind.Environment, false);
            }

            Award(PointEvent.BodyKill, count);
        }

        /// <summary>Kart yiginin puan carpanlarini cuzdana gecirir (M-03).</summary>
        private void OnLoadoutChanged(CardLoadout loadout)
        {
            _wallet?.ApplyModifiers(RunModifiers.Total(CardStat.KillPoints),
                                    RunModifiers.Total(CardStat.RepairPoints));
        }

        /// <summary>Yeni run: cüzdan sıfırlanır (AC-5).</summary>
        private void OnRunRestarted()
        {
            _wallet = new PlayerWallet(economyConfig.ToRuntime());
            _spendable = 0;
            _earned = 0;
        }

        /// <summary>Puan yazar. <b>Yalnızca sunucuda çağrılmalı.</b></summary>
        [Server]
        public void Award(PointEvent pointEvent, int times = 1)
        {
            _wallet.Award(pointEvent, times);
            _spendable = _wallet.SpendablePoints;
            _earned = _wallet.TotalEarned;

            // Kazanilan TOPLAM bildirilir, artis degil: ikinci bir toplama yapmak
            // iki sayinin er gec ayrismasi demektir (config-data.md, hesaplanmis deger).
            RunSignals.Current.NoteScore(_earned);
        }

        /// <summary>
        /// Tur başında dirilmenin fiyatı (2026-09-09).
        ///
        /// <para><b>Tura göre artıyor</b>, kart yenilemesiyle aynı gerekçe: sabit bir
        /// fiyat geç turlarda bedavaya döner ve ölüm bir sonuç olmaktan çıkar.</para>
        /// </summary>
        public int ReviveCostForRound(int round)
        {
            if (economyConfig == null) return 0;

            EconomyConfig economy = economyConfig.ToRuntime();
            int clamped = round < 1 ? 1 : round;

            return economy.PricesReviveBase +
                   economy.PricesReviveAddPerRound * (clamped - 1);
        }

        /// <summary>
        /// Puanı <b>koşulsuz</b> düşürür — diriliş bedeli için.
        ///
        /// <para><b>Neden <see cref="TrySpend"/> değil:</b> diriliş bir <i>satın alma</i>
        /// değil bir <i>tahsilat</i>. Puan yetmediğinde işlem başarısız olmuyor, kısmi
        /// ödeniyor ve karşılığında daha az can veriliyor (PlayerDownState). TrySpend'in
        /// "yetmiyorsa hiçbir şey olmaz" sözleşmesi burada yanlış olurdu.</para>
        /// </summary>
        [Server]
        public void ServerSpend(int amount)
        {
            if (amount <= 0) return;

            _wallet.TryPurchase(Mathf.Min(amount, _wallet.SpendablePoints));
            _spendable = _wallet.SpendablePoints;
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
