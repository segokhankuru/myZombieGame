using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Patlamanın <b>görünen</b> hâli: hızla büyüyen ve sönen bir küre. 2026-09-05.
    ///
    /// <para><b>Neden gerekiyordu</b> (geliştirici): <i>"patlama özelliğini seçince bir
    /// patlama etkisi hissedilmiyor."</i> İlk sürümde patlama hasarı veriyordu ama
    /// ekranda hiçbir şey olmuyordu — yani kart <b>çalışıyor ama görünmüyordu</b>.
    /// Görünmeyen bir etki, oyuncu için olmayan bir etkidir; kartın kendisi kadar
    /// önemli olan şey onun okunabilirliği (PILLAR-04).</para>
    ///
    /// <para><b>Havuzlu:</b> patlama başına nesne yaratmak, zincirleme patlamada bir
    /// karede onlarca tahsis demek olurdu (csharp-code.md). Sekiz küre dönüşümlü
    /// kullanılır; dokuzuncu patlama en eskisinin üstüne yazar.</para>
    ///
    /// <para><b>Işık yok, parçacık yok.</b> Gri kutuda okunması gereken tek şey "burada
    /// bir patlama oldu ve şu kadar alanı kapsadı". Gerçek VFX sanat aşamasının işi;
    /// bu, o gelene kadar oyunun test edilebilmesi için.</para>
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ExplosionFlash : MonoBehaviour
    {
        /// <summary>Patlamanın görünme süresi. Kısa: okunmalı ama görüşü kapatmamalı.</summary>
        private const float LifeSeconds = 0.28f;

        private const int PoolSize = 8;

        private static ExplosionFlash[] _pool;
        private static int _next;
        private static Material _material;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform _transform;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private float _remaining;
        private float _radius;

        /// <summary>
        /// Bir patlama gösterir. <b>Yalnızca görsel</b> — hasarı çağıran taraf verir.
        /// </summary>
        public static void Show(Vector3 position, float radius)
        {
            EnsurePool();

            ExplosionFlash flash = _pool[_next];
            _next = (_next + 1) % PoolSize;

            flash.Begin(position, radius);
        }

        private static void EnsurePool()
        {
            if (_pool != null && _pool[0] != null) return;

            _pool = new ExplosionFlash[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "ExplosionFlash";

                // Carpismaz: patlamanin gorseli oyuncuyu ve zombileri itemez.
                Destroy(go.GetComponent<Collider>());

                go.GetComponent<Renderer>().sharedMaterial = FlashMaterial();

                var flash = go.AddComponent<ExplosionFlash>();
                flash.Initialize();

                DontDestroyOnLoad(go);

                _pool[i] = flash;
            }
        }

        /// <summary>
        /// Tek bir <b>paylaşılan</b> materyal: küre başına materyal, küre başına çizim
        /// çağrısı demektir (shader-graphics.md).
        /// </summary>
        private static Material FlashMaterial()
        {
            if (_material != null) return _material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color");

            _material = new Material(shader) { name = "mat_explosion_flash_runtime" };
            _material.SetColor(BaseColorId, new Color(1f, 0.65f, 0.2f));

            return _material;
        }

        private void Initialize()
        {
            _transform = transform;
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();

            gameObject.SetActive(false);
        }

        private void Begin(Vector3 position, float radius)
        {
            _radius = radius;
            _remaining = LifeSeconds;

            _transform.position = position;
            _transform.localScale = Vector3.one * (radius * 0.4f);

            gameObject.SetActive(true);
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;

            if (_remaining <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            float t = 1f - _remaining / LifeSeconds;

            // Hizli buyume, hizli sonme: patlama BASLADIGI anda en parlak. Yavas
            // buyuyen bir kure patlama degil, kabaran bir balon gibi okunur.
            float scale = Mathf.Lerp(_radius * 0.4f, _radius * 2f, t) ;
            _transform.localScale = Vector3.one * scale;

            Color color = new Color(1f, Mathf.Lerp(0.65f, 0.25f, t), 0.15f, 1f - t);

            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
