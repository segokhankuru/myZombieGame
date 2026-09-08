using System;
using System.IO;
using System.Text;

namespace Bunker.Systems.Telemetry
{
    /// <summary>
    /// <see cref="CombatLog"/>'un diske yazan yarısı. 2026-09-08.
    ///
    /// <para><b>Neden tamponlu:</b> yoğun bir turda saniyede yüzlerce satır düşüyor.
    /// <c>RunLogWriter</c>'ın "her satırda aç-yaz-kapat" yaklaşımı orada doğruydu
    /// (run başına bir satır), burada kare süresini yiyen bir dosya sistemi çağrısına
    /// dönerdi. Satırlar bellekte birikir, <see cref="FlushEveryLines"/>'da bir kez
    /// yazılır — ve ölüm, run sonu, tur sonu gibi <i>okunmaya değer</i> anlarda
    /// <see cref="Flush"/> elle çağrılır.</para>
    ///
    /// <para><b>Neden oturum başına ayrı dosya, tek bir büyük dosya değil:</b>
    /// geliştirici "şu oturumda ne oldu" diye bakıyor. Tek dosya her açılışta uzar ve
    /// aranan an on binlerce satırın arasında kalır; ayrıca çöken bir oturum bütün
    /// geçmişi riske atar. Dosya adı zamandan geliyor, yani sıralaması da doğru.</para>
    ///
    /// <para><b>Saf C#</b> — Unity görmez, gerçek dosya sistemine karşı EditMode'da
    /// test edilir (<c>test-code.md</c>).</para>
    /// </summary>
    public sealed class CombatLogWriter
    {
        /// <summary>Kaç satırda bir diske yazılır.</summary>
        public const int FlushEveryLines = 64;

        private readonly StringBuilder _buffer = new StringBuilder(8 * 1024);

        private int _pending;
        private bool _errorReported;

        /// <param name="directory">Yazılacak klasör. Yoksa yaratılır.</param>
        /// <param name="fileName">Dosya adı. Oturum başına benzersiz olmalı.</param>
        public CombatLogWriter(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Klasor bos olamaz.", nameof(directory));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Dosya adi bos olamaz.", nameof(fileName));

            Directory = directory;
            FilePath = Path.Combine(directory, fileName);
        }

        public string Directory { get; }

        public string FilePath { get; }

        /// <summary>Kaç satır yazıldı (tamponda bekleyenler dahil). Teşhis için.</summary>
        public int WrittenCount { get; private set; }

        /// <summary>İlk yazma hatasının mesajı, yoksa <c>null</c>.</summary>
        public string LastError { get; private set; }

        /// <summary>
        /// Bir satır ekler. <b>Satır sonu burada konur</b> — çağıranın her yerde
        /// hatırlaması gereken bir şey olmamalı.
        /// </summary>
        public void Write(string line)
        {
            if (line == null) return;

            _buffer.Append(line).Append('\n');
            WrittenCount++;
            _pending++;

            if (_pending >= FlushEveryLines) Flush();
        }

        /// <summary>
        /// Tamponu diske ekler.
        ///
        /// <para><b>Catch bilerek geniş</b> (<see cref="RunLogWriter"/> ile aynı ders):
        /// bu yazıcı ölüm yolundan da çağrılıyor. Buradan kaçan bir istisna skor
        /// ekranını hiç açmazdı — bir ölçüm aracının ölçtüğü şeyi bozması, aracın
        /// kendisinden kötüdür.</para>
        /// </summary>
        public void Flush()
        {
            if (_buffer.Length == 0) return;

            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllText(FilePath, _buffer.ToString(), Encoding.UTF8);

                _buffer.Clear();
                _pending = 0;
            }
            catch (Exception e)
            {
                if (!_errorReported)
                {
                    _errorReported = true;
                    LastError = $"Savas gunlugu yazilamadi ({FilePath}): {e.Message}";
                }

                // Tampon TEMIZLENIR: yazilamayan satirlar birikirse bellek sinirsiz
                // buyur ve ikinci hata birincisinden cok daha kotu olur.
                _buffer.Clear();
                _pending = 0;
            }
        }
    }
}
