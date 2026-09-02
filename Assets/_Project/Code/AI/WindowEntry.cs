using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Bir pencere açıklığı: zombinin binaya girdiği yer. Gri kutu üreteci her zemin
    /// kat penceresine bunu koyar (`BlockoutGenerator`).
    ///
    /// <para><b>Neden bir bileşen, sadece bir işaret değil:</b> zombinin tırmanışı iki
    /// noktaya ihtiyaç duyar — dışarıda durup bekleyeceği yer ve içeride ayak basacağı
    /// yer. İkisini de duvarın yönünden türetmek zorundayız; bu bilgi pencerede yaşamalı,
    /// zombide değil. Barikat (M1-08) da aynı bileşene bağlanacak.</para>
    ///
    /// <para><b>Yön sözleşmesi:</b> transform'un <c>forward</c>'u binanın <b>dışını</b>
    /// gösterir. Üreteç bunu duvarın dış normalinden kurar.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Window Entry")]
    public sealed class WindowEntry : MonoBehaviour
    {
        [Header("Olcu")]
        [Tooltip("Pencerenin dis ve ic taraftaki bekleme noktalarinin duvara uzakligi. " +
                 "Duvar kalinligindan buyuk olmali, yoksa nokta duvarin icinde kalir.")]
        [SerializeField] private float standoffMeters = 1.1f;

        [Tooltip("Pencerenin alt kenari (zeminden). Zombi tirmanirken bu yuksekligin " +
                 "biraz ustunden gecer - tirmanis yayinin tepesi.")]
        [SerializeField] private float sillHeightMeters = 1f;

        [Header("Durum")]
        [Tooltip("Kapali pencereden zombi girmez. Barikat sistemi (M1-08) burayi kullanacak.")]
        [SerializeField] private bool open = true;

        private Transform _transform;

        public bool IsOpen => open;

        /// <summary>Zombinin tırmanmadan önce durduğu, dışarıdaki nokta (NavMesh üzerinde).</summary>
        public Vector3 OutsidePoint
        {
            get
            {
                Vector3 p = _transform.position + _transform.forward * standoffMeters;
                p.y = 0f;
                return p;
            }
        }

        /// <summary>Zombinin tırmanışı bitirdiğinde ayak bastığı, içerideki nokta.</summary>
        public Vector3 InsidePoint
        {
            get
            {
                Vector3 p = _transform.position - _transform.forward * standoffMeters;
                p.y = 0f;
                return p;
            }
        }

        /// <summary>Tırmanış yayının tepe noktası — zombinin pencere eşiğine bindiği an.</summary>
        public Vector3 SillPoint
        {
            get
            {
                Vector3 p = _transform.position;
                p.y = sillHeightMeters;
                return p;
            }
        }

        private void Awake() => _transform = transform;

        // Editor'de Awake calismaz; ureteci ve gizmo'yu bozmamak icin guvenlik agi.
        private void OnValidate() => _transform = transform;

        /// <summary>Üretecin ölçüleri geçirdiği yer. Elle Inspector'dan da ayarlanabilir.</summary>
        public void Configure(float sill, float standoff)
        {
            sillHeightMeters = sill;
            standoffMeters = standoff;
        }

        public void SetOpen(bool value) => open = value;

        private void OnDrawGizmosSelected()
        {
            if (_transform == null) _transform = transform;

            Gizmos.color = open ? Color.green : Color.red;
            Gizmos.DrawWireSphere(OutsidePoint, 0.25f);
            Gizmos.DrawWireSphere(InsidePoint, 0.25f);
            Gizmos.DrawLine(OutsidePoint, SillPoint);
            Gizmos.DrawLine(SillPoint, InsidePoint);
        }
    }
}
