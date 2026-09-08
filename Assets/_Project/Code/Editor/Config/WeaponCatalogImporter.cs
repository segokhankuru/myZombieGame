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
    /// <c>config/content/weapons.json</c> → <c>Assets/_Project/Config/weapons.asset</c>.
    ///
    /// <para><b>Yön tek:</b> JSON kaynak, varlık çıktı. Üretilen varlığı elle düzenlemek
    /// boru hattını bozar (config-protocol.md). Kart içe aktarıcısıyla aynı desen.</para>
    ///
    /// <para><b>Eksik alan HATADIR, uyarı değil.</b> Bir silahın atış hızı eksik
    /// kalırsa sessizce sıfır olur ve silah hiç ateş etmez — teşhisi bir oyun testi
    /// süren hata türü.</para>
    /// </summary>
    public static class WeaponCatalogImporter
    {
        private const string SourcePath = "config/content/weapons.json";
        private const string AssetPath = "Assets/_Project/Config/weapons.asset";

        [MenuItem("Bunker/Config/Silahlari Ice Aktar", false, 22)]
        public static void ImportMenu() => Import();

        /// <summary>Başsız giriş: <c>-executeMethod</c> için.</summary>
        public static void ImportBatch() => EditorApplication.Exit(Import() ? 0 : 1);

        public static bool Import()
        {
            string root = Directory.GetCurrentDirectory();
            string source = Path.Combine(root, SourcePath);

            if (!File.Exists(source))
            {
                Debug.LogError($"[Silah] Kaynak yok: {SourcePath}");
                return false;
            }

            JsonValue json;

            try
            {
                json = JsonValue.Parse(File.ReadAllText(source));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Silah] {SourcePath} ayristirilamadi: {e.Message}");
                return false;
            }

            JsonValue array = json["weapons"];

            if (!array.IsArray)
            {
                Debug.LogError("[Silah] 'weapons' bir dizi degil.");
                return false;
            }

            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<WeaponCatalogAsset.Entry>();

            for (int i = 0; i < array.Items.Count; i++)
            {
                JsonValue w = array.Items[i];
                string id = w["id"].IsString ? w["id"].AsString : null;

                if (string.IsNullOrEmpty(id))
                {
                    errors.Add($"weapons[{i}]: 'id' yok. Id kalicidir ve bos olamaz.");
                    continue;
                }

                if (!ids.Add(id))
                {
                    errors.Add($"weapons[{i}]: '{id}' id'si tekrar ediyor.");
                    continue;
                }

                var entry = new WeaponCatalogAsset.Entry
                {
                    id = id,
                    displayName = w["name"].IsString ? w["name"].AsString : id,
                    text = w["text"].IsString ? w["text"].AsString : string.Empty
                };

                // Her sayi ZORUNLU: eksik bir alanin sessizce sifir olmasi, hic ates
                // etmeyen ya da hic hasar vermeyen bir silah demektir.
                if (!Number(w, "damage", id, errors, out entry.damage)) continue;
                if (!Number(w, "roundsPerMinute", id, errors, out entry.roundsPerMinute)) continue;
                if (!Number(w, "headshotMultiplier", id, errors, out entry.headshotMultiplier)) continue;
                if (!Number(w, "rangeMeters", id, errors, out entry.rangeMeters)) continue;
                if (!Number(w, "spreadDegrees", id, errors, out entry.spreadDegrees)) continue;
                if (!Integer(w, "pelletCount", id, errors, out entry.pelletCount)) continue;
                if (!Integer(w, "magazineCapacity", id, errors, out entry.magazineCapacity)) continue;
                if (!Integer(w, "reserveCapacity", id, errors, out entry.reserveCapacity)) continue;
                if (!Integer(w, "startingReserve", id, errors, out entry.startingReserve)) continue;
                if (!Number(w, "reloadSeconds", id, errors, out entry.reloadSeconds)) continue;
                if (!Number(w, "recoilPitchPerShot", id, errors, out entry.recoilPitchPerShot)) continue;
                if (!Number(w, "recoilYawPerShot", id, errors, out entry.recoilYawPerShot)) continue;
                if (!Number(w, "recoilRecoveryPerSecond", id, errors, out entry.recoilRecoveryPerSecond)) continue;
                if (!Number(w, "recoilMaxPitch", id, errors, out entry.recoilMaxPitch)) continue;
                if (!Integer(w, "price", id, errors, out entry.price)) continue;
                if (!Integer(w, "ammoPrice", id, errors, out entry.ammoPrice)) continue;

                // ISTEGE BAGLI ve varsayilani false: dolumun mermi mermi ilerlemesi
                // istisnadir, kural degil. Zorunlu yapmak, dort silahin ucune anlamsiz
                // bir "false" satiri yazdirmak olurdu.
                entry.reloadPerShell = w["reloadPerShell"].Kind == JsonKind.Bool &&
                                       w["reloadPerShell"].AsBool;

                entries.Add(entry);
            }

            if (errors.Count > 0)
            {
                foreach (string e in errors) Debug.LogError($"[Silah] {e}");
                Debug.LogError($"[Silah] ICE AKTARMA BASARISIZ - {errors.Count} hata. " +
                               "Varlik DEGISTIRILMEDI.");
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(AssetPath);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            int version = json["version"].IsNumber ? (int)json["version"].AsNumber : 1;

            asset.Fill(version, entries);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Silah] {entries.Count} silah ice aktarildi -> {AssetPath}");
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
