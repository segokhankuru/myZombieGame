using Bunker.Systems.Combat;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// "Beni kovalayabilirsin" işareti. Oyuncu prefab'ına eklenir; zombiler yalnızca
    /// bu bileşeni taşıyan nesneleri hedefler.
    ///
    /// <para><b>Bağımlılık yönü:</b> bu bileşen <c>Bunker.AI</c>'da yaşar ama oyuncu
    /// nesnesinin üstünde durur. Böylece <c>Bunker.Gameplay</c>'in AI'ya, AI'nın da
    /// Gameplay'e bağımlılığı olmaz — ikisi de yalnızca bu işareti bilir.</para>
    ///
    /// <para>Hedefin canı varsa (<see cref="IDamageable"/>) zombi vuruşu oraya yazılır.
    /// Yoksa vuruş yine olur, sadece kimseye hasar yazılmaz — M1-04'te oyuncu canı
    /// henüz yok (M1-11), ve bu eksiklik zombiyi çökertmemeli.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Target Beacon")]
    public sealed class ZombieTargetBeacon : MonoBehaviour
    {
        [Tooltip("Hedef noktasinin ayaktan yuksekligi. Zombi govdenin ortasina vurur, " +
                 "ayaga degil - mesafe olcumu de oradan yapilmali.")]
        [SerializeField] private float aimHeightMeters = 1f;

        private Transform _transform;
        private IDamageable _damageable;

        /// <summary>Zombinin nişan aldığı nokta (gövde ortası).</summary>
        public Vector3 Position
        {
            get
            {
                Vector3 p = _transform.position;
                p.y += aimHeightMeters;
                return p;
            }
        }

        /// <summary>Ayak konumu — zombinin yol hedefi bu, nişan noktası değil.</summary>
        public Vector3 GroundPosition => _transform.position;

        /// <summary>Ölü ya da devre dışı bir hedef kovalanmaz.</summary>
        public bool IsTargetable => isActiveAndEnabled && (_damageable == null || _damageable.IsAlive);

        private void Awake()
        {
            _transform = transform;

            // Cana sahip olmak zorunlu degil: M1-04'te oyuncunun cani yok. Kare basina
            // GetComponent yasak oldugu icin bir kez cozuluyor (csharp-code.md).
            _damageable = GetComponent<IDamageable>();
        }

        private void OnEnable() => ZombieTargets.Register(this);
        private void OnDisable() => ZombieTargets.Unregister(this);

        /// <summary>Zombi vuruşu buraya iner. Can yoksa sessizce yutulur.</summary>
        public void ReceiveAttack(float damage)
        {
            _damageable?.ApplyDamage(new DamageInfo(damage, DamageKind.Melee));
        }
    }
}
