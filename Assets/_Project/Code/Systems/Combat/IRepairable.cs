namespace Bunker.Systems.Combat
{
    /// <summary>
    /// Oyuncunun tamir edebildiği şey. M1-08 barikatı için; kapı ve tuzak sistemleri
    /// de aynı arayüzden geçebilir.
    ///
    /// <para><b>Neden arayüz:</b> tamir eden taraf <c>Bunker.Gameplay</c>'de, barikat
    /// ise <c>Bunker.AI</c>'da (pencerenin üstünde) yaşıyor ve <b>Gameplay AI'ya bağımlı
    /// olamaz</b> (gameplay-code.md). Işın neye çarptığını bilmez; çarptığı şey tamir
    /// edilebilir olduğunu kendisi söyler — <see cref="IDamageable"/> ile aynı desen.</para>
    /// </summary>
    public interface IRepairable
    {
        /// <summary>Şu an tamir edilebilir mi (eksik parçası var mı).</summary>
        bool NeedsRepair { get; }

        /// <summary>
        /// Bir kare boyunca tamir eder. <b>Bir parça tamamlandıysa <c>true</c> döner</b> —
        /// puan o ana yazılır, tuşa basılı tutmaya değil.
        /// </summary>
        bool Repair(float deltaTime);
    }
}
