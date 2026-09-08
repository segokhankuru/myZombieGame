using System;

namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Sunucu tarafı hız sınırı: "bu eylem çok mu sık geldi?"
    ///
    /// <para><b>BUG-002'nin dersi bir sınıfa dönüştü.</b> Kural iki parçalı:</para>
    /// <list type="number">
    /// <item>Komut ağdan bir kare sonra ulaşır, o yüzden bir <b>ağ payı</b> olmalı —
    /// yoksa dürüst oyuncunun tıkı yenir.</item>
    /// <item>O pay <b>birikmemeli</b> — bir sonraki izinli an, eylemin geldiği andan
    /// değil <i>planlanan</i> andan ilerler. Aksi hâlde her seferinde biraz erken
    /// gelerek sürekli bir hız avantajı elde edilir; ilk sürümde tam olarak bu oldu ve
    /// 400 atış/dakikalık silah fiilen 2000 atıyordu.</item>
    /// </list>
    ///
    /// <para>Saf C#, kendi saatini okumaz.</para>
    /// </summary>
    public sealed class ActionRateLimiter
    {
        private float _interval;
        private readonly float _tolerance;

        private float _nextAllowedTime = float.NegativeInfinity;

        /// <param name="intervalSeconds">İki eylem arasındaki en kısa süre.</param>
        /// <param name="toleranceSeconds">
        /// Ağ payı. Varsayılan 0.12 sn: 30 FPS'te bir kare (33 ms) artı makul bir
        /// gidiş-dönüş marjı. Bir denge değeri değil, mühendislik sabiti.
        /// </param>
        public ActionRateLimiter(float intervalSeconds, float toleranceSeconds = 0.12f)
        {
            _interval = intervalSeconds < 0f ? 0f : intervalSeconds;
            _tolerance = toleranceSeconds < 0f ? 0f : toleranceSeconds;
        }

        /// <summary>
        /// Aralığı günceller. <b>Atış hızı çalışma anında değişir</b> — kartlar
        /// (SYS-02 Tempo/Balistik) ve tezgâh yükseltmeleri onu artırır.
        ///
        /// <para><b>Neden gerekliydi</b> (2026-09-06, oyun logu: onlarca satır
        /// <c>"Sunucu atisi reddetti: TooFast"</c>): aralık kurucuda sabitleniyordu.
        /// Atış hızı kartı alan oyuncunun <i>istemcisi</i> hızlanıyor, sunucunun
        /// sınırlayıcısı eski aralıkta kalıyordu. Ağ payı birikmediği için
        /// <c>_nextAllowedTime</c> yavaş yavaş öne kaçıyor ve bir noktadan sonra
        /// <b>her atış reddediliyordu</b> — oyuncu tarafında "silahım hasar vermiyor"
        /// olarak okunur.</para>
        ///
        /// <para><b>Bir sonraki izinli an geri çekilmez:</b> aralığı kısaltmak, zaten
        /// planlanmış bekleyişi iptal etmez. Aksi hâlde kart alındığı an bedava bir
        /// atış düşerdi.</para>
        /// </summary>
        public void SetInterval(float intervalSeconds)
        {
            _interval = intervalSeconds < 0f ? 0f : intervalSeconds;
        }

        public bool TryAccept(float now)
        {
            if (now < _nextAllowedTime - _tolerance) return false;

            _nextAllowedTime = Math.Max(now, _nextAllowedTime) + _interval;
            return true;
        }

        public void Reset() => _nextAllowedTime = float.NegativeInfinity;
    }
}
