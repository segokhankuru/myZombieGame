using System;
using System.Collections.Generic;
using System.IO;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Config;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor.ConfigTools
{
    /// <summary>
    /// <c>config/content/cards.json</c> → <c>Assets/_Project/Config/cards.asset</c>.
    ///
    /// <para><b>Yön tek:</b> JSON kaynak, varlık çıktı. Üretilen varlığı elle
    /// düzenlemek boru hattını bozar — JSON ile motor birbirine ters şeyler söyler ve
    /// oyunun hangisini oynadığını kimse bilmez (config-protocol.md).</para>
    ///
    /// <para><b>Bilinmeyen etiket ya da stat HATADIR, uyarı değil.</b> Şema bir
    /// sözleşme: "Yikim" diye yazılmış bir etiket sessizce <c>Ballistics</c>'e
    /// düşseydi, kart yanlış havuzda çıkar ve kimse fark etmezdi.</para>
    /// </summary>
    public static class CardCatalogImporter
    {
        private const string SourcePath = "config/content/cards.json";
        private const string AssetPath = "Assets/_Project/Config/cards.asset";

        [MenuItem("Bunker/Config/Kartlari Ice Aktar", false, 21)]
        public static void ImportMenu() => Import();

        public static bool Import()
        {
            string root = Directory.GetCurrentDirectory();
            string source = Path.Combine(root, SourcePath);

            if (!File.Exists(source))
            {
                Debug.LogError($"[Kart] Kaynak yok: {SourcePath}");
                return false;
            }

            JsonValue json;

            try
            {
                json = JsonValue.Parse(File.ReadAllText(source));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Kart] {SourcePath} ayristirilamadi: {e.Message}");
                return false;
            }

            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<CardCatalogAsset.Entry>();

            JsonValue array = json["cards"];

            if (!array.IsArray)
            {
                Debug.LogError("[Kart] 'cards' bir dizi degil.");
                return false;
            }

            for (int i = 0; i < array.Items.Count; i++)
            {
                JsonValue c = array.Items[i];
                string id = c["id"].IsString ? c["id"].AsString : null;

                if (string.IsNullOrEmpty(id))
                {
                    errors.Add($"cards[{i}]: 'id' yok. Id kalicidir ve bos olamaz.");
                    continue;
                }

                if (!ids.Add(id))
                {
                    // Ayni id iki kez: ikinci kart sessizce birincisini golgelerdi ve
                    // havuz sayisi yalan soylerdi.
                    errors.Add($"cards[{i}]: '{id}' id'si tekrar ediyor.");
                    continue;
                }

                if (!TryParse(c["tag"], out CardTag tag))
                {
                    errors.Add($"{id}: bilinmeyen etiket '{Text(c["tag"])}'. " +
                               $"Gecerli: {string.Join(", ", Enum.GetNames(typeof(CardTag)))}");
                    continue;
                }

                if (!TryParse(c["stat"], out CardStat stat))
                {
                    errors.Add($"{id}: bilinmeyen stat '{Text(c["stat"])}'. " +
                               $"Gecerli: {string.Join(", ", Enum.GetNames(typeof(CardStat)))}");
                    continue;
                }

                if (stat == CardStat.None)
                {
                    errors.Add($"{id}: stat 'None'. Hicbir seye dokunmayan bir kart, " +
                               "oyuncunun secip hicbir sey hissetmedigi karttir.");
                    continue;
                }

                if (!c["value"].IsNumber)
                {
                    errors.Add($"{id}: 'value' bir sayi degil.");
                    continue;
                }

                entries.Add(new CardCatalogAsset.Entry
                {
                    id = id,
                    displayName = c["name"].IsString ? c["name"].AsString : id,
                    text = c["text"].IsString ? c["text"].AsString : string.Empty,
                    tag = tag,
                    stat = stat,
                    value = (float)c["value"].AsNumber,
                    // Takim kartlari solo'da CIKMAZ (SYS-02 §6). Sema gereksiz
                    // tekrardan kacinsin diye etiketten turetiliyor; istisna gerekirse
                    // JSON'a acik alan eklenir.
                    soloValid = c.Has("soloValid") ? c["soloValid"].AsBool : tag != CardTag.Team,
                    coopValid = c.Has("coopValid") ? c["coopValid"].AsBool : true
                });
            }

            if (errors.Count > 0)
            {
                foreach (string e in errors) Debug.LogError($"[Kart] {e}");
                Debug.LogError($"[Kart] ICE AKTARMA BASARISIZ - {errors.Count} hata. " +
                               "Varlik DEGISTIRILMEDI.");
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<CardCatalogAsset>(AssetPath);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CardCatalogAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            int version = json["version"].IsNumber ? (int)json["version"].AsNumber : 1;

            asset.Fill(version, entries);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Kart] {entries.Count} kart ice aktarildi -> {AssetPath}\n" +
                      $"  {Summary(entries)}");

            return true;
        }

        /// <summary>Başsız giriş: <c>-executeMethod</c> için.</summary>
        public static void ImportBatch() => EditorApplication.Exit(Import() ? 0 : 1);

        private static string Summary(List<CardCatalogAsset.Entry> entries)
        {
            var counts = new Dictionary<CardTag, int>();

            foreach (CardCatalogAsset.Entry e in entries)
            {
                counts.TryGetValue(e.tag, out int n);
                counts[e.tag] = n + 1;
            }

            var parts = new List<string>();
            foreach (KeyValuePair<CardTag, int> kv in counts) parts.Add($"{kv.Key} {kv.Value}");

            return string.Join(", ", parts);
        }

        private static bool TryParse<T>(JsonValue value, out T result) where T : struct
        {
            result = default;
            return value.IsString && Enum.TryParse(value.AsString, out result);
        }

        private static string Text(JsonValue v) => v.IsString ? v.AsString : "(yok)";
    }
}
