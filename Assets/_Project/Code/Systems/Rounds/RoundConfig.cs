namespace Bunker.Systems.Rounds
{
    /// <summary>Zombinin tur bazlı hız kademesi.</summary>
    /// <remarks>
    /// Ayarlar sınıfı burada değil: <c>RoundsConfig</c> artık
    /// <c>config/schema/rounds.schema.json</c>'dan üretiliyor ve
    /// <c>Bunker.Systems.Config</c> içinde yaşıyor. Bu enum bir denge değeri değil,
    /// bir kademe adı — o yüzden elle yazılmış kalır.
    /// </remarks>
    public enum ZombieSpeedTier
    {
        Walk,
        Jog,
        Run
    }
}
