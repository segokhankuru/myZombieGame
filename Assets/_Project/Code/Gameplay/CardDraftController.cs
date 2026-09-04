using Bunker.Config;
using Bunker.Systems.Cards;
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

        [Tooltip("Tur basina kac secenek. SYS-02: uc karttan bir secim.")]
        [SerializeField] private int choicesPerDraft = 3;

        private CardPool _pool;
        private CardDraft _draft;

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

        private void OnRoundCleared(int round)
        {
            // Run bittiyse draft acilmaz: skor ekraninin arkasinda kart secmek
            // anlamsiz.
            if (RunSignals.IsRunOver) return;

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

        /// <summary>Bir yuvayı yeniler. Puanlı yenilemenin bedelini burada alır.</summary>
        public void RerollSlot(int slot, PlayerScore score)
        {
            if (!CardSignals.IsDraftOpen) return;

            bool costs = _draft.RerollCostsPoints(slot);

            if (costs)
            {
                // TODO(systems-designer, SYS-02 §7c): bedel TURLA ARTMALI ve
                // config/balance/cards.json'dan gelmeli. Sabit fiyat gec turlarda
                // bedavaya doner. Simdilik yenileme puansiz - fiyat egrisi
                // verilmeden uydurma bir sayi yazmak, denge kararini koda gommek
                // olurdu (config-data.md).
                _ = score;
            }

            _draft.RerollSlot(slot, CardSignals.Loadout, solo: true);
        }
    }
}
