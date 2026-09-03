using System;

namespace Bunker.Systems.Rounds
{
    /// <summary>
    /// Tur olaylarının yayın noktası. M1-08 / M1-13.
    ///
    /// <para><b>Neden statik bir yayın noktası:</b> turu yürüten <c>ZombieDirector</c>
    /// <c>Bunker.AI</c>'da, silah ve puan <c>Bunker.Gameplay</c>'de yaşıyor ve bu iki
    /// assembly birbirini <b>göremez</b> (gameplay-code.md). İkisinin de gördüğü tek
    /// yer <c>Bunker.Systems</c>. <see cref="ZombieTargets"/> ile aynı desen: bağımlılık
    /// ters çevrilir, kimse kimseye doğrudan uzanmaz.</para>
    ///
    /// <para><b>Statik olmanın bedeli ve önlemi:</b> Unity'de statik alanlar Play
    /// oturumları arasında yaşar. Abonelik sızarsa ikinci oturumda olaylar iki kez
    /// tetiklenir — sessiz ve şaşırtıcı bir hata. Bu yüzden iki kural zorunlu:
    /// <b>her abone <c>OnDisable</c>'da aboneliğini bırakır</b>, ve turu yürüten taraf
    /// açılışta <see cref="Clear"/> çağırır.</para>
    ///
    /// <para>M1-11'de gerçek bir oyun durumu servisi geldiğinde bu sınıf ona devredilir.</para>
    /// </summary>
    public static class RoundSignals
    {
        /// <summary>Yeni tur başladı (tur numarası).</summary>
        public static event Action<int> RoundStarted;

        /// <summary>Tur temizlendi (tur numarası).</summary>
        public static event Action<int> RoundCleared;

        public static void RaiseRoundStarted(int round) => RoundStarted?.Invoke(round);

        public static void RaiseRoundCleared(int round) => RoundCleared?.Invoke(round);

        /// <summary>
        /// Bütün abonelikleri siler. <b>Yalnızca turu yürüten taraf, açılışta çağırır</b> —
        /// Play oturumları arasında sızan aboneliklerin tek panzehiri budur.
        /// </summary>
        public static void Clear()
        {
            RoundStarted = null;
            RoundCleared = null;
        }
    }
}
