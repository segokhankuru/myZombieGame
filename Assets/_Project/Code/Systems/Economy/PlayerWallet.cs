using System;

namespace Bunker.Systems.Economy
{
    /// <summary>
    /// Bir oyuncunun puan durumu. M1-02.
    ///
    /// <para><b>İki ayrı sayı tutulur ve bu bilinçlidir.</b></para>
    ///
    /// <para>Klasik tur bazlı zombi modunda "puan hem para hem skordur" denir. Tek bir
    /// sayı olarak uygulanırsa şu ortaya çıkar: <b>kapı açan oyuncu skor kaybeder.</b>
    /// Hiçbir şey satın almayan, köşede bekleyip kill toplayan oyuncu tabloda birinci
    /// olur. Bu, SYS-01'in ödül felsefesini ve PILLAR-02'yi doğrudan çiğner — takıma
    /// kapı açmak cezalandırılmış olur.</para>
    ///
    /// <para>Çözüm: <see cref="SpendablePoints"/> harcandıkça azalır,
    /// <see cref="TotalEarned"/> asla azalmaz. Oyuncuya gösterilen "puan" harcanabilir
    /// bakiyedir; sicil ekranındaki skor kazanılan toplamdır.</para>
    ///
    /// <para><b>Saf C#.</b> Unity yok, ağ yok. Host bu nesneyi tutar; istemci yalnızca
    /// sonucu görür (ADR-0004: puan hesabı istemcide yapılmaz).</para>
    /// </summary>
    public sealed class PlayerWallet
    {
        private readonly EconomyConfig _config;

        public PlayerWallet(EconomyConfig config, int startingPoints = 0)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (startingPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingPoints),
                    "Baslangic puani negatif olamaz.");
            }

            SpendablePoints = startingPoints;
            TotalEarned = startingPoints;
        }

        /// <summary>Harcanabilir bakiye. Satın alma ve düşme cezası bunu azaltır.</summary>
        public int SpendablePoints { get; private set; }

        /// <summary>
        /// Run boyunca kazanılan toplam. **Asla azalmaz** — ne harcamayla ne cezayla.
        /// Sicil ekranındaki skor budur.
        /// </summary>
        public int TotalEarned { get; private set; }

        /// <summary>Bir olay için puan verir.</summary>
        public int Award(PointEvent pointEvent)
        {
            return Award(pointEvent, 1);
        }

        /// <summary>
        /// Bir olay için <paramref name="times"/> kez puan verir. Kazanılan puanı döner.
        /// Sıfır ya da negatif tekrar hiçbir şey yapmaz.
        /// </summary>
        public int Award(PointEvent pointEvent, int times)
        {
            if (times <= 0) return 0;

            int amount = _config.AwardFor(pointEvent) * times;
            if (amount <= 0) return 0;

            SpendablePoints = AddClamped(SpendablePoints, amount);
            TotalEarned = AddClamped(TotalEarned, amount);
            return amount;
        }

        /// <summary>
        /// Satın alma dener. Başarısızsa <b>hiçbir durum değişmez</b> — kısmi harcama
        /// diye bir şey yoktur.
        /// </summary>
        public PurchaseResult TryPurchase(int cost)
        {
            if (cost <= 0) return PurchaseResult.InvalidCost;
            if (SpendablePoints < cost) return PurchaseResult.InsufficientPoints;

            SpendablePoints -= cost;
            return PurchaseResult.Success;
        }

        /// <summary>
        /// Düşme cezasını uygular: harcanabilir bakiyenin yapılandırılmış oranı gider.
        /// <see cref="TotalEarned"/> etkilenmez — oyuncu düştü diye skorunu kaybetmez.
        /// Kaybedilen puanı döner.
        /// </summary>
        public int ApplyDownedPenalty()
        {
            if (SpendablePoints <= 0) return 0;

            float fraction = _config.DownedSpendableFraction;
            if (fraction <= 0f) return 0;
            if (fraction > 1f) fraction = 1f;

            // Asagi yuvarlama: ceza oyuncunun lehine, cunku yuvarlama hatasi
            // yuzunden bir kapinin acilamamasi kotu bir surprizdir.
            int lost = (int)Math.Floor(SpendablePoints * fraction);
            SpendablePoints -= lost;
            return lost;
        }

        /// <summary>Yeni run için sıfırlar.</summary>
        public void ResetForNewRun(int startingPoints = 0)
        {
            if (startingPoints < 0) startingPoints = 0;

            SpendablePoints = startingPoints;
            TotalEarned = startingPoints;
        }

        /// <summary>
        /// Uzun bir run'da toplamın taşmasını engeller. Taşma teorik olarak uzak ama
        /// sessizce negatife dönen bir skor, bulunması en zor hata türüdür.
        /// </summary>
        private static int AddClamped(int current, int amount)
        {
            long sum = (long)current + amount;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
