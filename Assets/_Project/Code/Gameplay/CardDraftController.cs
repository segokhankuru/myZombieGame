using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Config;
using Bunker.Systems.Economy;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Draft'ı açan taraf: her tur temizlendiğinde üç kart çıkarır. SYS-02 §7b.
    ///
    /// <para><b>Otorite host'tadır</b> (ADR-0004): hangi üç kartın çıktığı kalıcı
    /// sonucu olan bir karardır. Tohum sunucuda üretilir; istemci sonucu gösterir,
    /// belirlemez — <c>draft-ekrani.md</c>'nin "animasyon bilinen sonuca oynar"
    /// kuralının veri tarafı.</para>
    ///
    /// <para><b>Her tur sonu</b> (geliştirici kararı, 2026-09-04). Önceki tasarım her
    /// üç turda birdi; draft'lar arası mesafe tur 18'de 24 dakikaya çıkıyordu ve
    /// PILLAR-03'ün "ritmin zirvesi" tanımıyla bağdaşmıyordu.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Card Draft Controller")]
    public sealed class CardDraftController : MonoBehaviour
    {
        [Tooltip("config/content/cards.json'dan uretilen katalog.")]
        [SerializeField] private CardCatalogAsset catalog;

        [Tooltip("config/balance/economy.json'dan uretilen varlik. Puanli yenilemenin " +
                 "fiyati buradan gelir.")]
        [SerializeField] private EconomyConfigAsset economyConfig;

        [Tooltip("Tur basina kac secenek. SYS-02: uc karttan bir secim.")]
        [SerializeField] private int choicesPerDraft = 3;

        private CardPool _pool;
        private CardDraft _draft;
        private EconomyConfig _economy;

        // Draft'in acildigi tur. Yenileme fiyati turla artar: sabit fiyat gec
        // turlarda bedavaya doner (economy.json v4).
        private int _round = 1;

        private void Awake()
        {
            if (catalog == null || catalog.Count == 0)
            {
                // Sessiz varsayilan yok: katalog yoksa draft hic acilmaz ve oyuncu
                // "kart sistemi yok" diye okur. Sebebini soyle.
                Debug.LogError("[Kart] cards.asset atanmamis ya da bos. " +
                               "'Bunker/Config/Kartlari Ice Aktar' calistir.", this);
                enabled = false;
                return;
            }

            // Tohum run basina: ayni run icinde tekrarlanabilir, run'lar arasinda
            // farkli. Sabit tohum her run'da ayni uculeri verirdi.
            _pool = new CardPool(catalog.ToRuntime(), Random.Range(int.MinValue, int.MaxValue));
            _draft = new CardDraft(_pool, choicesPerDraft);

            if (economyConfig == null)
            {
                // Sessiz varsayilan yok (config-protocol.md). Fiyat bilinmiyorsa
                // puanli yenileme KAPALIDIR - bedava yenileme, ekranda "puanli"
                // yazip hicbir sey almamaktan iyidir ve sebebi burada duruyor.
                Debug.LogError("[Kart] economy.asset atanmamis - puanli yenileme kapali. " +
                               "'Bunker/Zombi/Test Alanini Kur' baglar.", this);
            }
            else
            {
                _economy = economyConfig.ToRuntime();
            }
        }

        private void OnEnable()
        {
            RoundSignals.RoundCleared += OnRoundCleared;
            RunSignals.RunRestarted += OnRunRestarted;
        }

        private void OnDisable()
        {
            RoundSignals.RoundCleared -= OnRoundCleared;
            RunSignals.RunRestarted -= OnRunRestarted;
        }

        /// <summary>
        /// Tur bitti — ekran <b>hemen değil, bir saniye sonra</b> açılır. 2026-09-08.
        ///
        /// <para><b>Neden</b> (geliştirici: <i>"tur biter bitmez değil, 1 sn sonra
        /// belirsin"</i>): son zombi öldüğü karede açılan ekran, oyuncunun o öldürmeyi
        /// <i>görmesine</i> izin vermiyor. Bir saniye, turun bittiğini anlamak için
        /// gereken en kısa süre — ve ekranın bir <b>ödül</b> gibi okunmasını sağlayan
        /// şey o boşluktur.</para>
        ///
        /// <para><b>Seçim kilidini değiştirmez</b> (<c>CardDraftHud.pickLockSeconds</c>):
        /// o kilit farklı bir sorunun cevabı — basılı duran farenin kartı görmeden
        /// seçmesi. Gecikme "ne zaman göründü", kilit "ne zaman tıklanabilir"
        /// sorusudur; ikisi ayrı kalmalı, yoksa birini ayarlamak diğerini bozar.</para>
        /// </summary>
        [Tooltip("Tur temizlendikten sonra kart ekranini acmadan once beklenen sure.")]
        [SerializeField] private float openDelaySeconds = 1f;

        private float _openTimer = -1f;
        private int _pendingRound;

        private void OnRoundCleared(int round)
        {
            // Run bittiyse draft acilmaz: skor ekraninin arkasinda kart secmek
            // anlamsiz.
            if (RunSignals.IsRunOver) return;

            _pendingRound = round;

            if (openDelaySeconds <= 0f)
            {
                OpenNow(round);
                return;
            }

            _openTimer = openDelaySeconds;
        }

        /// <summary>
        /// Gecikmeyi sayar. <b><c>unscaledDeltaTime</c></b>: tur biter bitmez
        /// <c>WorldClock</c> dünyayı durduran bir menü açarsa (tezgâh) ölçekli zaman
        /// akmaz ve ekran hiç gelmezdi.
        /// </summary>
        private void Update()
        {
            if (_openTimer < 0f) return;

            // Bu arada run bittiyse (yerdeki oyuncu kurtarilamadi) ekran acilmaz.
            if (RunSignals.IsRunOver)
            {
                _openTimer = -1f;
                return;
            }

            _openTimer -= Time.unscaledDeltaTime;
            if (_openTimer > 0f) return;

            _openTimer = -1f;
            OpenNow(_pendingRound);
        }

        private void OpenNow(int round)
        {
            _round = round < 1 ? 1 : round;

            // Solo host: uzak istemci sifir. Co-op geldiginde bu bayrak
            // NetworkServer.connections sayisindan gelecek.
            _draft.Open(CardSignals.Loadout, solo: true);

            if (!_draft.Slot(0).IsValid)
            {
                // Havuz tukendi. Sessizce bos bir ekran acmak yerine hic acma.
                Debug.Log("[Kart] Havuz tukendi, draft acilmadi.");
                return;
            }

            CardSignals.OpenDraft(_draft);
        }

        private void OnRunRestarted()
        {
            // Bekleyen bir acilis varsa iptal: yeni run'in ilk saniyesinde onceki
            // run'in kart ekraninin acilmasi, havuzun sifirlanmasindan once secim
            // yapmak demekti.
            _openTimer = -1f;

            CardSignals.ResetRun();
        }

        /// <summary>
        /// Bir yuvayı seçer. Arayüz burayı çağırır.
        ///
        /// <para>M-01 solo host'ta doğrudan; co-op geldiğinde bu bir
        /// <c>Command</c> olacak — seçim kalıcı sonucu olan bir karardır.</para>
        /// </summary>
        public void Pick(int slot)
        {
            if (!CardSignals.IsDraftOpen) return;

            CardDefinition card = _draft.Slot(slot);
            if (!_draft.Pick(slot, CardSignals.Loadout)) return;

            CardSignals.NotifyPicked(card);
        }

        /// <summary>
        /// Bu yuvayı yenilemenin puan bedeli. Ücretsiz hak duruyorsa <c>0</c>.
        ///
        /// <para><b>Turla artar</b> (economy.json v4): sabit bir fiyat geç turlarda
        /// bedavaya döner — tur 15'te 200 puan bir öldürmeden az eder ve yenileme bir
        /// karar olmaktan çıkar.</para>
        ///
        /// <para>Arayüz de burayı okur: fiyatı <b>düğmenin üstünde</b> göstermek,
        /// oyuncunun bastıktan sonra öğrenmesini engeller.</para>
        /// </summary>
        public int RerollCost(int slot)
        {
            if (_draft == null || !_draft.RerollCostsPoints(slot)) return 0;
            if (_economy == null) return 0;

            return _economy.PricesCardRerollBase +
                   _economy.PricesCardRerollAddPerRound * (_round - 1);
        }

        /// <summary>
        /// Bir yuvayı yeniler. Puanlı yenilemenin bedelini burada alır.
        ///
        /// <para><b>Önce ödeme, sonra yenileme</b> (tezgâhla aynı sıra): ters sırada,
        /// puan yetmediğinde kart değişmiş olurdu — bedava yenileme.</para>
        /// </summary>
        public void RerollSlot(int slot, PlayerScore score)
        {
            if (!CardSignals.IsDraftOpen) return;

            int cost = RerollCost(slot);

            if (cost > 0)
            {
                // Puan yoksa yenileme YAPILMAZ ve hak da harcanmaz.
                if (score == null) return;
                if (score.TrySpend(cost) != PurchaseResult.Success) return;
            }

            _draft.RerollSlot(slot, CardSignals.Loadout, solo: true);
        }
    }
}
