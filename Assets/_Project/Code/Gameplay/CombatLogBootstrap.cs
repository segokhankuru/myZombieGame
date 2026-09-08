using System;
using System.Globalization;
using System.IO;
using Bunker.Systems.Cards;
using Bunker.Systems.Pickups;
using Bunker.Systems.Rounds;
using Bunker.Systems.Telemetry;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Savaş günlüğünü oyuna bağlar: dosyayı açar, saati sürer, olayları yazar.
    /// 2026-09-08.
    ///
    /// <para><b>Neden bir <c>MonoBehaviour</c> DEĞİL de kendi kendini doğuran bir
    /// köprü:</b> sahneye elle konması gereken bir ölçüm aracı, konmayı unutulduğu
    /// gün sessizce yok olur — bu projedeki hataların en sık türü
    /// (<c>TelemetryBootstrap</c> ile birebir aynı gerekçe). Saat için tek bir gizli
    /// nesne doğuyor; onun tek işi <c>Update</c>'te bir <c>float</c> yazmak.</para>
    ///
    /// <para><b>Neden oturum başına bir dosya:</b> geliştirici bir <i>oturuma</i>
    /// bakıyor. Tek büyük dosyada aranan an on binlerce satırın arasında kalırdı.</para>
    /// </summary>
    public static class CombatLogBootstrap
    {
        /// <summary>Kayıtların yaşadığı klasör — run özetiyle aynı yer.</summary>
        public const string FolderName = TelemetryBootstrap.FolderName;

        private static bool _errorLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Once BIRAK, sonra abone ol. Domain reload kapaliyken statik olaylar bir
            // onceki Play oturumunun delegelerini tasir ve her olay IKI satir yazardi.
            Unsubscribe();

            string directory = ResolveDirectory();
            string fileName = "session-" +
                              DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
                              ".log";

            try
            {
                CombatLog.Install(new CombatLogWriter(directory, fileName));
            }
            catch (Exception e)
            {
                // Gunluk kurulamadi: oyun etkilenmez, CombatLog'un her cagrisi
                // sessizce duser.
                Debug.LogWarning($"[Savas gunlugu] Kurulamadi: {e.Message}");
                return;
            }

            CombatLog.Event("BASLA", "Bunker oturumu. Sutunlar: " +
                                     "[saniye tur] TUR  vuran[silah] -> hedef(parca)  hasar  can");

            Subscribe();
            Clock.Ensure();

            // Editorde yolu bir kez soyle: gelistirici dosyayi ARAYABILMELI.
            if (Application.isEditor)
            {
                Debug.Log($"[Savas gunlugu] {CombatLog.FilePath}");
            }
        }

        private static void Subscribe()
        {
            RoundSignals.RoundStarted += OnRoundStarted;
            RoundSignals.RoundCleared += OnRoundCleared;
            RunSignals.RunRestarted += OnRunRestarted;
            RunSignals.RunEnded += OnRunEnded;
            PowerupSignals.Picked += OnPowerupPicked;
            CardSignals.DraftClosed += OnCardPicked;

            Application.quitting += OnQuitting;
        }

        private static void Unsubscribe()
        {
            RoundSignals.RoundStarted -= OnRoundStarted;
            RoundSignals.RoundCleared -= OnRoundCleared;
            RunSignals.RunRestarted -= OnRunRestarted;
            RunSignals.RunEnded -= OnRunEnded;
            PowerupSignals.Picked -= OnPowerupPicked;
            CardSignals.DraftClosed -= OnCardPicked;

            Application.quitting -= OnQuitting;
        }

        private static void OnRoundStarted(int round)
        {
            CombatLog.SetRound(round);
            CombatLog.Event("TUR", $"--- tur {round} basladi ---");
        }

        private static void OnRoundCleared(int round)
        {
            CombatLog.Event("TUR", $"--- tur {round} temizlendi ---");
            CombatLog.Flush();
        }

        private static void OnRunRestarted()
        {
            CombatLog.RunRestarted();
        }

        private static void OnRunEnded(RunSummary summary)
        {
            CombatLog.Event("RUN", $"run bitti - tur {summary.RoundReached}, " +
                                   $"{summary.Kills} oldurme, {summary.PointsEarned} puan");
            CombatLog.Flush();
        }

        private static void OnPowerupPicked(PowerupKind kind, float amount, float seconds)
        {
            CombatLog.Event("ESYA", $"{kind} alindi (miktar {amount:0.##}, sure {seconds:0.##} sn)");
        }

        private static void OnCardPicked(CardDefinition card)
        {
            if (!card.IsValid) return;

            CombatLog.Event("KART", card.DisplayName);
        }

        private static void OnQuitting() => CombatLog.Flush();

        /// <summary>
        /// Klasör: editörde proje kökü, build'de <c>persistentDataPath</c>.
        /// Gerekçesi <see cref="TelemetryBootstrap"/>'te.
        /// </summary>
        private static string ResolveDirectory()
        {
            if (Application.isEditor)
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

                if (!string.IsNullOrEmpty(projectRoot))
                {
                    return Path.Combine(projectRoot, FolderName);
                }

                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Debug.LogError("[Savas gunlugu] Proje koku bulunamadi; kayitlar " +
                                   "persistentDataPath altina yazilacak.");
                }
            }

            return Path.Combine(Application.persistentDataPath, FolderName);
        }

        /// <summary>
        /// Günlüğün saati. <b>Tek işi bir sayı yazmak</b> — <c>Bunker.Systems</c>
        /// motoru göremediği için zamanı buradan alıyor.
        ///
        /// <para><b><c>unscaledTime</c> DEĞİL, oyun zamanı değil de oturum zamanı:</b>
        /// tezgâh açıkken dünya duruyor (<c>WorldClock</c>) ama günlüğün satırları
        /// arasında geçen gerçek süre okunabilir kalmalı — "iki vuruş arasında 80 ms"
        /// cümlesi ancak duraklamalardan etkilenmeyen bir saatle doğrudur.</para>
        /// </summary>
        [AddComponentMenu("")]
        private sealed class Clock : MonoBehaviour
        {
            private static Clock _instance;

            internal static void Ensure()
            {
                if (_instance != null) return;

                var host = new GameObject("_CombatLogClock");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideInHierarchy;

                _instance = host.AddComponent<Clock>();
            }

            private void Update() => CombatLog.SetClock(Time.unscaledTime);

            private void OnApplicationPause(bool paused)
            {
                if (paused) CombatLog.Flush();
            }

            private void OnDestroy()
            {
                if (_instance == this) _instance = null;

                CombatLog.Flush();
            }
        }
    }
}
