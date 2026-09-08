using UnityEngine;

namespace Bunker.Config
{
    /// <summary>
    /// Binanın ayak izi — <b>"burası içerisi"</b> sorusunun çalışma anındaki tek
    /// cevabı. 2026-09-07.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"zombilerin dropları bunkerın içinde
    /// düşmeli, dışarıda hiç böyle bir olay olmamalı."</i> Doğru bir kural: dışarıda
    /// düşen bir eşya oyuncuyu <b>barikatın dışına</b> çağırır — yani savunulacak
    /// yerden çıkmaya. Eşyanın işi oyuncuyu güvenli köşeden çıkarmaktı, binadan
    /// çıkarmak değil.</para>
    ///
    /// <para><b>Neden bir sahne bileşeni, C# sabiti değil:</b> ayak izi
    /// <c>BlockoutSettings</c>'te yaşıyor ve orada değişiyor. İkinci bir yere
    /// kopyalanmış sınır, harita büyüdüğü gün sessizce yanlış cevap veren bir sınırdır
    /// (SSoT). Üreteç bu bileşeni haritayla <i>aynı sayılardan</i> kurar.</para>
    ///
    /// <para><b>Yoksa yüksek sesle söyler:</b> sınır bulunamazsa kural
    /// <i>uygulanmaz</i> ve hata basılır. Sessizce "her yer dışarısı" demek bütün
    /// eşyaları yok ederdi — ölçülemeyen bir kayıp, bu projedeki hataların en pahalı
    /// türü.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Bunker Interior")]
    public sealed class BunkerInterior : MonoBehaviour
    {
        [Header("Ayak izi (metre, dunya uzayi)")]
        [SerializeField] private float west = -8f;
        [SerializeField] private float east = 22f;
        [SerializeField] private float south = -8f;
        [SerializeField] private float north = 8f;

        [Tooltip("Ic tarafa dogru pay. Duvarin tam dibinde olen zombinin esyasi " +
                 "duvarin icine dusmesin diye.")]
        [SerializeField] private float insetMeters = 0.6f;

        [Header("Yukseklik")]
        [SerializeField] private float floorY = -0.5f;
        [Tooltip("Ust katin tavani. Bundan yukarisi bina degil.")]
        [SerializeField] private float ceilingY = 9f;

        private static BunkerInterior _instance;
        private static bool _warned;

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Üretecin ölçüleri geçirdiği yer.</summary>
        public void Configure(float w, float e, float s, float n, float floor, float ceiling)
        {
            west = w; east = e; south = s; north = n;
            floorY = floor; ceilingY = ceiling;
        }

        /// <summary>Bu nokta binanın içinde mi?</summary>
        public bool Contains(Vector3 point)
        {
            return point.x >= west + insetMeters && point.x <= east - insetMeters
                && point.z >= south + insetMeters && point.z <= north - insetMeters
                && point.y >= floorY && point.y <= ceilingY;
        }

        /// <summary>
        /// Sahnedeki sınır. Yoksa <c>null</c> döner ve <b>bir kez</b> hata basar —
        /// her karede bağıran bir teşhis, okunmayan bir teşhistir.
        /// </summary>
        public static BunkerInterior Active
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = FindFirstObjectByType<BunkerInterior>();
                if (_instance != null) return _instance;

                if (!_warned)
                {
                    _warned = true;
                    Debug.LogError(
                        "[BunkerInterior] Sahnede sinir yok - 'ici mi disi mi' kurallari " +
                        "UYGULANMIYOR (esyalar disarida da dusebilir). Duzeltmek icin: " +
                        "Bunker > Level > LVL-01 Gri Kutu Uret.");
                }

                return null;
            }
        }

        /// <summary>
        /// Sınır kurulmuşsa içeride mi diye bakar; <b>kurulmamışsa engellemez</b>.
        /// Eksik bir teşhis nesnesi yüzünden bütün eşyaları yok etmek, çözdüğünden
        /// büyük bir hata olurdu.
        /// </summary>
        public static bool IsInside(Vector3 point)
        {
            BunkerInterior interior = Active;
            return interior == null || interior.Contains(point);
        }

        private void OnDrawGizmosSelected()
        {
            var center = new Vector3((west + east) / 2f, (floorY + ceilingY) / 2f,
                                     (south + north) / 2f);
            var size = new Vector3(east - west, ceilingY - floorY, north - south);

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
