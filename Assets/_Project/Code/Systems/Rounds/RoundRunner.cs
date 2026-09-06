using System;

namespace Bunker.Systems.Rounds
{
    /// <summary>Turun içinde bulunduğu evre.</summary>
    public enum RoundPhase
    {
        /// <summary>Nefes molası. Zombi doğmaz; oyuncu toplanır, tamir eder, alışveriş yapar.</summary>
        Breather,
        /// <summary>Tur açık: doğum sürüyor ya da hâlâ canlı zombi var.</summary>
        Active
    }

    /// <summary>
    /// Tur akışı: ne zaman kaç zombi doğar, tur ne zaman biter, mola ne kadar sürer.
    /// M1-05.
    ///
    /// <para><b>Saf C#.</b> Zombiyi doğurmaz, sahneyi bilmez — yalnızca <i>kaç tane</i>
    /// doğurulması gerektiğini söyler. Bu ayrım olmadan tur mantığı sahneye yapışır ve
    /// "20. turda ne oluyor" sorusu ancak 20 tur oynayarak cevaplanır (ÇK-16).</para>
    ///
    /// <para><b>Bütçe verir, emir vermez.</b> <see cref="Tick"/> bir doğum bütçesi
    /// döner; çağıran taraf gerçekten kaç tane doğurabildiğini
    /// <see cref="ReportSpawned"/> ile bildirir. Doğum noktası kapalı olabilir, havuz
    /// dolmuş olabilir — sayaç gerçekleşene göre ilerlemeli, niyete göre değil.
    /// Aksi hâlde tur, hiç doğmamış zombileri beklerken sonsuza kadar açık kalır.</para>
    /// </summary>
    public sealed class RoundRunner
    {
        private readonly RoundScaling _scaling;
        private readonly int _playerCount;

        private float _spawnTimer;

        public RoundRunner(RoundScaling scaling, int playerCount = 1)
        {
            _scaling = scaling ?? throw new ArgumentNullException(nameof(scaling));
            _playerCount = playerCount < 1 ? 1 : playerCount;

            Round = 0;
            Phase = RoundPhase.Breather;
        }

        /// <summary>Şu anki tur. Molada bir sonraki turun numarası henüz verilmemiştir.</summary>
        public int Round { get; private set; }

        public RoundPhase Phase { get; private set; }

        /// <summary>Bu evrede geçen süre.</summary>
        public float PhaseTimeSeconds { get; private set; }

        /// <summary>Turun toplam zombi sayısı. Molada bir sonraki turun sayısı değildir.</summary>
        public int TotalForRound { get; private set; }

        public int SpawnedThisRound { get; private set; }

        public int RemainingToSpawn => TotalForRound - SpawnedThisRound;

        /// <summary>Molanın bitmesine kalan süre. Aktif turda sıfır.</summary>
        public float BreatherRemainingSeconds =>
            Phase == RoundPhase.Breather
                ? Math.Max(0f, _scaling.BreatherSeconds - PhaseTimeSeconds)
                : 0f;

        /// <summary>Bu tick'te yeni bir tur başladı mı. <b>Tek tick doğrudur.</b></summary>
        public bool RoundStartedThisTick { get; private set; }

        /// <summary>Bu tick'te tur temizlendi mi (hepsi doğdu ve hepsi öldü).</summary>
        public bool RoundClearedThisTick { get; private set; }

