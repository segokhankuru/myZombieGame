using System;
using System.Collections.Generic;

namespace Bunker.Systems.Cards
{
    /// <summary>Bir yuvanın yenileme durumu.</summary>
    public enum RerollState
    {
        /// <summary>Ücretsiz yenileme hakkı duruyor.</summary>
        FreeAvailable,
        /// <summary>Ücretsiz kullanıldı; puanla bir kez daha yenilenebilir.</summary>
        PaidAvailable,
        /// <summary>İki hak da bitti.</summary>
        Exhausted
    }

    /// <summary>
    /// Bir draft turu: üç seçenek, yuva başına yenileme, tek seçim.
    ///
    /// <para><b>Saf C# ve deterministik.</b> Tohum dışarıdan verilir; aynı tohum aynı
    /// üçlüyü üretir. Bu, hem testi mümkün kılar hem de ileride ağ için şart:
    /// <b>sonucu host belirler</b> (draft-ekrani.md), animasyon bilinen sonuca oynar.
    /// Animasyonun sonucu belirlemesi hem hile kapısıdır hem dört istemcide farklı
    /// sonuç üretir.</para>
    ///
    /// <para><b>Yenileme yuva başına</b> (geliştirici kararı, 2026-09-04): 1 ücretsiz +
    /// 1 puanlı, üçüncü yok, ve <b>her draft'ta sıfırlanır</b>. Hepsini birden
    /// yenilemek beğendiğin kartları da atmaya zorlar — yani yenileme bir çözüm değil
    /// kumar olurdu.</para>
    /// </summary>
    public sealed class CardDraft
    {
        private readonly CardPool _pool;
        private readonly CardDefinition[] _slots;
        private readonly RerollState[] _rerolls;

