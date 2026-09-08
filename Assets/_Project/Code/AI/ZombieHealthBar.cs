using System.Collections.Generic;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Zombinin kafasının üstündeki can barı. <b>Geliştirme aracı</b> — hasarın
    /// gerçekten işlediğini gözle görmek için.
    ///
    /// <para><b>Bu bir oyun arayüzü değil.</b> Yayında zombinin kalan canı sayı olarak
    /// gösterilmez; oyuncu bunu <i>sendelemeden ve ölümden</i> okumalı (M1-13). Burası
    /// yalnızca ölçüm ve teşhis için var, o yüzden tek bir alanla kapatılabiliyor ve
    /// varsayılan olarak <b>yalnızca editörde</b> açık.</para>
    ///
    /// <para><b>Neden dünya uzayında kutular, Canvas değil:</b> zombi başına bir
    /// Canvas, zombi başına bir yeniden düzenleme (rebuild) demektir — 40 zombide bu
    /// ölçmek istediğimiz kare süresini bozar. İki ölçeklenen kutu bedavaya yakındır.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Health Bar (gelistirme)")]
    public sealed class ZombieHealthBar : MonoBehaviour
    {
        [Header("Gorunurluk")]
        [Tooltip("Kapatirsan bar hic olusturulmaz. Yayin oncesi kapatilacak - " +
                 "oyuncu zombinin canini sayidan degil davranisindan okumali.")]
        [SerializeField] private bool showInBuild;

        [Header("Olcu")]
        [SerializeField] private float heightAboveHeadMeters = 0.45f;
        [SerializeField] private float widthMeters = 0.7f;
        [SerializeField] private float thicknessMeters = 0.09f;

        [Tooltip("Bu mesafeden uzaktaki barlar gizlenir. Kalabalikta ekran bar " +
                 "cizgileriyle dolmasin (PILLAR-04).")]
        [SerializeField] private float visibleDistanceMeters = 25f;

        private ZombieAgent _agent;
        private Transform _root;
        private Transform _fill;
        private Renderer _fillRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Camera _camera;
        private float _lastFraction = -1f;
        private float _lastCameraSearch = -99f;

        private static Material _barMaterial;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // --- yazi katmani icin kayit defteri (2026-09-07) ---

        /// <summary>
        /// Şu an sahnede olan barlar. <see cref="ZombieHealthLabels"/> sayıları
        /// buradan okur.
        ///
        /// <para><b>Neden kayıt defteri, neden her zombide bir <c>OnGUI</c> değil:</b>
        /// <c>OnGUI</c> bileşen başına kare başına <i>iki</i> çağrıdır (Layout + Repaint)
        /// ve 40 zombide ölçmeye çalıştığımız kare süresini bozar. Tek bir çizici,
        /// listeyi dolaşıp bitirir.</para>
        /// </summary>
        internal static readonly List<ZombieHealthBar> Registry = new List<ZombieHealthBar>(64);

        /// <summary>Barın dünyadaki konumu (yazılar buna göre yerleşir).</summary>
        internal Vector3 BarWorldPosition => _root != null ? _root.position : transform.position;

        /// <summary>Barın yarım genişliği — sol kenarı bulmak için.</summary>
        internal float BarHalfWidthMeters => widthMeters * 0.5f;

        /// <summary>
        /// Bar bu karede çizildi mi (uzaksa, ölüyse ya da havuza döndüyse hayır).
        ///
        /// <para><b><c>activeInHierarchy</c>, <c>activeSelf</c> değil:</b> havuza dönen
        /// zombi kökünden kapatılır ama barın kendi nesnesi açık kalır. <c>activeSelf</c>
        /// sorulsaydı sahada olmayan zombilerin canı ekranda yazmaya devam ederdi.</para>
        /// </summary>
        internal bool IsShowing => _root != null && _root.gameObject.activeInHierarchy;

        /// <summary>Barın sahibi. Sayıları yazan taraf canı buradan okur.</summary>
        internal ZombieAgent Agent => _agent;

        private void Awake()
        {
            _agent = GetComponent<ZombieAgent>();

            bool active = showInBuild || Application.isEditor;
            if (!active || _agent == null)
            {
                enabled = false;
                return;
            }

            Build();
            _propertyBlock = new MaterialPropertyBlock();

            Registry.Add(this);
            ZombieHealthLabels.EnsureInstalled();
        }

        private void OnEnable()
        {
            // Havuzdan cikan zombi tam canla baslar; bar eski sahibinin degerinde
            // kalmamali. -1 "henuz cizilmedi" demek, bir sonraki karede yeniden yazilir.
            _lastFraction = -1f;
        }

        private void OnDestroy()
        {
            // Awake'in yarattigini OnDestroy yok eder.
            Registry.Remove(this);
            if (_root != null) Destroy(_root.gameObject);
        }

        private void Build()
        {
            var rootObject = new GameObject("HealthBar");
            _root = rootObject.transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, 1.8f + heightAboveHeadMeters, 0f);

            _fill = CreateQuad("Fill", new Color(0.85f, 0.20f, 0.15f), 0f);
            CreateQuad("Back", new Color(0.08f, 0.08f, 0.08f), 0.01f);

            _fillRenderer = _fill.GetComponent<Renderer>();
        }

        private Transform CreateQuad(string name, Color color, float depthOffset)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quad.name = name;
            DestroyImmediate(quad.GetComponent<Collider>());

            quad.transform.SetParent(_root, false);
            quad.transform.localPosition = new Vector3(0f, 0f, depthOffset);
            quad.transform.localScale = new Vector3(widthMeters, thicknessMeters, 0.01f);

            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = BarMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Renk paylasilan materyali klonlamadan verilir (shader-graphics.md).
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);

            return quad.transform;
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            if (_camera == null) AcquireCamera();
            if (_camera == null) return;

            Vector3 toCamera = _camera.transform.position - _root.position;
            float distanceSqr = toCamera.sqrMagnitude;

            bool visible = distanceSqr <= visibleDistanceMeters * visibleDistanceMeters
                           && _agent.IsAlive;

            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;

            // Kameraya donuk dur. LateUpdate'te, cunku kamera Update'te hareket eder.
            _root.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);

            UpdateFill();
        }

        /// <summary>
        /// Kamerayı bulur. <b><c>Camera.main</c> tek başına yetmez:</b> o özellik
        /// yalnızca "MainCamera" etiketli kamerayı döner ve oyuncu kamerası etiketsizse
        /// <c>null</c> gelir. İlk sürümde tam olarak bu oldu — can barları göründü ama
        /// hiç güncellenmedi, çünkü bütün güncelleme kameranın bulunmasına bağlıydı.
        ///
        /// <para>Arama saniyede birden fazla yapılmaz: oyuncu ağ üzerinden geç gelir,
        /// ama kare başına kamera aramak 40 zombide bedava değildir.</para>
        /// </summary>
        private void AcquireCamera()
        {
            if (Time.unscaledTime - _lastCameraSearch < 1f) return;
            _lastCameraSearch = Time.unscaledTime;

            _camera = Camera.main;
            if (_camera != null) return;

            // Etiketsiz kamera da olsa bar calismali; gorunur olmak etiketten
            // daha onemli.
            _camera = FindFirstObjectByType<Camera>();
        }

        private void UpdateFill()
        {
            float fraction = _agent.HealthFraction01;

            // Degismediyse dokunma: her kare olcek yazmak, degismeyen bir sey icin
            // kare basina is demektir (ui-code.md'nin "yalnizca degisince guncelle").
            if (Mathf.Approximately(fraction, _lastFraction)) return;
            _lastFraction = fraction;

            _fill.localScale = new Vector3(widthMeters * fraction, thicknessMeters, 0.01f);

            // Sola dayali kucul: ortadan kuculmek hangi tarafin eksildigini okutmaz.
            _fill.localPosition = new Vector3(-widthMeters * (1f - fraction) * 0.5f, 0f, 0f);

            _fillRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId,
                Color.Lerp(new Color(0.85f, 0.15f, 0.10f), new Color(0.30f, 0.85f, 0.25f), fraction));
            _fillRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static Material BarMaterial()
        {
            if (_barMaterial != null) return _barMaterial;

            // Unlit: can bari isiktan etkilenmemeli, karanlik kosede okunamaz olur.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _barMaterial = new Material(shader) { name = "mat_healthbar_runtime" };
            return _barMaterial;
        }
    }
}
