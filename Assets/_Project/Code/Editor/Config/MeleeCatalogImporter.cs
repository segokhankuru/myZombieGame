using System;
using System.Collections.Generic;
using System.IO;
using Bunker.Config;
using Bunker.Systems.Config;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor.ConfigTools
{
    /// <summary>
    /// <c>config/content/melee.json</c> → <c>Assets/_Project/Config/melee.asset</c>.
    /// 2026-09-08.
    ///
    /// <para><b>Yön tek:</b> JSON kaynak, varlık çıktı (config-protocol.md). Silah
    /// kataloğu içe aktarıcısıyla birebir aynı desen — ikinci bir kalıp icat etmek,
    /// birini düzelten kişinin diğerini unutması demekti.</para>
    ///
    /// <para><b>Eksik alan HATADIR.</b> Sessizce sıfır olan bir çarpan, hiç hasar
    /// vermeyen bir bıçak demektir; teşhisi bir oyun testi süren hata türü.</para>
    /// </summary>
    public static class MeleeCatalogImporter
    {
        private const string SourcePath = "config/content/melee.json";
        private const string AssetPath = "Assets/_Project/Config/melee.asset";

        [MenuItem("Bunker/Config/Yakin Dovus Ice Aktar", false, 23)]
        public static void ImportMenu() => Import();

        /// <summary>Başsız giriş: <c>-executeMethod</c> için.</summary>
        public static void ImportBatch() => EditorApplication.Exit(Import() ? 0 : 1);

        public static bool Import()
        {
            string root = Directory.GetCurrentDirectory();
            string source = Path.Combine(root, SourcePath);

            if (!File.Exists(source))
            {
                Debug.LogError($"[Yakin dovus] Kaynak yok: {SourcePath}");
                return false;
            }

            JsonValue json;

            try
            {
                json = JsonValue.Parse(File.ReadAllText(source));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Yakin dovus] {SourcePath} ayristirilamadi: {e.Message}");
                return false;
            }

            JsonValue array = json["melee"];

            if (!array.IsArray)
            {
                Debug.LogError("[Yakin dovus] 'melee' bir dizi degil.");
                return false;
            }

            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<MeleeCatalogAsset.Entry>();

            for (int i = 0; i < array.Items.Count; i++)
            {
                JsonValue m = array.Items[i];
                string id = m["id"].IsString ? m["id"].AsString : null;

                if (string.IsNullOrEmpty(id))
                {
                    errors.Add($"melee[{i}]: 'id' yok. Id kalicidir ve bos olamaz.");
                    continue;
                }

                if (!ids.Add(id))
                {
                    errors.Add($"melee[{i}]: '{id}' id'si tekrar ediyor.");
                    continue;
                }

                var entry = new MeleeCatalogAsset.Entry
                {
                    id = id,
                    displayName = m["name"].IsString ? m["name"].AsString : id,
                    text = m["text"].IsString ? m["text"].AsString : string.Empty
                };

                if (!Number(m, "damageMultiplier", id, errors, out entry.damageMultiplier)) continue;
                if (!Number(m, "swingTimeMultiplier", id, errors, out entry.swingTimeMultiplier)) continue;
                if (!Number(m, "rangeMultiplier", id, errors, out entry.rangeMultiplier)) continue;
                if (!Integer(m, "price", id, errors, out entry.price)) continue;

                entries.Add(entry);
            }

            // BASLANGIC SILAHI SART: fiyati sifir olan tam bir satir olmali. Hicbiri
            // bedava degilse oyuncu run'a bicaksiz baslar ve V tusu sessizce hicbir
            // sey yapmaz - sebebi bulunmasi en zor hata turu.
            if (errors.Count == 0)
            {
                bool hasStarter = false;
                foreach (MeleeCatalogAsset.Entry e in entries)
                {
                    if (e.price <= 0) { hasStarter = true; break; }
                }

                if (!hasStarter)
                {
                    errors.Add("Fiyati 0 olan bir baslangic silahi yok - oyuncu run'a " +
                               "bicaksiz baslardi.");
                }
            }

            if (errors.Count > 0)
            {
                foreach (string e in errors) Debug.LogError($"[Yakin dovus] {e}");
                Debug.LogError($"[Yakin dovus] ICE AKTARMA BASARISIZ - {errors.Count} hata. " +
                               "Varlik DEGISTIRILMEDI.");
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<MeleeCatalogAsset>(AssetPath);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MeleeCatalogAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            int version = json["version"].IsNumber ? (int)json["version"].AsNumber : 1;

            asset.Fill(version, entries);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Yakin dovus] {entries.Count} silah ice aktarildi -> {AssetPath}");
            return true;
        }

        private static bool Number(JsonValue owner, string key, string id,
                                   List<string> errors, out float value)
        {
            if (!owner[key].IsNumber)
            {
                errors.Add($"{id}: '{key}' eksik ya da sayi degil.");
                value = 0f;
                return false;
            }

            value = (float)owner[key].AsNumber;
            return true;
        }

        private static bool Integer(JsonValue owner, string key, string id,
                                    List<string> errors, out int value)
        {
            if (!Number(owner, key, id, errors, out float number))
            {
                value = 0;
                return false;
            }

            value = Mathf.RoundToInt(number);
            return true;
        }
    }
}
