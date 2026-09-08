using Bunker.Systems.Cards;
using Bunker.Systems.Rounds;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// <see cref="RoundSignals"/> ve <see cref="RunSignals"/>'in statik aboneliklerini
    /// <b>her oyun başlangıcında, hiçbir sahne nesnesi uyanmadan önce</b> temizler.
    ///
    /// <para><b>Neden ayrı bir sınıf:</b> ilk sürümde temizliği <c>ZombieDirector.Awake</c>
    /// yapıyordu ve bu bir <b>sıralama yarışı</b> üretti. Unity sahne nesnelerini
    /// tek tek uyandırır (her nesne için önce <c>Awake</c>, hemen ardından
    /// <c>OnEnable</c>); yönetmenden <i>önce</i> uyanan barikatlar abone oluyor,
    /// yönetmen uyanınca o abonelikler siliniyordu. Sonuç: <b>pencerelerin bir kısmı
    /// tur başında yenileniyor, bir kısmı yenilenmiyordu</b> — ve hangisinin
    /// yenileneceği nesne sırasına bağlı olduğu için rastgele görünüyordu.</para>
    ///
    /// <para><c>SubsystemRegistration</c> sahne yüklenmeden önce koşar, yani sıraya
    /// hiç girmez. Statik durumu olan her sistemin ihtiyaç duyduğu şey budur; Unity'de
    /// "Domain Reload" kapalıyken statikler Play oturumları arasında yaşar ve sızan
    /// abonelik olayları iki kez tetikler.</para>
    /// </summary>
    public static class RoundSignalsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RoundSignals.Clear();

            // M1-11: run durumu da statiktir ve ayni sizinti riskini tasir. Temizlik
            // burada olmazsa ikinci Play oturumu, birincinin bitmis run'iyla acilir -
            // yani oyun daha ilk karede skor ekraninda baslar.
            RunSignals.Clear();

            // M-03: kart yigini da statiktir ve ayni sizinti riskini tasir.
            CardSignals.Clear();
            RunModifiers.Clear();

            // M-04: menu ve oturum yayinlari. Menu -> oyun -> menu dongusunde
            // temizlenmezlerse ikinci oturumda "oda ac" iki kez tetiklenir ve
            // duraklatma menusu kapali oldugu halde acik sanilir.
            // 2026-09-07: esya (drop) yayini ve suren etkileri. Temizlenmezse ikinci
            // Play oturumu birincinin dondurmasiyla acilir ve olu abonelere yayin
            // yapilir.
            Systems.Pickups.PowerupSignals.Clear();
            Systems.Pickups.PowerupState.Clear();

            CombatFeedback.Clear();
            Systems.Ui.MenuSignals.Clear();
            Systems.Net.SessionSignals.Clear();
        }
    }
}
