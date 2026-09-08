using Bunker.Systems.Combat;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Zombinin neresine isabet edildiği. <b>Anatomi vuruş kutusunun kendisinde</b>
    /// yaşar; silah nereye çarptığını bilmez.
    /// </summary>
    public enum ZombiePart
    {
        Body,
        Head,
        LegLeft,
        LegRight
    }

    /// <summary>
    /// Zombinin üstündeki bir vuruş kutusu. Silah (M1-06) ışını neye çarptığını bilmez;
    /// çarptığı şey ne olduğunu <b>kendisi</b> söyler.
    ///
    /// <para><b>Neden bileşen, katman değil:</b> kafa vuruşu bir katman testiyle de
    /// yapılabilirdi, ama o zaman kafa kutusunun varlığı proje ayarlarında saklı bir
    /// bilgi olurdu. Bileşen olarak prefab'ta görünür ve yeni zombi tipi eklerken
    /// unutulmaz.</para>
    ///
    /// <para><b>Bacaklar da bir kutudur</b> (2026-09-05): kafa zombiyi bitirir, bacak
    /// onu <i>yavaşlatır</i>. Nişan almanın iki ayrı ödülü olması, sürünün içinde hedef
    /// seçmeyi bir karar hâline getirir — üçüncü bir zombi tipi yazmadan.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Hitbox")]
    public sealed class ZombieHitbox : MonoBehaviour, IDamageable
    {
        [Tooltip("Bu kutu zombinin neresi. Ekonomi kafa vurusuna ayri puan verir " +
                 "(SYS-ekonomi); bacak vurusu zombiyi surunmeye dusurur.")]
        [SerializeField] private ZombiePart part = ZombiePart.Body;

        [Tooltip("Hasari yazacak zombi. Bos birakilirsa ust nesnelerde aranir.")]
        [SerializeField] private ZombieAgent owner;

        public ZombiePart Part => part;
        public bool IsHead => part == ZombiePart.Head;
        public ZombieAgent Owner => owner;
        public bool IsAlive => owner != null && owner.IsAlive;

        /// <summary>Kutu kendi anatomisini bilir; silah bilmez.</summary>
        public bool CountsAsHeadshot => part == ZombiePart.Head;

        /// <summary>
        /// Kutunun sahibi olan zombi. Delici mermi "ayni yaratiga iki kez vurma"
        /// kuralini bununla uygular: kafa ve govde ayni yaratigin parcalari.
        /// </summary>
        public IDamageable DamageRoot => owner != null ? owner : (IDamageable)this;

        private void Awake()
        {
            if (owner == null) owner = GetComponentInParent<ZombieAgent>();

            if (owner == null)
            {
                // Sessiz varsayilan yok: sahibi olmayan bir vurus kutusu, mermilerin
                // sessizce yok oldugu bir hatadir (csharp-code.md).
                Debug.LogError($"[Zombie] '{name}' vurus kutusunun ZombieAgent sahibi yok. " +
                               "Kutu bir ZombieAgent'in altinda olmali ya da 'owner' " +
                               "alani elle doldurulmali.", this);
                enabled = false;
            }
        }

        public DamageResult ApplyDamage(in DamageInfo damage)
        {
            if (owner == null) return default;

            // Kafa bilgisi kutudan gelir, atistan degil: silah nereye isabet ettigini
            // bilmek zorunda kalmaz.
            //
            // KAYNAK KONUMU DA TASINIR (2026-09-06). Bu satirlarin yoklugu, merminin
            // GELDIGI YONU tam burada dusuruyordu - mermiler zombinin kendisine degil
            // vurus kutularina isabet ediyor, yani gecen her atis buradan geciyor.
            // Itme yonu de o yuzden hep zombinin baktigi yone gore hesaplaniyordu.
            var routed = new DamageInfo(damage.Amount, damage.Kind,
                                        damage.Headshot || part == ZombiePart.Head,
                                        damage.SourceX, damage.SourceZ, damage.Source);

            return owner.ApplyDamageToPart(routed, part);
        }
    }
}
