using System.Collections.Generic;
using Bunker.Systems.Combat;

namespace Bunker.Systems.Rounds
{
    /// <summary>
    /// Bir run'ın dondurulmuş sonucu. Skor ekranının okuduğu tek veri. M1-11.
    ///
    /// <para><b>Neden bir struct ve neden dondurulmuş:</b> skor ekranı açıkken sahnede
    /// hâlâ bir şeyler oluyor olabilir (son mermi havada, son zombi ölüyor). Ekranın
    /// canlı bir sayacı okuması, oyuncunun gözü önünde değişen bir skor demektir —
    /// PILLAR-02'nin "kazanılan her puan görünür" sözünün tam tersi.</para>
    /// </summary>
    public readonly struct RunSummary
    {
        /// <summary>Ulaşılan tur. Ölünen tur budur, tamamlanan son tur değil.</summary>
        public readonly int RoundReached;

        /// <summary>Run'ın toplam süresi, saniye.</summary>
        public readonly float DurationSeconds;

        public readonly int Kills;

        /// <summary>Kafa vuruşuyla gelen öldürmeler. <see cref="Kills"/>'in bir alt kümesi.</summary>
        public readonly int HeadshotKills;

        /// <summary>Bıçakla gelen öldürmeler. <see cref="Kills"/>'in bir alt kümesi.</summary>
        public readonly int MeleeKills;

        /// <summary>Run boyunca kazanılan toplam puan. Harcamak bunu düşürmez (SYS-01).</summary>
        public readonly int PointsEarned;

        /// <summary>
        /// Ölünen yer, dünya koordinatı. Ölüm hiç olmadıysa sıfırdır.
        ///
        /// <para><b>Neden üç float ve <c>Vector3</c> değil:</b> <c>Bunker.Systems</c>
        /// motoru görmez (<c>noEngineReferences</c>). Yan faydası, satırın hangi
        /// motorla üretildiğinden bağımsız okunabilir olması.</para>
        /// </summary>
        public readonly float DeathX;
        public readonly float DeathY;
        public readonly float DeathZ;

        /// <summary>Ölüm yeri gerçekten bildirildi mi. Sıfır bir koordinattır, "yok" değil.</summary>
        public readonly bool HasDeathPosition;

        public RunSummary(int roundReached, float durationSeconds, int kills,
                          int headshotKills, int meleeKills, int pointsEarned,
                          float deathX = 0f, float deathY = 0f, float deathZ = 0f,
                          bool hasDeathPosition = false)
        {
            RoundReached = roundReached;
            DurationSeconds = durationSeconds;
            Kills = kills;
            HeadshotKills = headshotKills;
            MeleeKills = meleeKills;
            PointsEarned = pointsEarned;
            DeathX = deathX;
            DeathY = deathY;
            DeathZ = deathZ;
            HasDeathPosition = hasDeathPosition;
        }

        /// <summary>Kafa vuruşu oranı, 0-1. Hiç öldürme yoksa sıfır.</summary>
        public float HeadshotRatio01 => Kills <= 0 ? 0f : (float)HeadshotKills / Kills;
    }

    /// <summary>
    /// Run'ın sayaçları. M1-11.
    ///
    /// <para><b>Saf C#.</b> Sahneyi, Unity'yi, zamanı bilmez — kendisine ne olduğu
    /// söylenir. Bu sayede "20. turda skor ne olur" sorusu 20 tur oynamadan
    /// cevaplanabilir (ÇK-16).</para>
    ///
    /// <para><b>Gerçekleşeni sayar, niyeti değil</b> — <see cref="RoundRunner"/> ile
    /// aynı kural. Öldürme puanı yazan taraf öldürmeyi bildirir; tahmin eden kimse
    /// yoktur.</para>
    ///
    /// <para><b>Donduktan sonra sağırdır.</b> Oyuncu öldükten sonra havadaki mermi bir
    /// zombiye isabet edip onu öldürebilir. O öldürme skor ekranındaki sayıyı
    /// değiştirirse, oyuncu gözünün önünde değişen bir skor görür. <see cref="Freeze"/>
    /// çağrıldıktan sonra gelen her bildirim sessizce yok sayılır.</para>
    ///
    /// <para>M1-12 bu sayaçları diske yazacak. Bu sınıf onun da kaynağıdır — telemetri
    /// ile skor ekranının farklı sayılar göstermesi, ikisini de güvenilmez yapar.</para>
    /// </summary>
    public sealed class RunRecorder
    {
        private bool _frozen;

        private float _deathX, _deathY, _deathZ;
        private bool _hasDeathPosition;

        // Her turun kacinci saniyede basladigi. Bir kez ayrilir, temizlenir -
        // yeniden yaratilmaz (csharp-code.md).
        private readonly List<float> _roundStartSeconds = new List<float>(32);

        /// <summary>Ulaşılan tur.</summary>
        public int RoundReached { get; private set; }

        public float DurationSeconds { get; private set; }

        public int Kills { get; private set; }

        public int HeadshotKills { get; private set; }

        public int MeleeKills { get; private set; }

        public int PointsEarned { get; private set; }

        /// <summary>Dondurulmuş mu. Donmuş bir kaydedici hiçbir bildirimi kabul etmez.</summary>
        public bool IsFrozen => _frozen;

        /// <summary>
        /// Her turun kaçıncı saniyede başladığı. Sıra turla aynıdır: `[0]` tur 1.
        ///
        /// <para><b>ÇK-13 bu listeden cevaplanır</b> — "tur 10'a ~15 dakikada ulaşılıyor
        /// mu" sorusu, run'ı tekrar oynamadan okunur. Yalnızca toplam süre yazsaydık,
        /// hangi turun uzadığı sorusu ölçülemez kalırdı.</para>
        /// </summary>
        public IReadOnlyList<float> RoundStartSeconds => _roundStartSeconds;

