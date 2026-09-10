using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Mağaza paketlerinin dokularına <b>boyut tavanı ve sıkıştırma</b> uygular. 2026-09-10.
    ///
    /// <para><b>Neden</b> (geliştirici: <i>"build alırken kullanılmayan objeler vs
    /// build'in içinde olmamalı, daha compact build olmalı"</i>): build raporu 369 MB
    /// kullanıcı varlığının <b>%94'ünün doku</b> olduğunu söylüyordu. En büyük on üç
    /// kalem Toby bitki atlaslarıydı: her biri 4096×4096, her biri 21 MB. Sisli,
    /// yağmurlu, loş bir sahnede 5 m ötedeki çalının 4096 ile 1024 arasındaki farkını
    /// ekranda kimse göremez; indirme süresindeki farkı herkes görür.
    /// <b>Ölçülen sonuç: 369 MB → 81.5 MB kullanıcı varlığı, build klasörü 673 → 161 MB.</b></para>
    ///
    /// <para><b>"Kullanılmayan" değil, "fazla çözünürlüklü":</b> Unity zaten hiçbir
    /// sahnenin referans vermediği varlığı build'e koymuyor. Build'i büyüten şey
    /// kullanılan dokuların boyu.</para>
    ///
    /// <para><b>Sıkıştırmasız format da yakalanır</b> (aynı gün, ikinci ölçüm): tavan
    /// uygulandıktan sonra listenin tepesinde 1024'lük ama 4-5 MB'lık dokular kaldı —
    /// paket onları <c>RGBA32</c>/<c>ARGB32</c> olarak işaretlemişti. Sıkıştırmasız bir
    /// doku, tavanın dört katı yer kaplar ve tavan onu görmez.</para>
    ///
    /// <para><b>Üçüncü parti klasörü düzenlenmiyor mu?</b> Dosyaya dokunulmuyor, değişen
    /// yalnızca <i>içe aktarma ayarı</i> — asset-art.md tam olarak bunu istiyor:
    /// "içe aktarma ayarları koddur, disiplin değil; klasöre göre postprocessor
    /// uygular". Paket yeniden indirilse bu sınıf ayarı yeniden uygular.</para>
    ///
    /// <para><b>Yalnızca AŞAĞI çeker.</b> Paketin kendisi 512 getirdiyse 512 kalır;
    /// zaten sıkıştırılmış bir formata dokunulmaz.</para>
    /// </summary>
    public sealed class ThirdPartyTextureBudget : AssetPostprocessor
    {
        /// <summary>
        /// Klasör → en büyük doku kenarı. <b>En uzun eşleşen önek kazanır</b>, yani bir
        /// paketin içindeki bir alt klasör kendi tavanını alabilir.
        ///
        /// <para>Tek kaynak burası (SSoT): başka bir belgede ikinci bir kopya yok.</para>
        /// </summary>
        private static readonly (string Folder, int MaxSize)[] Rules =
        {
            // Dis arazi: bitki/kaya atlaslari 4096 geliyor. Sis ve yagmur altinda 1024.
            ("Assets/Toby Fredson", 1024),

            // Resources klasoru: KULLANILSA DA KULLANILMASA DA build'e girer. Icindekiler
            // ruzgar kontrolcusunun editor gizmo dokulari ve logosu - oyunda gorunmez.
            ("Assets/Toby Fredson/The Toby Foliage Engine/(TTFE)_Core/Resources/(TTFE) GLOBAL CONTROLLER/TTFE_Wind Gizmo", 128),

            // Duvarlar, sinir parcalari, tahta: 3x3 kiremitleniyor.
            ("Assets/The Wasteland LITE", 1024),
            ("Assets/Tim's Substances", 1024),

            // El modelleri ve karakterler: ekranin kucuk bir kismi, loş isik.
            ("Assets/Blink", 1024),
            ("Assets/Low Poly Weapons VOL.1", 1024),
            ("Assets/Low Poly Pistol Weapon Pack 2", 1024),
            ("Assets/Low Poly ShotGun Weapon Pack 1", 1024),
            ("Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite", 1024),
            ("Assets/Kevin Iglesias", 1024),
            ("Assets/PolyOne", 1024),
        };

        /// <summary>
        /// İçe aktarma anı: tavanı aşan ya da sıkıştırmasız ayar düzeltilir. Bir paket
        /// yeniden indirilse ya da biri Inspector'da 4096'ya çıkarsa bir sonraki içe
        /// aktarmada geri gelir.
        /// </summary>
        private void OnPreprocessTexture()
        {
            int cap = CapFor(assetPath);
            if (cap <= 0) return;

            if (assetImporter is TextureImporter importer) Clamp(importer, cap, apply: true);
        }

        /// <summary>Bu yolun tavanı; kural yoksa <c>0</c>.</summary>
        public static int CapFor(string path)
        {
            int best = 0;
            int bestLength = -1;

            foreach ((string folder, int maxSize) in Rules)
            {
                if (folder.Length <= bestLength) continue;
                if (!path.StartsWith(folder + "/", System.StringComparison.Ordinal)) continue;

                best = maxSize;
                bestLength = folder.Length;
            }

            return best;
        }

        /// <summary>
        /// Bütçeyi aşan her ayarı bulur; <paramref name="apply"/> ise düzeltir. Bir şey
        /// bütçe dışıysa <c>true</c>. Tespit ve düzeltme <b>aynı kuralı</b> okusun diye
        /// tek fonksiyon — iki ayrı kopya, "hepsi bütçede" deyip dokuyu düzeltmeyen bir
        /// araç demek olurdu.
        /// </summary>
        private static bool Clamp(TextureImporter importer, int cap, bool apply)
        {
            bool over = false;

            if (importer.maxTextureSize > cap)
            {
                over = true;
                if (apply) importer.maxTextureSize = cap;
            }

            // SIKISTIRMASIZ FORMAT. Varsayilan platform ayarinda acikca RGBA32/ARGB32
            // secilmisse 'Compressed' secenegi hicbir sey yapmaz.
            TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();

            if (IsUncompressed(defaults.format))
            {
                over = true;

                if (apply)
                {
                    defaults.format = TextureImporterFormat.Automatic;
                    importer.SetPlatformTextureSettings(defaults);
                }
            }

            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                over = true;
                if (apply) importer.textureCompression = TextureImporterCompression.Compressed;
            }

            // Platforma ozel ayar varsa varsayilan ayar ONA uygulanmaz - Standalone
            // uzerine yazilmis bir 4096 ya da RGBA32, yukaridaki satirlari sessizce
            // etkisiz birakirdi.
            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");

            if (standalone.overridden &&
                (standalone.maxTextureSize > cap || IsUncompressed(standalone.format)))
            {
                over = true;

                if (apply)
                {
                    standalone.maxTextureSize = Mathf.Min(standalone.maxTextureSize, cap);
                    if (IsUncompressed(standalone.format)) standalone.format = TextureImporterFormat.Automatic;
                    importer.SetPlatformTextureSettings(standalone);
                }
            }

            return over;
        }

        /// <summary>Açıkça seçilmiş sıkıştırmasız formatlar. <c>Automatic</c> sayılmaz.</summary>
        private static bool IsUncompressed(TextureImporterFormat format) =>
            format == TextureImporterFormat.RGBA32 || format == TextureImporterFormat.ARGB32 ||
            format == TextureImporterFormat.RGB24 || format == TextureImporterFormat.RGB16 ||
            format == TextureImporterFormat.ARGB16 || format == TextureImporterFormat.RGBA16;

        [MenuItem("Bunker/Build/Magaza Dokularini Butceye Indir", false, 200)]
        public static void ApplyMenu() => Apply();

        /// <summary>Komut satırı girişi (<c>unity-exec.ps1</c>). Kendisi çıkar.</summary>
        public static void ApplyBatch() => EditorApplication.Exit(Apply() ? 0 : 1);

        /// <summary>
        /// Bütçeyi aşan dokuları bulur ve <b>yalnızca onları</b> yeniden içe aktarır.
        ///
        /// <para><b>Idempotent</b>: ikinci koşuda hiçbir şey aşmıyor, hiçbir şey içe
        /// aktarılmıyor, hiçbir dosya kirlenmiyor (editor-tools.md).</para>
        /// </summary>
        public static bool Apply()
        {
            var over = new List<(string Path, int Cap)>();
            var seen = new HashSet<string>();

            foreach ((string folder, int _) in Rules)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;   // paket kaldirilmis olabilir

                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    // Alt klasor kuralli bir doku ust klasorun taramasinda da cikar; yalnizca
                    // bir kez ve EN UZUN eslesen kuralla sayilir.
                    if (!seen.Add(path)) continue;

                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                    int cap = CapFor(path);
                    if (cap <= 0) continue;

                    if (Clamp(importer, cap, apply: false)) over.Add((path, cap));
                }
            }

            if (over.Count == 0)
            {
                Debug.Log("[Doku butcesi] Magaza dokularinin hepsi butcede - yeniden ice aktarilacak bir sey yok.");
                return true;
            }

            var log = new StringBuilder();
            log.Append("[Doku butcesi] ").Append(over.Count).Append(" doku butceye indirildi (tavan ve/veya sikistirma):\n");

            AssetDatabase.StartAssetEditing();

            try
            {
                for (int i = 0; i < over.Count; i++)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(over[i].Path);

                    if (!Clamp(importer, over[i].Cap, apply: true)) continue;

                    importer.SaveAndReimport();

                    if (i < 15)
                    {
                        log.Append("  <= ").Append(over[i].Cap).Append("  ").Append(over[i].Path).Append('\n');
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (over.Count > 15) log.Append("  ... ve ").Append(over.Count - 15).Append(" doku daha\n");

            Debug.Log(log.ToString());
            return true;
        }
    }
}
