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
        public readonly float DistanceToTargetMeters;

        /// <summary>Zombi hâlâ binanın dışında mı — yani pencereden girmesi gerekiyor mu.</summary>
        public readonly bool NeedsWindowEntry;
        public readonly float DistanceToWindowMeters;

        /// <summary>Ölçülen gerçek hız. Sıkışma bundan anlaşılır, niyetten değil.</summary>
        public readonly float ActualSpeedMetersPerSecond;

        public ZombieSenses(
            bool hasTarget,
            float distanceToTargetMeters,
            bool needsWindowEntry = false,
            float distanceToWindowMeters = 0f,
            float actualSpeedMetersPerSecond = 0f)
        {
            HasTarget = hasTarget;
            DistanceToTargetMeters = distanceToTargetMeters;
            NeedsWindowEntry = needsWindowEntry;
            DistanceToWindowMeters = distanceToWindowMeters;
            ActualSpeedMetersPerSecond = actualSpeedMetersPerSecond;
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
        }

        /// <summary>Ölüm. Her durumdan geçerlidir ve geri dönüşü yoktur.</summary>
        public void Kill()
        {
            if (State == ZombieState.Dead) return;
            Enter(ZombieState.Dead);
        }

        private float _stuckTimerSeconds;

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
                        Enter(ZombieState.Vaulting);
                    }
                    else
                    {
                        TickStuck(deltaTime, senses);
                    }
                    break;

                case ZombieState.Vaulting:
                    if (StateTimeSeconds >= _config.WindowEntryVaultSeconds) Enter(ZombieState.Chasing);
                    break;

                case ZombieState.Chasing:
                    if (senses.HasTarget && senses.DistanceToTargetMeters <= _config.AttackRangeMeters)
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

                    // Telegrafin bedeli: oyuncu geri cekildiyse vurus iskalar.
                    bool inReach = senses.HasTarget &&
                                   senses.DistanceToTargetMeters <=
                                   _config.AttackRangeMeters + _config.AttackRangeToleranceMeters;

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

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