        /// <summary>
        /// Bir adım ilerletir ve <b>bu adımda doğurulabilecek zombi sayısını</b> döner.
        /// </summary>
        /// <param name="deltaTime">Geçen süre.</param>
        /// <param name="aliveCount">Sahada canlı duran zombi sayısı.</param>
        public int Tick(float deltaTime, int aliveCount)
        {
            RoundStartedThisTick = false;
            RoundClearedThisTick = false;

            if (deltaTime < 0f) deltaTime = 0f;
            if (aliveCount < 0) aliveCount = 0;

            PhaseTimeSeconds += deltaTime;

            if (Phase == RoundPhase.Breather)
            {
                if (PhaseTimeSeconds < _scaling.BreatherSeconds) return 0;

                BeginRound();
                return 0;
            }

            // --- aktif tur
            if (RemainingToSpawn <= 0)
            {
                // Son zombi de olunce tur biter. "Hepsi dogdu" yetmez - oyuncunun
                // sahayi temizlemesi turun kendisidir.
                if (aliveCount == 0)
                {
                    Phase = RoundPhase.Breather;
                    PhaseTimeSeconds = 0f;
                    RoundClearedThisTick = true;
                }

                return 0;
            }

            // Tavan TURA GORE (2026-09-05): performans tavani (MaxConcurrent) ile
            // birlikte, o turun anlik yuk tavani da uygulanir. Sahadaki zombi sayisi
            // tavana dayandiginda dogum DURUR; biri olunce yenisi gelir. Yigilma
            // olmadan baskinin sabit kalmasi, barikat tamirini mumkun kilan tek sey.
            int capacity = _scaling.AliveCapForRound(Round) - aliveCount;
            if (capacity <= 0) return 0;

            float interval = _scaling.SpawnIntervalForRound(Round);
            _spawnTimer += deltaTime;

            if (interval <= 0f)
            {
                _spawnTimer = 0f;
                return Math.Min(capacity, RemainingToSpawn);
            }

            // Kac dogum araligi gectiyse o kadar dogum hakki. Uzun bir kare (yukleme,
            // takilma) tek dogum yiyip turu yavaslatmamali.
            int budget = (int)(_spawnTimer / interval);
            if (budget <= 0) return 0;

            _spawnTimer -= budget * interval;

            if (budget > capacity) budget = capacity;
            if (budget > RemainingToSpawn) budget = RemainingToSpawn;

            return budget;
        }

        /// <summary>
        /// Gerçekten kaç zombinin doğduğunu bildirir. <see cref="Tick"/>'in verdiği
        /// bütçeden az olabilir — doğum noktası kapalıysa ya da havuz dolduysa.
        /// </summary>
        public void ReportSpawned(int count)
        {
            if (count <= 0) return;

            SpawnedThisRound += count;
            if (SpawnedThisRound > TotalForRound) SpawnedThisRound = TotalForRound;
        }

        /// <summary>
        /// Belirli bir turdan başlatır. Hata ayıklama ve oyun testi için — bir turu
        /// oynamadan görebilmek, denge turunun tamamıdır.
        /// </summary>
        public void JumpToRound(int round)
        {
            Round = round < 1 ? 1 : round;
            Phase = RoundPhase.Active;
            PhaseTimeSeconds = 0f;
            _spawnTimer = 0f;
            SpawnedThisRound = 0;
            TotalForRound = _scaling.TotalZombiesForRound(Round, _playerCount);
            RoundStartedThisTick = true;
            RoundClearedThisTick = false;
        }

        /// <summary>Run'ı baştan başlatır.</summary>
        public void Reset()
        {
            Round = 0;
            Phase = RoundPhase.Breather;
            PhaseTimeSeconds = 0f;
            _spawnTimer = 0f;
            SpawnedThisRound = 0;
            TotalForRound = 0;
            RoundStartedThisTick = false;
            RoundClearedThisTick = false;
        }

        private void BeginRound()
        {
            Round++;
            Phase = RoundPhase.Active;
            PhaseTimeSeconds = 0f;
            SpawnedThisRound = 0;
            TotalForRound = _scaling.TotalZombiesForRound(Round, _playerCount);

            // Turun ilk zombisi molanin bitiminde HEMEN dogsun: ilk dogum icin bir
            // aralik daha beklemek, her turun basina sessiz bir bosluk ekler ve
            // PILLAR-03'un "kesintisiz tur" sozunu her turda bir kez cigner.
            _spawnTimer = _scaling.SpawnIntervalForRound(Round);
            RoundStartedThisTick = true;
        }
    }
}
