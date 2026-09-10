using System;

namespace Bunker.Systems.Pickups
{
    /// <summary>
    /// Oyuncunun cebi: yerden toplanan eşyalar burada <b>birikir</b> ve istenen anda
    /// kullanılır (2026-09-09).
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"yerden topladığımız dropları
    /// stacklenebilen şekilde biriktirebilelim... atıyorum canı 6'ya koydun ve birikti,
    /// canım azaldığında 6'ya basınca canım dolsun."</i> Önceki hâlde eşya toplandığı
    /// <b>an</b> patlıyordu ve bu, eşyanın çoğunu çöpe atıyordu: canı tamken üstünden
    /// geçtiğin bir can eşyası hiçbir şey yapmadan yok oluyordu, dondurma da sürünün
    /// henüz gelmediği bir anda harcanıyordu. Eşya artık bir <b>kaynak</b>; ne zaman
    /// harcanacağı oyuncunun kararı.</para>
    ///
    /// <para><b>Slot = eşya türü.</b> <see cref="PowerupKind"/> sırası doğrudan slot
    /// sırasıdır ve tuş <c>6 + sıra</c>: 6 can, 7 mermi, 8 yavaşlatma, 9 dondurma,
    /// 0 nuke. Ayrı bir eşleme tablosu <b>yok</b> — olsaydı enum'a altıncı bir eşya
    /// eklendiği gün iki yerin birden değişmesi gerekirdi ve biri unutulurdu
    /// (csharp-code.md: aynı iş kuralı iki yerde duramaz).</para>
    ///
    /// <para><b>Tavan var ve config'den geliyor</b> (<c>zombie.json → drops.stackPerSlot</c>).
    /// Tavansız bir cep, geç turda oyuncuya on can ve altı nuke taşıtır; yani zorluk
    /// eğrisi envanterle iptal edilir. Tavan dolduğunda toplama <b>başarısız olur</b>
    /// ve eşya yerde kalır — sessizce yutulmaz, çünkü yutulan bir eşya oyuncunun
    /// öğrenemediği bir kuraldır.</para>
    ///
    /// <para><b>Saf C#.</b> Unity'yi bilmez, sahne gerektirmez: yığılma, tavan ve
    /// tüketim kuralları Unity açmadan test edilir (ÇK-16 ile aynı gerekçe).</para>
    /// </summary>
    public sealed class PowerupInventory
    {
        /// <summary>Slot sayısı = eşya türü sayısı. İkisi ayrılamaz (bkz. sınıf notu).</summary>
        public static readonly int SlotCount = Enum.GetValues(typeof(PowerupKind)).Length;

        /// <summary>
        /// İlk drop slotunun tuşu. <b>1 bıçak, 2-3 ateşli silahlar, 4-8 eşyalar</b>.
        ///
        /// <para><b>6'dan 4'e indi</b> (2026-09-09): ateşli silah taşıma sınırı ikiye
        /// inince (<c>weapon.json → loadout.firearmSlots</c>) araya boş iki tuş kalıyordu.
        /// Geliştiricinin şikâyeti zaten buydu — <i>"silahlar numaralara sığmadı"</i> —
        /// ve boşluk bırakmak şikâyetin yarısını çözmüş olurdu: en uzak eşya tuşu 0
        /// olarak kalır ve elin klavyede yer değiştirmesi gerekirdi.</para>
        ///
        /// <para><b>Sayı burada, silah slotu config'de</b> ve ikisi elle hizalanıyor.
        /// Türetmek daha temiz görünürdü ama <c>Bunker.Systems</c> silah config'ini
        /// bilmiyor ve bilmemeli; bu satırın bedeli, silah slotu değişirse buranın da
        /// değişmesi. O yüzden kural tek bir yerde <b>yazılı</b>: 1 + silah slotu + 1.</para>
        /// </summary>
        public const int FirstSlotNumber = 4;

        private readonly int[] _counts;
        private readonly int _capacityPerSlot;

