using Bunker.Audio;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Bunker.Systems.Pickups;
using Bunker.Systems.Rounds;
using Bunker.Systems.Telemetry;
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

        /// <summary>
        /// Ham can ve tavanı. <b>Durum paneli için</b> (2026-09-06, geliştirici:
        /// <i>"mevcut canımın hasarımın net bilgisini bilmeliyim"</i>).
        ///
        /// <para>Oran zaten senkron ama tek başına yetmiyor: kart alan oyuncunun tavanı
        /// değişiyor ve "%60 can" cümlesi her turda başka bir sayı demek. Kaç vuruş
        /// dayanabileceğini bilmek için ham sayı gerekiyor.</para>
        /// </summary>
        [SyncVar] private float _syncedCurrent = 100f;

        [SyncVar] private float _syncedMax = 100f;

        /// <summary>0 (ölü) ile 1 (tam) arası. HUD burayı okur.</summary>
        public float Fraction01 => _syncedFraction01;

        /// <summary>Can uyarı eşiğinin altında mı (AC-7).</summary>
        public bool IsLow => _syncedLow;

        public bool IsAlive => _syncedFraction01 > 0f;

        /// <summary>Su anki can, ham sayi. Durum paneli okur.</summary>
        public float CurrentPoints => _syncedCurrent;

        /// <summary>Maksimum can (kart ve tezgah dahil), ham sayi.</summary>
        public float MaxPoints => _syncedMax;

        /// <summary>Oyuncuda kafa kutusu yok — zombiler telegrafı olan tek bir vuruş yapar.</summary>
        public bool CountsAsHeadshot => false;

        /// <summary>Oyuncunun kendisi; alt vurus kutusu yok.</summary>
        public IDamageable DamageRoot => this;

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
            PowerupSignals.Picked += OnPowerupPicked;
        }

        /// <summary>
        /// Kaldırılan ya da yeni turda dirilen oyuncunun canı. 2026-09-07.
        ///
        /// <para><b>Tam canla dönmez</b> (<c>fraction01</c> ile çağrılır): tam canla
        /// kalkmak yere düşmeyi bedelsiz yapardı. Yarı canla kalkmak, kalkar kalkmaz
        /// geri çekilmeyi bir <i>karar</i> hâline getirir.</para>
        ///
        /// <para><b>Yalnızca sunucu.</b> Can otoritenin bilgisi (ADR-0004).</para>
        /// </summary>
        [Server]
        public void ServerReviveTo(float fraction01)
        {
            if (_health == null) return;

            _health.ResetFull();

            float target = Mathf.Clamp01(fraction01);
            if (target < 1f)
            {
                _health.ApplyDamage(new DamageInfo(_health.Max * (1f - target),
                                                   DamageKind.Environment));
            }

            PublishState();
        }

        public override void OnStopServer()
        {
            // OnStartServer'in kurdugunu OnStopServer bozar (csharp-code.md).
            RunSignals.RunRestarted -= OnRunRestarted;
            CardSignals.LoadoutChanged -= OnLoadoutChanged;
            PowerupSignals.Picked -= OnPowerupPicked;

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

            // SAVAS GUNLUGU - oyuncuya gelen her hasarin TEK cikis noktasi
            // (2026-09-08, gelistirici: "olum aninda halen tek yiyorum, bunu en
            // saglikli boyle bakarak anlayacagim"). Kalan can vurustan SONRAKI deger;
            // yani satirlar yan yana okununca "dort vurus, 0.1 saniye" gorunur.
            if (result.Absorbed > 0f)
            {
                CombatLog.PlayerDamage(damage.Source ?? damage.Kind.ToString(), null,
                                       result.Absorbed, _health.Current, _health.Max,
                                       result.Killed);
            }

            // Geri bildirim VURULAN OYUNCUYA gider, sunucuda kalmaz: co-op'ta hasarı
            // uygulayan makine ile onu hisseden oyuncu farklı olabilir.
            if (result.Absorbed > 0f && connectionToClient != null)
            {
                TargetReportDamageTaken(connectionToClient, result.Absorbed,
                                        new Vector3(damage.SourceX, transform.position.y,
                                                    damage.SourceZ));
            }

            if (result.Killed)
            {
                // NE OLDURDU (2026-09-05): "birden oldum" cumlesini tahmin olmaktan
                // cikaran tek sey. Skor ekrani bunu yazar.
                CombatFeedback.NoteLethalHit(damage.Amount, damage.Kind);

                // Olum dokumu: son 16 vurus, aralarindaki sureyle. "Tek mi yedim"
                // sorusunun cevabi bu blok (2026-09-08).
                CombatLog.PlayerDied(damage.Source ?? damage.Kind.ToString());

                // Olum yeri, run sonu YAYILMADAN once bildirilir: RaisePlayerDied
                // sayaclari DONDURUR ve ondan sonra gelen hicbir bildirim kabul
                // edilmez (M1-12). Sira ters olsaydi telemetri her run'da olum yerini
                // sessizce bos yazardi.
                Vector3 position = transform.position;
                RunSignals.Current.NoteDeathPosition(position.x, position.y, position.z);

                // OLUM ARTIK ONCE "YERE DUSME" (2026-09-07, co-op). Run yalnizca
                // AYAKTA KIMSE KALMAYINCA biter - solo'da bu ikisi ayni an, yani eski
                // davranis aynen korunuyor ve ayri bir "solo mu" kuralina gerek yok.
                // Iki ayri kural, iki ayri hata demek olurdu.
                var down = GetComponent<PlayerDownState>();
                if (down != null) down.ServerGoDown();

                if (down == null || PlayerDownState.EveryoneOut())
                {
                    // Run sonu bir kez olur. Ayni karede ikinci bir zombi vurursa
                    // RunSignals kapiyi kapatir - HealthPool'un "olum bir kez olur"
                    // kuralinin run seviyesindeki karsiligi.
                    RunSignals.RaisePlayerDied();
                }
            }

            return result;
        }

        /// <summary>
        /// Hasarın <b>hissedilen</b> tarafı: ses, sayı, yön ve açık menünün kapanması.
        ///
        /// <para><b>Neden hepsi burada:</b> dördü de vurulan oyuncuya ait. Sunucuda
        /// çalıştırılsalardı co-op'ta ses host'ta çalar, sayı host'un ekranında belirir
        /// ve vurulan oyuncu hiçbir şey görmezdi.</para>
        /// </summary>
        [TargetRpc]
        private void TargetReportDamageTaken(NetworkConnection target, float amount,
                                             Vector3 sourcePosition)
        {
            // Vurulmanin sesi 2B: kendi canindan gitmesi uzayda bir yerde olmaz.
            GameAudio.Play(SfxId.PlayerHurt);

            // Kac hasar yedin ve NEREDEN (2026-09-05). Bu iki bilgi olmadan
            // "birden oldum" cumlesi kurulur ve olum haksizlik gibi okunur.
            CombatFeedback.RaiseDamageTaken(amount, sourcePosition);

            // HASAR ALDIYSAN MENU KAPANIR (2026-09-05, oyun testi). Menu acikken
            // girdin kesiliyor ama dunya donmeye devam ediyor; menunun arkasinda
            // olmek "hic hasar yemeden game over" diye okunur. Tezgahin mola kurali
            // bunun ONLEMI, bu satir EMNIYET KEMERI.
            CardSignals.SetShopOpen(false);
        }

        /// <summary>
        /// Kart odulu: maksimum canin bir oranı kadar iyilesme (KAN etiketi).
        ///
        /// <para><b>Olu oyuncu iyilesmez</b> - olumden donus bir tasarim karari ve
        /// M-01'de yok. Buradan sessizce gelmesi, run sonunun hic gorunmemesine yol
        /// acardi.</para>
        /// </summary>
        [Server]
        public void ServerHealFraction(float fraction01)
        {
            if (_health == null || !_health.IsAlive) return;
            if (fraction01 <= 0f) return;

            _health.Heal(_health.Max * fraction01);
            PublishState();
        }

        /// <summary>
        /// Yerden can eşyası toplandı (2026-09-07).
        ///
        /// <para><b>Miktarı eşya taşır</b>, bu sınıf bilmez: oran
        /// <c>zombie.json → drops.healthFraction01</c>'de ve tek bir yerde durur
        /// (config-data.md). Burada bir sayı olsaydı, dengeyi ayarlayan kişi ikisinden
        /// hangisinin geçerli olduğunu bilemezdi.</para>
        /// </summary>
        private void OnPowerupPicked(Systems.Pickups.PowerupKind kind, float amount, float seconds)
        {
            if (kind != Systems.Pickups.PowerupKind.Health) return;
            if (!isServer) return;

            ServerHealFraction(amount);
        }

        private void PublishState()
        {
            float fraction = _health.Fraction01;

            // Degismeyen bir SyncVar'a yazmak ag trafigi uretmez ama karsilastirma
            // bedavadir ve niyeti gorunur kilar (ui-code.md: degisince guncelle).
            if (!Mathf.Approximately(_syncedFraction01, fraction)) _syncedFraction01 = fraction;

            bool low = _health.IsAlive && fraction <= _lowFraction;
            if (_syncedLow != low) _syncedLow = low;

            if (!Mathf.Approximately(_syncedCurrent, _health.CurrentPoints))
                _syncedCurrent = _health.CurrentPoints;

            if (!Mathf.Approximately(_syncedMax, _health.MaxPoints))
                _syncedMax = _health.MaxPoints;
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
