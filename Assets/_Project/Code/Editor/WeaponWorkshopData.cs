using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Bunker.Systems.Config;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>Silaha takılı bir aksesuar ve yerleşimi (<c>weapon-art.json</c>).</summary>
    internal sealed class AttachmentData
    {
        public string Name;
        public string PrefabPath;

        /// <summary>Aksesuar merkezinin namlu boyunca yeri: 0 = namlu ağzı, 1 = dipçik.</summary>
        public float AlongBarrel01 = 0.5f;

        /// <summary>Aksesuarın alt kenarı ile gövdenin tepesi arası (silah yüksekliği oranı).</summary>
        public float GapOfWeaponHeight;

        /// <summary>Yana kaydırma (silah genişliği oranı), artı = silahın sağı.</summary>
        public float SideOfWeaponWidth;

        public float ScaleMultiplier = 1f;
        public Vector3 EulerDegrees;

        /// <summary><c>_</c> ile başlayan açıklama anahtarları; kayıtta korunur.</summary>
        public List<KeyValuePair<string, string>> Notes = new List<KeyValuePair<string, string>>();

        public AttachmentData Clone()
        {
            var copy = (AttachmentData)MemberwiseClone();
            copy.Notes = new List<KeyValuePair<string, string>>(Notes);
            return copy;
        }
    }

    /// <summary>Bir ateşli silahın görünümü (<c>weapon-art.json</c>).</summary>
    internal sealed class WeaponArtData
    {
        public string WeaponId;
        public string ModelPath;
        public float LengthMeters = 0.5f;
        public string NativeForward = "-Z";
        public string NativeUp = "+Y";
        public Vector3 HandOffsetMeters;
        public List<AttachmentData> Attachments = new List<AttachmentData>();
        public List<KeyValuePair<string, string>> Notes = new List<KeyValuePair<string, string>>();

        public Vector3 ForwardVector => WeaponAxes.ToVector(NativeForward);
        public Vector3 UpVector => WeaponAxes.ToVector(NativeUp);

        public WeaponArtData Clone()
        {
            var copy = (WeaponArtData)MemberwiseClone();
            copy.Notes = new List<KeyValuePair<string, string>>(Notes);
            copy.Attachments = new List<AttachmentData>(Attachments.Count);
            foreach (AttachmentData a in Attachments) copy.Attachments.Add(a.Clone());
            return copy;
        }
    }

    /// <summary>Bir silahın ya da hazır ailenin sesleri (<c>weapon-audio.json</c>).</summary>
    internal sealed class WeaponSoundData
    {
        /// <summary><c>weapons</c> satırında dolu.</summary>
        public string WeaponId;

        /// <summary><c>presets</c> satırında dolu.</summary>
        public string PresetName;

        public List<string> Fire = new List<string>();
        public string ReloadOut = string.Empty;
        public string ReloadIn = string.Empty;
        public string DryFire = string.Empty;
        public string Equip = string.Empty;
        public float BasePitch = 1f;
        public List<KeyValuePair<string, string>> Notes = new List<KeyValuePair<string, string>>();

        public WeaponSoundData Clone()
        {
            var copy = (WeaponSoundData)MemberwiseClone();
            copy.Fire = new List<string>(Fire);
            copy.Notes = new List<KeyValuePair<string, string>>(Notes);
            return copy;
        }

        /// <summary>Sesleri başka bir satırdan kopyalar (id ve ad korunur).</summary>
        public void CopySoundsFrom(WeaponSoundData other)
        {
            Fire = new List<string>(other.Fire);
            ReloadOut = other.ReloadOut;
            ReloadIn = other.ReloadIn;
            DryFire = other.DryFire;
            Equip = other.Equip;
            BasePitch = other.BasePitch;
        }
    }

    /// <summary>Bir ateşli silahın dengesi (<c>weapons.json</c> satırı).</summary>
    internal sealed class WeaponStatsData
    {
        public string Id;
        public string Name;
        public string Text = string.Empty;

        /// <summary>Anahtar → değer. Anahtarlar <see cref="WeaponStatFields"/>'da.</summary>
        public Dictionary<string, double> Numbers = new Dictionary<string, double>();

        public bool ReloadPerShell;

        /// <summary>0 = dürbün yok (anahtar dosyaya hiç yazılmaz).</summary>
        public float ScopeMagnification;

        public string ScopeStyle = "LongRange";
        public List<KeyValuePair<string, string>> Notes = new List<KeyValuePair<string, string>>();

        public bool HasScope => ScopeMagnification > 1f;

        public WeaponStatsData Clone()
        {
            var copy = (WeaponStatsData)MemberwiseClone();
            copy.Numbers = new Dictionary<string, double>(Numbers);
            copy.Notes = new List<KeyValuePair<string, string>>(Notes);
            return copy;
        }

        /// <summary><c>_</c> ile başlayan bir notu yazar ya da değiştirir.</summary>
        public void SetNote(string key, string value)
        {
            for (int i = 0; i < Notes.Count; i++)
            {
                if (Notes[i].Key != key) continue;
                Notes[i] = new KeyValuePair<string, string>(key, value);
                return;
            }

            Notes.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    /// <summary>
    /// <c>weapons.json</c>'daki sayısal alanlar, dosyadaki satır düzeniyle. 2026-09-10.
    ///
    /// <para><b>Tek tablo:</b> okuyucu, yazıcı ve pencere aynı listeyi geziyor. Bir alan
    /// eklendiğinde üç yere ayrı ayrı yazmak, birinin unutulması demekti.</para>
    /// </summary>
    internal static class WeaponStatFields
    {
        public readonly struct Field
        {
            public readonly string Key;
            public readonly string Label;
            public readonly bool Integer;

            public Field(string key, string label, bool integer)
            {
                Key = key; Label = label; Integer = integer;
            }
        }

        /// <summary>Dosyadaki satırlar: her iç dizi tek satırda yazılır.</summary>
        public static readonly Field[][] Lines =
        {
            new[] { new Field("damage", "Hasar (mermi/saçma başına)", false),
                    new Field("roundsPerMinute", "Atış / dakika", false),
                    new Field("headshotMultiplier", "Kafa çarpanı", false) },
            new[] { new Field("rangeMeters", "Menzil (m)", false),
                    new Field("spreadDegrees", "Dağılım (derece)", false),
                    new Field("pelletCount", "Saçma sayısı", true) },
            new[] { new Field("magazineCapacity", "Şarjör", true),
                    new Field("reserveCapacity", "Tur sonu ikmal ölçüsü", true),
                    new Field("startingReserve", "Alınca gelen yedek", true) },
            new[] { new Field("reloadSeconds", "Dolum süresi (sn)", false) },
            new[] { new Field("recoilPitchPerShot", "Yukarı tepme / atış", false),
                    new Field("recoilYawPerShot", "Yana tepme / atış", false) },
            new[] { new Field("recoilRecoveryPerSecond", "Tepme toparlanma / sn", false),
                    new Field("recoilMaxPitch", "En fazla yukarı tepme", false) },
            new[] { new Field("price", "Fiyat (puan)", true),
                    new Field("ammoPrice", "Mermi fiyatı (puan)", true) }
        };

        public static IEnumerable<Field> All
        {
            get
            {
                foreach (Field[] line in Lines)
                {
                    foreach (Field field in line) yield return field;
                }
            }
        }

        public static bool IsNumberKey(string key)
        {
            foreach (Field field in All)
            {
                if (field.Key == key) return true;
            }

            return false;
        }
    }

    /// <summary>Eksen metni ("+X", "-Z" ...) ile vektör arasında çeviri.</summary>
    internal static class WeaponAxes
    {
        public static readonly string[] Names = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };

        public static bool IsValid(string axis) => System.Array.IndexOf(Names, axis) >= 0;

        public static Vector3 ToVector(string axis) => axis switch
        {
            "+X" => Vector3.right,
            "-X" => Vector3.left,
            "+Y" => Vector3.up,
            "-Y" => Vector3.down,
            "+Z" => Vector3.forward,
            _ => Vector3.back
        };

        /// <summary>İki eksen aynı doğrultuda mı (namlu ile üst aynı eksen olamaz).</summary>
        public static bool SameLine(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a[1] == b[1];
    }

    /// <summary>Bir şema dosyasından aralık ve açıklama okur.</summary>
    internal sealed class SchemaInfo
    {
        private readonly JsonValue _root;
        public readonly string Path;

        public SchemaInfo(string path)
        {
            Path = path;
            _root = WeaponWorkshopData.ReadJson(path, "aralik sinirlari buradan okunuyor");
        }

        /// <summary>
        /// <c>properties</c> zincirinde bir özellik. Dizi adımlarında <c>items</c>'a otomatik iner.
        /// </summary>
        public JsonValue Property(params string[] path)
        {
            JsonValue node = _root;

            foreach (string step in path)
            {
                if (node["type"].IsString && node["type"].AsString == "array") node = node["items"];
                node = node["properties"][step];
            }

            if (node["type"].IsString && node["type"].AsString == "array" && node["items"]["minimum"].IsNumber)
            {
                node = node["items"];
            }

            return node;
        }

        public Vector2 Range(params string[] path)
        {
            JsonValue property = Property(path);

            if (!property["minimum"].IsNumber || !property["maximum"].IsNumber)
            {
                throw new InvalidDataException(
                    $"{Path}: '{string.Join(".", path)}' icin minimum/maximum yok - ekle, pencere " +
                    "degeri bu sinirla kisiyor");
            }

            return new Vector2((float)property["minimum"].AsNumber, (float)property["maximum"].AsNumber);
        }

        public string Description(params string[] path)
        {
            JsonValue property = Property(path);
            return property["description"].IsString ? property["description"].AsString : string.Empty;
        }
    }

    /// <summary>
    /// Silah Atölyesi'nin verisi: <b>denge</b> (<c>weapons.json</c>), <b>görünüm</b>
    /// (<c>weapon-art.json</c>) ve <b>ses</b> (<c>weapon-audio.json</c>). 2026-09-10.
    ///
    /// <para><b>Neden üç dosya, tek dosya değil:</b> üçünün sahibi ayrı (systems-designer,
    /// technical-artist, audio-director) ve üçü de ayrı bir üreticiye akıyor
    /// (<c>WeaponCatalogImporter</c>, <c>ArtIntegration</c>, <c>AudioIntegration</c>). Tek
    /// dosya, bir ses değişikliğinin bütün modelleri yeniden üretmesi demek olurdu.</para>
    ///
    /// <para><b>Neden koddaki tablolardan taşındı</b> (geliştirici: <i>"import ettiği tüm
    /// silahları ve attachmentları ekleyip düzenleyebileceğim yapı kur, sesleri de. Böylece
    /// senin düzenlemene ihtiyacım kalmaz"</i>): model eşleşmesi <c>ArtIntegration</c>'da,
    /// ses ailesi <c>AudioIntegration</c>'da C# tablosuydu; yeni bir silah kod değişikliği
    /// istiyordu.</para>
    ///
    /// <para><b>Kayıt değişmeyen dosyaya dokunmaz</b> (editor-tools.md): içerik aynıysa
    /// <c>lastTuned</c> da değişmez ve git'te boş bir fark oluşmaz.</para>
    /// </summary>
    internal sealed class WeaponWorkshopData
    {
        public const string StatsPath = "config/content/weapons.json";
        public const string ArtPath = "config/content/weapon-art.json";
        public const string AudioPath = "config/content/weapon-audio.json";

        public const string StatsSchemaPath = "config/schema/weapons.schema.json";
        public const string ArtSchemaPath = "config/schema/weapon-art.schema.json";
        public const string AudioSchemaPath = "config/schema/weapon-audio.schema.json";

        private static readonly Regex IdPattern = new Regex("^weapon\\.[a-z0-9_]+$");

        public List<WeaponStatsData> Stats = new List<WeaponStatsData>();
        public List<string> RetiredIds = new List<string>();
        public List<WeaponArtData> Art = new List<WeaponArtData>();
        public List<WeaponSoundData> Sounds = new List<WeaponSoundData>();
        public List<WeaponSoundData> Presets = new List<WeaponSoundData>();

        private FileHeader _statsHeader;
        private FileHeader _artHeader;
        private FileHeader _audioHeader;

        private sealed class FileHeader
        {
            public int Version = 1;
            public string Owner;
            public string System;
            public string LastTuned;
            public List<KeyValuePair<string, string>> Notes = new List<KeyValuePair<string, string>>();

            public FileHeader Clone()
            {
                var copy = (FileHeader)MemberwiseClone();
                copy.Notes = new List<KeyValuePair<string, string>>(Notes);
                return copy;
            }
        }

        // ================================================================ okuma

        /// <summary>Üç dosyayı okur. Bozuksa <b>ne yapılacağını söyleyen</b> bir hatayla patlar.</summary>
        public static WeaponWorkshopData Load()
        {
            var errors = new StringBuilder();
            var data = new WeaponWorkshopData();

            JsonValue stats = ReadJson(StatsPath, "silah dengesi buradan okunuyor");
            data._statsHeader = ReadHeader(stats, "systems-designer");
            data.Stats = ParseStats(stats, errors);

            foreach (JsonValue id in stats["retiredIds"].Items)
            {
                if (id.IsString) data.RetiredIds.Add(id.AsString);
            }

            JsonValue art = ReadJson(ArtPath, "silah modelleri buradan okunuyor");
            data._artHeader = ReadHeader(art, "technical-artist");
            data.Art = ParseArt(art, errors);

            JsonValue audio = ReadJson(AudioPath, "silah sesleri buradan okunuyor");
            data._audioHeader = ReadHeader(audio, "audio-director");
            data.Sounds = ParseSounds(audio["weapons"], "weapons", errors);
            data.Presets = ParseSounds(audio["presets"], "presets", errors);

            if (errors.Length > 0) throw new InvalidDataException($"Silah dosyalari gecersiz:\n{errors}");

            return data;
        }

        /// <summary>Yalnızca görünüm dosyası (<c>ArtIntegration</c> için).</summary>
        public static List<WeaponArtData> LoadArt()
        {
            var errors = new StringBuilder();
            List<WeaponArtData> result = ParseArt(ReadJson(ArtPath, "silah modelleri buradan okunuyor"), errors);

            if (errors.Length > 0) throw new InvalidDataException($"{ArtPath} gecersiz:\n{errors}");
            return result;
        }

        /// <summary>Yalnızca silah sesleri (<c>AudioIntegration</c> için).</summary>
        public static List<WeaponSoundData> LoadSounds()
        {
            var errors = new StringBuilder();
            JsonValue root = ReadJson(AudioPath, "silah sesleri buradan okunuyor");
            List<WeaponSoundData> result = ParseSounds(root["weapons"], "weapons", errors);

            if (errors.Length > 0) throw new InvalidDataException($"{AudioPath} gecersiz:\n{errors}");
            return result;
        }

        public WeaponWorkshopData Clone()
        {
            var copy = new WeaponWorkshopData
            {
                RetiredIds = new List<string>(RetiredIds),
                _statsHeader = _statsHeader.Clone(),
                _artHeader = _artHeader.Clone(),
                _audioHeader = _audioHeader.Clone()
            };

            foreach (WeaponStatsData s in Stats) copy.Stats.Add(s.Clone());
            foreach (WeaponArtData a in Art) copy.Art.Add(a.Clone());
            foreach (WeaponSoundData s in Sounds) copy.Sounds.Add(s.Clone());
            foreach (WeaponSoundData p in Presets) copy.Presets.Add(p.Clone());

            return copy;
        }

        public WeaponArtData FindArt(string id) => Art.Find(a => a.WeaponId == id);
        public WeaponSoundData FindSound(string id) => Sounds.Find(s => s.WeaponId == id);
        public WeaponStatsData FindStats(string id) => Stats.Find(s => s.Id == id);

        // ============================================================ yeni silah

        /// <summary>
        /// Görünen addan kalıcı bir id üretir: <c>"Desert Eagle"</c> → <c>weapon.desert_eagle</c>.
        /// </summary>
        public static string MakeId(string name)
        {
            var slug = new StringBuilder(name.Length);
            bool lastUnderscore = true;

            foreach (char raw in name.ToLowerInvariant())
            {
                char c = raw switch
                {
                    'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', _ => raw
                };

                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');

                if (ok)
                {
                    slug.Append(c);
                    lastUnderscore = false;
                }
                else if (!lastUnderscore)
                {
                    slug.Append('_');
                    lastUnderscore = true;
                }
            }

            return "weapon." + slug.ToString().Trim('_');
        }

        /// <summary>Yeni bir id kullanılabilir mi. Kullanılamıyorsa sebebi döner.</summary>
        public string CheckNewId(string id)
        {
            if (!IdPattern.IsMatch(id)) return $"'{id}' gecerli bir id degil (weapon.harf_rakam).";
            if (FindStats(id) != null) return $"'{id}' zaten var.";
            if (RetiredIds.Contains(id)) return $"'{id}' emekliye ayrilmis bir id - bir daha kullanilamaz.";
            return null;
        }

        // ================================================================= yazma

        /// <summary>
        /// Değişen dosyaları yazar. Hangilerinin değiştiğini döner (üreticiler yalnızca
        /// onlar için çalışır).
        /// </summary>
        public (bool Stats, bool Art, bool Audio) Save()
        {
            string today = System.DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            bool stats = WriteIfChanged(StatsPath, d => d.StatsText(d._statsHeader.LastTuned),
                                        () => _statsHeader.LastTuned = today, () => StatsText(today));
            bool art = WriteIfChanged(ArtPath, d => d.ArtText(d._artHeader.LastTuned),
                                      () => _artHeader.LastTuned = today, () => ArtText(today));
            bool audio = WriteIfChanged(AudioPath, d => d.AudioText(d._audioHeader.LastTuned),
                                        () => _audioHeader.LastTuned = today, () => AudioText(today));

            return (stats, art, audio);
        }

        /// <summary>Diskteki halden farklı mı (pencerenin "kaydedilmemiş" işareti).</summary>
        public bool DiffersFrom(WeaponWorkshopData saved) =>
            StatsText(_statsHeader.LastTuned) != saved.StatsText(saved._statsHeader.LastTuned)
            || ArtText(_artHeader.LastTuned) != saved.ArtText(saved._artHeader.LastTuned)
            || AudioText(_audioHeader.LastTuned) != saved.AudioText(saved._audioHeader.LastTuned);

        private bool WriteIfChanged(string path, System.Func<WeaponWorkshopData, string> current,
                                    System.Action stamp, System.Func<string> stamped)
        {
            string onDisk = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;

            if (onDisk == current(this)) return false;

            stamp();

            // Once gecici dosyaya, sonra yerine: yarida kalan bir yazma tek kaynagi
            // bozuk birakmamali.
            string temp = path + ".tmp";
            File.WriteAllText(temp, stamped(), new UTF8Encoding(false));
            File.Copy(temp, path, overwrite: true);
            File.Delete(temp);

            return true;
        }

        public string StatsText(string lastTuned)
        {
            var json = new StringBuilder(8192);
            WriteHeader(json, _statsHeader, lastTuned);

            json.Append("  \"retiredIds\": [");
            for (int i = 0; i < RetiredIds.Count; i++)
            {
                if (i > 0) json.Append(", ");
                json.Append(Quote(RetiredIds[i]));
            }
            json.Append("],\n\n");

            json.Append("  \"weapons\": [\n");

            for (int i = 0; i < Stats.Count; i++)
            {
                WeaponStatsData w = Stats[i];
                var lines = new List<string>
                {
                    $"\"id\": {Quote(w.Id)}",
                    $"\"name\": {Quote(w.Name)}"
                };

                foreach (WeaponStatFields.Field[] line in WeaponStatFields.Lines)
                {
                    var parts = new List<string>(line.Length + 1);

                    foreach (WeaponStatFields.Field field in line)
                    {
                        w.Numbers.TryGetValue(field.Key, out double value);
                        parts.Add($"\"{field.Key}\": {(field.Integer ? FormatInt(value) : Format(value))}");
                    }

                    if (line[0].Key == "reloadSeconds")
                    {
                        if (w.ReloadPerShell) parts.Add("\"reloadPerShell\": true");
                        lines.Add(string.Join(", ", parts));

                        // Durbun dolumdan hemen sonra, dosyadaki eski yerinde.
                        if (w.HasScope)
                        {
                            lines.Add($"\"scopeMagnification\": {Format(w.ScopeMagnification)}, " +
                                      $"\"scopeStyle\": {Quote(w.ScopeStyle)}");
                        }

                        continue;
                    }

                    lines.Add(string.Join(", ", parts));
                }

                if (!string.IsNullOrEmpty(w.Text)) lines.Add($"\"text\": {Quote(w.Text)}");
                foreach (KeyValuePair<string, string> note in w.Notes) lines.Add($"{Quote(note.Key)}: {Quote(note.Value)}");

                WriteObject(json, lines, "    ", i < Stats.Count - 1);
            }

            json.Append("  ]\n}\n");
            return json.ToString();
        }

        public string ArtText(string lastTuned)
        {
            var json = new StringBuilder(4096);
            WriteHeader(json, _artHeader, lastTuned);

            json.Append("  \"weapons\": [\n");

            for (int i = 0; i < Art.Count; i++)
            {
                WeaponArtData a = Art[i];

                json.Append("    {\n");
                json.Append("      \"weaponId\": ").Append(Quote(a.WeaponId)).Append(",\n");
                json.Append("      \"model\": ").Append(Quote(a.ModelPath)).Append(",\n");
                foreach (KeyValuePair<string, string> note in a.Notes)
                {
                    json.Append("      ").Append(Quote(note.Key)).Append(": ").Append(Quote(note.Value)).Append(",\n");
                }
                json.Append("      \"lengthMeters\": ").Append(Format(a.LengthMeters))
                    .Append(", \"nativeForward\": ").Append(Quote(a.NativeForward))
                    .Append(", \"nativeUp\": ").Append(Quote(a.NativeUp)).Append(",\n");
                json.Append("      \"handOffsetMeters\": ").Append(Vector(a.HandOffsetMeters)).Append(",\n");

                if (a.Attachments.Count == 0)
                {
                    json.Append("      \"attachments\": []\n");
                }
                else
                {
                    json.Append("      \"attachments\": [\n");

                    for (int j = 0; j < a.Attachments.Count; j++)
                    {
                        AttachmentData t = a.Attachments[j];
                        var lines = new List<string>
                        {
                            $"\"name\": {Quote(t.Name)}",
                            $"\"prefab\": {Quote(t.PrefabPath)}"
                        };

                        foreach (KeyValuePair<string, string> note in t.Notes) lines.Add($"{Quote(note.Key)}: {Quote(note.Value)}");

                        lines.Add($"\"alongBarrel01\": {Format(t.AlongBarrel01)}, " +
                                  $"\"gapOfWeaponHeight\": {Format(t.GapOfWeaponHeight)}, " +
                                  $"\"sideOfWeaponWidth\": {Format(t.SideOfWeaponWidth)}");
                        lines.Add($"\"scaleMultiplier\": {Format(t.ScaleMultiplier)}, " +
                                  $"\"eulerDegrees\": {Vector(t.EulerDegrees)}");

                        WriteObject(json, lines, "        ", j < a.Attachments.Count - 1);
                    }

                    json.Append("      ]\n");
                }

                json.Append(i < Art.Count - 1 ? "    },\n" : "    }\n");
            }

            json.Append("  ]\n}\n");
            return json.ToString();
        }

        public string AudioText(string lastTuned)
        {
            var json = new StringBuilder(8192);
            WriteHeader(json, _audioHeader, lastTuned);

            json.Append("  \"presets\": [\n");
            for (int i = 0; i < Presets.Count; i++) WriteSounds(json, Presets[i], i < Presets.Count - 1);
            json.Append("  ],\n\n");

            json.Append("  \"weapons\": [\n");
            for (int i = 0; i < Sounds.Count; i++) WriteSounds(json, Sounds[i], i < Sounds.Count - 1);
            json.Append("  ]\n}\n");

            return json.ToString();
        }

        private static void WriteSounds(StringBuilder json, WeaponSoundData s, bool comma)
        {
            var lines = new List<string>();

            if (s.PresetName != null) lines.Add($"\"name\": {Quote(s.PresetName)}");
            else lines.Add($"\"weaponId\": {Quote(s.WeaponId)}");

            foreach (KeyValuePair<string, string> note in s.Notes) lines.Add($"{Quote(note.Key)}: {Quote(note.Value)}");

            var fire = new StringBuilder("\"fire\": [");
            for (int i = 0; i < s.Fire.Count; i++)
            {
                if (i > 0) fire.Append(", ");
                fire.Append(Quote(s.Fire[i]));
            }
            fire.Append(']');
            lines.Add(fire.ToString());

            lines.Add($"\"reloadOut\": {Quote(s.ReloadOut)}");
            lines.Add($"\"reloadIn\": {Quote(s.ReloadIn)}");
            lines.Add($"\"dryFire\": {Quote(s.DryFire)}");
            lines.Add($"\"equip\": {Quote(s.Equip)}");
            lines.Add($"\"basePitch\": {Format(s.BasePitch)}");

            WriteObject(json, lines, "    ", comma);
        }

        private static void WriteObject(StringBuilder json, List<string> lines, string indent, bool comma)
        {
            json.Append(indent).Append("{\n");

            for (int i = 0; i < lines.Count; i++)
            {
                json.Append(indent).Append("  ").Append(lines[i]);
                json.Append(i < lines.Count - 1 ? ",\n" : "\n");
            }

            json.Append(indent).Append(comma ? "},\n" : "}\n");
        }

        private static void WriteHeader(StringBuilder json, FileHeader header, string lastTuned)
        {
            json.Append("{\n");
            json.Append("  \"version\": ").Append(header.Version.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"_meta\": {\n");
            json.Append("    \"owner\": ").Append(Quote(header.Owner)).Append(",\n");
            json.Append("    \"system\": ").Append(Quote(header.System)).Append(",\n");
            json.Append("    \"lastTuned\": ").Append(Quote(lastTuned)).Append("\n");
            json.Append("  },\n\n");

            foreach (KeyValuePair<string, string> note in header.Notes)
            {
                json.Append("  ").Append(Quote(note.Key)).Append(": ").Append(Quote(note.Value)).Append(",\n\n");
            }
        }

        // ============================================================ ayristirma

        internal static JsonValue ReadJson(string path, string why)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"{path} bulunamadi - {why}. Dosyayi git'ten geri al (git checkout -- {path}).");
            }

            try
            {
                return JsonValue.Parse(File.ReadAllText(path));
            }
            catch (System.FormatException e)
            {
                throw new InvalidDataException($"{path} gecerli JSON degil: {e.Message}", e);
            }
        }

        private static FileHeader ReadHeader(JsonValue root, string defaultOwner)
        {
            var header = new FileHeader
            {
                Version = root["version"].IsNumber ? (int)root["version"].AsNumber : 1,
                Owner = root["_meta"]["owner"].IsString ? root["_meta"]["owner"].AsString : defaultOwner,
                System = root["_meta"]["system"].IsString ? root["_meta"]["system"].AsString : "SYS-silah",
                LastTuned = root["_meta"]["lastTuned"].IsString ? root["_meta"]["lastTuned"].AsString : string.Empty
            };

            foreach (string key in root.Keys)
            {
                if (key.StartsWith("_") && key != "_meta" && root[key].IsString)
                {
                    header.Notes.Add(new KeyValuePair<string, string>(key, root[key].AsString));
                }
            }

            return header;
        }

        private static List<WeaponStatsData> ParseStats(JsonValue root, StringBuilder errors)
        {
            var result = new List<WeaponStatsData>();
            JsonValue list = root["weapons"];

            if (!list.IsArray)
            {
                errors.Append($"  {StatsPath}: 'weapons' dizisi yok\n");
                return result;
            }

            for (int i = 0; i < list.Items.Count; i++)
            {
                JsonValue w = list.Items[i];
                string where = $"{StatsPath} weapons[{i}]";

                var entry = new WeaponStatsData
                {
                    Id = Text(w, "id", where, errors),
                    Name = Text(w, "name", where, errors),
                    Text = w["text"].IsString ? w["text"].AsString : string.Empty,
                    ReloadPerShell = w["reloadPerShell"].Kind == JsonKind.Bool && w["reloadPerShell"].AsBool,
                    ScopeMagnification = w["scopeMagnification"].IsNumber ? (float)w["scopeMagnification"].AsNumber : 0f,
                    ScopeStyle = w["scopeStyle"].IsString ? w["scopeStyle"].AsString : "LongRange"
                };

                foreach (string key in w.Keys)
                {
                    if (key.StartsWith("_"))
                    {
                        if (w[key].IsString) entry.Notes.Add(new KeyValuePair<string, string>(key, w[key].AsString));
                        continue;
                    }

                    if (key == "id" || key == "name" || key == "text" || key == "reloadPerShell"
                        || key == "scopeMagnification" || key == "scopeStyle"
                        || WeaponStatFields.IsNumberKey(key)) continue;

                    errors.Append($"  {where}: bilinmeyen anahtar '{key}' - {StatsSchemaPath}'da yok\n");
                }

                foreach (WeaponStatFields.Field field in WeaponStatFields.All)
                {
                    if (w[field.Key].IsNumber) entry.Numbers[field.Key] = w[field.Key].AsNumber;
                    else errors.Append($"  {where}: '{field.Key}' eksik ya da sayi degil\n");
                }

                result.Add(entry);
            }

            return result;
        }

        private static List<WeaponArtData> ParseArt(JsonValue root, StringBuilder errors)
        {
            var result = new List<WeaponArtData>();
            JsonValue list = root["weapons"];

            if (!list.IsArray)
            {
                errors.Append($"  {ArtPath}: 'weapons' dizisi yok\n");
                return result;
            }

            for (int i = 0; i < list.Items.Count; i++)
            {
                JsonValue w = list.Items[i];
                string where = $"{ArtPath} weapons[{i}]";

                var entry = new WeaponArtData
                {
                    WeaponId = Text(w, "weaponId", where, errors),
                    ModelPath = Text(w, "model", where, errors),
                    LengthMeters = Number(w, "lengthMeters", where, errors),
                    NativeForward = Text(w, "nativeForward", where, errors),
                    NativeUp = Text(w, "nativeUp", where, errors),
                    HandOffsetMeters = Vector(w, "handOffsetMeters", where, errors)
                };

                if (!WeaponAxes.IsValid(entry.NativeForward) || !WeaponAxes.IsValid(entry.NativeUp)
                    || WeaponAxes.SameLine(entry.NativeForward, entry.NativeUp))
                {
                    errors.Append($"  {where}: nativeForward/nativeUp gecersiz ya da ayni eksen " +
                                  $"({entry.NativeForward}, {entry.NativeUp})\n");
                }

                CollectNotes(w, entry.Notes, where, errors,
                             "weaponId", "model", "lengthMeters", "nativeForward", "nativeUp",
                             "handOffsetMeters", "attachments");

                JsonValue attachments = w["attachments"];
                if (!attachments.IsArray) errors.Append($"  {where}: 'attachments' dizisi yok\n");

                for (int j = 0; j < attachments.Items.Count; j++)
                {
                    JsonValue t = attachments.Items[j];
                    string at = $"{where} attachments[{j}]";

                    var attachment = new AttachmentData
                    {
                        Name = Text(t, "name", at, errors),
                        PrefabPath = Text(t, "prefab", at, errors),
                        AlongBarrel01 = Number(t, "alongBarrel01", at, errors),
                        GapOfWeaponHeight = Number(t, "gapOfWeaponHeight", at, errors),
                        SideOfWeaponWidth = Number(t, "sideOfWeaponWidth", at, errors),
                        ScaleMultiplier = Number(t, "scaleMultiplier", at, errors),
                        EulerDegrees = Vector(t, "eulerDegrees", at, errors)
                    };

                    CollectNotes(t, attachment.Notes, at, errors,
                                 "name", "prefab", "alongBarrel01", "gapOfWeaponHeight",
                                 "sideOfWeaponWidth", "scaleMultiplier", "eulerDegrees");

                    entry.Attachments.Add(attachment);
                }

                result.Add(entry);
            }

            return result;
        }

        private static List<WeaponSoundData> ParseSounds(JsonValue list, string arrayName, StringBuilder errors)
        {
            var result = new List<WeaponSoundData>();

            if (!list.IsArray)
            {
                errors.Append($"  {AudioPath}: '{arrayName}' dizisi yok\n");
                return result;
            }

            bool presets = arrayName == "presets";

            for (int i = 0; i < list.Items.Count; i++)
            {
                JsonValue s = list.Items[i];
                string where = $"{AudioPath} {arrayName}[{i}]";

                var entry = new WeaponSoundData
                {
                    WeaponId = presets ? null : Text(s, "weaponId", where, errors),
                    PresetName = presets ? Text(s, "name", where, errors) : null,
                    ReloadOut = OptionalText(s, "reloadOut", where, errors),
                    ReloadIn = OptionalText(s, "reloadIn", where, errors),
                    DryFire = OptionalText(s, "dryFire", where, errors),
                    Equip = OptionalText(s, "equip", where, errors),
                    BasePitch = Number(s, "basePitch", where, errors)
                };

                if (!s["fire"].IsArray || s["fire"].Items.Count == 0)
                {
                    errors.Append($"  {where}: 'fire' en az bir ses dosyasi olmali\n");
                }

                foreach (JsonValue clip in s["fire"].Items)
                {
                    if (clip.IsString) entry.Fire.Add(clip.AsString);
                }

                CollectNotes(s, entry.Notes, where, errors,
                             presets ? "name" : "weaponId", "fire", "reloadOut", "reloadIn",
                             "dryFire", "equip", "basePitch");

                result.Add(entry);
            }

            return result;
        }

        private static void CollectNotes(JsonValue node, List<KeyValuePair<string, string>> notes,
                                         string where, StringBuilder errors, params string[] known)
        {
            foreach (string key in node.Keys)
            {
                if (key.StartsWith("_"))
                {
                    if (node[key].IsString) notes.Add(new KeyValuePair<string, string>(key, node[key].AsString));
                    continue;
                }

                if (System.Array.IndexOf(known, key) < 0)
                {
                    errors.Append($"  {where}: bilinmeyen anahtar '{key}'\n");
                }
            }
        }

        private static string Text(JsonValue node, string key, string where, StringBuilder errors)
        {
            if (node[key].IsString && node[key].AsString.Length > 0) return node[key].AsString;

            errors.Append($"  {where}: '{key}' eksik ya da metin degil\n");
            return string.Empty;
        }

        private static string OptionalText(JsonValue node, string key, string where, StringBuilder errors)
        {
            if (node[key].IsString) return node[key].AsString;

            errors.Append($"  {where}: '{key}' eksik (ses yoksa bos metin yaz: \"\")\n");
            return string.Empty;
        }

        private static float Number(JsonValue node, string key, string where, StringBuilder errors)
        {
            if (node[key].IsNumber) return (float)node[key].AsNumber;

            errors.Append($"  {where}: '{key}' eksik ya da sayi degil\n");
            return 0f;
        }

        private static Vector3 Vector(JsonValue node, string key, string where, StringBuilder errors)
        {
            JsonValue array = node[key];

            if (!array.IsArray || array.Items.Count != 3 || !array.Items[0].IsNumber
                || !array.Items[1].IsNumber || !array.Items[2].IsNumber)
            {
                errors.Append($"  {where}: '{key}' uc sayilik bir dizi olmali ([x, y, z])\n");
                return Vector3.zero;
            }

            return new Vector3((float)array.Items[0].AsNumber, (float)array.Items[1].AsNumber,
                               (float)array.Items[2].AsNumber);
        }

        // ============================================================== bicim

        /// <summary>Dört ondalık yeter: silah boyunun on binde biri, 0.1 mm'nin altında.</summary>
        internal static string Format(double value) =>
            System.Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

        private static string FormatInt(double value) =>
            ((long)System.Math.Round(value)).ToString(CultureInfo.InvariantCulture);

        private static string Vector(Vector3 v) => $"[{Format(v.x)}, {Format(v.y)}, {Format(v.z)}]";

        internal static string Quote(string value)
        {
            value ??= string.Empty;
            var s = new StringBuilder(value.Length + 2);
            s.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': s.Append("\\\""); break;
                    case '\\': s.Append("\\\\"); break;
                    case '\n': s.Append("\\n"); break;
                    case '\r': s.Append("\\r"); break;
                    case '\t': s.Append("\\t"); break;
                    default:
                        if (c < 0x20) s.Append("\\u").Append(((int)c).ToString("x4"));
                        else s.Append(c);
                        break;
                }
            }

            s.Append('"');
            return s.ToString();
        }
    }
}
