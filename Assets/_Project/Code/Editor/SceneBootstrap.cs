using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bunker.Editor
{
    /// <summary>
    /// Unity boş bir hiyerarşiyle açılırsa çalışılan sahneyi kendi açar.
    ///
    /// <para><b>Neden var:</b> editörün "en son açık sahne" kaydı
    /// (<c>Library/LastSceneManagerSetup.txt</c>) kalıcı değildir — başsız bir toplu
    /// çalıştırma, <c>Library</c> temizliği ya da depoyu başka bir makinede açmak onu
    /// siler. O zaman geliştirici boş bir sahneyle karşılaşır ve haritanın kaybolduğunu
    /// sanır. Bu bir kullanıcı hatası değil, editörün varsayılan davranışıdır; çözümü
    /// de bir talimat değil, bu dosya.</para>
    ///
    /// <para><b>Araya girmez:</b> yalnızca açık sahne <i>isimsiz ve boş</i> ise devreye
    /// girer. Kaydedilmiş herhangi bir sahne açıksa hiçbir şey yapmaz — bir aracın
    /// geliştiricinin üstünde çalıştığı sahneyi değiştirmesi kabul edilemez.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class SceneBootstrap
    {
        private const string DefaultScenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";
        private const string SkipKey = "Bunker.SceneBootstrap.Skip";

        static SceneBootstrap()
        {
            // delayCall: alan yuklemesi sirasinda sahne acmak Unity'yi yariyolda yakalar.
            EditorApplication.delayCall += TryOpenDefaultScene;
        }

        [MenuItem("Bunker/Level/Calisma Sahnesini Ac", false, 80)]
        public static void OpenDefaultScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DefaultScenePath, OpenSceneMode.Single);
        }

        /// <summary>Otomatik açılışı kapatır/açar. Kapalıyken menüden elle açılabilir.</summary>
        [MenuItem("Bunker/Level/Acilista Sahneyi Ac", false, 81)]
        private static void ToggleAutoOpen()
        {
            bool skip = !EditorPrefs.GetBool(SkipKey, false);
            EditorPrefs.SetBool(SkipKey, skip);
            Debug.Log($"[Bunker] Acilista calisma sahnesini ac: {(skip ? "KAPALI" : "ACIK")}");
        }

        [MenuItem("Bunker/Level/Acilista Sahneyi Ac", true)]
        private static bool ToggleAutoOpenValidate()
        {
            Menu.SetChecked("Bunker/Level/Acilista Sahneyi Ac", !EditorPrefs.GetBool(SkipKey, false));
            return true;
        }

        private static void TryOpenDefaultScene()
        {
            if (EditorPrefs.GetBool(SkipKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Application.isBatchMode) return;

            Scene active = EditorSceneManager.GetActiveScene();

            // Kaydedilmis bir sahne aciksa dokunma. Bos ve isimsiz sahne, editorun
            // "acacak sahne bulamadim" hali demektir.
            if (!string.IsNullOrEmpty(active.path)) return;
            if (active.rootCount > 0) return;
            if (EditorSceneManager.sceneCount > 1) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultScenePath) == null) return;

            EditorSceneManager.OpenScene(DefaultScenePath, OpenSceneMode.Single);
            Debug.Log($"[Bunker] Bos hiyerarsi ile acildi, calisma sahnesi yuklendi: " +
                      $"{DefaultScenePath}  (Bunker/Level menusunden kapatilabilir)");
        }
    }
}
