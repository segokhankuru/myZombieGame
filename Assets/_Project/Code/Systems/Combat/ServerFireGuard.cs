using System;
using Bunker.Systems.Config;

namespace Bunker.Systems.Combat
{
    /// <summary>Sunucunun bir atışı neden reddettiği.</summary>
    public enum FireRejection
    {
        /// <summary>Kabul edildi.</summary>
        None,
        /// <summary>Atış hızından daha sık ateş edilmiş.</summary>
        TooFast,
        /// <summary>Şarjörde mermi yok ve dolum tamamlanmamış.</summary>
        NoRounds,
        /// <summary>Hiç mermi kalmamış.</summary>
        NoReserve
    }

    /// <summary>
    /// Sunucunun atış denetleyicisi. M1-06 / BUG-002.
    ///
    /// <para><b>Sunucunun işi hile önlemektir, simülasyonu birebir tekrarlamak değil.</b>
    /// İlk sürüm sunucuda ikinci bir <see cref="WeaponState"/> tutuyordu ve istemcinin
    /// durumuyla <i>kare kare</i> aynı olmasını bekliyordu. Olmuyor: komut ağdan bir
    /// kare sonra geliyor, dolayısıyla sunucunun sayacı hep bir kare geride. Sonuç,
    /// oyuncunun gördüğü şekliyle "arada bir tık yeniyor" — ve bu, silahın
    /// hissiyatını doğrudan bozan bir şey.</para>
    ///
    /// <para>Bu sınıf aynı kuralları <b>toleransla</b> uygular. Tolerans bir denge
    /// değeri değil, bir <b>ağ payı</b>: bir kare (30 FPS'te 33 ms) artı gidiş-dönüş
    /// gecikmesi için makul bir marj. Toleransın bedeli, hile yapan bir istemcinin
    /// atış hızını en fazla bu kadar aşabilmesidir — yani hiçbir şey.</para>
    ///
    /// <para><b>Saf C#</b>, kendi saatini okumaz: zaman dışarıdan verilir, böylece bir
    /// dakikalık atış dizisi testte mikrosaniyede sınanır.</para>
    /// </summary>
    public sealed class ServerFireGuard
    {
        private readonly WeaponConfig _config;
        private readonly float _tolerance;

        private float _nextAllowedShotTime = float.NegativeInfinity;
        private float _reloadRequestedTime = float.NegativeInfinity;
        private bool _reloadPending;

        private int _roundsInMagazine;
        private int _reserve;

        /// <param name="toleranceSeconds">
        /// Ağ payı. Varsayılan 0.12 sn: 30 FPS'te bir kare (33 ms) artı yerel ağda
        /// makul bir gidiş-dönüş için marj. Bu bir denge değeri değildir, o yüzden
        /// config'te değil burada yaşar (config-data.md'nin mühendislik sabiti istisnası).
        /// </param>
        public ServerFireGuard(WeaponConfig config, float toleranceSeconds = 0.12f)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tolerance = toleranceSeconds < 0f ? 0f : toleranceSeconds;

            _roundsInMagazine = Math.Max(1, _config.MagazineCapacity);
            _reserve = Math.Min(_config.MagazineStartingReserve, _config.MagazineReserveCapacity);
        }

        public int RoundsInMagazine => _roundsInMagazine;
        public int Reserve => _reserve;

        private float SecondsBetweenShots =>
            _config.FireRoundsPerMinute <= 0f ? 0f : 60f / _config.FireRoundsPerMinute;

        /// <summary>
        /// İstemci "ateş ettim" dedi. Makul mü?
        /// </summary>
        /// <param name="now">Sunucu saati (saniye).</param>
        public FireRejection TryAcceptShot(float now)
        {
            // Bekleyen dolum, suresi (tolerans dusulerek) dolduysa burada tamamlanir.
            // Ayri bir "dolum bitti" mesaji beklemek, kaybolabilecek ikinci bir mesaj
            // daha eklerdi.
            SettlePendingReload(now);

            // Tolerans BIRIKMEZ. Ilk surumde her atistan ayri ayri dusuluyordu; o
            // hesapla 0.15 saniyelik bir aralik, 0.03 saniyede bir atisa izin veriyordu
            // - yani ayarlananin bes kati. Test yakaladi.
            //
            // Dogrusu: bir sonraki izinli an, atisin GELDIGI andan degil PLANLANAN
            // andan ilerler. Boylece erken gelen bir atis oncekini one cekmez; tolerans
            // yalnizca bir karelik titremeyi yutar, surekli bir hiz avantaji vermez.
            if (now < _nextAllowedShotTime - _tolerance)
            {
                return FireRejection.TooFast;
            }

            if (_roundsInMagazine <= 0)
            {
                return _reserve > 0 ? FireRejection.NoRounds : FireRejection.NoReserve;
            }

            _roundsInMagazine--;
            _nextAllowedShotTime = Math.Max(now, _nextAllowedShotTime) + SecondsBetweenShots;
            return FireRejection.None;
        }

        /// <summary>
        /// İstemci dolum başlattığını bildirdi. <b>Süreye sunucu karar verir</b> — dolum
        /// süresini kısaltarak avantaj alınamaz. Dolum sırasında tekrar bildirmek süreyi
        /// baştan başlatır, yani spam ederek anında dolum yapılamaz.
        /// </summary>
        public void NoteReload(float now)
        {
            if (_reserve <= 0) return;
            if (_roundsInMagazine >= _config.MagazineCapacity) return;

            _reloadRequestedTime = now;
            _reloadPending = true;
        }

        /// <summary>Yedeğe mermi ekler (duvar silahı, dağıtıcı).</summary>
        public void AddReserve(int amount)
        {
            if (amount <= 0) return;
            _reserve = Math.Min(_reserve + amount, _config.MagazineReserveCapacity);
        }

        /// <summary>Yeni run.</summary>
        public void Reset()
        {
            _roundsInMagazine = Math.Max(1, _config.MagazineCapacity);
            _reserve = Math.Min(_config.MagazineStartingReserve, _config.MagazineReserveCapacity);
            _nextAllowedShotTime = float.NegativeInfinity;
            _reloadRequestedTime = float.NegativeInfinity;
            _reloadPending = false;
        }

        /// <summary>Bu atışın hasarı. Kafa çarpanı burada uygulanır.</summary>
        public float DamageFor(bool headshot) =>
            headshot ? _config.FireDamage * _config.FireHeadshotMultiplier : _config.FireDamage;

        private void SettlePendingReload(float now)
        {
            if (!_reloadPending) return;
            if (now - _reloadRequestedTime < _config.MagazineReloadSeconds - _tolerance) return;

            _reloadPending = false;

            int needed = _config.MagazineCapacity - _roundsInMagazine;
            int moved = Math.Min(needed, _reserve);

            _roundsInMagazine += moved;
            _reserve -= moved;
        }
    }
}
