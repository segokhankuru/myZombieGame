using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Bunker.Systems.Rounds;

namespace Bunker.Systems.Telemetry
{
    /// <summary>
    /// Biten run'ları diske yazar. M1-12.
    ///
    /// <para><b>Neden var:</b> M-01'in ÇK-13'ü ("tur 10'a ulaşmak ~15 dakika sürüyor")
    /// bir kanaat değil, bir ölçüm. ÇK-17 ("tekrar oynamak istiyor musun") bir histir
    /// ama <i>kaç run oynandığının</i> arkasındaki sayı burada durur.</para>
    ///
    /// <para><b>Neden JSONL</b> (satır başına bir JSON nesnesi), tek bir dizi değil:
    /// bir diziye eklemek dosyanın sonunu okuyup yeniden yazmayı gerektirir ve o sırada
    /// çöken oyun dosyanın <b>tamamını</b> bozar. Satır eklemek atomiğe yakındır —
    /// çöken oyun en fazla son satırı yarım bırakır, öncekiler okunur kalır.</para>
    ///
    /// <para><b>Saf C#.</b> <c>Bunker.Systems</c> motoru görmez
    /// (<c>noEngineReferences</c>), yani yol dışarıdan verilir ve hatalar
    /// <see cref="LastError"/> üzerinden okunur, <c>Debug.LogError</c> ile değil. Yan
    /// faydası: gerçek dosya sistemine karşı, Unity açmadan test edilebilmesi
    /// (ÇK-16).</para>
    ///
    /// <para><b>Telemetri oyunu bozamaz.</b> Disk dolu, klasör salt okunur, yol
    /// geçersiz — hepsi olur. Hiçbiri run'ı, skor ekranını ya da yeniden başlatmayı
    /// engellemez. Hata <b>bir kez</b> bildirilir; her run'da tekrar bağıran bir
    /// ölçüm aracı, ölçtüğü şeyden daha çok gürültü üretir.</para>
    /// </summary>
    public sealed class RunLogWriter
    {
        /// <summary>
        /// Satır biçiminin sürümü. <b>Alan eklenince değil, anlam değişince artar</b> —
        /// eski satırları okuyan bir araç neyle karşılaştığını bilmeli.
        /// </summary>
        public const int SchemaVersion = 1;

        public const string FileName = "runs.jsonl";

        private readonly string _filePath;
        private readonly StringBuilder _line = new StringBuilder(512);

        private bool _errorReported;

        /// <param name="directory">Yazılacak klasör. Yoksa yaratılır.</param>
        public RunLogWriter(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Telemetri klasoru bos olamaz.", nameof(directory));

            Directory = directory;
            _filePath = Path.Combine(directory, FileName);
        }

        public string Directory { get; }

        public string FilePath => _filePath;

        /// <summary>Kaç run yazıldı. Test ve teşhis için.</summary>
        public int WrittenCount { get; private set; }

        /// <summary>
        /// İlk yazma hatasının mesajı, yoksa <c>null</c>.
        ///
        /// <para>Yalnızca ilki tutulur: ilk hata sebebi söyler, sonrakiler onu
        /// tekrarlar.</para>
        /// </summary>
        public string LastError { get; private set; }

        /// <summary>
        /// Bir run'ı dosyaya ekler.
        /// </summary>
        /// <returns>Yazıldıysa <c>true</c>. <b>Dönüş değeri yok sayılabilir</b> —
        /// çağıran taraf başarısızlıkta bir şey yapmaz, yapmamalı da.</returns>
        /// <param name="usedRoundSkip">Bu run'da F7/F8 ile tur atlandı mı. Atlandıysa
        /// zamanlama bir ölçüm değildir ve özet aracı bu run'ı ÇK-13 hesabına
        /// katmaz.</param>
        public bool Append(in RunSummary summary, IReadOnlyList<float> roundStartSeconds,
                           DateTime endedAtUtc, bool usedRoundSkip = false)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);

                BuildLine(summary, roundStartSeconds, endedAtUtc, usedRoundSkip);

                // AppendAllText her cagrida acar-yazar-kapatir. Acik tutulan bir akis,
                // oyun cokerse bosaltilmamis tamponu da beraberinde goturur.
                File.AppendAllText(_filePath, _line.ToString(), Encoding.UTF8);

