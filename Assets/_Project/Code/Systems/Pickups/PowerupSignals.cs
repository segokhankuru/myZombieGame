using System;

namespace Bunker.Systems.Pickups
{
    /// <summary>
    /// Eşya toplandı — assembly sınırını aşan yayın (2026-09-07).
    ///
    /// <para><b>Neden bir sinyal, doğrudan çağrı değil:</b> eşyayı dünyada tutan
    /// nesne <c>Bunker.AI</c>'da, canı <c>Bunker.Gameplay</c>'de, mermiyi de yine
    /// Gameplay'de duruyor ve <b>Gameplay, AI'ı göremez</b> (gameplay-code.md). Ters
    /// bağımlılık kurmak yerine olay yukarı doğru gider; <c>RoundSignals</c> ile aynı
    /// desen.</para>
    ///
    /// <para><b>Yük olayla birlikte gider.</b> Ne kadar iyileştirdiği ya da kaç şarjör
    /// verdiği <c>zombie.json → drops</c>'ta yazar ve olayı yayan taraf onu okur;
    /// abone tarafın kendi sayısını tutması, aynı denge değerinin iki yerde durması
    /// olurdu (config-data.md).</para>
    ///
    /// <para><b>Aboneliğin kuralı</b> (RunSignals ile aynı): <c>OnEnable</c>'da abone
    /// ol, <c>OnDisable</c>'da bırak. Statik bir olaya abone kalan yok edilmiş bir
    /// nesne, sahne değişiminde patlar.</para>
    /// </summary>
    public static class PowerupSignals
    {
        /// <summary>
        /// Oyuncu bir eşyayı <b>kullandı</b>: türü, oransal yükü ve süresi.
        ///
        /// <para><c>amount</c>: cana göre oran (0.35), mermiye göre şarjör sayısı (3).
        /// <c>seconds</c>: süreli etkilerde süre, anlık etkilerde sıfır.</para>
        ///
        /// <para><b>2026-09-09'a kadar bu olay "topladı" demekti</b> ve eşya yerden
        /// alındığı an patlıyordu. Artık toplama ile kullanma iki ayrı an
        /// (<see cref="Stored"/>): eşya önce cebe girer, sonra oyuncunun bastığı tuşla
        /// burası çalışır. <b>Etkiyi uygulayan taraf hiç değişmedi</b> — can
        /// Gameplay'de, saha etkisi AI'da; değişen tek şey olayın <i>ne zaman</i>
        /// atıldığı.</para>
        /// </summary>
        public static event Action<PowerupKind, float, float> Picked;

        /// <summary>
        /// Oyuncu bir eşyayı <b>yerden aldı ve cebine koydu</b> (2026-09-09).
        ///
        /// <para>Etki uygulanmaz — bu olay yalnızca envanteri ve arayüzü ilgilendirir.
        /// Yükü yine taşır, çünkü eşyanın <i>ne kadar</i> iyileştirdiğini bilen taraf
        /// onu düşüren taraftır (<c>zombie.json → drops</c>) ve cebin kendi sayısını
        /// tutması aynı denge değerini iki yere koymak olurdu (config-data.md).</para>
        /// </summary>
        public static event Action<PowerupKind, float, float> Stored;

        /// <summary>
        /// Cebe koymayı <b>kabul eden</b> taraf — tek sahipli, çoklu değil.
        ///
        /// <para><b>Neden bir olay değil, bir delege:</b> toplama <i>başarısız
        /// olabilir</i> (slot dolu) ve çağıran tarafın bunu bilmesi gerekiyor — eşyayı
        /// yok edecek mi, yerde mi bırakacak. Çok aboneli bir olay dönüş değeri
        /// taşıyamaz; taşısaydı bile "iki cepten biri aldı" diye bir durum çıkardı ve
        /// eşyanın kimde olduğu belirsizleşirdi. Cep <b>bir tanedir</b>: yerel
        /// oyuncunun cebi.</para>
        ///
        /// <para>M-02 notu: co-op'ta bunun yerine sunucunun, eşyayı toplayan
        /// <i>oyuncuyu</i> bulup onun cebine koyması gerekir (netcode.md — aynı tick
        /// çakışması). Şu anki tek sahiplik solo host varsayımıdır.</para>
        /// </summary>
        public static Func<PowerupKind, float, float, bool> StoreRequest;

        /// <summary>Kullanıldı. <b>Yalnızca sunucu</b> çağırır (ADR-0004).</summary>
        public static void RaisePicked(PowerupKind kind, float amount, float seconds) =>
            Picked?.Invoke(kind, amount, seconds);

        /// <summary>
        /// Eşyayı cebe koymayı dener.
        /// </summary>
        /// <returns>
        /// Girdiyse <c>true</c> — çağıran eşyayı yok eder. <c>false</c> ise slot dolu
        /// ve eşya <b>yerde kalmalıdır</b>.
        /// </returns>
        /// <remarks>
        /// <b>Cep bağlı değilse eşya eskisi gibi ANINDA çalışır.</b> Sandbox ve test
        /// sahnelerinde oyuncu prefab'ı yok; orada sessizce hiçbir şey yapmayan bir
        /// eşya, "drop bozulmuş" diye saatler harcatırdı. Yedek yol, eşyanın her
        /// sahnede bir şey yapmasını garanti eder.
        /// </remarks>
        public static bool TryStore(PowerupKind kind, float amount, float seconds)
        {
            if (StoreRequest == null)
            {
                RaisePicked(kind, amount, seconds);
                return true;
            }

            if (!StoreRequest(kind, amount, seconds)) return false;

            Stored?.Invoke(kind, amount, seconds);
            return true;
        }

        /// <summary>
        /// Oyun açılışında zinciri koparır. Domain reload kapalıyken statik olay,
        /// bir önceki oynatmanın ölü abonelerini taşır.
        /// </summary>
        public static void Clear()
        {
            Picked = null;
            Stored = null;
            StoreRequest = null;
        }
    }
}
