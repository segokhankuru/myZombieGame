using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor.ConfigTools
{
    /// <summary>
    /// `config/` altındaki JSON'dan C# ve ScriptableObject üretir. Tuning katmanının
    /// tek yazarı budur.
    ///
    /// <code>
    /// config/schema/&lt;alan&gt;.json   sekil + aralik + tasarim gerekcesi
    /// config/balance/&lt;alan&gt;.json  sayilar
    ///          |  ConfigImporter
    ///          v
    /// Code/Systems/Config/Generated/&lt;Alan&gt;Config.g.cs      saf C#, testlerin kullandigi
    /// Code/Config/Generated/&lt;Alan&gt;ConfigAsset.g.cs         ScriptableObject sinifi
    /// Assets/_Project/Config/&lt;alan&gt;.asset                  calisma aninda okunan varlik
    /// </code>
    ///
    /// <para><b>Yön tek yönlüdür.</b> JSON kaynaktır; üretilen her şey çıktıdır ve
    /// elle düzenlenmez. Üretilen bir `.asset` elle değiştirilirse JSON ile motor
    /// birbirine yalan söylemeye başlar ve oyunun hangi sayıyla koştuğunu kimse bilemez
    /// (config-protocol.md).</para>
    ///
    /// <para><b>İki geçiş gerekir</b> ve bu gizlenmedi: birinci geçiş C# üretir, Unity
    /// onu derler, ikinci geçiş varlıkları doldurur. Yeni üretilmiş bir tip, onu üreten
    /// alan yüklemesi içinde var olamaz — bunu bir arka plan durum makinesiyle saklamak,
    /// yarım kalmış bir içe aktarmayı teşhis edilemez hâle getirirdi.</para>
    /// </summary>
    public static class ConfigImporter
    {
        private const string SchemaFolder = "config/schema";
        private const string BalanceFolder = "config/balance";
        private const string RuntimeCodeFolder = "Assets/_Project/Code/Systems/Config/Generated";
        private const string AssetCodeFolder = "Assets/_Project/Code/Config/Generated";
        private const string AssetFolder = "Assets/_Project/Config";
        private const string RuntimeNamespace = "Bunker.Systems.Config";
        private const string AssetNamespace = "Bunker.Config";

        // ---------------------------------------------------------------- menu

        [MenuItem("Bunker/Config/Ice Aktar (kod + varlik)", false, 300)]
        public static void ImportFromMenu()
        {
            if (!GenerateCode(out int domains)) return;

            AssetDatabase.Refresh();

            // Editor'de derleme bittikten sonra ikinci gecis kendiliginden kosar.
            EditorPrefs.SetBool(PendingKey, true);

            Debug.Log($"[Config] {domains} alan icin kod uretildi. Derleme bitince " +
                      "varliklar otomatik dolduruluyor...");
        }

        [MenuItem("Bunker/Config/Varliklari Doldur", false, 301)]
        public static void FillFromMenu() => FillAssets();

        private const string PendingKey = "Bunker.ConfigImporter.Pending";

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void AfterReload()
        {
            if (!EditorPrefs.GetBool(PendingKey, false)) return;
            EditorPrefs.SetBool(PendingKey, false);

            EditorApplication.delayCall += () => FillAssets();
        }

        // ---------------------------------------------------------------- komut satiri

        /// <summary>Birinci geçiş: kod üret. Hata varsa sıfırdan farklı çıkar.</summary>
        public static void GenerateCodeBatch()
        {
            bool ok = GenerateCode(out int domains);
            Debug.Log($"[Config] Kod uretimi: {(ok ? "TAMAM" : "HATALI")} ({domains} alan).");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>İkinci geçiş: üretilmiş tiplerle varlıkları doldur.</summary>
        public static void FillAssetsBatch()
        {
            bool ok = FillAssets();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // ---------------------------------------------------------------- 1. gecis

        public static bool GenerateCode(out int domainCount)
        {
            domainCount = 0;

            string root = Directory.GetCurrentDirectory();
            string schemaDir = Path.Combine(root, SchemaFolder);

            if (!Directory.Exists(schemaDir))
            {
                Debug.LogError($"[Config] Sema klasoru yok: {SchemaFolder}");
                return false;
            }

            Directory.CreateDirectory(Path.Combine(root, RuntimeCodeFolder));
            Directory.CreateDirectory(Path.Combine(root, AssetCodeFolder));

            var errors = new List<string>();
            var generated = new List<string>();

            foreach (string schemaPath in Directory.GetFiles(schemaDir, "*.schema.json"))
            {
                string domain = Path.GetFileName(schemaPath).Replace(".schema.json", string.Empty);
                string balancePath = Path.Combine(root, BalanceFolder, domain + ".json");

                if (!File.Exists(balancePath))
                {
                    errors.Add($"{domain}: semasi var ama denge dosyasi yok " +
                               $"({BalanceFolder}/{domain}.json). Sema tek basina oyuna " +
                               "hicbir sey vermez.");
                    continue;
                }

                ConfigDomain schema;

                try
                {
                    schema = ConfigDomain.Load(domain, File.ReadAllText(schemaPath));
                }
                catch (Exception e)
                {
                    errors.Add($"{domain}: sema okunamadi - {e.Message}");
                    continue;
                }

                var domainErrors = new List<string>();
                Dictionary<string, double> values;

                try
                {
                    values = schema.ReadValues(File.ReadAllText(balancePath), domainErrors);
                }
                catch (Exception e)
                {
                    errors.Add($"{domain}: denge dosyasi okunamadi - {e.Message}");
                    continue;
                }

                if (domainErrors.Count > 0)
                {
                    errors.AddRange(domainErrors);
                    continue;
                }

                WriteIfChanged(Path.Combine(root, RuntimeCodeFolder, schema.ClassName + ".g.cs"),
                               BuildRuntimeClass(schema, values, domain));

                WriteIfChanged(Path.Combine(root, AssetCodeFolder, schema.AssetClassName + ".g.cs"),
                               BuildAssetClass(schema, values, domain));

                generated.Add(schema.ClassName);
                domainCount++;
            }

            if (errors.Count > 0)
            {
                var sb = new StringBuilder("[Config] ICE AKTARMA BASARISIZ:\n");
                foreach (string e in errors) sb.Append("  ").Append(e).Append('\n');
                sb.Append("  Hicbir dosya yazilmadi olabilir - yukaridaki her satir duzeltilmeli.");
                Debug.LogError(sb.ToString());
                return false;
            }

            Debug.Log($"[Config] Kod uretildi: {string.Join(", ", generated)}");
            return true;
        }

        /// <summary>
        /// Aynı içeriği tekrar yazmaz. Değişmemiş bir dosyayı kirletmek gereksiz bir
        /// derleme turu ve boş bir diff demektir (editor-tools.md).
        /// </summary>
        private static void WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content) return;
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        // ---------------------------------------------------------------- saf C# sinifi

        private static string BuildRuntimeClass(
            ConfigDomain schema, Dictionary<string, double> values, string domain)
        {
            var sb = new StringBuilder(4096);

            Header(sb, domain);
            sb.Append("namespace ").Append(RuntimeNamespace).Append("\n{\n");

            sb.Append("    /// <summary>\n")
              .Append("    /// <c>config/balance/").Append(domain).Append(".json</c> dosyasinin\n")
              .Append("    /// calisma ani karsiligi. Salt okunur ve Unity'ye bagimli degildir -\n")
              .Append("    /// testler bunu sahne acmadan kurar.\n")
              .Append("    /// </summary>\n");

            sb.Append("    public sealed class ").Append(schema.ClassName).Append("\n    {\n");

            foreach (ConfigProperty p in schema.Properties)
            {
                Doc(sb, p, "        ");
                sb.Append("        public readonly ").Append(p.TypeName).Append(' ')
                  .Append(p.CSharpName).Append(";\n\n");
            }

            sb.Append("        /// <summary>\n")
              .Append("        /// Varsayilanlar <c>config/balance/").Append(domain)
              .Append(".json</c> dosyasindan URETILDI.\n")
              .Append("        /// Testler yalnizca ilgilendikleri alani gecer.\n")
              .Append("        /// </summary>\n");

            sb.Append("        public ").Append(schema.ClassName).Append("(\n");

            for (int i = 0; i < schema.Properties.Count; i++)
            {
                ConfigProperty p = schema.Properties[i];
                sb.Append("            ").Append(p.TypeName).Append(' ').Append(p.ParamName)
                  .Append(" = ").Append(DefaultLiteral(p, values))
                  .Append(i == schema.Properties.Count - 1 ? ")\n" : ",\n");
            }

            sb.Append("        {\n");

            foreach (ConfigProperty p in schema.Properties)
            {
                sb.Append("            ").Append(p.CSharpName).Append(" = ")
                  .Append(p.ParamName).Append(";\n");
            }

            sb.Append("        }\n    }\n}\n");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- ScriptableObject

        private static string BuildAssetClass(
            ConfigDomain schema, Dictionary<string, double> values, string domain)
        {
            var sb = new StringBuilder(4096);

            Header(sb, domain);
            sb.Append("using ").Append(RuntimeNamespace).Append(";\n")
              .Append("using UnityEngine;\n\n");

            sb.Append("namespace ").Append(AssetNamespace).Append("\n{\n");

            sb.Append("    /// <summary>\n")
              .Append("    /// <c>").Append(domain).Append(".json</c> icin calisma ani varligi.\n")
              .Append("    /// Inspector'da gorunen araliklar semadaki tasarim niyetidir.\n")
              .Append("    /// <b>Elle duzenlenmez</b> - bir sonraki ice aktarma uzerine yazar.\n")
              .Append("    /// </summary>\n");

            sb.Append("    public sealed class ").Append(schema.AssetClassName)
              .Append(" : ScriptableObject\n    {\n");

            string lastGroup = null;

            foreach (ConfigProperty p in schema.Properties)
            {
                if (p.Group != lastGroup)
                {
                    lastGroup = p.Group;
                    if (!string.IsNullOrEmpty(p.Group))
                    {
                        sb.Append("        [Header(").Append(ConfigDomain.Quote(p.Group)).Append(")]\n");
                    }
                }

                if (!string.IsNullOrEmpty(p.Description))
                {
                    sb.Append("        [Tooltip(").Append(ConfigDomain.Quote(p.Description)).Append(")]\n");
                }

                if (p.Minimum.HasValue && p.Maximum.HasValue)
                {
                    sb.Append("        [Range(")
                      .Append(RangeLiteral(p, p.Minimum.Value)).Append(", ")
                      .Append(RangeLiteral(p, p.Maximum.Value)).Append(")]\n");
                }

                sb.Append("        [SerializeField] private ").Append(p.TypeName).Append(' ')
                  .Append(p.FieldName).Append(" = ").Append(DefaultLiteral(p, values)).Append(";\n\n");
            }

            sb.Append("        /// <summary>Saf C# karsiligini uretir. Boot bunu bir kez cagirir.</summary>\n");
            sb.Append("        public ").Append(schema.ClassName).Append(" ToRuntime()\n        {\n");
            sb.Append("            return new ").Append(schema.ClassName).Append("(\n");

            for (int i = 0; i < schema.Properties.Count; i++)
            {
                sb.Append("                ").Append(schema.Properties[i].FieldName)
                  .Append(i == schema.Properties.Count - 1 ? ");\n" : ",\n");
            }

            sb.Append("        }\n    }\n}\n");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 2. gecis

        /// <summary>
        /// Üretilmiş ScriptableObject varlıklarını denge JSON'undan doldurur.
        /// Tipler derlenmiş olmalı — yoksa hangi alanın eksik olduğunu söyler.
        /// </summary>
        public static bool FillAssets()
        {
            string root = Directory.GetCurrentDirectory();
            string schemaDir = Path.Combine(root, SchemaFolder);

            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                Directory.CreateDirectory(Path.Combine(root, AssetFolder));
                AssetDatabase.Refresh();
            }

            var errors = new List<string>();
            var filled = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string schemaPath in Directory.GetFiles(schemaDir, "*.schema.json"))
                {
                    string domain = Path.GetFileName(schemaPath).Replace(".schema.json", string.Empty);
                    string balancePath = Path.Combine(root, BalanceFolder, domain + ".json");

                    if (!File.Exists(balancePath)) continue;

                    ConfigDomain schema = ConfigDomain.Load(domain, File.ReadAllText(schemaPath));
                    var domainErrors = new List<string>();
                    Dictionary<string, double> values =
                        schema.ReadValues(File.ReadAllText(balancePath), domainErrors);

                    if (domainErrors.Count > 0) { errors.AddRange(domainErrors); continue; }

                    Type type = FindType($"{AssetNamespace}.{schema.AssetClassName}");

                    if (type == null)
                    {
                        errors.Add($"{schema.AssetClassName} tipi bulunamadi. Once " +
                                   "'Bunker/Config/Ice Aktar' ile kod uretilmeli ve " +
                                   "Unity derlemeyi bitirmeli.");
                        continue;
                    }

                    if (FillOne(type, schema, values, domain, errors)) filled.Add(domain);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            if (errors.Count > 0)
            {
                var sb = new StringBuilder("[Config] VARLIK DOLDURMA BASARISIZ:\n");
                foreach (string e in errors) sb.Append("  ").Append(e).Append('\n');
                Debug.LogError(sb.ToString());
                return false;
            }

            Debug.Log($"[Config] Varliklar dolduruldu: {string.Join(", ", filled)} " +
                      $"({AssetFolder})");
            return true;
        }

        private static bool FillOne(Type type, ConfigDomain schema,
                                    Dictionary<string, double> values, string domain,
                                    List<string> errors)
        {
            string path = $"{AssetFolder}/{domain}.asset";
            var asset = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
            }

            var serialized = new SerializedObject(asset);

            foreach (ConfigProperty p in schema.Properties)
            {
                SerializedProperty field = serialized.FindProperty(p.FieldName);

                if (field == null)
                {
                    errors.Add($"{domain}: '{p.FieldName}' alani {type.Name} uzerinde yok. " +
                               "Uretilen kod guncel degil - once kod uretimini calistir.");
                    continue;
                }

                if (!values.TryGetValue(p.JsonPath, out double value)) continue;

                if (p.IsInteger) field.intValue = (int)Math.Round(value);
                else field.floatValue = (float)value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return true;
        }

        // ---------------------------------------------------------------- yardimcilar

        private static void Header(StringBuilder sb, string domain)
        {
            sb.Append("// <auto-generated>\n")
              .Append("//     Bu dosya URETILDI. Elle degistirme - bir sonraki ice aktarmada\n")
              .Append("//     uzerine yazilir ve degisiklik sessizce kaybolur.\n")
              .Append("//\n")
              .Append("//     Kaynak : config/schema/").Append(domain).Append(".schema.json\n")
              .Append("//              config/balance/").Append(domain).Append(".json\n")
              .Append("//     Uretim : Bunker/Config/Ice Aktar\n")
              .Append("// </auto-generated>\n\n");
        }

        private static void Doc(StringBuilder sb, ConfigProperty p, string indent)
        {
            sb.Append(indent).Append("/// <summary>").Append(Escape(p.Description)).Append("</summary>\n");

            if (p.Minimum.HasValue || p.Maximum.HasValue)
            {
                sb.Append(indent).Append("/// <remarks>Aralik: ")
                  .Append(p.Minimum.HasValue ? ConfigDomain.Literal(p.Minimum.Value) : "-")
                  .Append(" .. ")
                  .Append(p.Maximum.HasValue ? ConfigDomain.Literal(p.Maximum.Value) : "-")
                  .Append(" | JSON: ").Append(p.JsonPath).Append("</remarks>\n");
            }
        }

        private static string Escape(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string DefaultLiteral(ConfigProperty p, Dictionary<string, double> values)
        {
            if (!values.TryGetValue(p.JsonPath, out double v)) v = 0d;

            return p.IsInteger
                ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
                : ConfigDomain.Literal(v) + "f";
        }

        private static string RangeLiteral(ConfigProperty p, double v) =>
            p.IsInteger
                ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
                : ConfigDomain.Literal(v) + "f";

        private static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(fullName, false);
                if (t != null) return t;
            }

            return null;
        }
    }
}
