using System.IO;
using Bunker.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bunker.Editor
{
    /// <summary>
    /// Modellerin <b>gerçekten nasıl durduğunu</b> resme çeker. 2026-09-07.
    ///
    /// <para><b>Neden var:</b> silahların yönü mesh köşelerinden <i>tahmin ediliyor</i>
    /// (<c>ArtIntegration.Measure</c>) ve ilk tahmin yanlış çıktı — geliştirici oyunda
    /// gördü: <i>"silahlar ters, bana doğru dönük."</i> Bir sezginin doğru olup
    /// olmadığını oyunu açıp bakarak öğrenmek, her denemede bir tur oyun testi demek.
    /// Bu araç aynı soruyu <b>saniyeler içinde</b> cevaplıyor.</para>
    ///
    /// <para><b>Kamera el modelinin kamerasıdır</b>: aynı konum, aynı görüş açısı,
    /// aynı yakın kesme düzlemi. Yani buradaki resim, oyuncunun ekranının sol alt
    /// köşesinde göreceği şeyin ta kendisi. Farklı bir açıdan bakan bir önizleme,
    /// "düzeldi" der ve oyunda hâlâ ters durur.</para>
    ///
    /// <para><b>GPU ister:</b> <c>-nographics</c> ile çalışmaz. Kendi komut satırından
    /// çağrılır (bkz. <c>.claude/tools/art-preview.ps1</c>).</para>
    /// </summary>
    public static class ArtPreview
    {
        private const string OutputFolder = "Logs/art-preview";
        private const int Width = 640;
        private const int Height = 400;

        /// <summary>El modelinin ev konumu — <c>PlayerViewmodel.GunHome</c> ile aynı.</summary>
        internal static readonly Vector3 GunHome = new Vector3(0.17f, -0.15f, 0.42f);

        public static void RenderBatch()
        {
            try
            {
                Render();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Onizleme] Basarisiz: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Bunker/Gorunum/Model Onizlemesi Cek", false, 121)]
        public static void Render()
        {
            ArtCatalogAsset catalog = ArtIntegration.LoadCatalog();
            if (catalog == null)
            {
                Debug.LogError("[Onizleme] art.asset yok. Once: Bunker > Gorunum > " +
                               "Magaza Modellerini Bagla.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);

            // TEK sahne kipinde acilir, ek olarak degil: toplu kipte acilista
            // "isimsiz ve kaydedilmemis" bir sahne durur ve Unity onun uzerine ek
            // sahne acmayi reddeder ("Cannot create a new scene additively with an
            // untitled scene unsaved"). Onizleme hicbir sahneyi kaydetmiyor, o yuzden
            // acik olani degistirmesi de bir sey kaybettirmez.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                     NewSceneMode.Single);
            var written = new System.Text.StringBuilder(256);

            try
            {
                Camera camera = BuildStage(scene);

                // BICAKLAR DA CEKILIR (2026-09-08). Uc yakin dovus modelinin yonu
                // TAHMIN EDILMIYOR, olculuyor (ArtIntegration.MeasureMelee) - ama
                // olcum de bir hipotez ve dogrulanmasi lazim. Silahlarda tam olarak
                // bu adim atlandigi icin dordunun ikisi ters cikmisti; ayni hatayi
                // bicaklarda tekrarlamanin sebebi yok.
                // Ates silahlari weapon-art.json'dan (2026-09-10): Silah Atolyesi'nden
                // eklenen silah onizlemeye kendiliginden girer, burada unutulmaz.
                var ids = new System.Collections.Generic.List<string>();
                foreach (WeaponArtData art in WeaponWorkshopData.LoadArt()) ids.Add(art.WeaponId);

                ids.Add("melee.dagger");
                ids.Add("melee.sword");
                ids.Add("melee.axe");

                foreach (string id in ids)
                {
                    ArtCatalogAsset.WeaponModel model =
                        id.StartsWith("melee.", System.StringComparison.Ordinal)
                            ? catalog.Melee(id)
                            : catalog.Weapon(id);
                    if (model == null)
                    {
                        written.Append($"  {id}: katalogda yok\n");
                        continue;
                    }

                    var host = new GameObject($"Preview_{id}");
                    SceneManager.MoveGameObjectToScene(host, scene);
                    host.transform.position = GunHome;

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                        model.prefab, host.transform);

                    instance.transform.localPosition = model.localPosition;
                    instance.transform.localRotation = Quaternion.Euler(model.localEulerAngles);
                    instance.transform.localScale = Vector3.one * model.localScale;

                    // Namlu ucu bir KURE ile isaretlenir: resimde alevin nereye
                    // dusecegi de gorunmeli, yoksa "silah duz duruyor ama alev
                    // dipcikten cikiyor" hatasi resimde kacar.
                    MarkMuzzle(host.transform, model.muzzleLocal);

                    string path = $"{OutputFolder}/{Sanitize(id)}.png";
                    Capture(camera, path);
                    written.Append($"  {path}\n");

                    Object.DestroyImmediate(host);

                    // HAM HALI de cekilir: modelin KENDI eksenleri, hicbir donus
                    // uygulanmadan, renkli cubuklarla. Olculen donus yanlissa
                    // "duzeltilmis" resim yalnizca yanlisin nasil gorundugunu
                    // gosterir; dogruyu yazabilmek icin modelin nativ yonunu gormek
                    // gerekiyor (2026-09-07: dort silahin ikisi ters cikti).
                    string rawPath = $"{OutputFolder}/raw_{Sanitize(id)}.png";
                    CaptureRaw(scene, camera, model.prefab, rawPath);
                    written.Append($"  {rawPath}\n");
                }

                // EL VE KOL (2026-09-08): uretilen mesh gercekten ele benziyor mu.
                // Kutulardan mesh'e gecisin dogrulanmasi, oyunu acmadan yapilabilmeli.
                written.Append(CaptureHands(scene, camera));

                // Zombi: uc metre uzaktan, oyuncunun goz hizasindan.
                if (catalog.ZombiePrefab != null)
                {
                    var host = new GameObject("Preview_zombie");
                    SceneManager.MoveGameObjectToScene(host, scene);
                    host.transform.position = new Vector3(0f, -1.6f, 3.2f);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                        catalog.ZombiePrefab, host.transform);

                    instance.transform.localPosition = catalog.ZombieLocalPosition;
                    instance.transform.localRotation =
                        Quaternion.Euler(0f, catalog.ZombieYawDegrees, 0f);
                    instance.transform.localScale = Vector3.one * catalog.ZombieScale;

                    string path = $"{OutputFolder}/zombie.png";
                    Capture(camera, path);
                    written.Append($"  {path}\n");

                    // YURUYUS KARELERI (2026-09-07): adim dongusunun dort evresi.
                    //
                    // Bir yuruyusun dogru olup olmadigini tek karede anlamak mumkun
                    // degil - bacaklarin ZIT fazda olup olmadigi ancak birkac evreye
                    // bakinca goruluyor. Ilk surum tam da bu yuzden bozuk cikti:
                    // kemikleri kendi eksenlerinde dondurmek bacaklari one degil YANA
                    // savuruyordu ve bunu ancak oyunda gorduk.
                    written.Append(CaptureWalkFrames(camera, instance));

                    Object.DestroyImmediate(host);
                }
            }
            finally
            {
                // Tek sahne kipinde acildigi icin kapatilmiyor: son sahneyi kapatmak
                // Unity'de gecersiz. Kaydedilmedigi icin diske hicbir sey yazilmiyor.
            }

            Debug.Log($"[Onizleme] Cekildi:\n{written}");
        }

        /// <summary>
        /// Kamera ve ışık. <b>Işık iki taraftan</b>: tek yönlü ışıkta silahın bir yüzü
        /// tamamen siyah kalır ve resimde yönü okunmaz — önizlemenin tek işi yönü
        /// okutmak.
        /// </summary>
        private static Camera BuildStage(Scene scene)
        {
            var cameraObject = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.20f, 0.24f);
            camera.enabled = false;   // elle Render() cagriliyor

            AddLight(scene, new Vector3(35f, 25f, 0f), 1.4f);
            AddLight(scene, new Vector3(15f, -140f, 0f), 0.6f);

            return camera;
        }

        private static void AddLight(Scene scene, Vector3 euler, float intensity)
        {
            var lightObject = new GameObject("PreviewLight");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(euler);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Üretilen <b>el ve kol mesh'ini</b> yakından çeker. 2026-09-08.
        ///
        /// <para><b>Neden ayrı bir çekim:</b> kollar oyuncu prefab'ında değil, yerel
        /// oyuncu doğduğunda <i>çalışma anında</i> kuruluyor — yani editörde hiçbir
        /// yerde görünmüyorlar. "Kutuya benziyor mu" sorusunu oyunu açmadan
        /// cevaplayabilmek için mesh'i doğrudan sahneye koyup bakıyoruz.</para>
        ///
        /// <para>Kamera <b>yakın</b>: elin parmakları ayırt edilebilmeli. Silah
        /// çekimlerinin mesafesinden bakılsaydı el bir leke olurdu ve "düzeldi" derdik.</para>
        /// </summary>
        private static string CaptureHands(Scene scene, Camera camera)
        {
            var host = new GameObject("Preview_hands");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.position = new Vector3(0f, -0.02f, 0.62f);
            host.transform.rotation = Quaternion.Euler(12f, 28f, 0f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var skin = new Material(lit) { name = "mat_preview_skin" };
            skin.SetColor("_BaseColor", new Color(0.52f, 0.40f, 0.33f));
            skin.SetFloat("_Smoothness", 0.2f);

            var sleeve = new Material(lit) { name = "mat_preview_sleeve" };
            sleeve.SetColor("_BaseColor", new Color(0.22f, 0.24f, 0.22f));
            sleeve.SetFloat("_Smoothness", 0.15f);

            // On kol: mesh'in boyu 1 birim, olcek uzunlugu verir (oyundaki gibi).
            AddMesh(host.transform, "Forearm", Bunker.Gameplay.ArmMesh.Arm(), sleeve,
                    new Vector3(0f, 0f, -0.22f), Quaternion.identity,
                    new Vector3(1f, 1f, 0.22f));

            AddMesh(host.transform, "Hand", Bunker.Gameplay.ArmMesh.Hand(), skin,
                    Vector3.zero, Quaternion.Euler(-18f, 0f, 0f), Vector3.one);

            string path = $"{OutputFolder}/hands.png";
            Capture(camera, path);

            Object.DestroyImmediate(host);
            return $"  {path}\n";
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh,
                                    Material material, Vector3 localPosition,
                                    Quaternion localRotation, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// Yürüyüş döngüsünün dört evresini çeker.
        ///
        /// <para><b>Yandan bakılır</b>, önden değil: bacakların öne mi yana mı gittiği
        /// tam olarak profilden okunur. Önden bakan bir önizleme, ilk sürümdeki hatayı
        /// (bacaklar yana savruluyor) gizlerdi.</para>
        /// </summary>
        private static string CaptureWalkFrames(Camera camera, GameObject model)
        {
            var animator = model.GetComponentInParent<Bunker.AI.ZombieWalkAnimator>();

            if (animator == null)
            {
                animator = model.AddComponent<Bunker.AI.ZombieWalkAnimator>();
            }

            // DERIYI HER CIZIMDE YENIDEN HESAPLA.
            //
            // Oyun donerken skinning her karede guncellenir; ama burada oyun donmuyor
            // ve <c>camera.Render()</c> elle cagriliyor. O durumda Unity kemik
            // matrislerini YENIDEN HESAPLAMIYOR ve dort faz da ayni resmi veriyordu -
            // yani "animasyon calismiyor" gibi gorunuyordu, oysa kemikler donuyordu.
            // Aracin yanlis olcmesi, yanlis yeri duzelttirir.
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.forceMatrixRecalculationPerRender = true;
                skin.updateWhenOffscreen = true;
            }

            var written = new System.Text.StringBuilder(128);

            // Profil gorunum icin EBEVEYNI cevir, modeli degil.
            //
            // Modelin kendi localPosition'i bir kaydirma tasiyor (ayak tabanini sifira
            // oturtan olcu); onu dondurmek kaydirmayi da dondurur ve zombi cerceveden
            // cikar - ilk denemede tam olarak bu oldu.
            Transform pivot = model.transform.parent != null
                ? model.transform.parent
                : model.transform;

            Quaternion original = pivot.localRotation;
            pivot.localRotation = original * Quaternion.Euler(0f, -90f, 0f);

            float[] phases = { 0f, 0.25f, 0.5f, 0.75f };

            foreach (float phase in phases)
            {
                animator.EditorPreviewPose(phase);

                // ADIMI KEMIKTEN OLC, RESIMDEN DEGIL.
                //
                // Editorde deri (skinning) elle cagrilan bir Render'da bir kare geriden
                // gelebiliyor - yani resimler adimi gostermeyebilir ama kemikler
                // donmus olur. Ayak kemiginin dunya konumu bu belirsizligi tamamen
                // ortadan kaldiriyor: iki faz arasindaki fark ADIM UZUNLUGUDUR.
                Transform footLeft = FindBone(model.transform, "foot.L");
                Transform footRight = FindBone(model.transform, "foot.R");

                Debug.Log($"[Onizleme] faz {phase:F2}  " +
                          $"solAyak={(footLeft != null ? footLeft.position.ToString("F3") : "yok")}  " +
                          $"sagAyak={(footRight != null ? footRight.position.ToString("F3") : "yok")}");

                string path = $"{OutputFolder}/zombie_walk_{Mathf.RoundToInt(phase * 100f):D2}.png";
                Capture(camera, path);
                written.Append($"  {path}\n");
            }

            pivot.localRotation = original;
            return written.ToString();
        }

        /// <summary>
        /// Modeli <b>hiç döndürmeden</b>, kendi eksen çubuklarıyla birlikte çeker.
        ///
        /// <para>Çubuklar: <b>kırmızı +X, yeşil +Y, mavi +Z</b>. Model bir birim küpe
        /// sığacak kadar ölçeklenir ve merkezi orijine alınır, böylece dört paketin
        /// dördü aynı çerçevede karşılaştırılabilir.</para>
        ///
        /// <para><b>Kamera 3/4 açıdan bakar</b>, önden değil: önden bakan bir kamerada
        /// derinlik ekseni tek bir noktaya çöker ve tam olarak öğrenmek istediğimiz şey
        /// görünmez olur.</para>
        /// </summary>
        private static void CaptureRaw(Scene scene, Camera camera, GameObject prefab,
                                       string path)
        {
            var host = new GameObject("Raw");
            SceneManager.MoveGameObjectToScene(host, scene);

            // Kameranin onunde, 3/4 acidan bakilacak sekilde.
            host.transform.position = new Vector3(0f, 0f, 1.6f);
            host.transform.rotation = Quaternion.Euler(20f, -35f, 0f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, host.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Bir birim kupe sigdir, sonra merkezini orijine al.
            Bounds bounds = LocalBounds(instance);
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = size > 0.0001f ? 0.9f / size : 1f;

            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = -bounds.center * scale;

            Rod(host.transform, Vector3.right, new Color(1f, 0.25f, 0.2f));    // +X
            Rod(host.transform, Vector3.up, new Color(0.3f, 1f, 0.35f));       // +Y
            Rod(host.transform, Vector3.forward, new Color(0.35f, 0.6f, 1f));  // +Z

            Capture(camera, path);

            Object.DestroyImmediate(host);
        }

        /// <summary>Tek bir eksen çubuğu: orijinden dışarı, ucunda kalın bir küp.</summary>
        private static void Rod(Transform parent, Vector3 direction, Color color)
        {
            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rod.transform.SetParent(parent, false);
            rod.transform.localPosition = direction * 0.35f;
            rod.transform.localRotation = Quaternion.identity;
            rod.transform.localScale = direction * 0.7f + OtherAxes(direction) * 0.02f;

            Paint(rod, color);

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.transform.SetParent(parent, false);
            tip.transform.localPosition = direction * 0.72f;
            tip.transform.localScale = Vector3.one * 0.07f;

            Paint(tip, color);
        }

        private static Vector3 OtherAxes(Vector3 direction) =>
            new Vector3(1f - Mathf.Abs(direction.x), 1f - Mathf.Abs(direction.y),
                        1f - Mathf.Abs(direction.z));

        private static void Paint(GameObject target, Color color)
        {
            Object.DestroyImmediate(target.GetComponent<Collider>());

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Unlit/Color");
            if (unlit == null) return;

            var material = new Material(unlit);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            target.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Nesnenin kendi yerel uzayındaki sınırları (renderer'lardan).</summary>
        private static Bounds LocalBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;
            var bounds = new Bounds(toLocal.MultiplyPoint3x4(renderers[0].bounds.center),
                                    Vector3.zero);

            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                bounds.Encapsulate(toLocal.MultiplyPoint3x4(world.min));
                bounds.Encapsulate(toLocal.MultiplyPoint3x4(world.max));
            }

            return bounds;
        }

        private static void MarkMuzzle(Transform parent, Vector3 localPosition)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "MuzzleMarker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = Vector3.one * 0.03f;

            Object.DestroyImmediate(marker.GetComponent<Collider>());

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Unlit/Color");

            if (unlit == null) return;

            var material = new Material(unlit);
            material.SetColor("_BaseColor", new Color(1f, 0.2f, 0.1f));
            material.SetColor("_Color", new Color(1f, 0.2f, 0.1f));
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };

            RenderTexture previous = RenderTexture.active;

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;

                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>Kemigi adiyla bulur (olcum icin).</summary>
        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == boneName) return candidate;
            }

            return null;
        }

        private static string Sanitize(string value) => value.Replace('.', '_');
    }
}
