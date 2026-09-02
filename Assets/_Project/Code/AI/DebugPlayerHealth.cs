using Bunker.Systems.Combat;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// <b>Geçici.</b> Oyuncuya bir can havuzu verir, yalnızca M1-04'te zombi vuruşunun
    /// bir sonucu olsun diye. Gerçek oyuncu canı, düşme, ayağa kalkma ve ölüm ekranı
    /// <b>M1-11</b>'in işidir ve bu bileşen o zaman silinir.
    ///
    /// <para>Burada durmasının sebebi dürüst: vuruşun hasar yazacak bir yeri olmadan
    /// "zombi saldırıyor" iddiası test edilemez. Kalıcı sistem gelene kadarki en küçük
    /// iskele bu.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Debug Player Health (gecici)")]
    public sealed class DebugPlayerHealth : MonoBehaviour, IDamageable
    {
        // TODO(gameplay-programmer, M1-11): Bu bilesen M1-11'de kalici oyuncu cani ile
        // degistirilecek. 100 bir denge degeri degil, zombie.json'daki 'damage'
        // aciklamasinin dayandigi REFERANS degerdir - orada "oyuncu cani 100 kabul
        // edilir" yaziyor. Ikisi ayrilirsa hasar aciklamasi anlamsizlasir.
        [SerializeField] private float referenceMaxHealth = 100f;

        [Tooltip("Acikken can sifira inince yeniden dolar. M1-04'te olum ekrani yok; " +
                 "kapatirsan oyuncu olur ve zombiler hedefsiz kalir.")]
        [SerializeField] private bool respawnOnDeath = true;

        private HealthPool _pool;

        public float Fraction01 => _pool?.Fraction01 ?? 1f;
        public float Current => _pool?.Current ?? 0f;
        public float Max => referenceMaxHealth;
        public bool IsAlive => _pool == null || _pool.IsAlive;

        /// <summary>Oyuncuda kafa vuruşu yok — zombiler telegrafı olan tek bir vuruş yapar.</summary>
        public bool CountsAsHeadshot => false;

        private void Awake() => _pool = new HealthPool(referenceMaxHealth);

        public DamageResult ApplyDamage(in DamageInfo damage)
        {
            DamageResult result = _pool.ApplyDamage(damage);

            if (result.Killed && respawnOnDeath)
            {
                Debug.Log("[Zombie/Sandbox] Oyuncu oldu - M1-04'te olum ekrani yok, can dolduruldu.");
                _pool.ResetTo(referenceMaxHealth);
            }

            return result;
        }
    }
}
