using System.Collections.Generic;
using System.Text;
using Bunker.Audio;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Mağaza ses paketlerini oyuna bağlar: menü müziği, ayak sesleri, silah sesleri
    /// (2026-09-09).
    ///
    /// <para><b>Neden bir araç, elle sürükleme değil</b> (CLAUDE.md #10): otuz küsur
    /// klip, altı silah ailesi ve her birinin içe aktarma ayarı var. Elle bağlanmış bir
    /// liste, ilk dosya taşındığında sessizce bozulur ve bunu ancak bir oyun testinde
    /// "ses gelmiyor" olarak duyarsın. Araç, eksik dosyayı <b>içe aktarma anında</b>
    /// yüksek sesle söyler.</para>
    ///
    /// <para><b>Üçüncü parti klasörleri değiştirilmez</b> (asset-art.md) — yalnızca
    /// <i>içe aktarma ayarları</i> düzeltilir, ki o da dosyanın kendisi değil Unity'nin
    /// yorumu. Klipler kopyalanmıyor: ses, modelin aksine URP dönüşümü gerektirmiyor.</para>
    ///
    /// <para><b>Zemin seçimi: DirtyGround.</b> Paket on iki zemin getiriyor ama oyunun
    /// tek haritası var ve zemini beton/toprak. On iki zemini de bağlamak, hiçbirinin
    /// kullanılmadığı bir katalog üretirdi; zemin algılama (raycast + materyal eşleme)
    /// gerçek bir iş ve bu sürümün kapsamında değil. Diğer zeminler pakette duruyor,
    /// gerektiğinde buraya bir satır yazılır.</para>
    /// </summary>
    public static class AudioIntegration
    {
        internal const string CatalogPath = "Assets/_Project/Config/audio.asset";

        private const string Music =
            "Assets/Music Loops Mini Set/Mystical Music Loops/Mystical  Loop #1.wav";

        private const string Steps = "Assets/Footsteps - Essentials/Footsteps_DirtyGround";
        // Silah sesleri ve perdeleri artik burada bir tablo DEGIL (2026-09-10):
        // config/content/weapon-audio.json. Bunker > Silah Atolyesi > Ses sekmesi yazar;
        // perdenin neden var oldugu da o dosyanin notunda.

        /// <summary>Komut satırı girişi. <b>Kendisi çıkar</b> (ArtIntegration ile aynı kural).</summary>
        public static void LinkAudioBatch()
        {
            try
            {
                LinkAudio();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Ses] Toplu kosu basarisiz: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Bunker/Gorunum/Ses Dosyalarini Bagla", false, 130)]
        public static void LinkAudio()
        {
            var log = new StringBuilder(512);
            int problems = 0;

            // --- muzik
            AudioClip music = Load(Music, ref problems, log);

            // Muzik AKAR, bellege acilmaz (audio-code.md): iki dakikalik bir dongu
            // decompress-on-load olsaydi onlarca MB bellek yerdi.
            SetImport(Music, AudioClipLoadType.Streaming, preloadAudioData: false);

            // --- ayak sesleri
            AudioClip[] walk = LoadRange(Steps + "/Footsteps_DirtyGround_Walk/" +
                                         "Footsteps_DirtyGround_Walk_{0:00}.wav", 1, 10,
                                         ref problems, log);

            AudioClip[] run = LoadRange(Steps + "/Footsteps_DirtyGround_Run/" +
                                        "Footsteps_DirtyGround_Run_{0:00}.wav", 1, 10,
                                        ref problems, log);

            AudioClip[] land = LoadRange(Steps + "/Footsteps_DirtyGround_Land/" +
                                         "Footsteps_DirtyGround_Jump_Land_{0:00}.wav", 1, 3,
                                         ref problems, log);

            // --- silahlar (config/content/weapon-audio.json; dosya bozuksa burada
            //     patlar - yarim bir katalog "silah sessiz" olarak duyulurdu)
            List<WeaponSoundData> weaponSounds = WeaponWorkshopData.LoadSounds();
            var families = new List<AudioCatalogAsset.WeaponSounds>(weaponSounds.Count);

            foreach (WeaponSoundData source in weaponSounds)
            {
                var sounds = new AudioCatalogAsset.WeaponSounds
                {
                    weaponId = source.WeaponId,
                    basePitch = source.BasePitch,
                    fire = LoadWeaponClips(source.Fire, ref problems, log),
                    reloadOut = LoadWeaponClip(source.ReloadOut, ref problems, log),
                    reloadIn = LoadWeaponClip(source.ReloadIn, ref problems, log),
                    dryFire = LoadWeaponClip(source.DryFire, ref problems, log),
                    equip = LoadWeaponClip(source.Equip, ref problems, log)
                };

                families.Add(sounds);
                log.Append($"  {source.WeaponId,-16} perde {source.BasePitch:0.00}  " +
                           $"ates {sounds.fire.Length} varyant\n");
            }

            // --- varlik
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalogAsset>(CatalogPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Fill(music, walk, run, land, families);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            log.Insert(0, $"[Ses] Katalog -> {CatalogPath}\n" +
                          $"  muzik {(music != null ? "var" : "YOK")}, " +
                          $"yurume {walk.Length}, kosu {run.Length}, inis {land.Length}\n");

            if (problems > 0)
            {
                // Eksik dosya SESSIZ kalmaz: sesi olmayan bir silah, oyun testinde
                // teshis edilmesi en pahali hata turudur.
                log.Append($"\n  {problems} dosya bulunamadi - o sesler SENTEZLENMIS " +
                           "yola dusecek (ADR-0009).");
                Debug.LogWarning(log.ToString());
            }
            else
            {
                Debug.Log(log.ToString());
            }
        }

        /// <summary>
        /// Bir silah klibi. <b>Boş yol eksik sayılmaz</b> — "bu silahın ele alma sesi yok"
        /// geçerli bir karar ve sentezlenmiş yola düşer. Dolu ama bulunamayan yol ise
        /// yüksek sesle söylenir.
        /// </summary>
        private static AudioClip LoadWeaponClip(string path, ref int problems, StringBuilder log)
        {
            if (string.IsNullOrEmpty(path)) return null;

            AudioClip clip = Load(path, ref problems, log);

            // Silah sesleri kisa ve SIK calar: bellege acilir (audio-code.md).
            if (clip != null) SetImport(path, AudioClipLoadType.DecompressOnLoad, preloadAudioData: true);

            return clip;
        }

        private static AudioClip[] LoadWeaponClips(List<string> paths, ref int problems, StringBuilder log)
        {
            var clips = new List<AudioClip>(paths.Count);

            foreach (string path in paths)
            {
                AudioClip clip = LoadWeaponClip(path, ref problems, log);
                if (clip != null) clips.Add(clip);
            }

            return clips.ToArray();
        }

        private static AudioClip Load(string path, ref int problems, StringBuilder log)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip == null)
            {
                problems++;
                log.Append($"  EKSIK  {path}\n");
            }

            return clip;
        }

        /// <summary>
        /// Numaralı bir varyant dizisi yükler. <b>Eksik olanı atlar, durmaz</b>: dokuz
        /// varyantla çalışan bir ses, hiç çalışmayan bir sesten iyi.
        /// </summary>
        private static AudioClip[] LoadRange(string format, int from, int to,
                                             ref int problems, StringBuilder log)
        {
            var clips = new List<AudioClip>(to - from + 1);

            for (int i = from; i <= to; i++)
            {
                string path = string.Format(format, i);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

                if (clip == null)
                {
                    problems++;
                    log.Append($"  EKSIK  {path}\n");
                    continue;
                }

                // Kisa ve SIK calan sesler bellege ACILIR (audio-code.md): her adimda
                // bir kod cozme, kare suresine giren gereksiz bir is.
                SetImport(path, AudioClipLoadType.DecompressOnLoad, preloadAudioData: true);

                clips.Add(clip);
            }

            return clips.ToArray();
        }

        /// <summary>
        /// İçe aktarma ayarını düzeltir. <b>Zaten doğruysa dosyaya dokunmaz</b> —
        /// idempotent olmayan bir araç, her çalıştırmada yüzlerce dosyayı yeniden içe
        /// aktartır (editor-tools.md).
        /// </summary>
        private static void SetImport(string path, AudioClipLoadType loadType,
                                      bool preloadAudioData)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            // preloadAudioData Unity 6'da AudioImporter'dan SampleSettings'e TASINDI
            // (platform basina yerel ayar). Eski alan hâlâ derleniyor ama uyari degil
            // HATA uretiyor - yani kullanilamaz.
            bool same = settings.loadType == loadType &&
                        settings.preloadAudioData == preloadAudioData &&
                        settings.compressionFormat == AudioCompressionFormat.Vorbis;

            if (same) return;

            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.preloadAudioData = preloadAudioData;

            importer.defaultSampleSettings = settings;

            // 3B calan sesler MONO olmali (audio-code.md): 3B konumlandirilan bir
            // stereo dosya hem bellek israfidir hem de kotu uzamsallasir. Ayak sesi ve
            // silah sesi 2B caliyor ama mono yine de yarim bellek demek.
            importer.forceToMono = loadType == AudioClipLoadType.DecompressOnLoad;

            importer.SaveAndReimport();
        }
    }
}
