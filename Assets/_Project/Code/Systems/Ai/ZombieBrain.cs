using System;
using Bunker.Systems.Config;

namespace Bunker.Systems.Ai
{
    /// <summary>Zombinin durumları. Her durum oyuncunun okuyabileceği bir şey ifade eder.</summary>
    public enum ZombieState
    {
        /// <summary>Doğdu, henüz hareket etmiyor. Belirme anını görünür kılar.</summary>
        Emerging,
        /// <summary>Dışarıda, gireceği pencereye yürüyor.</summary>
        ApproachingWindow,
        /// <summary>Pencerenin barikatını söküyor. Bu süre oyuncunun kazandığı zamandır.</summary>
        Tearing,
        /// <summary>Pencereden tırmanıyor. Bu sürede savunmasız ve yavaş.</summary>
        Vaulting,
        /// <summary>İçeride, oyuncuyu kovalıyor.</summary>
        Chasing,
        /// <summary>Vuruş hazırlığı — <b>telegraf</b>. Oyuncu bunu görüp geri çekilebilir.</summary>
        WindingUp,
        /// <summary>Vuruşun isabet karesi.</summary>
        Striking,
        /// <summary>Vuruş sonrası açıklık. Oyuncunun kaçma penceresi.</summary>
        Recovering,
        /// <summary>Yolu kapandı; kurtarma bekliyor.</summary>
        Stuck,
        Dead
    }

    /// <summary>Zombinin ne için hareket ettiği. Motor tarafı hedefi buna göre seçer.</summary>
    public enum ZombieMoveIntent
    {
        None,
        Window,
        Player
    }