        /// <param name="capacityPerSlot">
        /// <c>zombie.json → drops.stackPerSlot</c>. Bir slotta en fazla kaç eşya durur.
        /// </param>
        public PowerupInventory(int capacityPerSlot)
        {
            // Sifir ya da negatif bir tavan, sessizce "hicbir sey toplanamaz" demek
            // olurdu ve oyun testinde "droplar calismiyor" diye okunurdu - teshisi en
            // zor hata turu. En az bir tane tasinabilmeli.
            _capacityPerSlot = capacityPerSlot < 1 ? 1 : capacityPerSlot;
            _counts = new int[SlotCount];
        }

        /// <summary>Bir slotun tavanı.</summary>
        public int CapacityPerSlot => _capacityPerSlot;

        /// <summary>Bu türden kaç tane var.</summary>
        public int Count(PowerupKind kind) => _counts[(int)kind];

        /// <summary>Bu slotta (0 tabanlı) kaç tane var.</summary>
        public int CountAt(int slot) =>
            slot >= 0 && slot < SlotCount ? _counts[slot] : 0;

        /// <summary>Bu türden en az bir tane var mı — kullanılabilir mi.</summary>
        public bool Has(PowerupKind kind) => _counts[(int)kind] > 0;

        /// <summary>Bu slot dolu mu; toplama başarısız olacak mı.</summary>
        public bool IsFull(PowerupKind kind) => _counts[(int)kind] >= _capacityPerSlot;

        /// <summary>Cepte hiç eşya yok mu (arayüz çubuğu bunu sorar).</summary>
        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _counts.Length; i++)
                {
                    if (_counts[i] > 0) return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Bir eşyayı cebe koyar.
        /// </summary>
        /// <returns>
        /// Girdiyse <c>true</c>. Slot doluysa <c>false</c> — <b>çağıran taraf eşyayı
        /// yerde bırakmalıdır.</b> Yutup <c>true</c> dönmek, oyuncuya "topladım" deyip
        /// hiçbir şey vermemek olurdu.
        /// </returns>
        public bool TryStore(PowerupKind kind)
        {
            int slot = (int)kind;
            if (_counts[slot] >= _capacityPerSlot) return false;

            _counts[slot]++;
            return true;
        }

        /// <summary>
        /// Bir eşyayı harcar.
        /// </summary>
        /// <returns>
        /// Harcandıysa <c>true</c>. Cepte yoksa <c>false</c> — çağıran taraf hiçbir
        /// etki uygulamamalıdır. <b>Önce tüket, sonra uygula</b>: ters sırada, etkinin
        /// içinden gelen bir hata sayacı azaltmadan bırakır ve eşya sonsuzlaşır.
        /// </returns>
        public bool TryConsume(PowerupKind kind)
        {
            int slot = (int)kind;
            if (_counts[slot] <= 0) return false;

            _counts[slot]--;
            return true;
        }

        /// <summary>Slot numarasından (0 tabanlı) eşya türü.</summary>
        public static PowerupKind KindAt(int slot) => (PowerupKind)slot;

        /// <summary>
        /// Bu eşyanın ekranda göründüğü tuş: 6, 7, 8, 9 ve beşincisi <b>0</b>.
        ///
        /// <para>Klavyenin sırası 6-7-8-9-0'dır; beşinciye "10" yazmak, oyuncunun
        /// olmayan bir tuşu aramasına yol açardı.</para>
        /// </summary>
        public static int KeyNumberFor(int slot)
        {
            int number = FirstSlotNumber + slot;
            return number >= 10 ? number - 10 : number;
        }

        /// <summary>
        /// Yeni run: cep boşalır.
        ///
        /// <para>Buradaki eksik bir satır, ikinci run'ın birincinin nuke'uyla başlaması
        /// demek — <c>CardLoadout.Reset</c> ile aynı sınıf hata.</para>
        /// </summary>
        public void Clear() => Array.Clear(_counts, 0, _counts.Length);
    }
}
