namespace Bunker.Systems.Diagnostics
{
    /// <summary>
    /// Teşhis sayaçları. Ölçüm araçlarının okuduğu, üreten sistemlerin yazdığı ortak yer.
    ///
    /// <para><b>Neden burada:</b> `Bunker.AI` agent sayısını üretiyor, `Bunker.UI` onu
    /// ekranda gösteriyor. Ama mimari kural nettir — <i>hiçbir şey UI'ye bağımlı olamaz</i>
    /// (ARCHITECTURE.md §2). AI'nın HUD'a referans vermesi o kuralı çiğnerdi. İkisinin de
    /// gördüğü tek yer `Bunker.Systems`, yani zemin katı.</para>
    ///
    /// <para><b>Statik olmasının gerekçesi:</b> systems-code.md servisleri statikten
    /// çekmeyi yasaklar, çünkü test edilemez hale gelirler. Bu bir servis değil, oyun
    /// mantığına hiç dokunmayan bir teşhis sayacı. Buradaki bir değer yanlış olsa
    /// oyunda hiçbir şey değişmez — yalnızca ekrandaki sayı yanlış olur. Oyun kuralı
    /// asla bu sınıfı okumaz.</para>
    /// </summary>
    public static class DiagnosticCounters
    {
        /// <summary>Sahnede o an aktif olan yapay zekâ agent'ı sayısı.</summary>
        public static int ActiveAgents;

        /// <summary>Sahne geçişlerinde çağrılır. Domain reload kapalıyken sayaçlar taşınır.</summary>
        public static void Reset()
        {
            ActiveAgents = 0;
        }
    }
}
