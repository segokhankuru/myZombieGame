using System;
using System.Collections.Generic;

namespace Bunker.Systems.Cards
{
    /// <summary>
    /// Oyuncunun bu run'da topladığı kartlar ve onlardan çıkan toplam etki.
    ///
    /// <para><b>Saf C#.</b> Unity'yi bilmez; silah, can ve ekonomi buradan <i>okur</i>.
    /// Kart etkisini her sistemin kendi içinde hesaplaması, aynı kuralı beş yere
    /// dağıtmak olurdu (csharp-code.md: aynı iş kuralı iki yerde duramaz).</para>
    ///
    /// <para><b>Kartlar arasında TOPLAMA, katmanlar arasında ÇARPMA</b> (SYS-02 §3.1).
    /// Beş adet +%20 kart çarpımsal olsaydı 2.49x ederdi ve 20. turda denge yok
    /// olurdu; toplamsal 2.00x eder ve tablodan ayarlanabilir.</para>
    ///
    /// <para><b>Hasar azaltma ayrı ele alınır</b> (§3.2): yüzde olarak istiflenirse
    /// beş kart ölümsüzlük yapar. <see cref="DamageTakenMultiplier"/> efektif can
    /// üzerinden hesaplar — asla sıfıra ulaşmaz, azalan getiri kendiliğinden gelir.</para>
    /// </summary>
    public sealed class CardLoadout
    {
        /// <summary>Etiket bonusunun açılması için gereken kart sayısı (SYS-02 §2).</summary>
        public const int TagBonusThreshold = 3;

        private readonly List<CardDefinition> _cards = new List<CardDefinition>(32);

        // Etiket basina sayac ve stat basina toplam. Bir kez ayrilir, temizlenir -
        // kart eklendiginde yeniden hesaplanmaz (csharp-code.md).
        private readonly int[] _tagCounts = new int[Enum.GetValues(typeof(CardTag)).Length];
        private readonly float[] _totals = new float[Enum.GetValues(typeof(CardStat)).Length];

        public IReadOnlyList<CardDefinition> Cards => _cards;

        public int Count => _cards.Count;

        /// <summary>
        /// Bir kartı yığına ekler.
        ///
        /// <para><b>Aynı kart tekrar alınabilir ve etkisi TOPLANIR</b> (geliştirici
        /// kararı, 2026-09-05) — <see cref="CardDefinition.Unique"/> olanlar hariç.
        /// Önceki kural her alınan kartı havuzdan siliyordu; 21 kartlık havuz yirmi
        /// turda tükeniyor ve draft boş açılıyordu.</para>
        /// </summary>
        /// <returns>Eklendiyse <c>true</c>. Tek seferlik bir kartın ikinci kopyası
        /// <c>false</c> döner.</returns>
        public bool Add(in CardDefinition card)
        {
            if (!card.IsValid) return false;
            if (card.Unique && Has(card.Id)) return false;

            _cards.Add(card);
            _tagCounts[(int)card.Tag]++;
            _totals[(int)card.Stat] += card.Value;

            return true;
        }

        /// <summary>
        /// Bu id yığında var mı. <b>Havuz bunu yalnızca tek seferlik kartlar için
        /// sorar</b> — tekrar edebilen bir kartın elde olması, tekrar çıkmasına engel
        /// değildir.
        /// </summary>
        public bool Has(string id)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (string.Equals(_cards[i].Id, id, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        public int TagCount(CardTag tag) => _tagCounts[(int)tag];

        /// <summary>Etiket bonusu açıldı mı. Takım kartları etiket bonusu saymaz.</summary>
        public bool HasTagBonus(CardTag tag) =>
            tag != CardTag.Team && _tagCounts[(int)tag] >= TagBonusThreshold;

        /// <summary>
        /// Bir stat'ın kartlardan gelen toplam katkısı.
        ///
        /// <para>Oransal statlar için 0.40 = +%40. Mutlak olanlar (şarjör, yedek,
        /// penetrasyon) doğrudan sayıdır.</para>
        /// </summary>
        public float Total(CardStat stat) => _totals[(int)stat];

        /// <summary>Oransal bir stat'ın çarpanı: <c>1 + toplam</c>.</summary>
        public float Multiplier(CardStat stat) => 1f + _totals[(int)stat];

        /// <summary>
        /// Alınan hasarın çarpanı. <b>Asla sıfıra ulaşmaz</b> (§3.2).
        ///
        /// <para><c>%20 daha az hasar</c> = <c>+0.25 efektif can</c>: 1/1.25 = 0.80.
        /// Dört kart hasarı yarıya indirir, sekiz kart üçte bire — tavan koymak
        /// gerekmez.</para>
        /// </summary>
        public float DamageTakenMultiplier => 1f / (1f + _totals[(int)CardStat.EffectiveHealth]);

        /// <summary>
        /// Yeni run: her şey sıfırlanır.
        ///
        /// <para>Buradaki eksik bir alan, ikinci run'ın birincinin build'iyle
        /// başlaması demektir — M1-11'in AC-6'sıyla aynı sınıf hata.</para>
        /// </summary>
        public void Reset()
        {
            _cards.Clear();
            Array.Clear(_tagCounts, 0, _tagCounts.Length);
            Array.Clear(_totals, 0, _totals.Length);
        }
    }
}
