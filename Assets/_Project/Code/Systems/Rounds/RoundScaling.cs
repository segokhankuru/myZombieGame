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

        /// <summary>
        /// <paramref name="nextRound"/> başlamadan önceki nefes molası (2026-09-07).
        ///
        /// <para><b>Tek bir süre değil, iki süre.</b> İlk turlarda molada yapılacak iş
        /// yoktur — barikat sağlamdır, tezgâhta alacak puan yoktur — ve 20 saniye boş
        /// bekleme olarak geçer. Geç turlarda ise tamir, tezgâh, silah ve kart okuma
        /// aynı molaya sığmak zorunda; kısa mola orada turu, oyuncunun göremediği bir
        /// hazırlık eksiğiyle kaybettirir.</para>
        ///
        /// <para>Sınır ve iki değer <c>rounds.json → pacing</c>'de; buraya sayı
        /// yazılmaz (config-data.md).</para>
        /// </summary>
        public float BreatherSecondsForRound(int nextRound)
        {
            return ClampRound(nextRound) <= _config.PacingBreatherEarlyUntilRound
                ? _config.PacingBreatherSecondsEarly
                : _config.PacingBreatherSecondsLate;
        }

        /// <summary>
        /// Tur temizlenince geri gelen yedek mermi oranı (yedek <b>tavanının</b> oranı).
        /// </summary>
        public float RoundEndReserveAmmoFraction01 => _config.RoundEndReserveAmmoFraction01;

        /// <summary>Tur temizlenince geri gelen barikat tahtası oranı (tam barikatın oranı).</summary>
        public float RoundEndBarricadeBoardsFraction01 => _config.RoundEndBarricadeBoardsFraction01;

        /// <summary>
        /// Bu turda sahada <b>aynı anda</b> durabilecek en fazla zombi.
        ///
        /// <para><b>Turun toplamı değil, anlık yükü</b> (2026-09-05). Önceki hâlde tek
        /// sınır <see cref="MaxConcurrent"/> (performans tavanı) idi; turun bütün
        /// zombileri kısa aralıklarla arka arkaya doğuyor, sahada yığılıyor ve
        /// oyuncunun barikata dönüp tamir edecek boşluğu kalmıyordu. Tavan dolduğunda
        /// doğum durur, biri ölünce yenisi gelir — yani baskı <b>sabit</b> kalır,
        /// birikmez.</para>
        ///
        /// <para>Her zaman <see cref="MaxConcurrent"/> ile sınırlıdır: bu ikisinden
        /// biri oynanabilirlik, diğeri PERF-BUDGET tavanıdır ve performans tavanı
        /// tartışmaya açık değildir.</para>
        /// </summary>
        public int AliveCapForRound(int round)
        {
            round = ClampRound(round);

            float cap = _config.CountAliveCapAtRoundOne +
                        _config.CountAliveCapAddPerRound * (round - 1);

            int result = (int)Math.Round(cap, MidpointRounding.AwayFromZero);

            if (result > MaxConcurrent) result = MaxConcurrent;
            return result < 1 ? 1 : result;
        }

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

        // ---------------------------------------------------------------- boss

        /// <summary>
        /// Bu turda boss çıkar mı (2026-09-06).
        ///
        /// <para><b>Neden düzenli aralık, rastgele değil:</b> oyuncu <b>hazırlanabilmeli</b>.
        /// Beşinci turun bir boss turu olduğunu bilmek, dördüncü turun molasında
        /// tezgâha gitmeyi bir <i>plan</i> yapar. Rastgele bir boss, hazırlığı
        /// imkânsız kılar ve ölümü şansa bağlar (ai-code.md: tahmin edilebilir,
        /// optimal olandan iyidir).</para>
        /// </summary>
        public bool IsBossRound(int round)
        {
            int every = _config.BossEveryRounds;
            if (every <= 0) return false;

            round = ClampRound(round);
            return round % every == 0;
        }

        /// <summary>Boss'un canı: turun zombi canının katı.</summary>
        public float BossHealthForRound(int round) =>
            HealthForRound(round) * _config.BossHealthMultiplier;

        /// <summary>
        /// Boss'un hızı. <b>Turun hızından yavaş</b> — çok canlı VE hızlı bir düşman,
        /// oyuncuya kaçmaktan başka seçenek bırakmaz.
        /// </summary>
        public float BossSpeedForRound(int round) =>
            SpeedForRound(round) * _config.BossSpeedMultiplier;

        public float BossScaleMultiplier => _config.BossScaleMultiplier;

        public float BossPointsMultiplier => _config.BossPointsMultiplier;

        /// <summary>
        /// Bu boss turunda kaç boss çıkar (2026-09-11). Boss turu değilse 0.
        ///
        /// <para><b>Co-op ve tek oyuncu farklı hızda:</b> geliştiricinin kuralı co-op'ta
        /// her boss turunda bir fazla (1, 2, 3), tek oyuncuda iki boss turunda bir
        /// (1, 1, 2, 2). Dört kişilik bir ekip tek bossa karşı kalınca boss turu olay
        /// olmaktan çıkıyor; tek oyuncu ise dikkati dağıtacak ikinci biri olmadan co-op
        /// hızındaki bossa dayanamaz.</para>
        /// </summary>
        public int BossCountForRound(int round, bool coop)
        {
            if (!IsBossRound(round)) return 0;

            // Kacinci boss turu (1 tabanli). IsBossRound, every > 0 oldugunu garanti eder.
            int bossRoundIndex = ClampRound(round) / _config.BossEveryRounds;

            int step = coop ? _config.BossExtraEveryBossRoundsCoop : _config.BossExtraEveryBossRoundsSolo;
            if (step < 1) step = 1;   // sifira bolme; sema zaten 1'in altini reddediyor

            int count = 1 + (bossRoundIndex - 1) / step;

            int max = _config.BossMaxPerRound < 1 ? 1 : _config.BossMaxPerRound;
            return count > max ? max : count;
        }

        /// <summary>
        /// <paramref name="bossIndex"/>'inci boss turun <b>kaçıncı doğumunda</b> gelir.
        ///
        /// <para><b>İlk boss yine turun ilk zombisi</b> (2026-09-06 kararı korunuyor);
        /// diğerleri turun doğum sırasına eşit aralıkla yayılır. Hepsi birden gelseydi
        /// boss turu kaçılabilir bir olay olmaktan çıkıp bir duvar olurdu. Zombi sayısı
        /// boss sayısından azsa bosslar arka arkaya gelir — boss hakkı kaybolmaz.</para>
        /// </summary>
        public int BossSpawnIndex(int bossIndex, int bossCount, int totalForRound)
        {
            if (bossIndex <= 0 || bossCount <= 1) return 0;
            if (totalForRound < bossCount) return bossIndex;

            return bossIndex * totalForRound / bossCount;
        }

        // ---------------------------------------------------------------- hasar

        /// <summary>
        /// Zombi vuruşunun tura göre çarpanı (2026-09-11). Taban hasar
        /// <c>zombie.json → attack.damage</c>'da; burası yalnızca çarpan.
        ///
        /// <para><b>Doğrusal, bileşik değil:</b> bileşik %3 50. turda dört kat eder ve
        /// can kartlarını siler. Tavanı var: tavansız bir hasar yeterince ileri turda
        /// her teması ölüm yapar.</para>
        /// </summary>
        public float ZombieDamageMultiplierForRound(int round)
        {
            round = ClampRound(round);

            float multiplier = 1f + _config.DamageGrowthPerRound01 * (round - 1);
            float cap = _config.DamageMaxMultiplier < 1f ? 1f : _config.DamageMaxMultiplier;

            return multiplier > cap ? cap : multiplier;
        }

        /// <summary>
        /// Boss vuruşunun çarpanı: <b>turun çarpanı × boss çarpanı</b>. Boss da turla
        /// sertleşir; yoksa geç turda normal zombiyle arasındaki fark kapanırdı.
        /// </summary>
        public float BossDamageMultiplierForRound(int round) =>
            ZombieDamageMultiplierForRound(round) * _config.BossDamageMultiplier;

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