        /// <summary>Ölüm yeri bildirildi mi. Sıfır bir koordinattır, "yok" değil.</summary>
        public bool HasDeathPosition => _hasDeathPosition;

        /// <summary>
        /// Bu run'da tur atlandı mı (F7/F8 hata ayıklama kısayolu).
        ///
        /// <para><b>Böyle bir run'ın zamanlaması ölçüm değildir</b> — atlanan turların
        /// başlangıç saniyesi uydurmadır. Telemetri bunu satıra yazar, özet aracı da
        /// ÇK-13 hesabının dışında bırakır. İşaretlenmeseydi bir hata ayıklama run'ı,
        /// "tur 10'a ne kadar sürede ulaşılıyor" sorusunun cevabını sessizce
        /// bozardı.</para>
        /// </summary>
        public bool UsedRoundSkip { get; private set; }

        /// <summary>Süreyi ilerletir. Turu yürüten taraf çağırır.</summary>
        public void Tick(float deltaTime)
        {
            if (_frozen || deltaTime <= 0f) return;

            DurationSeconds += deltaTime;
        }

        /// <summary>
        /// Ulaşılan turu bildirir. Geriye gitmez: turdan tura atlayan hata ayıklama
        /// kısayolu (F7/F8) skoru düşürmemeli, çünkü ulaşılan tur bir rekordur.
        /// </summary>
        public void NoteRound(int round)
        {
            if (_frozen || round <= RoundReached) return;

            // Bir tur atlandi mi (F7/F8). ISARETLENIR, cunku atlanan turlarin
            // baslangic zamani UYDURMADIR: asagidaki dolgu hepsine ayni saniyeyi
            // yazar ve "tur basina sure" tablosunda sifir saniyelik turlar gorunur.
            // Isaretsiz birakilsaydi, bir hata ayiklama run'i CK-13'un ("tur 10'a
            // ~15 dakikada ulasiliyor mu") cevabini sessizce bozardi.
            if (round > _roundStartSeconds.Count + 1) UsedRoundSkip = true;

            // Atlanan turlar icin de yer tutulur: listedeki i. eleman HER ZAMAN
            // (i+1). turdur. Hizalama bozulursa "tur 10 kacinci saniyede basladi"
            // sorusunun cevabi kayar.
            while (_roundStartSeconds.Count < round) _roundStartSeconds.Add(DurationSeconds);

            RoundReached = round;
        }

        /// <summary>
        /// Ölünen yeri bildirir. <b>İlk bildirim geçerlidir</b> — ölüm bir kez olur ve
        /// öldükten sonra hareket eden bir ceset ölüm yerini kaydırmamalı.
        /// </summary>
        public void NoteDeathPosition(float x, float y, float z)
        {
            if (_frozen || _hasDeathPosition) return;

            _deathX = x;
            _deathY = y;
            _deathZ = z;
            _hasDeathPosition = true;
        }

        /// <summary>Bir öldürme bildirir. Puanı ayrıca <see cref="NoteScore"/> taşır.</summary>
        public void NoteKill(DamageKind kind, bool headshot)
        {
            if (_frozen) return;

            Kills++;

            if (kind == DamageKind.Melee) MeleeKills++;
            else if (headshot) HeadshotKills++;
        }

        /// <summary>
        /// Kazanılan toplam puanı bildirir.
        ///
        /// <para>Artış değil <b>toplam</b> alır. Cüzdan zaten kazanılan toplamı tutuyor;
        /// burada ikinci bir toplama yapmak, ikisinin er geç ayrışması demektir —
        /// <c>config-data.md</c>'nin "hesaplanmış değer saklama" kuralının aynısı.</para>
        /// </summary>
        public void NoteScore(int totalEarned)
        {
            if (_frozen || totalEarned <= PointsEarned) return;

            PointsEarned = totalEarned;
        }

        /// <summary>
        /// Sayaçları dondurur ve sonucu döner. İkinci çağrı aynı sonucu verir —
        /// ölüm bir kez olur (<see cref="Combat.HealthPool"/> ile aynı kural).
        /// </summary>
        public RunSummary Freeze()
        {
            _frozen = true;
            return Snapshot();
        }

        /// <summary>Sayaçları donmadan okur. HUD ve telemetri için.</summary>
        public RunSummary Snapshot() =>
            new RunSummary(RoundReached, DurationSeconds, Kills,
                           HeadshotKills, MeleeKills, PointsEarned,
                           _deathX, _deathY, _deathZ, _hasDeathPosition);

        /// <summary>
        /// Yeni bir run için her şeyi sıfırlar.
        ///
        /// <para>Buradaki eksik bir alan, ikinci run'ın skor ekranında birincinin
        /// sayısını gösterir ve bu hata testten değil oyun testinden çıkar.</para>
        /// </summary>
        public void Reset()
        {
            _frozen = false;
            RoundReached = 0;
            DurationSeconds = 0f;
            Kills = 0;
            HeadshotKills = 0;
            MeleeKills = 0;
            PointsEarned = 0;

            _deathX = 0f;
            _deathY = 0f;
            _deathZ = 0f;
            _hasDeathPosition = false;
            UsedRoundSkip = false;

            // Temizlenir, yeniden yaratilmaz (csharp-code.md).
            _roundStartSeconds.Clear();
        }
    }
}
