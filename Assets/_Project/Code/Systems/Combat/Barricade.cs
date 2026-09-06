using System;
using Bunker.Systems.Config;

namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Bir pencerenin barikatı: kaç tahta duruyor, zombi ne kadar söktü, oyuncu ne
    /// kadar taktı. M1-08.
    ///
    /// <para><b>Barikatın bütün değeri kazandırdığı zamandır.</b> Zombiyi durdurmaz,
    /// geciktirir — ve oyuncuya "burayı tutabilirim" dedirten şey o gecikmedir.
    /// Mutlak bir duvar olsaydı tek pencereyi tutmak yeterli olurdu ve harita
    /// anlamını yitirirdi.</para>
    ///
    /// <para><b>Saf C#.</b> Söküm ve tamir ilerlemesi burada; tahtaların nerede
    /// durduğu, kimin baktığı ve neyin göründüğü motor tarafının işi.</para>
    ///
    /// <para><b>İlerleme birikir ama tahta atlamaz:</b> bir tick içinde ne kadar süre
    /// geçerse geçsin, tek çağrıda en fazla bir tahta düşer/eklenir. Uzun bir kare
    /// (yükleme, takılma) barikatın tamamını bir anda süpürmemeli.</para>
    /// </summary>
    public sealed class Barricade
    {
        private readonly BarricadeConfig _config;

        private float _tearProgress;
        private float _repairProgress;

        public Barricade(BarricadeConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Capacity = Math.Max(1, _config.BoardsPerWindow);
            Boards = Math.Clamp(_config.BoardsStartingCount, 0, Capacity);
        }

        /// <summary>Tam barikattaki tahta sayısı.</summary>
        public int Capacity { get; }

        /// <summary>Şu an duran tahta sayısı.</summary>
        public int Boards { get; private set; }

        public bool IsFull => Boards >= Capacity;

        /// <summary>
        /// Zombi buradan içeri girebilir mi. Barikatın tamamen boşalması gerekmez —
        /// <c>beforeEntry</c> kadar tahta kalmışken zombi aradan sızar.
        /// </summary>
        public bool AllowsEntry => Boards <= _config.BoardsBeforeEntry;

        /// <summary>Sökülmekte olan tahtanın ilerlemesi (0..1). Görsel geri bildirim için.</summary>
        public float TearProgress01 =>
            _config.BoardsZombieSecondsPerBoard <= 0f
                ? 0f
                : Clamp01(_tearProgress / _config.BoardsZombieSecondsPerBoard);

        /// <summary>Takılmakta olan tahtanın ilerlemesi (0..1).</summary>
        public float RepairProgress01 =>
            _config.RepairSecondsPerBoard <= 0f
                ? 0f
                : Clamp01(_repairProgress / _config.RepairSecondsPerBoard);

        /// <summary>
        /// Zombi söküyor. <b>Bir tahta düştüyse true döner</b> — ses, görsel ve
        /// telemetri o ana bağlanır.
        /// </summary>
        public bool Tear(float deltaTime)
        {
            if (deltaTime <= 0f) return false;
            if (Boards <= 0) return false;

            // Sokme ve tamir ayni anda ilerleyemez: biri digerinin ilerlemesini
            // sifirlar. Aksi halde iki taraf da yarim tahta biriktirip sirayla
            // tamamlar ve kimin kazandigi okunmaz olurdu.
            _repairProgress = 0f;
            _tearProgress += deltaTime;

            if (_tearProgress < _config.BoardsZombieSecondsPerBoard) return false;

            _tearProgress = 0f;
            Boards--;
            return true;
        }

        /// <summary>
        /// Oyuncu tamir ediyor. Bir tahta takıldıysa <c>true</c> döner — <b>puan o ana
        /// yazılır</b>, tamir tuşuna basılı tutmaya değil.
        /// </summary>
        public bool Repair(float deltaTime)
        {
            if (deltaTime <= 0f) return false;
            if (IsFull) return false;

            _tearProgress = 0f;
            _repairProgress += deltaTime;

            if (_repairProgress < _config.RepairSecondsPerBoard) return false;

            _repairProgress = 0f;
            Boards++;
            return true;
        }

        /// <summary>
        /// Kimse dokunmuyor. Yarım kalan iş <b>durur ama silinmez</b>: oyuncu tamire
        /// ara verip geri döndüğünde baştan başlamamalı, zombi de öyle.
        /// </summary>
        public void Idle()
        {
        }

        /// <summary>
        /// Tur sonu <b>kısmi</b> yenilenme: tam barikatın <paramref name="fraction01"/>
        /// kadarı geri gelir (2026-09-05).
        ///
        /// <para><b>Neden kısmi, tam değil:</b> tam yenilenme tamir etmeyi bir karar
        /// olmaktan çıkarır — tahtalar zaten dönecekse mola boyunca tamir etmenin
        /// anlamı kalmaz. Hiç yenilenmemesi ise geç turlarda molanın tamamını tamire
        /// bağlar ve tezgâha gitmeyi imkânsız kılar. Oran <c>rounds.json</c>'da
        /// (<c>roundEnd.barricadeBoardsFraction01</c>), koda gömülü değil.</para>
        ///
        /// <para>Yarım kalan söküm ilerlemesi <b>silinir</b>: tur bitti, o pencerede
        /// çalışan zombi artık yok.</para>
        /// </summary>
        /// <returns>Gerçekten eklenen tahta sayısı.</returns>
        public int RestoreFraction(float fraction01)
        {
            if (fraction01 <= 0f) return 0;
            if (IsFull) return 0;

            int add = (int)Math.Round(Capacity * fraction01, MidpointRounding.AwayFromZero);

            // Oran sifirdan buyukse en az bir tahta gelir: "yenilendi" denip hicbir
            // seyin degismemesi, oyuncuya kuralı YANLIS ogretir.
            if (add < 1) add = 1;

            int before = Boards;
            Boards = Math.Min(Capacity, Boards + add);

            _tearProgress = 0f;
            _repairProgress = 0f;

            return Boards - before;
        }

        /// <summary>Yeni run: barikat başlangıç hâline döner.</summary>
        public void Reset()
        {
            Boards = Math.Clamp(_config.BoardsStartingCount, 0, Capacity);
            _tearProgress = 0f;
            _repairProgress = 0f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
