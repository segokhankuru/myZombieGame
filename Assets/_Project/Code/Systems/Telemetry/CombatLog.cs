using System.Globalization;
using System.Text;

namespace Bunker.Systems.Telemetry
{
    /// <summary>
    /// Bir oturumun <b>bütün hasar hareketi</b>, satır satır. 2026-09-08.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"ölüm anında hâlâ tek yiyorum çünkü
    /// bunu en sağlıklı böyle bakarak anlayacağım."</i> Bir ölümün sebebi, ekranda
    /// geçen yarım saniyeden okunamaz. <c>runs.jsonl</c> run'ın <i>özetini</i> tutuyor
    /// (kaç tur, kaç öldürme) ama tek bir vuruşu göstermiyor — yani "30 mu yedim,
    /// dört kere 30 mu" sorusunu cevaplayamıyor. Bu dosya tam o soruyu cevaplar.</para>
    ///
    /// <para><b>Neden ayrı bir dosya, <c>Debug.Log</c> değil:</b> Unity konsolu bir
    /// oturumun binlerce hasar satırını taşıyamaz — her satır bir yığın izi toplar ve
    /// ölçüm aracı ölçtüğü şeyi bozar. Ayrıca konsol aranabilir değildir; bu dosya bir
    /// metin editöründe aranabilir.</para>
    ///
    /// <para><b>Ölüm anı ayrı basılır.</b> Oyuncu öldüğünde son
    /// <see cref="RecentCapacity"/> vuruş, <b>aralarındaki süreyle birlikte</b> tekrar
    /// yazılır. "Tek mi yedim" sorusunun cevabı tam olarak o bloktur: dört ayrı
    /// vuruşun 0.1 saniyeye sığması, ekranda tek vuruş gibi görünür — ve o, hasar
    /// sayısıyla değil aynı karede vuran zombi sayısıyla ilgili bir sorundur.</para>
    ///
    /// <para><b>Saf C#.</b> <c>Bunker.Systems</c> motoru görmez; saat ve klasör
    /// dışarıdan verilir (<c>CombatLogBootstrap</c>) — <see cref="RunLogWriter"/> ile
    /// aynı gerekçe.</para>
    ///
    /// <para><b>Ölçüm aracı oyunu bozamaz.</b> Kurulmamışken her çağrı bir <c>null</c>
    /// kontrolüdür; yazma hatası bir kez bildirilip yutulur.</para>
    /// </summary>
    public static class CombatLog
    {
        /// <summary>Ölüm dökümünde kaç vuruş geriye bakılır.</summary>
        public const int RecentCapacity = 16;

        private static CombatLogWriter _writer;

        /// <summary>Oturum başından beri geçen saniye. Motoru gören taraf yazar.</summary>
        private static float _seconds;

        private static int _round = 1;

        private static readonly StringBuilder Line = new StringBuilder(192);

        // Son vuruslarin halka tamponu. SABIT boyut, vurus basina tahsis yok
        // (csharp-code.md): olcum araci olctugu seyi bozmamali.
        private static readonly RecentHit[] Recent = new RecentHit[RecentCapacity];
        private static int _recentCount;
        private static int _recentNext;

        private readonly struct RecentHit
        {
            public readonly float Seconds;
            public readonly string Attacker;
            public readonly string Weapon;
            public readonly float Amount;
            public readonly float Remaining;

            public RecentHit(float seconds, string attacker, string weapon,
                             float amount, float remaining)
            {
                Seconds = seconds;
                Attacker = attacker;
                Weapon = weapon;
                Amount = amount;
                Remaining = remaining;
            }
        }

        /// <summary>Yazıcı kuruldu mu. Kurulmadan yapılan her çağrı sessizce düşer.</summary>
        public static bool IsInstalled => _writer != null;

        /// <summary>Dosyanın yolu; kurulu değilse <c>null</c>.</summary>
        public static string FilePath => _writer == null ? null : _writer.FilePath;