    /// <summary>
    /// Beyne verilen dünya bilgisi. <b>Zombinin bildiği her şey burada</b> — beyin
    /// oyuncunun transform'unu okumaz, yalnızca kendisine söyleneni bilir (ai-code.md).
    /// </summary>
    public readonly struct ZombieSenses
    {
        public readonly bool HasTarget;

        /// <summary>
        /// Zombinin gövdesiyle hedefin gövdesi arasındaki <b>boşluk</b>, yatayda.
        ///
        /// <para><b>Merkez mesafesi DEĞİL</b> (2026-09-06, oyun testi). Önceki sürüm
        /// iki transform'un arasını ölçüyordu; zombinin yarıçapı 0,30 ve oyuncununki
        /// 0,40 olduğu için 1,6 metrelik menzil, gövdeler arasında <b>0,9 metre boşluk
        /// varken</b> vuruyordu. Geliştirici bunu <i>"en öndekiyle mesafem varken
        /// vurmuş oluyor"</i> diye okudu — doğru okumuş, kol o kadar uzun değil.</para>
        ///
        /// <para><b>Neden bu ölçü yığın hatasını da çözüyor:</b> merkez mesafesiyle
        /// öndeki zombinin <i>arkasındaki</i> zombi de menzil içinde kalıyordu (0,7 m
        /// aralıklarla dizilen üç zombinin üçü de 2,2 m'nin içinde). Gövde boşluğu, bir
        /// zombinin ancak gerçekten değecek kadar yakınken vurmasını sağlar.</para>
        ///
        /// <para>Değer <b>negatif olabilir</b> — gövdeler iç içe geçmiştir. Karşılaştırma
        /// yine doğru çalışır.</para>
        /// </summary>
        public readonly float GapToTargetMeters;

        /// <summary>Zombi hâlâ binanın dışında mı — yani pencereden girmesi gerekiyor mu.</summary>
        public readonly bool NeedsWindowEntry;
        public readonly float DistanceToWindowMeters;

        /// <summary>Ölçülen gerçek hız. Sıkışma bundan anlaşılır, niyetten değil.</summary>
        public readonly float ActualSpeedMetersPerSecond;

        /// <summary>
        /// Pencere barikatlı mı — yani zombinin girmeden önce sökmesi gerekiyor mu (M1-08).
        /// </summary>
        public readonly bool WindowBlocked;

        /// <summary>
        /// Bu yaratığın <b>erişim çarpanı</b> (2026-09-06). Boss 1,7 kat büyük ve kolu
        /// da o kadar uzun; normal zombide 1.
        ///
        /// <para><b>Neden çarpan, menzilin kendisi değil:</b> menzil bir denge sayısı ve
        /// <c>zombie.json</c>'da yaşıyor. Buraya menzil koymak aynı sayının ikinci bir
        /// kopyasını üretirdi (config-data.md).</para>
        /// </summary>
        public readonly float ReachMultiplier;

        /// <summary>
        /// Zombinin merkeziyle hedefin merkezi arasındaki <b>yatay mesafe</b>
        /// (2026-09-10). <see cref="GapToTargetMeters"/>'ten farkı: yarıçaplar düşülmez.
        ///
        /// <para><b>Neden ikinci bir ölçü</b> (geliştirici: <i>"zombiler 1 m'den uzaktan
        /// vuramaz, bosslarda 2 m"</i>): gövde boşluğu "değecek kadar yakın mı"yı söyler
        /// ve yarıçaplara bağlıdır — bir yarıçap yanlış ölçülürse menzil sessizce büyür.
        /// Merkez mesafesi yarıçaptan bağımsız bir <b>mutlak tavan</b> verir: hangi ölçü
        /// hatası olursa olsun o mesafeden vuruş yok.</para>
        ///
        /// <para>Varsayılan <c>0</c>: mesafe vermeyen çağrı (eski testler) tavana takılmaz.
        /// Motor tarafı (<c>ZombieAgent</c>) her düşünmede gerçek değeri geçer.</para>
        /// </summary>
        public readonly float DistanceToTargetMeters;

        /// <summary>Boss mu — mutlak tavanın hangisi uygulanacak.</summary>
        public readonly bool IsBoss;

        public ZombieSenses(
            bool hasTarget,
            float gapToTargetMeters,
            bool needsWindowEntry = false,
            float distanceToWindowMeters = 0f,
            float actualSpeedMetersPerSecond = 0f,
            bool windowBlocked = false,
            float reachMultiplier = 1f,
            float distanceToTargetMeters = 0f,
            bool isBoss = false)
        {
            HasTarget = hasTarget;
            GapToTargetMeters = gapToTargetMeters;
            NeedsWindowEntry = needsWindowEntry;
            DistanceToWindowMeters = distanceToWindowMeters;
            ActualSpeedMetersPerSecond = actualSpeedMetersPerSecond;
            WindowBlocked = windowBlocked;
            ReachMultiplier = reachMultiplier <= 0f ? 1f : reachMultiplier;
            DistanceToTargetMeters = distanceToTargetMeters;
            IsBoss = isBoss;
        }
    }

    /// <summary>
    /// Zombi karar mantığı. <b>Saf C#, sahnesiz, milisaniyede test edilir</b> (ÇK-16).
    ///
    /// <para><b>Neden durum makinesi, boolean değil:</b> "tırmanıyor mu / vuruyor mu /
    /// ölü mü" üç boolean sekiz durum eder ve ikisi test edilir (gameplay-code.md).
    /// Burada her karede tam olarak bir durum vardır ve geçişler tek yerdedir.</para>
    ///
    /// <para><b>Telegraf sözleşmesi (ai-code.md):</b> hiçbir vuruş habersiz gelmez.
    /// <see cref="ZombieState.WindingUp"/> süresi boyunca oyuncu geri çekilirse vuruş
    /// <i>ıskalar</i> ve zombi kovalamaya döner. Bu bir hata değil, tasarlanmış bir
    /// kayıptır: zombiyi okunabilir ve yenilebilir yapan şey budur.</para>
    ///
    /// <para><b>Hasarı beyin uygulamaz.</b> Beyin "şimdi vurdu" der; hasarı kime ve
    /// nasıl yazacağına motor tarafı karar verir. Bu ayrım olmadan bu sınıf Unity'ye
    /// bağlanır ve test edilemez hâle gelir.</para>
    /// </summary>
    public sealed class ZombieBrain
    {
        private readonly ZombieConfig _config;

