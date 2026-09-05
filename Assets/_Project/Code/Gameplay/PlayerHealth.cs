using Bunker.Audio;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Oyuncunun canı ve run'ın sonu. M1-11. <c>DebugPlayerHealth</c>'in yerini alır.
    ///
    /// <para><b>Otorite host'tadır</b> (ADR-0004): can ve ölüm kalıcı sonucu olan
    /// şeylerdir, istemci kendi canını yazamaz. Hasar sunucuda uygulanır, can
    /// <see cref="_syncedFraction01"/> ile istemciye <b>gösterilmek üzere</b> iner.
    /// M-01 solo host modunda koşar ama seam yerinde durur — multiplayer sırası
    /// geldiğinde bu sınıf yeniden yazılmaz.</para>
    ///
    /// <para><b>Neden oran senkronize ediliyor, ham can değil:</b> HUD'un ihtiyacı olan
    /// tek şey oran. Ham canı göndermek, tavanın da gönderilmesini gerektirir (iki
    /// senkron alan, ikisi arasında bir tutarsızlık ihtimali) ve bunun karşılığında
    /// hiçbir şey kazandırmaz.</para>
    ///
    /// <para>Yenilenme kuralının kendisi <see cref="RegeneratingHealth"/>'te, saf C#
    /// olarak yaşar ve Unity açmadan test edilir (ÇK-16).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Health")]
    public sealed class PlayerHealth : NetworkBehaviour, IDamageable
    {
        [Tooltip("config/balance/player.json'dan uretilen varlik.")]
        [SerializeField] private PlayerConfigAsset playerConfig;

        private RegeneratingHealth _health;
        private float _lowFraction;

        /// <summary>İstemcide gösterilen can oranı. Sunucuda yazılır.</summary>
        [SyncVar] private float _syncedFraction01 = 1f;

        /// <summary>Ekran kenarı uyarısı yanmalı mı (AC-7). Oran eşikten ucuz.</summary>
        [SyncVar] private bool _syncedLow;

        /// <summary>0 (ölü) ile 1 (tam) arası. HUD burayı okur.</summary>
        public float Fraction01 => _syncedFraction01;

        /// <summary>Can uyarı eşiğinin altında mı (AC-7).</summary>
        public bool IsLow => _syncedLow;

        public bool IsAlive => _syncedFraction01 > 0f;

        /// <summary>Oyuncuda kafa kutusu yok — zombiler telegrafı olan tek bir vuruş yapar.</summary>
        public bool CountsAsHeadshot => false;

        private void Awake()
        {
            if (playerConfig == null)
            {
                // Sessiz varsayilan yok: eksik config boot'ta yuksek sesle patlar
                // (systems-code.md). Can varsayilanla calissaydi, denge sayisinin
                // hangisi oldugunu kimse bilemezdi.
                Debug.LogError("[Can] player.asset atanmamis. 'Bunker/Config/Ice Aktar' " +
                               "ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            PlayerConfig config = playerConfig.ToRuntime();

            _lowFraction = config.HealthLowFraction;
            _health = new RegeneratingHealth(config.HealthMaxPoints,
                                             config.HealthRegenDelaySeconds,
                                             config.HealthRegenPerSecond,
                                             config.HealthLowFraction);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            RunSignals.RunRestarted += OnRunRestarted;
            CardSignals.LoadoutChanged += OnLoadoutChanged;
        }

        public override void OnStopServer()
        {
            // OnStartServer'in kurdugunu OnStopServer bozar (csharp-code.md).
            RunSignals.RunRestarted -= OnRunRestarted;
            CardSignals.LoadoutChanged -= OnLoadoutChanged;

            base.OnStopServer();
        }

        private void Update()
        {
            // Yenilenmeyi yalnizca otorite yurutur; istemci gordugunu gosterir.
            if (!isServer || _health == null) return;
            if (RunSignals.IsRunOver) return;

            _health.Tick(Time.deltaTime);
            PublishState();
        }

        public DamageResult ApplyDamage(in DamageInfo damage)
        {
            // Run bittikten sonra gelen hasar sayilmaz: olu bir oyuncuyu vurmaya devam
            // eden zombi, skor ekrani acikken can barini oynatmamali.
            if (_health == null || RunSignals.IsRunOver) return new DamageResult(0f, false, damage.Amount);

            DamageResult result = _health.ApplyDamage(damage);
            PublishState();

            // Vurulmanin sesi 2B: kendi canindan gitmesi uzayda bir yerde olmaz.
            if (result.Absorbed > 0f) GameAudio.Play(SfxId.PlayerHurt);

            if (result.Killed)
            {
                // Olum yeri, run sonu YAYILMADAN once bildirilir: RaisePlayerDied
                // sayaclari DONDURUR ve ondan sonra gelen hicbir bildirim kabul
                // edilmez (M1-12). Sira ters olsaydi telemetri her run'da olum yerini
                // sessizce bos yazardi.
                Vector3 position = transform.position;
                RunSignals.Current.NoteDeathPosition(position.x, position.y, position.z);


                // Run sonu bir kez olur. Ayni karede ikinci bir zombi vurursa
                // RunSignals kapiyi kapatir - HealthPool'un "olum bir kez olur"
                // kuralinin run seviyesindeki karsiligi.
                RunSignals.RaisePlayerDied();
            }

            return result;
        }

        private void PublishState()
        {
            float fraction = _health.Fraction01;

            // Degismeyen bir SyncVar'a yazmak ag trafigi uretmez ama karsilastirma
            // bedavadir ve niyeti gorunur kilar (ui-code.md: degisince guncelle).
            if (!Mathf.Approximately(_syncedFraction01, fraction)) _syncedFraction01 = fraction;

            bool low = _health.IsAlive && fraction <= _lowFraction;
            if (_syncedLow != low) _syncedLow = low;
        }

        private void OnRunRestarted()
        {
            if (_health == null) return;

            _health.ResetFull();
            PublishState();
        }

        /// <summary>
        /// Kart yigini degisti: maks can, hasar azaltma ve yenilenme gecikmesi tazelenir.
        ///
        /// <para><b>Yalnizca sunucuda</b> (ADR-0004): can otorite tarafinda yasar,
        /// istemci gordugunu gosterir.</para>
        /// </summary>
        private void OnLoadoutChanged(CardLoadout loadout)
        {
            if (_health == null) return;

            _health.ApplyModifiers(RunModifiers.Total(CardStat.MaxHealth),
                                   RunModifiers.DamageTakenMultiplier,
                                   RunModifiers.Total(CardStat.RegenDelay));

            PublishState();
        }
    }
}