        /// <summary>
        /// Yazıcıyı takar. <b>Yalnızca köprü çağırır.</b> İkinci çağrı öncekini
        /// boşaltır: domain reload kapalıyken ikinci Play oturumu aynı dosyaya iki
        /// kez yazardı.
        /// </summary>
        public static void Install(CombatLogWriter writer)
        {
            if (_writer != null) _writer.Flush();

            _writer = writer;
            _seconds = 0f;
            _round = 1;
            _recentCount = 0;
            _recentNext = 0;
        }

        /// <summary>Oturum saatini ilerletir. Köprünün <c>Update</c>'i çağırır.</summary>
        public static void SetClock(float sessionSeconds) => _seconds = sessionSeconds;

        /// <summary>Hangi turdayız. Her satırın başında yazar.</summary>
        public static void SetRound(int round) => _round = round < 1 ? 1 : round;

        /// <summary>Tamponu diske boşaltır.</summary>
        public static void Flush()
        {
            if (_writer != null) _writer.Flush();
        }

        // ------------------------------------------------------------- olaylar

        /// <summary>
        /// Bir hasar olayı. <b>Tek satır, sabit alan sırası</b> — dosya gözle
        /// taranabilen bir tabloya benzemeli.
        /// </summary>
        /// <param name="attacker">Vuran: "Oyuncu", "Zombi#42", "Patlama", "Nuke".</param>
        /// <param name="weapon">Ne ile: "TUFEK", "BALTA", "pence", "sarapnel".</param>
        /// <param name="target">Kim yedi: "Oyuncu", "Zombi#42", "Barikat".</param>
        /// <param name="part">Neresine: "govde", "kafa", "sol bacak" ya da <c>null</c>.</param>
        /// <param name="amount">Uygulanan hasar.</param>
        /// <param name="remaining">Vuruştan SONRA kalan can. Bilinmiyorsa negatif.</param>
        /// <param name="max">Hedefin can tavanı. Bilinmiyorsa negatif.</param>
        /// <param name="distanceMeters">
        /// Vuranla vurulan arasındaki yatay mesafe; bilinmiyorsa negatif (2026-09-10).
        /// "Kaç metreden yedim" sorusu tahminle değil satırla cevaplansın diye.
        /// </param>
        public static void Damage(string attacker, string weapon, string target, string part,
                                  float amount, float remaining, float max,
                                  bool killed = false, bool headshot = false,
                                  float distanceMeters = -1f)
        {
            if (_writer == null) return;

            Line.Clear();
            Head("HASAR");

            Line.Append(attacker ?? "?");
            if (!string.IsNullOrEmpty(weapon)) Line.Append('[').Append(weapon).Append(']');

            Line.Append(" -> ").Append(target ?? "?");
            if (!string.IsNullOrEmpty(part)) Line.Append('(').Append(part).Append(')');

            Line.Append("  ").Append(Number(amount));

            if (max >= 0f || remaining >= 0f)
            {
                Line.Append("  can ").Append(Number(remaining < 0f ? 0f : remaining));
                if (max >= 0f) Line.Append('/').Append(Number(max));
            }

            if (headshot) Line.Append("  KAFA");
            if (killed) Line.Append("  OLDURDU");

            if (distanceMeters >= 0f)
            {
                Line.Append("  mesafe ").Append(Number(distanceMeters)).Append(" m");
            }

            _writer.Write(Line.ToString());
        }

        /// <summary>
        /// Oyuncunun yediği hasar: satırı yazar <b>ve</b> ölüm dökümü için hatırlar.
        ///
        /// <para><b>Neden ayrı bir giriş:</b> ölüm sorusu yalnızca oyuncuyla ilgili.
        /// Her hasarı hatırlamak, kalabalık bir turda halkayı saniyede yüz kez
        /// döndürür ve ölüm anına dair hiçbir şey kalmaz.</para>
        /// </summary>
        /// <param name="distanceMeters">
        /// Vuranın yatay mesafesi; bilinmiyorsa negatif (2026-09-10).
        /// </param>
        public static void PlayerDamage(string attacker, string weapon, float amount,
                                        float remaining, float max, bool killed,
                                        float distanceMeters = -1f)
        {
            Damage(attacker, weapon, "Oyuncu", null, amount, remaining, max, killed,
                   distanceMeters: distanceMeters);

            if (_writer == null) return;

            Recent[_recentNext] = new RecentHit(_seconds, attacker, weapon, amount, remaining);
            _recentNext = (_recentNext + 1) % RecentCapacity;
            if (_recentCount < RecentCapacity) _recentCount++;
        }

