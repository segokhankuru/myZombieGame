using System;
using Bunker.Systems.Config;

namespace Bunker.Systems.Rounds
{
    /// <summary>
    /// Tur numarasından zombi sayısı, canı, hızı ve tempo değerlerini üretir.
    /// M1-01. Oyunun zorluk eğrisinin tamamı burada.
    ///
    /// <para><b>Saf C#.</b> Unity'ye hiçbir referansı yok, sahne gerektirmez, milisaniye
    /// içinde test edilir. Bu sınıfın doğruluğu 30. turda ne olacağını Unity açmadan
    /// bilmemizi sağlar — dengeleme döngüsünün tamamı buna dayanıyor.</para>
    ///
    /// <para><b>Eğrinin şekli:</b> önce doğrusal, sonra çarpımsal. Doğrusal faz oyuncuya
    /// kuralları öğretir ve haritayı açacak puanı biriktirmesine izin verir; çarpımsal
    /// faz koşmayı zorunlu kılar. İki fazın olmaması bu türün en sık hatasıdır — saf
    /// doğrusal eğri sonsuza kadar kolay kalır, saf çarpımsal eğri oyuncuyu daha
    /// öğrenmeden ezer.</para>
    /// </summary>
    public sealed class RoundScaling
    {
        private readonly RoundsConfig _config;

        public RoundScaling(RoundsConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Aynı anda canlı olabilecek maksimum zombi (PERF-BUDGET tavanı).</summary>
        public int MaxConcurrent => _config.CountMaxConcurrent;

        /// <summary>Turlar arası nefes molası.</summary>
        public float BreatherSeconds => _config.PacingBreatherSeconds;

        /// <summary>
        /// Turun toplam zombi sayısı. Hepsi aynı anda canlı olmaz —
        /// <see cref="MaxConcurrent"/> tavanı vardır, kalanı sırada bekler.
        /// </summary>
        public int TotalZombiesForRound(int round, int playerCount)
        {
            round = ClampRound(round);
            playerCount = playerCount < 1 ? 1 : playerCount;

            float perPlayer = GrowCurve(
                round,
                _config.CountPerPlayerAtRoundOne,
                _config.CountLinearAddPerPlayerPerRound,
                _config.CountLinearPhaseUntilRound,
                _config.CountGrowthMultiplierAfterLinear,
                float.MaxValue);

            int total = (int)Math.Round(perPlayer * playerCount, MidpointRounding.AwayFromZero);
            return total < 1 ? 1 : total;
        }

        /// <summary>Turdaki bir zombinin canı. <c>cap</c> ile sınırlıdır.</summary>
        public float HealthForRound(int round)
        {
            round = ClampRound(round);

            return GrowCurve(
                round,
                _config.HealthAtRoundOne,
                _config.HealthLinearAddPerRound,
                _config.HealthLinearPhaseUntilRound,
                _config.HealthGrowthMultiplierAfterLinear,
                _config.HealthCap);
        }

        /// <summary>Turun hız kademesi.</summary>
        public ZombieSpeedTier SpeedTierForRound(int round)
        {
            round = ClampRound(round);

            if (round <= _config.SpeedWalkUntilRound) return ZombieSpeedTier.Walk;
            if (round <= _config.SpeedJogUntilRound) return ZombieSpeedTier.Jog;
            return ZombieSpeedTier.Run;
        }

        /// <summary>Turun zombi hızı, metre/saniye.</summary>
        public float SpeedForRound(int round)
        {
            return SpeedTierForRound(round) switch
            {
                ZombieSpeedTier.Walk => _config.SpeedWalkMetersPerSecond,
                ZombieSpeedTier.Jog => _config.SpeedJogMetersPerSecond,
                _ => _config.SpeedRunMetersPerSecond
            };
        }

        /// <summary>
        /// İki zombi doğumu arasındaki süre. Tur ilerledikçe kısalır ama
        /// <c>spawnIntervalFloorSeconds</c> altına inmez — taban olmazsa geç turlarda
        /// bütün sürü aynı anda belirir ve oyuncunun tepki verecek zamanı kalmaz.
        /// </summary>
        public float SpawnIntervalForRound(int round)
        {
            round = ClampRound(round);

            // Sayı büyüdükçe aralık aynı oranda kısalır: turun toplam süresi
            // makul bir bantta kalsin, zombi sayisiyla dogrusal uzamasin.
            float countAtOne = _config.CountPerPlayerAtRoundOne;
            float countNow = GrowCurve(
                round,
                _config.CountPerPlayerAtRoundOne,
                _config.CountLinearAddPerPlayerPerRound,
                _config.CountLinearPhaseUntilRound,
                _config.CountGrowthMultiplierAfterLinear,
                float.MaxValue);

            float ratio = countAtOne / countNow;
            float interval = _config.PacingSpawnIntervalSecondsAtRoundOne * ratio;

            return interval < _config.PacingSpawnIntervalFloorSeconds
                ? _config.PacingSpawnIntervalFloorSeconds
                : interval;
        }

        /// <summary>
        /// Ortak eğri: <paramref name="linearUntil"/> turuna kadar doğrusal artış,
        /// sonrasında çarpımsal. Sonuç <paramref name="cap"/> ile sınırlanır.
        /// </summary>
        private static float GrowCurve(
            int round, float atRoundOne, float linearAdd,
            int linearUntil, float multiplierAfter, float cap)
        {
            float value;

            if (round <= linearUntil)
            {
                value = atRoundOne + linearAdd * (round - 1);
            }
            else
            {
                float atLinearEnd = atRoundOne + linearAdd * (linearUntil - 1);
                value = atLinearEnd * (float)Math.Pow(multiplierAfter, round - linearUntil);
            }

            return value > cap ? cap : value;
        }

        private static int ClampRound(int round) => round < 1 ? 1 : round;
    }
}