        public ZombieBrain(ZombieConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            State = ZombieState.Emerging;
        }

        public ZombieState State { get; private set; }

        /// <summary>Mevcut durumda geçen süre. Telegraf ilerlemesi bundan çıkar.</summary>
        public float StateTimeSeconds { get; private set; }

        /// <summary>
        /// Bu <see cref="Tick"/> çağrısında vuruş isabet etti mi. <b>Tek kare doğrudur</b> —
        /// motor tarafı bunu görüp hasarı bir kez uygular.
        /// </summary>
        public bool AttackLandedThisTick { get; private set; }

        /// <summary>Telegrafın tamamlanma oranı (0..1). Görsel/işitsel geri bildirim için.</summary>
        public float WindupProgress01 =>
            State != ZombieState.WindingUp || _config.AttackWindupSeconds <= 0f
                ? 0f
                : Clamp01(StateTimeSeconds / _config.AttackWindupSeconds);

        /// <summary>Tırmanışın tamamlanma oranı (0..1). Motor tarafı konumu buna göre sürer.</summary>
        public float VaultProgress01 =>
            State != ZombieState.Vaulting || _config.WindowEntryVaultSeconds <= 0f
                ? 0f
                : Clamp01(StateTimeSeconds / _config.WindowEntryVaultSeconds);

        public bool IsAlive => State != ZombieState.Dead;

        /// <summary>
        /// Zombi bu karede kendi ayaklarıyla yürüyor mu. Tırmanış ve vuruş sırasında
        /// hayır — o anlarda konumu başka bir şey sürer ya da hiç sürmez.
        /// </summary>
        public bool WantsMovement =>
            State == ZombieState.ApproachingWindow ||
            State == ZombieState.Chasing ||
            State == ZombieState.Stuck;

        public ZombieMoveIntent MoveIntent => State switch
        {
            ZombieState.ApproachingWindow => ZombieMoveIntent.Window,
            ZombieState.Chasing => ZombieMoveIntent.Player,
            ZombieState.Stuck => ZombieMoveIntent.Player,
            _ => ZombieMoveIntent.None
        };

        /// <summary>
        /// Havuzdan yeniden kullanım. Havuzlanmış nesnenin önceki hayatından durum
        /// taşıması yasaktır (systems-code.md).
        /// </summary>
        public void Reset()
        {
            State = ZombieState.Emerging;
            StateTimeSeconds = 0f;
            AttackLandedThisTick = false;
            _stuckTimerSeconds = 0f;
            _flinchRemaining = 0f;
        }

        /// <summary>Ölüm. Her durumdan geçerlidir ve geri dönüşü yoktur.</summary>
        public void Kill()
        {
            if (State == ZombieState.Dead) return;
            Enter(ZombieState.Dead);
        }

        /// <summary>
        /// Zombi isabet aldı. M1-13 — <b>vuruşun bir karşılığı olması</b>.
        ///
        /// <para>İki şey yapar. Birincisi <b>sendeleme</b>: zombi kısa bir süre yavaşlar,
        /// yani oyuncu hasarın kabul edildiğini görür. Bu olmadan sürü, mermilerin
        /// içinden yürüyen bir duvar gibi okunur ve silah ne kadar iyi ayarlanırsa
        /// ayarlansın sünger hissettirir.</para>
        ///
        /// <para>İkincisi ve önemlisi: <b>hazırlanan vuruşu keser.</b> Telegrafı gören
        /// oyuncunun elinde iki seçenek olur — geri çekilmek ya da vurup kesmek. Bu,
        /// tek bir satırla ateş etmeye taktik değeri veren yerdir; sendeleme yalnızca
        /// görsel olsaydı bu seçenek hiç doğmazdı.</para>
        ///
        /// <para>Ölü zombi sendelemez.</para>
        /// </summary>
        public void NotifyHit(bool headshot)
        {
            if (State == ZombieState.Dead) return;

            float duration = _config.HitReactionFlinchSeconds;
            if (headshot) duration *= _config.HitReactionHeadshotFlinchMultiplier;

            // Uzun olan kazanir: arka arkaya isabetler sendelemeyi KISALTMAMALI.
            if (duration > _flinchRemaining) _flinchRemaining = duration;

            if (State == ZombieState.WindingUp) Enter(ZombieState.Chasing);
        }

