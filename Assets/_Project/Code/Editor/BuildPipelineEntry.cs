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
        /// Oyunun açılış sahnesi. <b>İlk sırada olmak zorunda</b> — Unity build'in ilk
        /// sahnesini açar.
        /// </summary>
        private const string MenuScene = "Assets/_Project/Scenes/Menu/Menu.unity";

        /// <summary>
        /// Oyun sahnesi. Menüdeki "OYNA" ve Mirror'ın sahne geçişi buraya gider —
        /// build listesinde olmazsa geçiş çalışma anında başarısız olur.
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

            // Sessizce basarili olmasin: eksik bir sahne, calisma aninda "sahne
            // build'de degil" hatasi olarak ortaya cikar - yani arkadaslar oynarken.
            string[] scenes = { MenuScene, PlayScene };

            foreach (string scene in scenes)
            {
                if (!File.Exists(scene))
                {
                    Debug.LogError($"[Build] Sahne yok: {scene}");
                    return false;
                }
            }

            bool development = config.Equals("Development", StringComparison.OrdinalIgnoreCase);

            ApplyProductSettings(development);

            // Sahne listesi BUILD SIRASINDA belirlenir (yukarida), EditorBuildSettings'ten
            // okunmaz. Sebep: o liste elle degistirilebilen bir editor ayari ve
            // icinde SampleScene gibi artiklar birikir. Build'in neyi icerdigi
            // tahmine degil, bu satirlara bagli olmali.
            string exePath = Path.Combine(output, ProductName + ".exe");
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = development ? BuildOptions.Development : BuildOptions.None
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

            CopySteamAppId(output);

            Debug.Log($"[Build] TAMAM: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalTime.TotalMinutes:F1} dk.");

            return true;
        }

        /// <summary>
        /// <c>steam_appid.txt</c>'yi exe'nin yanına kopyalar. 2026-09-07.
        ///
        /// <para><b>Neden gerekli:</b> Steam istemcisi, oyunu <i>kendi başlatmadığında</i>
        /// hangi uygulama olduğumuzu bilemez ve <c>SteamClient.Init</c> hata verir. Bu
        /// dosya cevabı exe'nin yanında taşır. Projede zaten var (editörde Play tuşu
        /// için) ama <b>build çıktısına kopyalanmıyordu</b> — yani editörde çalışan
        /// davet, arkadaşa gönderilen zip'te sessizce ölüyordu. Tam olarak bu projede
        /// tekrar eden hata sınıfı: bir adımın insan hafızasına bırakılması.</para>
        ///
        /// <para><b>Yoksa build durmaz</b>, uyarır: Steam olmadan da oynanabiliyor
        /// (KCP ile adresle katılma). Eksik bir davet, oynanamayan bir oyundan
        /// iyidir — ama sessiz kalmamalı.</para>
        /// </summary>
        private static void CopySteamAppId(string output)
        {
            const string fileName = "steam_appid.txt";

            string source = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            if (!File.Exists(source))
            {
                Debug.LogWarning($"[Build] {fileName} proje kokunde yok - build'de STEAM " +
                                 "DAVETI CALISMAZ. 'Bunker/Steam/Kurulumu Kontrol Et' " +
                                 "dosyayi olusturur.");
                return;
            }

            File.Copy(source, Path.Combine(output, fileName), overwrite: true);
            Debug.Log($"[Build] {fileName} exe'nin yanina kopyalandi.");
        }

        /// <summary>
        /// Ürün kimliği.
        ///
        /// <para>Unity'nin varsayılanı <c>DefaultCompany / My project</c>. Bunun iki
        /// somut sonucu var: arkadaşlar pencere başlığında "My project" görür, ve
        /// telemetri <c>AppData/LocalLow/DefaultCompany/My project</c> altına yazar —
        /// yani bir sonraki oyunun kayıtlarıyla aynı klasöre.</para>
        /// </summary>
        /// <param name="development">
        /// Pencere ayarları <b>yalnızca Development build'de</b> zorlanır.
        ///
        /// <para>İlk sürüm bunu <i>her</i> build için yapıyordu ve bu bir tuzaktı:
        /// ileride buradan alınacak bir Release/Steam build'i, sırf "build aldım" diye
        /// pencereli prototip ayarlarıyla çıkardı. <c>PlayerSettings</c> kalıcıdır —
        /// build almanın yan etkisi olarak proje ayarını değiştirmek, sonucu
        /// çağıranın görmediği bir yerde saklar.</para>
        /// </param>
        private static void ApplyProductSettings(bool development)
        {
            if (PlayerSettings.productName != ProductName)
            {
                PlayerSettings.productName = ProductName;
                Debug.Log($"[Build] productName -> {ProductName}");
            }

            // Calisma dizini disindan da acilsa dogru davransin. Her build icin
            // dogru olan tek ayar bu.
            PlayerSettings.runInBackground = true;

            if (!development)
            {
                Debug.Log("[Build] Release: pencere ayarlarina DOKUNULMADI.");
                return;
            }

            // Pencereli baslar: oyun testinde gozlemci ekrani gormeli ve oyuncu
            // alt-tab yapabilmeli. Tam ekran bir gri kutu prototipi, gozlem
            // yapilmasi gereken bir seansta yanlis varsayilandir.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
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
