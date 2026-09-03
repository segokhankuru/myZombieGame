using System;

namespace Bunker.Systems.Rounds
{
    /// <summary>
    /// Run olaylarının yayın noktası: ölüm, run sonu, yeniden başlatma. M1-11.
    ///
    /// <para><b>Neden statik bir yayın noktası:</b> <see cref="RoundSignals"/> ile
    /// birebir aynı gerekçe. Turu yürüten <c>ZombieDirector</c> <c>Bunker.AI</c>'da,
    /// can ve puan <c>Bunker.Gameplay</c>'de, skor ekranı <c>Bunker.UI</c>'de yaşıyor.
    /// Üçünün de gördüğü tek yer <c>Bunker.Systems</c>. Bağımlılık ters çevrilir,
    /// kimse kimseye doğrudan uzanmaz.</para>
    ///
    /// <para><b>Statik olmanın bedeli ve önlemi</b> (RoundSignals'ın öğrettiği):
    /// Unity'de statik alanlar Play oturumları arasında yaşar. İki kural zorunlu:
    /// <b>her abone <c>OnDisable</c>'da aboneliğini bırakır</b>, ve
    /// <see cref="Clear"/> yalnızca açılışta, hiçbir sahne nesnesi uyanmadan önce
    /// çağrılır (<c>RoundSignalsBootstrap</c>). Bir sahne nesnesinin
    /// <c>Awake</c>'inden çağrılması bir sıralama yarışıdır ve ondan önce uyanmış
    /// aboneler sessizce hiçbir olay almaz.</para>
    ///
    /// <para><b>Tek istisna, bilerek:</b> yalnızca sunucuda anlamı olan aboneler
    /// (<c>PlayerHealth</c>, <c>PlayerScore</c>, <c>PurchasableDoor</c>)
    /// <c>OnStartServer</c>/<c>OnStopServer</c> çiftini kullanır. Sebebi, bu üçünün
    /// yaptığı işin (canı sıfırlamak, cüzdanı sıfırlamak, kapıyı kilitlemek)
    /// <b>otorite işi</b> olması: istemcide çalışması yanlış olurdu. Simetri yine
    /// tamdır ve <c>RoundSignalsBootstrap</c> yine emniyet ağıdır.</para>
    ///
    /// <para><b>Run sonu bir kez olur.</b> Aynı karede iki zombi vurursa iki run sonu
    /// üretilmez; <see cref="IsRunOver"/> kapıyı kapatır. Bu, <c>HealthPool</c>'un
    /// "ölüm bir kez olur" kuralının run seviyesindeki karşılığıdır.</para>
    /// </summary>
    public static class RunSignals
    {
        /// <summary>Bu run'ın canlı sayaçları. Yeniden başlatma bunu sıfırlar, değiştirmez.</summary>
        public static RunRecorder Current { get; } = new RunRecorder();

        /// <summary>Run bitti; taşıdığı özet dondurulmuştur.</summary>
        public static event Action<RunSummary> RunEnded;

        /// <summary>Yeni bir run başlıyor. Herkes kendi durumunu sıfırlar.</summary>
        public static event Action RunRestarted;

        /// <summary>Run bitti mi. Bittiğinde tur ilerlemez, girdi alınmaz.</summary>
        public static bool IsRunOver { get; private set; }

        /// <summary>Son biten run'ın özeti. Skor ekranı geç uyanırsa buradan okur.</summary>
        public static RunSummary LastSummary { get; private set; }

        /// <summary>
        /// Oyuncu öldü: run'ı bitirir ve özeti yayar.
        ///
        /// <para><b>Yalnızca sunucuda çağrılmalı</b> (ADR-0004): run sonu kalıcı
        /// sonucu olan bir şeydir.</para>
        /// </summary>
        public static void RaisePlayerDied()
        {
            if (IsRunOver) return;

            IsRunOver = true;
            LastSummary = Current.Freeze();
            RunEnded?.Invoke(LastSummary);
        }

        /// <summary>Yeni run ister. Skor ekranındaki R buraya bağlanır.</summary>
        public static void RequestRestart()
        {
            IsRunOver = false;
            Current.Reset();
            RunRestarted?.Invoke();
        }

        /// <summary>
        /// Bütün abonelikleri ve run durumunu siler.
        ///
        /// <para><b>Yalnızca oyun başlarken, hiçbir sahne nesnesi uyanmadan önce.</b>
        /// <see cref="RoundSignals.Clear"/> ile aynı kural ve aynı sebep.</para>
        /// </summary>
        public static void Clear()
        {
            RunEnded = null;
            RunRestarted = null;
            IsRunOver = false;
            LastSummary = default;
            Current.Reset();
        }
    }
}