        /// <summary>Zombi şu an sendeliyor mu.</summary>
        public bool IsFlinching => _flinchRemaining > 0f;

        /// <summary>
        /// Hareket hızının çarpanı. Sendelerken yavaşlar; motor tarafı bunu NavMesh
        /// hızına uygular.
        /// </summary>
        public float SpeedMultiplier =>
            IsFlinching ? _config.HitReactionFlinchSpeedMultiplier : 1f;

        private float _stuckTimerSeconds;
        private float _flinchRemaining;

        /// <summary>
        /// Bir düşünme adımı. <paramref name="deltaTime"/> son düşünmeden bu yana geçen
        /// süredir — <b>kare süresi değil</b>. Zombiler zaman dilimli düşünür
        /// (ai-code.md), yani bu değer 1/thinkHz civarıdır.
        /// </summary>
        public void Tick(float deltaTime, in ZombieSenses senses)
        {
            AttackLandedThisTick = false;

            if (State == ZombieState.Dead) return;

            if (deltaTime < 0f) deltaTime = 0f;
            StateTimeSeconds += deltaTime;

            if (_flinchRemaining > 0f) _flinchRemaining -= deltaTime;

            switch (State)
            {
                case ZombieState.Emerging:
                    if (StateTimeSeconds >= _config.SpawnEmergeDelaySeconds)
                    {
                        Enter(senses.NeedsWindowEntry
                            ? ZombieState.ApproachingWindow
                            : ZombieState.Chasing);
                    }
                    break;

                case ZombieState.ApproachingWindow:
                    // Pencere ortadan kalktıysa (barikat yıkıldı, delik açıldı) zombi
                    // artık normal kovalamaya döner - kilitli kalmaz.
                    if (!senses.NeedsWindowEntry)
                    {
                        Enter(ZombieState.Chasing);
                    }
                    else if (senses.DistanceToWindowMeters <= _config.WindowEntryTriggerDistanceMeters)
                    {
                        // Barikatliysa once sokulur (M1-08). Sokme suresi, oyuncunun
                        // barikattan kazandigi zamanin ta kendisidir.
                        Enter(senses.WindowBlocked ? ZombieState.Tearing : ZombieState.Vaulting);
                    }
                    else
                    {
                        TickStuck(deltaTime, senses);
                    }
                    break;

                case ZombieState.Tearing:
                    // Barikat yeterince acildi mi? Tamamen bosalmasi gerekmez; esigi
                    // barikatin kendisi bilir (BarricadeConfig.BoardsBeforeEntry).
                    if (!senses.NeedsWindowEntry) Enter(ZombieState.Chasing);
                    else if (!senses.WindowBlocked) Enter(ZombieState.Vaulting);
                    break;

                case ZombieState.Vaulting:
                    if (StateTimeSeconds >= _config.WindowEntryVaultSeconds) Enter(ZombieState.Chasing);
                    break;

                case ZombieState.Chasing:
                    // Hala disaridaysa pencereye geri doner. Bu satir olmadan bir kez
                    // sikisip kovalamaya gecen zombi, binaya girmesi gerektigini bir
                    // daha hic hatirlamiyordu: disaridaki NavMesh adasinda oyuncuya
                    // "en yakin ulasilabilir noktaya" yuruyup orada kaliyordu.
                    if (senses.NeedsWindowEntry)
                    {
                        Enter(ZombieState.ApproachingWindow);
                    }
                    else if (senses.HasTarget &&
                             senses.GapToTargetMeters <=
                             _config.AttackRangeMeters * senses.ReachMultiplier &&
                             WithinHardCap(senses))
                    {
                        Enter(ZombieState.WindingUp);
                    }
                    else
                    {
                        TickStuck(deltaTime, senses);
                    }
                    break;

                case ZombieState.WindingUp:
                    if (StateTimeSeconds < _config.AttackWindupSeconds) break;

                    // Telegrafin bedeli: oyuncu geri cekildiyse vurus iskalar. Mutlak
                    // tavan burada DA sorulur: telegraf baslarken 0.9 m'de olan oyuncu
                    // inis aninda 1.05 m'deyse kol toleransi ne derse desin vurus yok.
                    bool inReach = senses.HasTarget &&
                                   senses.GapToTargetMeters <=
                                   (_config.AttackRangeMeters + _config.AttackRangeToleranceMeters) *
                                   senses.ReachMultiplier &&
                                   WithinHardCap(senses);

                    if (inReach)
                    {
                        // Striking bir dusunme adimi kadar surer ve oyle kalmalidir:
                        // gorsel/isitsel geri bildirim isabet karesine baglanir
                        // (audio-code.md - "olan seyi soyleyen ses tam o karede calar").
                        Enter(ZombieState.Striking);
                        AttackLandedThisTick = true;
                    }
                    else
                    {
                        Enter(ZombieState.Chasing);
                    }
                    break;

                case ZombieState.Striking:
                    Enter(ZombieState.Recovering);
                    break;

                case ZombieState.Recovering:
                    if (StateTimeSeconds >= _config.AttackRecoverySeconds) Enter(ZombieState.Chasing);
                    break;

                case ZombieState.Stuck:
                    // Kurtarma hareketini motor tarafi yapar (yeniden yol, warp).
                    // Beyin yalnizca ne kadar surecegini soyler.
                    if (StateTimeSeconds >= _config.NavigationStuckRecoverySeconds)
                    {
                        _stuckTimerSeconds = 0f;
                        Enter(ZombieState.Chasing);
                    }
                    break;
            }
        }

