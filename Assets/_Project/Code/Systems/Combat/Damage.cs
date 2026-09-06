namespace Bunker.Systems.Combat
{
    /// <summary>Hasarın nereden geldiği. Ekonomi ödülü buna bakar (SYS-ekonomi).</summary>
    public enum DamageKind
    {
        Bullet,
        Melee,
        Environment
    }

    /// <summary>
    /// Tek bir hasar olayı. <b>Saf C#</b> — Unity tipi taşımaz, çünkü hasar bir oyun
    /// kuralıdır, bir sahne olayı değil. Nereye isabet ettiği (gövdenin neresi)
    /// bilerek yok: kural onu bilmek zorunda değil, görsel geri bildirim bilir.
    ///
    /// <para><b>Hasarın GELDIGI YON istisnadır</b> (2026-09-05). Oyun testi bulgusu:
    /// <i>"turlar ilerleyince tek yiyorum sanırım, ondan birden ölüyorum."</i> Oyuncu
    /// arkadan gelen vuruşu göremediği için ölümü haksızlık gibi okuyordu. Yön, HUD'un
    /// gösterebilmesi için hasarla birlikte taşınmak zorunda — ama <c>Vector3</c>
    /// olarak değil, <b>iki düz sayı</b> olarak: bu katman motor tipi tanımaz ve tanımaya
    /// başlarsa EditMode testlerinin tamamı Unity'ye bağlanır (systems-code.md).</para>
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageKind Kind;

        /// <summary>Kafa kutusuna isabet. Ekonomi ödülü ve bazı kartlar buna bakar.</summary>
        public readonly bool Headshot;

        /// <summary>Vuranın dünya konumu, yatay düzlem. İkisi de 0 ise "bilinmiyor".</summary>
        public readonly float SourceX;

        /// <summary>Vuranın dünya konumu, yatay düzlem.</summary>
        public readonly float SourceZ;

        /// <summary>Yön bilgisi var mı. Sıfır noktasından gelen hasar yok sayılır.</summary>
        public bool HasSource => SourceX != 0f || SourceZ != 0f;

        public DamageInfo(float amount, DamageKind kind = DamageKind.Bullet, bool headshot = false,
                          float sourceX = 0f, float sourceZ = 0f)
        {
            Amount = amount < 0f ? 0f : amount;
            Kind = kind;
            Headshot = headshot;
            SourceX = sourceX;
            SourceZ = sourceZ;
        }
    }

    /// <summary>Bir hasar uygulamasının sonucu.</summary>
    public readonly struct DamageResult
    {
        /// <summary>Gerçekten emilen hasar. Ölü bir hedefte 0'dır.</summary>
        public readonly float Absorbed;

        /// <summary>Bu vuruş öldürdü mü. <b>Yalnızca bir kez</b> true döner.</summary>
        public readonly bool Killed;

        /// <summary>Kalan can sıfırın altına inen kısım. Aşırı hasar geri bildirimi için.</summary>
        public readonly float Overkill;

        public DamageResult(float absorbed, bool killed, float overkill)
        {
            Absorbed = absorbed; Killed = killed; Overkill = overkill;
        }
    }

    /// <summary>
    /// Hasar alabilen her şey. <b>Unity'ye bağlı değil</b> — böylece hasar zinciri
    /// (silah → hedef → ekonomi) sahne açmadan test edilebilir (ÇK-16).
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>
        /// Buraya isabet etmek kafa vuruşu sayılır mı.
        ///
        /// <para><b>Hedef kendi anatomisini bilir, silah bilmez.</b> Alternatif, silahın
        /// katman testi yapması ya da bileşen tipini tanımasıydı; ikisi de yeni bir
        /// düşman tipi eklerken silaha dokunmayı gerektirirdi. Ayrıca çarpanın hasar
        /// uygulanmadan <i>önce</i> bilinmesi şart: sonradan ikinci bir uygulamayla
        /// eklemek tek atıştan iki hasar olayı üretir.</para>
        /// </summary>
        bool CountsAsHeadshot { get; }

        /// <summary>
        /// Bu hedefin <b>gerçek sahibi</b>. Bir vuruş kutusu için sahibi olan yaratık,
        /// başka her şey için kendisi.
        ///
        /// <para><b>Neden gerekiyor</b> (2026-09-05, delici mermi hatası): tek bir
        /// mermi aynı yaratığın birden fazla parçasından geçebilir (kafa, sonra gövde).
        /// "Aynı hedefe iki kez vurma" kuralı bir <i>kimlik</i> gerektiriyor ve bu
        /// kimliği hedefin kendisi vermeli — çağıran taraf tahmin etmemeli.</para>
        ///
        /// <para><b>Neden <c>transform.root</c> DEĞİL:</b> ilk sürüm tam olarak onu
        /// kullandı ve delici mermi hiç çalışmadı. Zombiler havuzun altında yaşıyor,
        /// yani <b>hepsinin root'u aynı nesne</b> — mermi ilk zombiden sonra herkesi
        /// "zaten vurdum" diye eledi. Sahne hiyerarşisinden kimlik türetmek, hiyerarşi
        /// değiştiği gün sessizce bozulur.</para>
        /// </summary>
        IDamageable DamageRoot { get; }

        DamageResult ApplyDamage(in DamageInfo damage);
    }
}
