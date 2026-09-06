using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Play'e basıldığında oyunun <b>ana menüden</b> başlamasını sağlar. M-04.
    ///
    /// <para><b>Neden ayrı bir sınıf ve neden <c>InitializeOnLoad</c></b> (2026-09-06
    /// hatası): ilk sürüm bu ayarı kurulum aracının içinden yazıyordu ve
    /// <b>çalışmıyordu</b> — <c>EditorSceneManager.playModeStartScene</c> projenin değil
    /// <b>editör oturumunun</b> ayarı. Başsız bir toplu çalıştırmada yazılan değer,
    /// geliştiricinin açtığı editöre hiç geçmiyordu; menü kuruldu, ayar yazıldı, oyun
    /// yine sandbox'tan başladı. Sessiz başarısızlığın bu projedeki kaçıncı örneği
    /// olduğunu saymayı bıraktık.</para>
    ///
    /// <para><c>InitializeOnLoad</c> her editör açılışında ve her derlemeden sonra
    /// koşar; tercih <c>EditorPrefs</c>'te (makineye özel, doğru yer).</para>
    ///
    /// <para><b>Kapatılabilir olması şart:</b> sandbox sahnesinde hızlı test etmek
    /// (bir turu tek başına denemek) menüden geçmeyi gerektirmemeli.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        private const string PrefKey = "Bunker.Menu.PlayFromMenu";
        private const string MenuPath = "Bunker/Menu/Play'i Menuden Baslat";

        static PlayModeStartScene()
        {
            // Editor yuklenirken sahne varliklari henuz hazir olmayabilir; bir kare
            // sonraya birak. delayCall bunun icin var.
            EditorApplication.delayCall += Apply;
        }

        [MenuItem(MenuPath, false, 11)]
        private static void Toggle()
        {
            bool enabled = !EditorPrefs.GetBool(PrefKey, true);
            EditorPrefs.SetBool(PrefKey, enabled);

            Apply();

            Debug.Log(enabled
                ? "[Menu] Play artik MENUDEN basliyor."
                : "[Menu] Play artik ACIK OLAN sahneden basliyor (sandbox testi icin).");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
            return true;
        }

        private static void Apply()
        {
            if (!EditorPrefs.GetBool(PrefKey, true))
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                MenuSceneGenerator.MenuScenePath);

            if (menuScene == null)
            {
                // Sessizce vazgecme: menu sahnesi yoksa sebebi soylenmeli, yoksa
                // "Play neden menuden baslamiyor" sorusu cevapsiz kalir.
                Debug.LogWarning($"[Menu] {MenuSceneGenerator.MenuScenePath} yok - Play " +
                                 "acik olan sahneden baslayacak. 'Bunker/Menu/Ana Menuyu Kur' calistir.");
                return;
            }

            if (EditorSceneManager.playModeStartScene == menuScene) return;

            EditorSceneManager.playModeStartScene = menuScene;
        }
    }
}