        /// <summary>
        /// Hareket etmek isteyip edemiyorsa sıkışmıştır. <b>Her navigasyon hatasının
        /// tanımlı bir durumu olmalı</b> (ai-code.md) — kurtarma yolu olmadan sevk
        /// edilen bir ajan, videolu hata raporu garantisidir.
        /// </summary>
        private void TickStuck(float deltaTime, in ZombieSenses senses)
        {
            if (senses.ActualSpeedMetersPerSecond > _config.NavigationStuckSpeedMetersPerSecond)
            {
                _stuckTimerSeconds = 0f;
                return;
            }

            _stuckTimerSeconds += deltaTime;
            if (_stuckTimerSeconds < _config.NavigationStuckAfterSeconds) return;

            _stuckTimerSeconds = 0f;
            Enter(ZombieState.Stuck);
        }

        private void Enter(ZombieState next)
        {
            State = next;
            StateTimeSeconds = 0f;
        }

        /// <summary>
        /// Mutlak vuruş tavanı (2026-09-10): merkezler arası yatay mesafe, normal zombide
        /// <c>attack.maxHitDistanceMeters</c>, boss'ta <c>attack.bossMaxHitDistanceMeters</c>.
        /// Kol uzunluğundan bağımsız — gerekçe <see cref="ZombieSenses.DistanceToTargetMeters"/>'te.
        /// </summary>
        private bool WithinHardCap(in ZombieSenses senses)
        {
            float cap = senses.IsBoss
                ? _config.AttackBossMaxHitDistanceMeters
                : _config.AttackMaxHitDistanceMeters;

            return senses.DistanceToTargetMeters <= cap;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
