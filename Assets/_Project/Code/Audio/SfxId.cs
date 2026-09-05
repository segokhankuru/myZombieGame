namespace Bunker.Audio
{
    /// <summary>
    /// Oyunun ses sözlüğü. <b>Her giriş bir OYUN OLAYIDIR</b>, bir dosya adı değil:
    /// "zombi saldırıya hazırlanıyor" bir olaydır, "growl_03.wav" bir varlıktır.
    ///
    /// <para><b>Neden böyle:</b> ses varlıkları geldiğinde değişecek olan şey bankadaki
    /// dalga biçimidir, çağıran kod değil. Gameplay kodu hangi dosyanın çalacağını
    /// bilmemeli — yalnızca <i>ne olduğunu</i> söylemeli (audio-code.md: geri bildirim
    /// sesleri oyun durumundan tetiklenir, animasyondan değil).</para>
    /// </summary>
    public enum SfxId
    {
        None = 0,

        // --- silah (2D, yerel oyuncu)
        GunShot,
        GunDryFire,
        ReloadOut,
        ReloadIn,
        HitMarker,
        HeadshotMarker,

        // --- bicak
        KnifeSwing,
        KnifeHit,

        // --- zombi (3D - yonu ve mesafesi duyulmali)
        ZombieGroan,
        ZombieAlert,
        ZombieAttack,
        ZombieHurt,
        ZombieDeath,
        ZombieVault,
        ZombieLegBreak,

        // --- dunya
        BarricadeTear,
        BarricadeRepair,
        PlayerHurt,

        // --- arayuz / ekonomi
        Purchase,
        Denied,
        RoundStart,
        RoundCleared,
        RunOver
    }
}
