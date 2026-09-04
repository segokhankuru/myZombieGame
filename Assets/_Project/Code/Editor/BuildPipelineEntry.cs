using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Başsız build girişi. <c>.claude/tools/build.ps1</c> bunu çağırır.
    ///
    /// <para><b>Neden var:</b> M-01'in karar kriteri ÇK-17 "en az 2 arkadaş denemesi"
    /// istiyor ve arkadaşlar geliştiricinin editöründe oynayamaz. Bu projede daha önce
    /// hiç build alınmadı — ilk build kendi sürprizlerini getirir ve onları arkadaşlar
    /// karşında otururken değil, şimdi öğrenmek gerekir.</para>
    ///
    /// <para><b>Hiçbir şey yüklemez.</b> Depo yüklemesi ayrı ve kullanıcı onayına bağlı
    /// bir adımdır (CLAUDE.md).</para>
    ///
    /// <para><b>Sessizce başarılı olmaz.</b> Hiç sahne yoksa, sahne devre dışıysa ya da
    /// derleme başarısızsa <b>sıfırdan farklı çıkış kodu</b> döner. Başarılı görünüp boş
    /// bir klasör bırakan bir build, bu projedeki en pahalı hata türünün build
    /// versiyonudur.</para>
    /// </summary>
    public static class BuildPipelineEntry
    {
        /// <summary>
        /// Oyunun adı. Build klasöründe, pencere başlığında ve
        /// <c>persistentDataPath</c>'te görünür — yani telemetri de buraya yazar.
        /// Unity'nin "My project" varsayılanı arkadaşlara gönderilecek bir isim değil.
        /// </summary>
        private const string ProductName = "Bunker";

        /// <summary>
        /// Build'e girmesi gereken sahne. <b>Tek sahne</b>: M-01'de menü yok, oyun
        /// doğrudan bununla açılır.
        /// </summary>
        private const string PlayScene = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

        public static void BuildFromArgs()
        {
            try
            {
                string target = Arg("--target", "StandaloneWindows64");
                string config = Arg("--config", "Development");
                string output = Arg("--out", null);

                if (string.IsNullOrEmpty(output))
                {
                    Debug.LogError("[Build] --out verilmedi.");
                    EditorApplication.Exit(2);
                    return;
                }

                EditorApplication.Exit(Run(target, config, output) ? 0 : 1);
            }
            catch (Exception e)
            {
                // Yakalanmayan bir istisna batch modda cikis kodunu 0 birakabilir -
                // yani basarisiz bir build "basarili" gorunur.
                Debug.LogError($"[Build] Beklenmeyen hata: {e}");
                EditorApplication.Exit(1);
            }
        }

        private static bool Run(string targetName, string config, string output)
        {
            if (!Enum.TryParse(targetName, out BuildTarget target))
            {
                Debug.LogError($"[Build] Bilinmeyen hedef: {targetName}");
                return false;
            }

            if (!File.Exists(PlayScene))
            {
                Debug.LogError($"[Build] Oyun sahnesi yok: {PlayScene}");
                return false;
            }

            ApplyProductSettings();

            // Sahne listesi BUILD SIRASINDA belirlenir, EditorBuildSettings'ten
            // okunmaz. Sebep: o liste elle degistirilebilen bir editor ayari ve
            // icinde SampleScene gibi artiklar birikir. Build'in neyi icerdigi
            // tahmine degil, bu satira bagli olmali.
            string[] scenes = { PlayScene };

            string exePath = Path.Combine(output, ProductName + ".exe");
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = config.Equals("Development", StringComparison.OrdinalIgnoreCase)
                    ? BuildOptions.Development
                    : BuildOptions.None
            };

            Debug.Log($"[Build] {target} / {config} -> {exePath}");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] BASARISIZ: {summary.result}, " +
                               $"{summary.totalErrors} hata.");
                return false;
            }

            Debug.Log($"[Build] TAMAM: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalTime.TotalMinutes:F1} dk.");

            return true;
        }

        /// <summary>
        /// Ürün kimliği.
        ///
        /// <para>Unity'nin varsayılanı <c>DefaultCompany / My project</c>. Bunun iki
        /// somut sonucu var: arkadaşlar pencere başlığında "My project" görür, ve
        /// telemetri <c>AppData/LocalLow/DefaultCompany/My project</c> altına yazar —
        /// yani bir sonraki oyunun kayıtlarıyla aynı klasöre.</para>
        /// </summary>
        private static void ApplyProductSettings()
        {
            if (PlayerSettings.productName != ProductName)
            {
                PlayerSettings.productName = ProductName;
                Debug.Log($"[Build] productName -> {ProductName}");
            }

            // Pencereli baslar: oyun testinde gozlemci ekrani gormeli ve oyuncu
            // alt-tab yapabilmeli. Tam ekran bir gri kutu prototipi, gozlem
            // yapilmasi gereken bir seansta yanlis varsayilandir.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;

            // Calisma dizini disindan da acilsa dogru davransin.
            PlayerSettings.runInBackground = true;
        }

        /// <summary>Komut satırı argümanı okur.</summary>
        private static string Arg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }

            return fallback;
        }
    }
}
