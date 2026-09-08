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
        /// Oyuncu bir eşya topladı: türü, oransal yükü ve süresi.
        ///
        /// <para><c>amount</c>: cana göre oran (0.35), mermiye göre şarjör sayısı (3).
        /// <c>seconds</c>: süreli etkilerde süre, anlık etkilerde sıfır.</para>
        /// </summary>
        public static event Action<PowerupKind, float, float> Picked;

        /// <summary>Yayınlar. <b>Yalnızca sunucu</b> çağırır (ADR-0004).</summary>
        public static void RaisePicked(PowerupKind kind, float amount, float seconds) =>
            Picked?.Invoke(kind, amount, seconds);

        /// <summary>
        /// Oyun açılışında zinciri koparır. Domain reload kapalıyken statik olay,
        /// bir önceki oynatmanın ölü abonelerini taşır.
        /// </summary>
        public static void Clear() => Picked = null;
    }
}
