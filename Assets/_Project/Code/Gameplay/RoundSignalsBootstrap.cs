using Bunker.Systems.Rounds;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// <see cref="RoundSignals"/>'in statik aboneliklerini <b>her oyun başlangıcında,
    /// hiçbir sahne nesnesi uyanmadan önce</b> temizler.
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
        }
    }
}
