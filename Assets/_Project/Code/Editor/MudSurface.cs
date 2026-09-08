using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Dış zeminin <b>ıslak çamur</b> malzemesini üretir. 2026-09-07.
    ///
    /// <para><b>Neden üretiliyor, pakete bakılmıyor</b> (geliştirici: <i>"zemini de
    /// çamur yap, varsa assetler içinde"</i>): altı paketin hiçbirinde zemin/toprak
    /// dokusu yok — The Wasteland LITE yalnızca duvar, metal ve yapı atlası taşıyor ve
    /// o atlasların UV'si parçalarına göre kesilmiş, geniş bir zemin plakasına
    /// gerilmez. Düz bir kahverengi ise yağmurun altında plastik gibi okunur.</para>
    ///
    /// <para><b>Gürültüden üretmek doğru cevap:</b> çamurun istediğimiz tek şeyi
    /// <i>düzensizlik</i>. Katmanlı Perlin, sekiz kilobaytlık bir PNG'de bunu verir ve
    /// yükseklikten türetilen normal harita ıslak yüzeyin ışığı kırmasını sağlar —
    /// yağmurun "ıslak" okunması buradan gelir, parlaklık değerinden değil.</para>
    ///
    /// <para><b>Idempotent</b> (editor-tools.md): aynı tohum, aynı doku. Var olan PNG
    /// yeniden üretilmez; dosya zaten oradaysa yalnızca materyal tazelenir.</para>
    /// </summary>
    public static class MudSurface
    {
        private const string Folder = "Assets/_Project/Art/Materials/Surfaces";
        private const string BasePath = Folder + "/tex_mud_basecolor.png";
        private const string NormalPath = Folder + "/tex_mud_normal.png";
        private const string MaterialPath = Folder + "/mat_apron_mud.mat";

        private const int Size = 512;

        /// <summary>Çamur materyalini garantiler; yoksa dokularıyla birlikte üretir.</summary>
        public static Material Ensure()
        {
            ArtIntegration.EnsureFolder(Folder);

            EnsureTextures();

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            bool created = false;
            if (material == null)
            {
                material = new Material(lit) { name = "mat_apron_mud" };
                created = true;
            }

            material.shader = lit;

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath);
            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);

            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 1.2f);
            }

            // Kiremitleme genis: apron 18 m'lik bir serit ve tek kiremit onu bir
            // posterde gerilmis fotografa cevirirdi.
            material.SetTextureScale("_BaseMap", new Vector2(14f, 14f));
            material.SetTextureScale("_BumpMap", new Vector2(14f, 14f));

            // ISLAKLIK: yuksek parlaklik + sifir metaliklik. Camurun kendisi mat,
            // uzerindeki su filmi parlak - ikisinin toplami bu. Kuru toprak
            // isteseydik smoothness 0.05 olurdu; yagmurun altinda oyle bir zemin
            // "yagmur yagiyor ama yer kuru" diye okunur ve sahneyi yalanci yapar.
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.62f);
            material.SetFloat("_Metallic", 0f);

            if (created) AssetDatabase.CreateAsset(material, MaterialPath);
            else EditorUtility.SetDirty(material);

            return material;
        }

        private static void EnsureTextures()
        {
            if (File.Exists(BasePath) && File.Exists(NormalPath)) return;

            float[,] height = BuildHeight();

            WritePng(BasePath, BuildBaseColor(height), normalMap: false);
            WritePng(NormalPath, BuildNormal(height), normalMap: true);

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Katmanlı Perlin yükseklik alanı. <b>Kenarları sarmalı (tileable):</b> zemin
        /// on dört kez kiremitleniyor ve dikiş çizgileri bir ızgara gibi okunur — çamur
        /// için en yanlış şey.
        /// </summary>
        private static float[,] BuildHeight()
        {
            var height = new float[Size, Size];

            // Dort oktav: buyuk cukurlar, orta tumsekler, ince tane.
            float[] frequencies = { 3f, 7f, 17f, 41f };
            float[] weights = { 0.50f, 0.28f, 0.15f, 0.07f };

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;

                    for (int o = 0; o < frequencies.Length; o++)
                    {
                        sum += weights[o] * TileablePerlin(x, y, frequencies[o]);
                    }

                    height[x, y] = Mathf.Clamp01(sum);
                }
            }

            return height;
        }

        /// <summary>
        /// Kenarları sarmalayan Perlin. Dokunun karşılıklı kenarlarındaki değerleri
        /// birbirine karıştırarak dikişi yok eder — <c>Mathf.PerlinNoise</c> tek başına
        /// sarmalamaz.
        /// </summary>
        private static float TileablePerlin(int x, int y, float frequency)
        {
            float u = x / (float)Size;
            float v = y / (float)Size;

            float a = Mathf.PerlinNoise(u * frequency, v * frequency);
            float b = Mathf.PerlinNoise((u - 1f) * frequency, v * frequency);
            float c = Mathf.PerlinNoise(u * frequency, (v - 1f) * frequency);
            float d = Mathf.PerlinNoise((u - 1f) * frequency, (v - 1f) * frequency);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>
        /// Yükseklikten renk. <b>Çukurlar koyu ve doygun, tümsekler açık ve gri:</b>
        /// su çukurda birikir, tümsek kurur. Bu tek kural çamura derinlik veriyor.
        /// </summary>
        private static Texture2D BuildBaseColor(float[,] height)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var pixels = new Color32[Size * Size];

            var wet = new Color(0.13f, 0.10f, 0.07f);   // birikinti
            var dry = new Color(0.34f, 0.29f, 0.23f);   // kurumus tumsek

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float h = height[x, y];

                    // SmoothStep: gecisin ortasini keskinlestirir, boylece
                    // birikintilerin kenari belli olur. Duz Lerp her seyi ayni tonda
                    // bir bulaniklige cevirirdi.
                    Color color = Color.Lerp(wet, dry, Mathf.SmoothStep(0f, 1f, h));

                    pixels[y * Size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Yükseklikten normal harita (Sobel eğimi).</summary>
        private static Texture2D BuildNormal(float[,] height)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var pixels = new Color32[Size * Size];

            const float strength = 6f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Sarmalayan komsuluk: doku kiremitlendigi icin kenarda da
                    // gecerli bir egim gerekiyor.
                    float left = height[(x - 1 + Size) % Size, y];
                    float right = height[(x + 1) % Size, y];
                    float down = height[x, (y - 1 + Size) % Size];
                    float up = height[x, (y + 1) % Size];

                    var normal = new Vector3((left - right) * strength,
                                             (down - up) * strength,
                                             1f).normalized;

                    pixels[y * Size + x] = new Color32(
                        (byte)((normal.x * 0.5f + 0.5f) * 255f),
                        (byte)((normal.y * 0.5f + 0.5f) * 255f),
                        (byte)((normal.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void WritePng(string path, Texture2D texture, bool normalMap)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            // Normal harita OLARAK isaretlenmeli: duz bir renk dokusu gibi ice
            // aktarilirsa Unity onu sRGB'ye cevirir ve egimler yanlis okunur - yuzey
            // "kabarik ama yanlis yonde" gorunur.
            importer.textureType = normalMap ? TextureImporterType.NormalMap
                                             : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;

            importer.SaveAndReimport();
        }
    }
}
