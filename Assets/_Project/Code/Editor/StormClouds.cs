using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// <b>Kara bulut</b> dokusunu ve malzemesini üretir. 2026-09-08.
    ///
    /// <para><b>Neden üretiliyor, bir pakete bakılmıyor</b> (<see cref="MudSurface"/>
    /// ile birebir aynı gerekçe): projedeki paketlerin hiçbirinde bulut dokusu yok.
    /// Katmanlı Perlin, on kilobaytlık bir PNG'de tam olarak istediğimiz şeyi verir —
    /// <i>düzensiz yoğunluk</i>. Bir bulutun tek talebi budur.</para>
    ///
    /// <para><b>Alfa kanal asıl iş:</b> renk her yerde aynı koyu gri; değişen şey
    /// <b>saydamlık</b>. Böylece bulut "gökyüzünün önünde bir tabaka" gibi okunur, düz
    /// boyanmış bir tavan gibi değil — ve iki katman üst üste kayarken aralarında
    /// sürekli değişen bir yoğunluk çıkar.</para>
    ///
    /// <para><b>Idempotent</b> (editor-tools.md): dosya varsa yeniden üretilmez.</para>
    /// </summary>
    public static class StormClouds
    {
        private const string Folder = "Assets/_Project/Art/Materials/Surfaces";
        private const string TexturePath = Folder + "/tex_storm_clouds.png";
        private const string MaterialPath = Folder + "/mat_storm_clouds.mat";

        private const int Size = 512;

        /// <summary>Bulut malzemesini garantiler; yoksa dokusuyla birlikte üretir.</summary>
        public static Material EnsureMaterial()
        {
            ArtIntegration.EnsureFolder(Folder);
            EnsureTexture();

            // UNLIT ve SAYDAM: buluttan gölge almak ya da vermek, bir gri kutunun
            // ödeyeceği bir bedel değil - ve karanlık bir sahnede ışık alan bir bulut
            // tamamen siyaha düşer, yani hiç görünmez.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            bool created = false;
            if (material == null)
            {
                material = new Material(shader) { name = "mat_storm_clouds" };
                created = true;
            }

            material.shader = shader;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture != null) material.SetTexture("_BaseMap", texture);

            // Saydam kip: URP ailesinde _Surface 1 = Transparent, _Blend 0 = Alpha.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = 3000;

            // Kurşuni ve KOYU: gotik olan sey rengin kendisi degil, gokyuzunun
            // kapali olmasi. Alfa dokudan geliyor; buradaki alfa genel yogunluk.
            material.SetColor("_BaseColor", new Color(0.16f, 0.17f, 0.20f, 0.88f));

            if (created) AssetDatabase.CreateAsset(material, MaterialPath);
            else EditorUtility.SetDirty(material);

            return material;
        }

        private static void EnsureTexture()
        {
            if (File.Exists(TexturePath)) return;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            // Uc oktav: buyuk kutleler, orta yirtiklar, ince tane. Camurdaki dort
            // oktavdan bir eksik - bulutun ince tanesi zaten uzakta gorunmez ve bir
            // oktav daha yalnizca dosya boyutu demek.
            float[] frequencies = { 2f, 5f, 11f };
            float[] weights = { 0.55f, 0.30f, 0.15f };

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;

                    for (int o = 0; o < frequencies.Length; o++)
                    {
                        sum += weights[o] * TileablePerlin(x, y, frequencies[o]);
                    }

                    // Kontrast: SmoothStep bulut kutlelerinin kenarini belirginlestirir.
                    // Duz gurultu, her yeri ayni yogunlukta bir pus yapardi - "kapali
                    // hava" degil "kirli cam" okunurdu.
                    float density = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sum - 0.32f) * 1.9f));

                    // Renk yerine ALFA degisir. Yirtiklardan gokyuzunun kendisi
                    // gorunur; delik olmayan bir bulut tavani bir plakadir.
                    byte alpha = (byte)(Mathf.Lerp(0.18f, 1f, density) * 255f);

                    // Kutlelerin ICI daha koyu: isik bulutun kalin yerine giremez.
                    byte value = (byte)(Mathf.Lerp(0.62f, 0.28f, density) * 255f);

                    pixels[y * Size + x] = new Color32(value, value, (byte)(value + 8), alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Kenarları sarmalayan Perlin — <see cref="MudSurface"/>'takinin aynısı.
        /// Bulut tavanı dokuz kez kiremitleniyor ve dikiş çizgileri gökyüzünde bir
        /// ızgara gibi okunur.
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
    }
}
