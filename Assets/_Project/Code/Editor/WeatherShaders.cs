using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bunker.Editor
{
    /// <summary>
    /// Hava shader'larını <b>her build'e dahil eder</b>. 2026-09-10.
    ///
    /// <para><b>Neden gerekli:</b> <c>OutdoorWeather</c> malzemelerini çalışma anında
    /// <c>Shader.Find</c> ile kuruyor, çünkü bileşen sahnede yoksa kendi kendine kuruluyor
    /// ve serileştirilmiş bir malzeme referansı taşıyamıyor. Unity ise bir shader'ı
    /// yalnızca bir sahne malzemesi ona referans veriyorsa build'e koyar. Sonuç:
    /// editörde çalışan yağmur ve sis build'de <b>sessizce</b> yok olurdu — bu projede
    /// tekrar eden hata sınıfı (BUG-001, BUG-003).</para>
    ///
    /// <para><b>Neden "Always Included Shaders":</b> iki shader'ın da keyword'ü yok ya da
    /// çok az (sis 1, yağmur 4 varyant). Bu listeye koymanın build maliyeti ihmal edilir.
    /// URP'nin kendi particle shader'ını buraya koymak ise yüzlerce varyant demekti.</para>
    ///
    /// <para><b>Idempotent</b> (editor-tools.md): iki kez çalıştırmak listeyi büyütmez;
    /// zaten doğruysa dosyaya dokunmaz.</para>
    /// </summary>
    public sealed class WeatherShaders : IPreprocessBuildWithReport
    {
        private static readonly string[] ShaderNames =
        {
            Bunker.Gameplay.OutdoorWeather.FogShaderName,
            Bunker.Gameplay.OutdoorWeather.RainShaderName
        };

        public int callbackOrder => 0;

        /// <summary>
        /// Build başlamadan önce. Shader bulunamazsa <b>build durur</b>: havası olmayan bir
        /// build başarılı görünüp arkadaşa gönderilirdi.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EnsureIncluded(out string error)) throw new BuildFailedException(error);
        }

        [MenuItem("Bunker/Gorunum/Hava Shader'larini Build'e Dahil Et", false, 124)]
        public static void EnsureIncludedMenu() => EnsureIncluded(out _);

        /// <summary>Komut satırı girişi (<c>unity-exec.ps1</c>). Kendisi çıkar.</summary>
        public static void EnsureIncludedBatch() => EditorApplication.Exit(EnsureIncluded(out _) ? 0 : 1);

        public static bool EnsureIncluded(out string error)
        {
            error = null;

            Object settings = GraphicsSettings.GetGraphicsSettings();
            var serialized = new SerializedObject(settings);
            SerializedProperty list = serialized.FindProperty("m_AlwaysIncludedShaders");

            if (list == null || !list.isArray)
            {
                error = "[Hava] GraphicsSettings'te 'm_AlwaysIncludedShaders' bulunamadi - Unity " +
                        "surumu alan adini degistirmis olabilir. Elle: Edit > Project Settings > " +
                        "Graphics > Always Included Shaders listesine hava shader'larini ekle.";
                Debug.LogError(error);
                return false;
            }

            var present = new HashSet<Shader>();

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Shader existing)
                {
                    present.Add(existing);
                }
            }

            int added = 0;

            foreach (string name in ShaderNames)
            {
                Shader shader = Shader.Find(name);

                if (shader == null)
                {
                    error = $"[Hava] Shader bulunamadi: '{name}'. Beklenen yer: " +
                            "Assets/_Project/Art/Shaders/Weather/. Dosya silinmis ya da " +
                            "derlenemiyor - Console'daki shader hatasina bak.";
                    Debug.LogError(error);
                    return false;
                }

                if (present.Contains(shader)) continue;

                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                added++;
            }

            if (added == 0)
            {
                Debug.Log("[Hava] Hava shader'lari zaten build'e dahil.");
                return true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Hava] {added} hava shader'i 'Always Included Shaders' listesine eklendi.");
            return true;
        }
    }
}