        /// <summary>Serbest bir olay satırı: tur, eşya, satın alma, kart.</summary>
        public static void Event(string kind, string detail)
        {
            if (_writer == null) return;

            Line.Clear();
            Head(kind);
            Line.Append(detail);

            _writer.Write(Line.ToString());
        }

        /// <summary>
        /// Oyuncu öldü: <b>son vuruşlar aralarındaki süreyle birlikte</b> dökülür.
        ///
        /// <para>Aradaki süre asıl bilgidir. Dört vuruşun 80 ms içine sığması,
        /// oyuncunun "tek yedim" diye okuduğu şeydir.</para>
        /// </summary>
        public static void PlayerDied(string cause)
        {
            if (_writer == null) return;

            Event("OLUM", "Oyuncu oldu. Son vurus: " + (cause ?? "bilinmiyor"));

            if (_recentCount == 0)
            {
                Event("OLUM", "  son vurus kaydi YOK - hasar hic loglanmamis.");
                _writer.Flush();
                return;
            }

            int start = (_recentNext - _recentCount + RecentCapacity) % RecentCapacity;
            bool hasPrevious = false;
            float previous = 0f;
            float total = 0f;

            for (int i = 0; i < _recentCount; i++)
            {
                RecentHit hit = Recent[(start + i) % RecentCapacity];
                total += hit.Amount;

                Line.Clear();
                Head("OLUM");
                Line.Append("  #").Append((i + 1).ToString(CultureInfo.InvariantCulture));
                Line.Append("  t=").Append(Number(hit.Seconds));
                Line.Append("  +").Append(hasPrevious ? Number(hit.Seconds - previous) : "-");
                Line.Append(" sn  ").Append(hit.Attacker ?? "?");

                if (!string.IsNullOrEmpty(hit.Weapon))
                {
                    Line.Append('[').Append(hit.Weapon).Append(']');
                }

                Line.Append("  ").Append(Number(hit.Amount));
                Line.Append("  -> can ").Append(Number(hit.Remaining < 0f ? 0f : hit.Remaining));

                _writer.Write(Line.ToString());

                previous = hit.Seconds;
                hasPrevious = true;
            }

            RecentHit first = Recent[start];
            RecentHit last = Recent[(_recentNext - 1 + RecentCapacity) % RecentCapacity];

            Event("OLUM", "  toplam " + Number(total) + " hasar, " +
                          Number(last.Seconds - first.Seconds) + " saniye icinde, " +
                          _recentCount.ToString(CultureInfo.InvariantCulture) + " vurusta.");

            _recentCount = 0;
            _recentNext = 0;

            // Olum, dosyanin okunmaya deger tek ani: burada diske YAZILIR. Tamponda
            // kalan bir olum satiri, oyun cokerse hic yazilmaz.
            _writer.Flush();
        }

        /// <summary>Yeni run: hafıza sıfırlanır, dosyaya ayraç düşer.</summary>
        public static void RunRestarted()
        {
            _recentCount = 0;
            _recentNext = 0;
            _round = 1;

            Event("RUN", "======================== yeni run ========================");
        }

        // ------------------------------------------------------------ yardimci

        private static void Head(string kind)
        {
            Line.Append('[').Append(Number(_seconds)).Append("s tur ")
                .Append(_round.ToString(CultureInfo.InvariantCulture)).Append("] ");

            Line.Append(kind);
            for (int i = kind.Length; i < 6; i++) Line.Append(' ');
            Line.Append(' ');
        }

        /// <summary>
        /// Kültürden bağımsız sayı. Türkçe Windows'ta <c>30.5f.ToString()</c>
        /// <c>"30,5"</c> üretir; bu satır <see cref="RunLogWriter"/>'daki kültür
        /// savunmasının aynısı.
        /// </summary>
        private static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
