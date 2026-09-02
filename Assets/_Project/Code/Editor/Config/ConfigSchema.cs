using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Bunker.Systems.Config;

namespace Bunker.Editor.ConfigTools
{
    /// <summary>Şemadaki tek bir ayarlanabilir değer.</summary>
    public sealed class ConfigProperty
    {
        /// <summary>Üst gruptaki adı (<c>count</c>). Üst seviye değerlerde boş.</summary>
        public string Group;
        public string Key;
        public bool IsInteger;
        public double? Minimum;
        public double? Maximum;
        public string Description;
        public bool Required;

        /// <summary>`config/balance/rounds.json` içindeki tam yolu — hata mesajları için.</summary>
        public string JsonPath => string.IsNullOrEmpty(Group) ? Key : $"{Group}.{Key}";

        /// <summary>
        /// C# adı. <b>Her zaman grup adıyla öneklenir.</b>
        ///
        /// <para>Çakışma olduğunda öneklemek daha kısa isimler verirdi, ama o kural
        /// şemaya yeni bir anahtar eklendiğinde <i>başka</i> bir alanın adını sessizce
        /// değiştirir. Üreteçte sürpriz olmaz: kural mekanik, isim tahmin edilebilir.</para>
        /// </summary>
        public string CSharpName => Pascal(Group) + Pascal(Key);

        public string ParamName => Camel(CSharpName);
        public string FieldName => Camel(CSharpName);
        public string TypeName => IsInteger ? "int" : "float";

        public static string Pascal(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        public static string Camel(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }
    }

    /// <summary>
    /// Bir tuning alanının şeması: `config/schema/&lt;domain&gt;.schema.json`.
    ///
    /// <para>Yalnızca oyunun ihtiyacı olan alt kümeyi okur — tip, aralık, açıklama,
    /// zorunluluk. JSON Schema'nın geri kalanı (oneOf, $ref, koşullar) bilerek
    /// desteklenmiyor: desteklenmeyen bir yapı sessizce yok sayılmak yerine <b>hata
    /// verir</b>, çünkü sessizce yok sayılan bir kısıt kısıt değildir.</para>
    /// </summary>
    public sealed class ConfigDomain
    {
        public string Domain;                     // "rounds"
        public string ClassName;                  // "RoundsConfig"
        public readonly List<ConfigProperty> Properties = new List<ConfigProperty>();

        public string AssetClassName => ClassName + "Asset";

        public static ConfigDomain Load(string domain, string schemaText)
        {
            JsonValue schema = JsonValue.Parse(schemaText);

            var result = new ConfigDomain
            {
                Domain = domain,
                ClassName = ConfigProperty.Pascal(domain) + "Config"
            };

            var required = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonValue r in schema["required"].Items) required.Add(r.AsString);

            JsonValue properties = schema["properties"];
            if (!properties.IsObject)
            {
                throw new FormatException($"{domain}.schema.json: 'properties' nesnesi yok.");
            }

            foreach (string key in properties.Keys)
            {
                if (IsMeta(key)) continue;

                JsonValue node = properties[key];

                if (node["properties"].IsObject)
                {
                    ReadGroup(result, key, node);
                    continue;
                }

                result.Properties.Add(ReadProperty(domain, string.Empty, key, node, required.Contains(key)));
            }

            if (result.Properties.Count == 0)
            {
                throw new FormatException($"{domain}.schema.json: ayarlanabilir hicbir deger bulunamadi.");
            }

            return result;
        }

        private static void ReadGroup(ConfigDomain domain, string group, JsonValue node)
        {
            var required = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonValue r in node["required"].Items) required.Add(r.AsString);

            JsonValue properties = node["properties"];

            foreach (string key in properties.Keys)
            {
                if (IsMeta(key)) continue;

                JsonValue child = properties[key];

                if (child["properties"].IsObject)
                {
                    throw new FormatException(
                        $"{domain.Domain}.schema.json: '{group}.{key}' ic ice ikinci seviye " +
                        "nesne. Uretec tek seviye gruplama destekler - duzlestir ya da " +
                        "ayri bir alan (domain) dosyasi ac.");
                }

                domain.Properties.Add(
                    ReadProperty(domain.Domain, group, key, child, required.Contains(key)));
            }
        }

