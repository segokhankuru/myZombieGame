using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Bunker.Systems.Config
{
    /// <summary>JSON değer türü.</summary>
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// Ayrıştırılmış bir JSON değeri.
    ///
    /// <para><b>Neden kendi ayrıştırıcımız var:</b> Unity'nin <c>JsonUtility</c>'si
    /// yalnızca önceden bilinen tipe okur — şemayı okuyup ondan C# üretmek için gereken
    /// şey tam tersi: şeklini bilmediğin bir belgeyi gezmek. Alternatif bir paket
    /// eklemekti; bir ADR'ye ve kalıcı bir bağımlılığa değmeyecek kadar küçük bir iş.</para>
    ///
    /// <para>Saf C#. Sahne yok, Unity yok, test edilebilir.</para>
    /// </summary>
    public sealed class JsonValue
    {
        private readonly Dictionary<string, JsonValue> _object;
        private readonly List<JsonValue> _array;
        private readonly string _string;
        private readonly double _number;
        private readonly bool _bool;

        public JsonKind Kind { get; }

        private JsonValue(JsonKind kind) { Kind = kind; }

        private JsonValue(bool value) { Kind = JsonKind.Bool; _bool = value; }
        private JsonValue(double value) { Kind = JsonKind.Number; _number = value; }
        private JsonValue(string value) { Kind = JsonKind.String; _string = value; }

        private JsonValue(Dictionary<string, JsonValue> value)
        {
            Kind = JsonKind.Object; _object = value;
        }

        private JsonValue(List<JsonValue> value) { Kind = JsonKind.Array; _array = value; }

        public static readonly JsonValue Null = new JsonValue(JsonKind.Null);

        public bool IsObject => Kind == JsonKind.Object;
        public bool IsArray => Kind == JsonKind.Array;
        public bool IsNumber => Kind == JsonKind.Number;
        public bool IsString => Kind == JsonKind.String;

        /// <summary>Sayının tam sayı olarak yazılıp yazılmadığı. <c>integer</c> şema tipi için.</summary>
        public bool IsIntegral => Kind == JsonKind.Number && Math.Abs(_number % 1d) < double.Epsilon;

        public double AsNumber => Kind == JsonKind.Number
            ? _number
            : throw new InvalidOperationException($"Deger sayi degil: {Kind}");

        public string AsString => Kind == JsonKind.String
            ? _string
            : throw new InvalidOperationException($"Deger metin degil: {Kind}");

        public bool AsBool => Kind == JsonKind.Bool
            ? _bool
            : throw new InvalidOperationException($"Deger mantiksal degil: {Kind}");

        public IReadOnlyList<JsonValue> Items => _array ?? (IReadOnlyList<JsonValue>)Array.Empty<JsonValue>();

        /// <summary>Nesne anahtarları, dosyadaki sırayla. Üretilen kodun sırası kararlı olmalı.</summary>
        public IEnumerable<string> Keys => _object != null ? _object.Keys : System.Linq.Enumerable.Empty<string>();

        public bool Has(string key) => _object != null && _object.ContainsKey(key);

        /// <summary>Yoksa <see cref="Null"/> döner — çağıran taraf null kontrolü yapmak zorunda kalmasın.</summary>
        public JsonValue this[string key] =>
            _object != null && _object.TryGetValue(key, out JsonValue v) ? v : Null;

        // ---------------------------------------------------------------- ayristirma

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            int index = 0;
            JsonValue value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);

            if (index < text.Length)
            {
                throw new FormatException(
                    $"JSON: belgenin sonundan sonra fazladan icerik var (konum {index}).");
            }

            return value;
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON: beklenmedik dosya sonu.");

            char c = s[i];

            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return new JsonValue(ParseString(s, ref i));
                case 't': Expect(s, ref i, "true"); return new JsonValue(true);
                case 'f': Expect(s, ref i, "false"); return new JsonValue(false);
                case 'n': Expect(s, ref i, "null"); return Null;
                default: return new JsonValue(ParseNumber(s, ref i));
            }
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            var map = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            i++; // '{'

            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return new JsonValue(map); }

            while (true)
            {
                SkipWhitespace(s, ref i);

                if (i >= s.Length || s[i] != '"')
                {
                    throw new FormatException($"JSON: nesne anahtari beklendi (konum {i}).");
                }

                string key = ParseString(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':')
                {
                    throw new FormatException($"JSON: '{key}' anahtarindan sonra ':' beklendi.");
                }

                i++;
                map[key] = ParseValue(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON: nesne kapanmadi.");

                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return new JsonValue(map); }

                throw new FormatException($"JSON: nesnede ',' ya da '}}' beklendi (konum {i}).");
            }
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            var list = new List<JsonValue>();
            i++; // '['

            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return new JsonValue(list); }

            while (true)
            {
                list.Add(ParseValue(s, ref i));

                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON: dizi kapanmadi.");

                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return new JsonValue(list); }

                throw new FormatException($"JSON: dizide ',' ya da ']' beklendi (konum {i}).");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // '"'
            var sb = new StringBuilder();

            while (true)
            {
                if (i >= s.Length) throw new FormatException("JSON: metin kapanmadi.");

                char c = s[i++];

                if (c == '"') return sb.ToString();

                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) throw new FormatException("JSON: kacis dizisi yarim kaldi.");

                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("JSON: \\u kacisi yarim.");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default:
                        throw new FormatException($"JSON: taninmayan kacis dizisi '\\{e}'.");
                }
            }
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;

            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;

            while (i < s.Length &&
                   (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                    s[i] == '-' || s[i] == '+'))
            {
                i++;
            }

            string token = s.Substring(start, i - start);

            // InvariantCulture sart: tr-TR makinede ondalik ayirici virgul, "1.4"
            // sessizce 14 olur. Sessiz on kat, bulunmasi en zor denge hatasidir.
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }

            throw new FormatException($"JSON: sayi cozulemedi: '{token}' (konum {start}).");
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length ||
                string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
            {
                throw new FormatException($"JSON: '{literal}' beklendi (konum {i}).");
            }

            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
