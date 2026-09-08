using Bunker.Systems.Net;
using UnityEngine;

namespace Bunker.UI
{
    /// <summary>
    /// Oyundan çıkmanın <b>tek</b> yolu. 2026-09-06.
    ///
    /// <para><b>Neden ayrı bir sınıf</b> (oyun testi: <i>"Q ya da çıkışa basınca yine
    /// çıkamadı"</i>): çıkış <c>BunkerNetworkManager</c>'ın bir olay dinleyicisindeydi,
    /// yani çıkabilmek o nesnenin var olmasına, <c>OnEnable</c>'ının koşmuş olmasına ve
    /// aboneliğinin hâlâ duruyor olmasına bağlıydı. Sahnede ikinci bir
    /// <c>NetworkManager</c> var (log: <i>"Multiple NetworkManagers detected"</i>);
    /// kopya yok edilirken kendi <c>OnDisable</c>'ını koşturuyor ve <b>statik</b> bir
    /// yöntemi bırakan <c>-=</c>, hangi örneğin bıraktığını ayırt edemez.</para>
    ///
    /// <para><b>Kural:</b> uygulamadan çıkmak bir ağ kararı değil. Bir dinleyicinin
    /// varlığına bağlanamaz. Burada olay yine tetikleniyor — dinleyiciler oturumu
    /// düzgün kapatsın diye — ama çıkış <b>ondan sonra ve her hâlükârda</b> oluyor.</para>
    ///
    /// <para><b>Neden <c>Bunker.UI</c>'de:</b> <c>Bunker.Systems</c> motoru görmüyor
    /// (<c>noEngineReferences</c>) ve bu kasıtlı — kuralların Unity açmadan test
    /// edilebilmesi buna bağlı. <c>Application.Quit</c> orada yazılamaz.</para>
    /// </summary>
    public static class AppExit
    {
        /// <summary>Oturumu kapatır ve <b>çıkar</b>. Çıkış düğmeleri bunu çağırır.</summary>
        public static void Quit()
        {
            // Once temizlik: sunucuyu kapat, bagli istemcilere veda et. Surec
            // olduğunde arkadasin ekrani "baglanti koptu" yerine "host ayrildi"
            // gorsun.
            SessionSignals.RequestQuit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