        private static ConfigProperty ReadProperty(
            string domain, string group, string key, JsonValue node, bool required)
        {
            string type = node["type"].IsString ? node["type"].AsString : null;

            if (type != "number" && type != "integer")
            {
                throw new FormatException(
                    $"{domain}.schema.json: '{(group.Length == 0 ? key : group + "." + key)}' " +
                    $"tipi '{type ?? "yok"}'. Uretec yalnizca 'number' ve 'integer' destekler; " +
                    "metin ve mantiksal degerler denge sayisi degildir (config-data.md).");
            }

            return new ConfigProperty
            {
                Group = group,
                Key = key,
                IsInteger = type == "integer",
                Minimum = node["minimum"].IsNumber ? node["minimum"].AsNumber : (double?)null,
                Maximum = node["maximum"].IsNumber ? node["maximum"].AsNumber : (double?)null,
                Description = node["description"].IsString ? node["description"].AsString : string.Empty,
                Required = required
            };
        }

        /// <summary>`$schema`, `_meta`, `_note` gibi veri olmayan anahtarlar.</summary>
        public static bool IsMeta(string key) =>
            string.IsNullOrEmpty(key) || key[0] == '_' || key[0] == '$';

        // ---------------------------------------------------------------- denge dosyasi

        /// <summary>
        /// Denge dosyasını şemaya karşı okur ve her özelliğin değerini döner.
        ///
        /// <para><b>Bilinmeyen anahtar hatadır, uyarı değil</b> (editor-tools.md): şema
        /// bir sözleşmedir. Yazım hatası içeren bir anahtar sessizce yok sayılırsa,
        /// denge değişikliği hiç uygulanmaz ve kimse aylarca fark etmez.</para>
        /// </summary>
        public Dictionary<string, double> ReadValues(string balanceText, List<string> errors)
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            JsonValue balance = JsonValue.Parse(balanceText);

            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (ConfigProperty p in Properties) known.Add(p.JsonPath);

            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (ConfigProperty p in Properties)
            {
                if (!string.IsNullOrEmpty(p.Group)) groups.Add(p.Group);
            }

            // --- bilinmeyen anahtarlar
            foreach (string key in balance.Keys)
            {
                if (IsMeta(key)) continue;

                JsonValue node = balance[key];

                if (node.IsObject)
                {
                    if (!groups.Contains(key))
                    {
                        errors.Add($"{Domain}.json: '{key}' grubu semada yok.");
                        continue;
                    }

                    foreach (string child in node.Keys)
                    {
                        if (IsMeta(child)) continue;
                        if (!known.Contains($"{key}.{child}"))
                        {
                            errors.Add($"{Domain}.json: '{key}.{child}' anahtari semada yok " +
                                       $"(config/schema/{Domain}.schema.json).");
                        }
                    }

                    continue;
                }

                if (!known.Contains(key))
                {
                    errors.Add($"{Domain}.json: '{key}' anahtari semada yok " +
                               $"(config/schema/{Domain}.schema.json).");
                }
            }

            // --- degerler
            foreach (ConfigProperty p in Properties)
            {
                JsonValue node = string.IsNullOrEmpty(p.Group)
                    ? balance[p.Key]
                    : balance[p.Group][p.Key];

                if (!node.IsNumber)
                {
                    if (p.Required)
                    {
                        errors.Add($"{Domain}.json: zorunlu anahtar '{p.JsonPath}' eksik " +
                                   $"ya da sayi degil. Sema yolu: " +
                                   $"config/schema/{Domain}.schema.json -> {p.JsonPath}");
                    }

                    continue;
                }

                if (p.IsInteger && !node.IsIntegral)
                {
                    errors.Add($"{Domain}.json: '{p.JsonPath}' tam sayi olmali, " +
                               $"'{node.AsNumber}' yazilmis.");
                    continue;
                }

                double v = node.AsNumber;

                if (p.Minimum.HasValue && v < p.Minimum.Value)
                {
                    errors.Add($"{Domain}.json: '{p.JsonPath}' = {Literal(v)}, en az " +
                               $"{Literal(p.Minimum.Value)} olmali. {p.Description}");
                }

                if (p.Maximum.HasValue && v > p.Maximum.Value)
                {
                    errors.Add($"{Domain}.json: '{p.JsonPath}' = {Literal(v)}, en fazla " +
                               $"{Literal(p.Maximum.Value)} olmali. {p.Description}");
                }

                values[p.JsonPath] = v;
            }

            return values;
        }

        public static string Literal(double value) =>
            value.ToString("0.###########", CultureInfo.InvariantCulture);

        /// <summary>C# kaynak koduna gömülecek metin.</summary>
        public static string Quote(string text)
        {
            var sb = new StringBuilder(text.Length + 8);
            sb.Append('"');

            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    default: sb.Append(c); break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }
    }
}