                WrittenCount++;
                return true;
            }
            catch (Exception e)
            {
                // Bos catch degil: hata TUTULUR ve bir kez bildirilir (csharp-code.md).
                //
                // BU CATCH BILEREK GENISTIR. Once yalnizca dort IO istisnasi
                // yakalaniyordu; kod incelemesi hakli olarak su zinciri gosterdi:
                // bu yazici RunSignals.RunEnded'in ILK abonesidir (BeforeSceneLoad'da
                // kurulur), yani buradan kacan bir istisna cok yayin zincirini keser
                // ve skor ekrani HIC GELMEZ, imlec kilitli kalir. Yakalanmayan tek
                // bir istisna tipi (SecurityException, surucu kaynakli bir tur)
                // oyunu oynanamaz yapardi.
                //
                // Kural: telemetri oyunu durduramaz. Bir olcum araci, olctugu seyi
                // bozuyorsa arac degil hatadir.
                if (!_errorReported)
                {
                    _errorReported = true;
                    LastError = $"Telemetri yazilamadi ({_filePath}): {e.Message}";
                }

                return false;
            }
        }

        /// <summary>
        /// Satırı kurar.
        ///
        /// <para><b>Her sayı <see cref="CultureInfo.InvariantCulture"/> ile yazılır.</b>
        /// Türkçe Windows'ta ondalık ayırıcı virgüldür ve <c>12.5f.ToString()</c>
        /// <c>"12,5"</c> üretir — bu, geçerli JSON olmayan bir dosya demektir ve hata
        /// aylar sonra, dosyayı okumaya çalışan araçta ortaya çıkar. Bu satır kültür
        /// hatasının bu projedeki tek savunmasıdır.</para>
        /// </summary>
        private void BuildLine(in RunSummary summary, IReadOnlyList<float> roundStartSeconds,
                               DateTime endedAtUtc, bool usedRoundSkip)
        {
            CultureInfo c = CultureInfo.InvariantCulture;

            _line.Clear();
            _line.Append('{');

            _line.Append("\"schema\":").Append(SchemaVersion);
            _line.Append(",\"endedAtUtc\":\"")
                 .Append(endedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", c)).Append('"');

            _line.Append(",\"roundReached\":").Append(summary.RoundReached.ToString(c));
            _line.Append(",\"durationSeconds\":").Append(Number(summary.DurationSeconds));
            _line.Append(",\"kills\":").Append(summary.Kills.ToString(c));
            _line.Append(",\"headshotKills\":").Append(summary.HeadshotKills.ToString(c));
            _line.Append(",\"meleeKills\":").Append(summary.MeleeKills.ToString(c));
            _line.Append(",\"pointsEarned\":").Append(summary.PointsEarned.ToString(c));

            _line.Append(",\"death\":");
            if (summary.HasDeathPosition)
            {
                _line.Append("{\"x\":").Append(Number(summary.DeathX))
                     .Append(",\"y\":").Append(Number(summary.DeathY))
                     .Append(",\"z\":").Append(Number(summary.DeathZ))
                     .Append('}');
            }
            else
            {
                // Sifir bir koordinattir, "yok" degil. null yazmak, haritanin
                // merkezinde sahte bir olum yigini olusmasini engeller.
                _line.Append("null");
            }

            // Atlanmis tur varsa zamanlama uydurmadir - ozet araci bu run'i CK-13
            // hesabinin disinda birakir. Isaretsiz yazmak, bir hata ayiklama run'inin
            // olcumu sessizce bozmasi demekti.
            _line.Append(",\"usedRoundSkip\":").Append(usedRoundSkip ? "true" : "false");

            _line.Append(",\"roundStartSeconds\":[");
            if (roundStartSeconds != null)
            {
                for (int i = 0; i < roundStartSeconds.Count; i++)
                {
                    if (i > 0) _line.Append(',');
                    _line.Append(Number(roundStartSeconds[i]));
                }
            }
            _line.Append(']');

            _line.Append("}\n");
        }

        /// <summary>Bir ondalık sayıyı kültürden bağımsız, iki basamakla yazar.</summary>
        private static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
