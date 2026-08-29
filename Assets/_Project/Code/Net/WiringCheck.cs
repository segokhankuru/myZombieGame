using Mirror;

namespace Bunker.Net
{
    /// <summary>
    /// Gecici dogrulama dosyasi. Bu dosyanin derlenmesi iki seyi kanitlar:
    /// 1. Bunker.Net -> Mirror referansi cozuluyor
    /// 2. Mirror, Unity 6000.3.23f1 uzerinde calisiyor (CK-9)
    ///
    /// LagCompensation'a da dokunuyor, cunku ADR-0004 tam olarak onun
    /// varligi uzerine kuruldu.
    /// M0-02'de gercek kod gelince silinecek.
    /// </summary>
    internal static class WiringCheck
    {
        internal static bool MirrorIsWired()
        {
            // Mirror.Core: sunucu durumu
            bool serverActive = NetworkServer.active;

            // Mirror.Core/LagCompensation: ADR-0004'un dayandigi tip
            var settings = new LagCompensationSettings();

            return serverActive || settings.captureInterval > 0f;
        }
    }
}
