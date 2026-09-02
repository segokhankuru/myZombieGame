using Bunker.Systems.Combat;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Zombinin üstündeki bir vuruş kutusu. Silah (M1-06) ışını neye çarptığını bilmez;
    /// çarptığı şey ne olduğunu <b>kendisi</b> söyler.
    ///
    /// <para><b>Neden bileşen, katman değil:</b> kafa vuruşu bir katman testiyle de
    /// yapılabilirdi, ama o zaman kafa kutusunun varlığı proje ayarlarında saklı bir
    /// bilgi olurdu. Bileşen olarak prefab'ta görünür ve yeni zombi tipi eklerken
    /// unutulmaz.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Hitbox")]
    public sealed class ZombieHitbox : MonoBehaviour, IDamageable
    {
        [Tooltip("Kafa kutusu mu. Ekonomi kafa vurusuna ayri puan verir (SYS-ekonomi).")]
        [SerializeField] private bool head;

        [Tooltip("Hasari yazacak zombi. Bos birakilirsa ust nesnelerde aranir.")]
        [SerializeField] private ZombieAgent owner;

        public bool IsHead => head;
        public ZombieAgent Owner => owner;
        public bool IsAlive => owner != null && owner.IsAlive;

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
            var routed = new DamageInfo(damage.Amount, damage.Kind, damage.Headshot || head);
            return owner.ApplyDamage(routed);
        }
    }
}