        public CardDraft(CardPool pool, int choiceCount = 3)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));

            if (choiceCount < 1)
                throw new ArgumentOutOfRangeException(nameof(choiceCount), "En az bir secenek olmali.");

            _slots = new CardDefinition[choiceCount];
            _rerolls = new RerollState[choiceCount];
        }

        public int SlotCount => _slots.Length;

        public bool IsOpen { get; private set; }

        public CardDefinition Slot(int index) => _slots[index];

        public RerollState Reroll(int index) => _rerolls[index];

        /// <summary>
        /// Yeni bir draft açar ve yuvaları doldurur.
        ///
        /// <para>Yenileme hakları burada <b>sıfırlanır</b> — her tur yeniden üç ücretsiz
        /// hak. Biriktirilmez, devredilmez.</para>
        /// </summary>
        public void Open(CardLoadout loadout, bool solo)
        {
            IsOpen = true;

            for (int i = 0; i < _slots.Length; i++)
            {
                _rerolls[i] = RerollState.FreeAvailable;
                _slots[i] = default;
            }

            // Yuvalar SIRAYLA doldurulur ve her biri oncekileri disarida birakir:
            // ayni kartin iki yuvada birden cikmasi, uc secenegi ikiye dusurur.
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = _pool.Draw(loadout, solo, _slots, i);
            }
        }

        /// <summary>
        /// Bir yuvayı yeniler.
        ///
        /// <para><b>Bedeli çağıran taraf öder.</b> Bu sınıf puana dokunmaz — puanı
        /// düşürmek sunucunun işi (ADR-0004) ve burada yapılsaydı cüzdanla iki ayrı
        /// yerden konuşulurdu.</para>
        /// </summary>
        /// <returns>Yenilendiyse <c>true</c>; hakkı bittiyse <c>false</c>.</returns>
        public bool RerollSlot(int index, CardLoadout loadout, bool solo)
        {
            if (!IsOpen) return false;
            if (_rerolls[index] == RerollState.Exhausted) return false;

            _rerolls[index] = _rerolls[index] == RerollState.FreeAvailable
                ? RerollState.PaidAvailable
                : RerollState.Exhausted;

            _slots[index] = _pool.Draw(loadout, solo, _slots, index);
            return true;
        }

        /// <summary>Bu yenileme puana mal olur mu.</summary>
        public bool RerollCostsPoints(int index) => _rerolls[index] == RerollState.PaidAvailable;

        /// <summary>
        /// Bir yuvayı seçer ve draft'ı kapatır.
        /// </summary>
        /// <returns>Seçim yığına eklendiyse <c>true</c>.</returns>
        public bool Pick(int index, CardLoadout loadout)
        {
            if (!IsOpen || loadout == null) return false;

            CardDefinition card = _slots[index];
            if (!card.IsValid) return false;

            IsOpen = false;
            return loadout.Add(card);
        }

        /// <summary>Seçim yapılmadan kapatır (AFK kaçış kapısı: ilk yuva otomatik seçilir).</summary>
        public bool PickFirstAvailable(CardLoadout loadout)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsValid) return Pick(i, loadout);
            }

            IsOpen = false;
            return false;
        }
    }

    /// <summary>
    /// Kart havuzu: kim çıkabilir, hangi ağırlıkla.
    ///
    /// <para><b>Etiket ağırlıklandırması</b> (SYS-02 §2): elinde bir etiketten 2 kart
    /// varsa o etiket daha sık çıkar. Bu bir kolaylık değil, GOAL-02'nin aracı —
    /// oyuncuları birbirinden <b>uzaklaştırır</b> ve yığınların örtüşmesini düşürür.
    /// Her tur draft'la birlikte örtüşme zaten sınırda (%36), bu ağırlık onu aşağı
    /// çeken tek mekanizma.</para>
    /// </summary>
    public sealed class CardPool
    {
        private readonly List<CardDefinition> _all;
        private readonly List<CardDefinition> _candidates = new List<CardDefinition>(64);
        private readonly Random _random;

        /// <summary>Eldeki etiketten kaç kart varsa ağırlık bu kadar artar.</summary>
        private const float TagWeightPerCard = 0.5f;

        public CardPool(IReadOnlyList<CardDefinition> cards, int seed)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));

            _all = new List<CardDefinition>(cards);
            _random = new Random(seed);
        }

        public int Count => _all.Count;

        /// <summary>
        /// Bir kart çeker.
        ///
        /// <para>Elenenler: zaten alınmış kartlar, bağlama uymayanlar (solo/co-op) ve
        /// bu draft'ta başka bir yuvada duranlar.</para>
        ///
        /// <para><b>Havuz tükenirse geçersiz bir kart döner</b> — çağıran taraf yuvayı
        /// boş gösterir. Sessizce tekrar eden bir kart vermek, oyuncuya üç seçenek
        /// varmış gibi gösterip ikisini aynı yapardı.</para>
        /// </summary>
        public CardDefinition Draw(CardLoadout loadout, bool solo,
                                   CardDefinition[] taken, int excludeUpTo)
        {
            _candidates.Clear();
            float totalWeight = 0f;

            for (int i = 0; i < _all.Count; i++)
            {
                CardDefinition c = _all[i];

                if (!c.AllowedIn(solo)) continue;
                if (loadout != null && loadout.Has(c.Id)) continue;
                if (AlreadyInDraft(c.Id, taken, excludeUpTo)) continue;

                _candidates.Add(c);
                totalWeight += Weight(c, loadout);
            }

            if (_candidates.Count == 0) return default;

            double roll = _random.NextDouble() * totalWeight;

            for (int i = 0; i < _candidates.Count; i++)
            {
                roll -= Weight(_candidates[i], loadout);
                if (roll <= 0d) return _candidates[i];
            }

            return _candidates[_candidates.Count - 1];
        }

        private static bool AlreadyInDraft(string id, CardDefinition[] taken, int count)
        {
            if (taken == null) return false;

            for (int i = 0; i < taken.Length; i++)
            {
                if (i == count) continue;
                if (string.Equals(taken[i].Id, id, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static float Weight(in CardDefinition card, CardLoadout loadout)
        {
            if (loadout == null || card.Tag == CardTag.Team) return 1f;

            return 1f + loadout.TagCount(card.Tag) * TagWeightPerCard;
        }
    }
}
