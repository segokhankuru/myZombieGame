using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Bulut tavanının <b>akışı</b>. 2026-09-08.
    ///
    /// <para><b>Neden çalışma anında, materyalde değil</b> (editor-tools.md): doku
    /// ofsetini editörde yazmak bulutları <i>durdurur</i> ve sahne dosyasını her
    /// kaydetmede değiştirir. Akış bir animasyon; animasyon çalışma anında olur.</para>
    ///
    /// <para><b><c>MaterialPropertyBlock</c> ile, <c>renderer.material</c> ile
    /// DEĞİL</b> (shader-graphics.md): ikincisi materyali klonlar — iki bulut katmanı,
    /// iki materyal, iki çizim çağrısı ve sızdıran iki kopya. Blok paylaşılan
    /// materyale dokunmaz.</para>
    ///
    /// <para><b>Kiremitleme de buradan geliyor:</b> iki katman aynı materyali
    /// paylaşıyor ama farklı sıklıkta kiremitleniyor. İkisi materyalde ayrı ayrı
    /// yazılsaydı iki materyal gerekirdi.</para>
    ///
    /// <para><b><c>unscaledTime</c> DEĞİL, oyun zamanı:</b> tezgâh açıkken dünya
    /// duruyor (<c>WorldClock</c>) ve gökyüzünün akmaya devam etmesi, duran bir
    /// dünyada tek hareket eden şey olurdu — duraklamanın kendisini yalanlar.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Cloud Drift")]
    public sealed class CloudDrift : MonoBehaviour
    {
        [Tooltip("Dokunun kac kez kiremitlendigi. Buyuk deger kucuk, sik bulutlar verir.")]
        [SerializeField] private float tiling = 8f;

        [Tooltip("Saniyedeki UV kaymasi. Kucuk tutulmali - hizli akan bir bulut " +
                 "tavani, gokyuzu degil bir ekran koruyucusu gibi okunur.")]
        [SerializeField] private Vector2 speed = new Vector2(0.005f, 0.003f);

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Vector2 _offset;

        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        /// <summary>Üretecin ayarları geçirdiği yer (<c>OutdoorScenery</c>).</summary>
        public void Configure(float tilingCount, Vector2 driftPerSecond)
        {
            tiling = tilingCount;
            speed = driftPerSecond;
        }

        private void Awake()
        {
            // Referans bir kez cozulur; kare basina GetComponent yasak
            // (csharp-code.md).
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();

            if (_renderer == null)
            {
                // Sessiz varsayilan yok: renderer'i olmayan bir bulut katmani, hicbir
                // sey yapmayan ama her kare calisan bir bilesendir.
                Debug.LogWarning("[Bulut] Renderer yok; akis calismayacak.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            _offset += speed * Time.deltaTime;

            // Ofset 0..1 arasinda tutulur: saatlerce oynanan bir oturumda buyuyen bir
            // float, doku koordinatlarinda gorunur bir hassasiyet kaybina doner
            // (bulutlar kademeli zipllamaya baslar).
            _offset.x -= Mathf.Floor(_offset.x);
            _offset.y -= Mathf.Floor(_offset.y);

            _renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapStId, new Vector4(tiling, tiling, _offset.x, _offset.y));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
