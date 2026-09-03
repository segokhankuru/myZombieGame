using System;
using System.IO;
using Bunker.Systems.Rounds;
using Bunker.Systems.Telemetry;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Telemetriyi oyuna bağlar: yolu verir, biten run'ı yazdırır. M1-12.
    ///
    /// <para><b>Neden burada ve neden bir <c>MonoBehaviour</c> değil:</b> yazma
    /// mantığının tamamı <see cref="RunLogWriter"/>'da, saf C# olarak yaşıyor —
    /// <c>Bunker.Systems</c> motoru göremediği için (<c>noEngineReferences</c>) yolu
    /// ve zamanı veren bir köprü gerekiyor. Köprünün sahnede bir nesnesi olsaydı, o
    /// nesnenin unutulduğu her sahnede telemetri <b>sessizce</b> çalışmazdı; en pahalı
    /// hata türü tam olarak budur.</para>
    ///
    /// <para><b>Sıralama:</b> <c>BeforeSceneLoad</c>, yani
    /// <see cref="RoundSignalsBootstrap"/>'in <c>SubsystemRegistration</c>'da yaptığı
    /// <see cref="RunSignals.Clear"/>'dan <b>sonra</b>. Ters sırada olsaydı bu
    /// aboneliği temizlik silerdi ve hiçbir run kaydedilmezdi.</para>
    /// </summary>
    public static class TelemetryBootstrap
    {
        /// <summary>Kayıtların yaşadığı klasör adı. Hem editörde hem build'de aynı.</summary>
        public const string FolderName = "telemetry";

        private static RunLogWriter _writer;
        private static bool _errorLogged;

        /// <summary>Yazıcı. Teşhis ve test için okunur.</summary>
        public static RunLogWriter Writer => _writer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Bu satir su an gereksiz: RoundSignalsBootstrap SubsystemRegistration'da
            // (yani buradan ONCE) RunSignals.Clear() cagirip butun delegeyi zaten
            // siliyor. Yine de duruyor, cunku bedeli sifir ve tek savunma olmasi
            // gereken sey BASKA bir dosyanin calisma sirasi degil. O sira bir gun
            // degisirse, ikinci Play oturumunda her run IKI satir yazar - ve bu,
            // fark edilmesi en zor telemetri hatasidir.
            RunSignals.RunEnded -= OnRunEnded;

            _writer = new RunLogWriter(ResolveDirectory());
            _errorLogged = false;

            RunSignals.RunEnded += OnRunEnded;
        }

        /// <summary>
        /// Kayıtların yazılacağı klasör.
        ///
        /// <para><b>Editörde proje kökü, build'de <c>persistentDataPath</c>.</b>
        /// İkisinin ayrı olmasının sebebi pratik: geliştirici dosyayı bulabilmeli, ama
        /// kurulu bir oyun kendi klasörüne yazamaz. Editörde <c>Assets/</c>'in
        /// <b>dışına</b> yazılır — içine yazmak her run'da bir içe aktarma tetikler ve
        /// editörü takar.</para>
        /// </summary>
        private static string ResolveDirectory()
        {
            if (Application.isEditor)
            {
                // dataPath = <proje>/Assets. Bir ust klasor proje kokudur.
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

                if (!string.IsNullOrEmpty(projectRoot))
                {
                    return Path.Combine(projectRoot, FolderName);
                }

                // Buraya dusmemeli. Dusuyorsa sessizce persistentDataPath'e kaymak,
                // gelistiricinin dosyayi bekledigi yerde bulamamasi demektir - ve
                // "telemetri calismiyor" diye saatler harcanir. Yuksek sesle soyle.
                Debug.LogError("[Telemetri] Proje koku bulunamadi. Kayitlar " +
                               "persistentDataPath altina yazilacak, proje klasorune degil.");
            }

            return Path.Combine(Application.persistentDataPath, FolderName);
        }

        private static void OnRunEnded(RunSummary summary)
        {
            if (_writer == null) return;

            bool written = _writer.Append(summary, RunSignals.Current.RoundStartSeconds,
                                          DateTime.UtcNow, RunSignals.Current.UsedRoundSkip);

            if (written || _errorLogged) return;

            // Hata BIR KEZ bildirilir (AC-4). Her run'da bagiran bir olcum araci,
            // olctugu seyden cok gurultu uretir - ve gurultu, gercek hatalari
            // gormeyi ogretir.
            _errorLogged = true;
            Debug.LogWarning($"[Telemetri] {_writer.LastError} " +
                             "Oyun etkilenmez; yalnizca olcum kaydi tutulamiyor.");
        }
    }
}
