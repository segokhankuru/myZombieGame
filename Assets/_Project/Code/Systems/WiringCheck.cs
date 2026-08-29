namespace Bunker.Systems
{
    /// <summary>
    /// Gecici dogrulama dosyasi. Bu dosyanin derlenmesi sunu kanitlar:
    /// Bunker.Systems, Unity'ye hicbir referansi olmadan derleniyor
    /// (asmdef: noEngineReferences = true, references = []).
    ///
    /// Bu, ADR-0004'un "oyun mantigi netcode kutuphanesinden bagimsizdir"
    /// kuralinin derleyici tarafindan zorlandiginin kanitidir.
    /// M0-02'de gercek kod gelince silinecek.
    /// </summary>
    internal static class WiringCheck
    {
        internal static int ZombieCountForRound(int round, int playerCount)
        {
            return round * playerCount;
        }
    }
}
