namespace Bunker.Systems.Combat
{
    /// <summary>Hasarın nereden geldiği. Ekonomi ödülü buna bakar (SYS-ekonomi).</summary>
    public enum DamageKind
    {
        Bullet,
        Melee,
        Environment
    }

    /// <summary>
    /// Tek bir hasar olayı. <b>Saf C#</b> — Unity tipi taşımaz, çünkü hasar bir oyun
    /// kuralıdır, bir sahne olayı değil. Nereye isabet ettiği (dünya koordinatı)
    /// bilerek yok: kural onu bilmek zorunda değil, görsel geri bildirim bilir.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageKind Kind;

        /// <summary>Kafa kutusuna isabet. Ekonomi ödülü ve bazı kartlar buna bakar.</summary>
        public readonly bool Headshot;

        public DamageInfo(float amount, DamageKind kind = DamageKind.Bullet, bool headshot = false)
        {
            Amount = amount < 0f ? 0f : amount;
            Kind = kind;
            Headshot = headshot;
        }
    }

    /// <summary>Bir hasar uygulamasının sonucu.</summary>
    public readonly struct DamageResult
    {
        /// <summary>Gerçekten emilen hasar. Ölü bir hedefte 0'dır.</summary>
        public readonly float Absorbed;

        /// <summary>Bu vuruş öldürdü mü. <b>Yalnızca bir kez</b> true döner.</summary>
        public readonly bool Killed;

        /// <summary>Kalan can sıfırın altına inen kısım. Aşırı hasar geri bildirimi için.</summary>
        public readonly float Overkill;

        public DamageResult(float absorbed, bool killed, float overkill)
        {
            Absorbed = absorbed; Killed = killed; Overkill = overkill;
        }
    }

    /// <summary>
    /// Hasar alabilen her şey. <b>Unity'ye bağlı değil</b> — böylece hasar zinciri
    /// (silah → hedef → ekonomi) sahne açmadan test edilebilir (ÇK-16).
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        DamageResult ApplyDamage(in DamageInfo damage);
    }
}
