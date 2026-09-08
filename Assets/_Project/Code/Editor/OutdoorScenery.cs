using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Bunkerin <b>dışarısı</b>: toprak yollar, bitki örtüsü ve kara bulut tavanı.
    /// 2026-09-08.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"The Toby Foliage Engine paketini
    /// import ettim, buradaki objeleri kurarak dışarıda bir ambiyans oluşsun.
    /// Barikatlara doğru toprak yollar gelsin ki zombilerin yürüme alanı belliymiş
    /// hissi versin. Ağaçlar, taşlar, çimler ekleyerek ortamı zenginleştir. Dışarıda
    /// hava kara bulutlarla kapanmış gibi gotik bir ortam hissettirsin."</i></para>
    ///
    /// <para><b>Yol bir süs değil, bir OKUMA aracı.</b> Dışarısı şu ana kadar tek
    /// düze bir çamur plakasıydı; zombiler geçitten çıkıp pencereye yürüyor ama
    /// oyuncunun bunu <i>önceden</i> bilmesinin bir yolu yoktu. Yer üzerine çizilmiş
    /// bir yol, sürünün nereden geleceğini savaş başlamadan söyler — telegrafın
    /// harita ölçeğindeki hâli (ai-code.md: oyuncu ne olacağını okuyabilmeli).
    /// Bitkiler <b>yolun dışında</b> kalır; yolu yol yapan şey, kenarındaki
    /// dağınıklıktır.</para>
    ///
    /// <para><b>Oynanışa dokunmaz</b> (asset-art.md): her parçanın çarpıştırıcısı
    /// silinir ve hiçbiri statik değildir — NavMesh'e giren bir dekor, oyun testinde
    /// "zombiler gelmiyor" olarak okunur ve sebebi haftalarca bulunamaz.</para>
    ///
    /// <para><b>Toby materyalleri DÖNÜŞTÜRÜLMEZ.</b> Paketin shader'ı zaten URP
    /// (<c>Tags{"RenderPipeline"="UniversalPipeline"}</c>) ve rüzgâr, yaprak
    /// saydamlığı, alfa kesme onun içinde. <c>ArtIntegration.ToUrp</c>'tan geçirmek
    /// onları düz <c>URP/Lit</c>'e indirger — yani ağaçlar donuk, yapraklar dikdörtgen
    /// olur. Üçüncü parti klasörü <b>okunur, düzenlenmez</b>.</para>
    ///
    /// <para><b>Deterministik</b> (editor-tools.md): sabit tohum. Aynı harita, aynı
    /// süs; iki kez çalıştırmak ikinci bir kopya üretmez çünkü üreteç kökü zaten
    /// baştan kuruyor.</para>
    /// </summary>
    public static class OutdoorScenery
    {
        private const string TobyPrefabs =
            "Assets/Toby Fredson/The Toby Foliage Engine/(TTFE)_Demo/Prefabs";

        private const string SurfaceFolder = "Assets/_Project/Art/Materials/Surfaces";

        /// <summary>Sabit tohum: aynı harita her seferinde aynı süslenir.</summary>
        private const int Seed = 20260908;

        // --- kaynak listeleri -------------------------------------------------
        //
        // "Fall" (sonbahar) varyantlari SECILDI: paketin yesil varyantlari bahar
        // bahcesi gibi okunuyor ve gotik bir zombi haritasiyla catisiyor. Olu yaprak
        // paleti, kara bulut tavani ve camur zeminle ayni cumleyi kuruyor.

        private static readonly string[] TreePaths =
        {
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Trees - (SL)/" +
                "VTSpecies_Shrub Fall/ShrubTree_C_Fall.prefab",
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Trees - (SL)/" +
                "VTSpecies_Shrub Fall/ShrubTree(NR)_D_Fall.prefab"
        };

        private static readonly string[] BushPaths =
        {
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Trees - (SL)/" +
                "VTSpecies_Shrub Fall/ShrubBush_B_Fall.prefab",
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Trees - (SL)/" +
                "VTSpecies_Shrub Fall/ShrubBush_C_Fall.prefab"
        };

        private static readonly string[] RockPaths =
        {
            TobyPrefabs + "/Prefabs_Static/RocksTTFEL_A.prefab",
            TobyPrefabs + "/Prefabs_Static/RocksTTFEL_B.prefab",
            TobyPrefabs + "/Prefabs_Static/RocksTTFEL_C.prefab",
            TobyPrefabs + "/Prefabs_Static/RocksTTFEL_D.prefab",
            TobyPrefabs + "/Prefabs_Static/RocksTTFEL_E.prefab",
            TobyPrefabs + "/Prefabs_Static/RocksTTFEL_F.prefab",
            TobyPrefabs + "/Prefabs_Static/RocksTTFEL_G.prefab"
        };

        private static readonly string[] GrassPaths =
        {
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Plants - (SL)/" +
                "VP_Grass Fall/GrassBig_A_Fall.prefab",
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Plants - (SL)/" +
                "VP_Grass Fall/GrassBig_B_Fall.prefab",
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Plants - (SL)/" +
                "VP_Grass Fall/GrassMedium_A_Fall.prefab",
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Plants - (SL)/" +
                "VP_Grass Fall/GrassShort_B_Fall.prefab",
            TobyPrefabs + "/Prefabs_Vegetation - (SL)/Vegetation_Plants - (SL)/" +
                "VP_Grass Fall/GrassShort_D_Fall.prefab"
        };

        /// <summary>Yolun genişliği. Zombi sürüsü yan yana iki-üç sığmalı.</summary>
        private const float PathWidthMeters = 3.6f;

        /// <summary>Bitkiler yolun merkezinden bu kadar uzakta başlar.</summary>
        private const float PathClearanceMeters = 2.6f;

        /// <summary>
        /// Dışarıyı kurar. <b><see cref="BlockoutGenerator"/> çağırır</b>, markerlar
        /// yerleştikten sonra — yollar gerçek pencere konumlarından türüyor.
        /// </summary>
        /// <param name="windows">
        /// Zemin kat pencerelerinin (konum, dışarı yönü) çiftleri. Boşsa yol
        /// çizilmez; bitki yine serpilir.
        /// </param>
        /// <returns>Yerleştirilen parça sayısı.</returns>
        public static int Decorate(BlockoutSettings s, Transform parent,
                                   IReadOnlyList<(Vector3 Position, Vector3 Outward)> windows)
        {
            Transform group = new GameObject("Scenery_Outside").transform;
            group.SetParent(parent, false);

            // BAKE DISINDA (2026-09-09). Carpistiricilari silmek YETMIYOR:
            // NavMeshSurface'in varsayilan geometri kaynagi Render Meshes ve bake
            // cizilen mesh'e bakar. Bu satir olmadan 943 renderer apronun NavMesh'ini
            // delik desik ediyor ve 14 metre disarida dogan zombi bir adanin ustune
            // dusup oldugu yerde kaliyor - olculdu, tahmin degil (NavMeshDecor).
            NavMeshDecor.MarkIgnoredIfPossible(group.gameObject);

            var paths = new List<PathSegment>(8);

            int placed = BuildPaths(s, group, windows, paths);
            placed += ScatterFoliage(s, group, paths);
            placed += BuildStormCeiling(s, group);

            return placed;
        }

        // ============================================================== yollar

        /// <summary>Bir yol parçası — bitki reddi bunları okur.</summary>
        private readonly struct PathSegment
        {
            public readonly Vector2 A;
            public readonly Vector2 B;

            public PathSegment(Vector3 a, Vector3 b)
            {
                A = new Vector2(a.x, a.z);
                B = new Vector2(b.x, b.z);
            }

            /// <summary>Bir noktanın parçaya en kısa yatay uzaklığı.</summary>
            public float DistanceTo(Vector2 point)
            {
                Vector2 ab = B - A;
                float lengthSqr = ab.sqrMagnitude;

                if (lengthSqr < 0.0001f) return Vector2.Distance(point, A);

                float t = Mathf.Clamp01(Vector2.Dot(point - A, ab) / lengthSqr);
                return Vector2.Distance(point, A + ab * t);
            }
        }

        /// <summary>
        /// Her zemin kat penceresinden <b>en yakın geçide</b> bir toprak yol çizer.
        ///
        /// <para><b>Neden geçide, haritanın kenarına değil:</b> zombiler çevre
        /// duvarındaki geçitlerden giriyor (<c>ArtIntegration.LineThePerimeter</c>
        /// geçitleri açık bırakıyor). Yol o geçitten başlamıyorsa hiçbir şey
        /// anlatmaz — hatta yanlış bir şey anlatır.</para>
        ///
        /// <para><b>Yol zeminin 2 cm ÜSTÜNDE ve gölge yazmaz:</b> aynı düzlemde iki
        /// yüzey z-fighting üretir (titreşen zemin), ve düz bir yol parçasının gölge
        /// haritasına girmesinin hiçbir karşılığı yok.</para>
        /// </summary>
        private static int BuildPaths(BlockoutSettings s, Transform group,
                                      IReadOnlyList<(Vector3 Position, Vector3 Outward)> windows,
                                      List<PathSegment> segments)
        {
            if (windows == null || windows.Count == 0) return 0;

            Material dirt = EnsurePathMaterial();
            if (dirt == null) return 0;

            Transform paths = new GameObject("Paths").transform;
            paths.SetParent(group, false);

            float apron = s.ApronWidth;
            float minX = s.West - apron;
            float maxX = s.East + apron;
            float minZ = s.South - apron;
            float maxZ = s.North + apron;

            float midX = (minX + maxX) * 0.5f;
            float midZ = (minZ + maxZ) * 0.5f;

            // Dort gecidin merkezi. LineThePerimeter geciti kenarin ORTASINDA
            // birakiyor; iki yerde ayri hesaplanan bir konum, birinin degistigi gun
            // sessizce ayrisir - o yuzden ayni turetme burada da aynen tekrarlaniyor
            // ve degisirse ikisi birlikte degismeli.
            var gates = new[]
            {
                new Vector3(midX, 0f, minZ),
                new Vector3(midX, 0f, maxZ),
                new Vector3(minX, 0f, midZ),
                new Vector3(maxX, 0f, midZ)
            };

            int placed = 0;

            for (int i = 0; i < windows.Count; i++)
            {
                (Vector3 position, Vector3 outward) = windows[i];

                // Yolun bina tarafindaki ucu: pencerenin biraz onu. Barikatin dibine
                // kadar getirmek, yolun duvarin icine girmis gibi gorunmesi demekti.
                Vector3 nearWindow = new Vector3(position.x, 0f, position.z) +
                                     outward.normalized * 1.6f;

                Vector3 gate = NearestGate(gates, nearWindow);

                placed += BuildPathQuad(paths, dirt, gate, nearWindow);
                segments.Add(new PathSegment(gate, nearWindow));
            }

            return placed;
        }

        private static Vector3 NearestGate(Vector3[] gates, Vector3 point)
        {
            Vector3 best = gates[0];
            float bestDistance = float.MaxValue;

            foreach (Vector3 gate in gates)
            {
                float distance = (gate - point).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = gate;
            }

            return best;
        }

        /// <summary>
        /// İki nokta arasına yatay bir yol dikdörtgeni koyar.
        ///
        /// <para><b>Kiremitleme parça başına</b>, <c>MaterialPropertyBlock</c> ile:
        /// materyali kopyalamak yol başına bir materyal ve bir çizim çağrısı demekti
        /// (shader-graphics.md). Sabit bir kiremit sayısı ise 40 m'lik yolda dokuyu
        /// gerer, 8 m'likte sıkıştırır — yolun ne kadar uzun olduğu ekranda
        /// okunmalı.</para>
        /// </summary>
        private static int BuildPathQuad(Transform parent, Material material,
                                         Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;

            float length = delta.magnitude;
            if (length < 1f) return 0;

            Vector3 direction = delta / length;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Path";
            quad.transform.SetParent(parent, false);

            // Zeminin 2 cm ustunde: ayni duzlemdeki iki yuzey z-fighting uretir.
            quad.transform.position = from + delta * 0.5f + Vector3.up * 0.02f;

            // Quad'in normali +Z. Yuzu yukari cevirmek icin forward = +Y; yerel Y
            // ekseni de yolun yonune oturuyor, yani olcek dogrudan (genislik, boy).
            quad.transform.rotation = Quaternion.LookRotation(Vector3.up, direction);
            quad.transform.localScale = new Vector3(PathWidthMeters, length, 1f);

            Object.DestroyImmediate(quad.GetComponent<Collider>());

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var block = new MaterialPropertyBlock();
            block.SetVector("_BaseMap_ST",
                            new Vector4(PathWidthMeters * 0.5f, length * 0.5f, 0f, 0f));
            renderer.SetPropertyBlock(block);

            GameObjectUtility.SetStaticEditorFlags(quad, (StaticEditorFlags)0);

            return 1;
        }

        /// <summary>
        /// Yolun malzemesi: çamurun <b>sıkışmış, açık</b> varyantı.
        ///
        /// <para><b>Neden aynı doku, farklı ton:</b> yolun okunması bir <i>kontrast</i>
        /// meselesi, ayrı bir malzeme meselesi değil. Aynı yüzeyin açığı, "burası
        /// çiğnenmiş" der; bambaşka bir doku, "buraya beton dökülmüş" derdi ve
        /// dışarıda kimsenin beton dökmediği bir dünyada yalan olurdu.</para>
        /// </summary>
        private static Material EnsurePathMaterial()
        {
            ArtIntegration.EnsureFolder(SurfaceFolder);

            const string path = SurfaceFolder + "/mat_dirt_path.mat";

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            bool created = false;
            if (material == null)
            {
                material = new Material(lit) { name = "mat_dirt_path" };
                created = true;
            }

            material.shader = lit;

            // Camur dokusunu paylasir - MudSurface onu zaten uretiyor ve idempotent.
            Material mud = MudSurface.Ensure();

            if (mud != null)
            {
                material.SetTexture("_BaseMap", mud.GetTexture("_BaseMap"));
                material.SetTexture("_BumpMap", mud.GetTexture("_BumpMap"));

                if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
            }

            // Acik ve sicak: cignenmis toprak, islak camurdan daha kuru okunur.
            material.SetColor("_BaseColor", new Color(0.72f, 0.62f, 0.50f));
            material.SetFloat("_Smoothness", 0.04f);
            material.SetFloat("_Metallic", 0f);

            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);

            return material;
        }

        // ============================================================== bitkiler

        /// <summary>
        /// Bitki örtüsü: <b>ağaçlar dışta, çalı ve taş ortada, çim her yerde</b> —
        /// ama hiçbiri yolun üstünde değil.
        ///
        /// <para><b>Neden ağaçlar dışta:</b> bir ağaç görüşü keser. Barikatın
        /// önündeki bir ağaç, oyuncunun pencereye yaklaşan zombiyi görmesini engeller
        /// — dekorun oynanışa dokunması budur. Çevre duvarının dibinde ise siluetle
        /// dünyanın kenarını kapatır, hiçbir şeyi gizlemez.</para>
        ///
        /// <para><b>Bina ayak izi ve kapı önü boş bırakılır</b>: içeride ağaç bitmez,
        /// ve pencerenin dibindeki bir çalı barikatı okunmaz yapar.</para>
        /// </summary>
        private static int ScatterFoliage(BlockoutSettings s, Transform group,
                                          List<PathSegment> paths)
        {
            var trees = Load(TreePaths);
            var bushes = Load(BushPaths);
            var rocks = Load(RockPaths);
            var grass = Load(GrassPaths);

            if (trees.Count == 0 && bushes.Count == 0 && rocks.Count == 0 && grass.Count == 0)
            {
                Debug.LogWarning("[Dis dekor] Toby Foliage parcalari bulunamadi; " +
                                 "disarisi ciplak kaldi. Paket import edilmemis olabilir.");
                return 0;
            }

            Transform host = new GameObject("Foliage").transform;
            host.SetParent(group, false);

            var random = new System.Random(Seed);

            float apron = s.ApronWidth;
            float minX = s.West - apron;
            float maxX = s.East + apron;
            float minZ = s.South - apron;
            float maxZ = s.North + apron;

            int placed = 0;

            // Agaclar: ceperin ic kenarinda, ince bir serit. Sayilari az - bir agac
            // pahali ve kalabalik bir orman kare butcesini yer (PERF-BUDGET).
            placed += Sprinkle(host, trees, random, paths, s,
                               minX, maxX, minZ, maxZ,
                               count: 18, bandFromEdge: 3.5f, bandDepth: 5f,
                               minScale: 0.85f, maxScale: 1.35f, clearance: 3.4f);

            // Calilar ve taslar: orta serit.
            placed += Sprinkle(host, bushes, random, paths, s,
                               minX, maxX, minZ, maxZ,
                               count: 26, bandFromEdge: 2.5f, bandDepth: apron - 6f,
                               minScale: 0.7f, maxScale: 1.2f, clearance: PathClearanceMeters);

            placed += Sprinkle(host, rocks, random, paths, s,
                               minX, maxX, minZ, maxZ,
                               count: 30, bandFromEdge: 1.5f, bandDepth: apron - 4f,
                               minScale: 0.5f, maxScale: 1.4f, clearance: PathClearanceMeters);

            // Cim: cok ve kucuk, yolun hemen kenarina kadar gelir - yolu YOL yapan
            // sey, kenarindaki dagiklik.
            placed += Sprinkle(host, grass, random, paths, s,
                               minX, maxX, minZ, maxZ,
                               count: 140, bandFromEdge: 0.5f, bandDepth: apron - 1.5f,
                               minScale: 0.6f, maxScale: 1.3f, clearance: 1.9f);

            return placed;
        }

        private static int Sprinkle(Transform parent, List<GameObject> prefabs,
                                    System.Random random, List<PathSegment> paths,
                                    BlockoutSettings s,
                                    float minX, float maxX, float minZ, float maxZ,
                                    int count, float bandFromEdge, float bandDepth,
                                    float minScale, float maxScale, float clearance)
        {
            if (prefabs.Count == 0 || bandDepth <= 0.1f) return 0;

            int placed = 0;

            // Deneme sayisi istenenin uc kati ile sinirli: red kosullari (yol, bina)
            // bazi haritalarda cok yer kapatabilir ve sonsuz donguye girmek, uretecin
            // Unity'yi kilitlemesi demek olurdu.
            int attempts = count * 6;

            for (int i = 0; i < attempts && placed < count; i++)
            {
                bool alongX = random.Next(2) == 0;
                bool positive = random.Next(2) == 0;

                float t = (float)random.NextDouble();
                float band = bandFromEdge + (float)random.NextDouble() * bandDepth;

                Vector3 position;

                if (alongX)
                {
                    float x = Mathf.Lerp(minX, maxX, t);
                    float z = positive ? maxZ - band : minZ + band;
                    position = new Vector3(x, 0f, z);
                }
                else
                {
                    float z = Mathf.Lerp(minZ, maxZ, t);
                    float x = positive ? maxX - band : minX + band;
                    position = new Vector3(x, 0f, z);
                }

                if (IsInsideFootprint(s, position, margin: 1.5f)) continue;
                if (IsOnPath(paths, position, clearance)) continue;

                GameObject prefab = prefabs[random.Next(prefabs.Count)];

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                if (instance == null) continue;

                // Bag koparilir: bir ucuncu parti prefab'a gecirme yazmak, paket
                // guncellendigi gun sessizce kaybolur (asset-art.md).
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                                                   InteractionMode.AutomatedAction);

                instance.transform.position = position;
                instance.transform.rotation = Quaternion.Euler(0f, random.Next(0, 360), 0f);

                float scale = Mathf.Lerp(minScale, maxScale, (float)random.NextDouble());
                instance.transform.localScale *= scale;

                StripForFoliage(instance);
                placed++;
            }

            return placed;
        }

        private static bool IsInsideFootprint(BlockoutSettings s, Vector3 point, float margin)
        {
            return point.x > s.West - margin && point.x < s.East + margin
                && point.z > s.South - margin && point.z < s.North + margin;
        }

        private static bool IsOnPath(List<PathSegment> paths, Vector3 point, float clearance)
        {
            var flat = new Vector2(point.x, point.z);

            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i].DistanceTo(flat) < clearance) return true;
            }

            return false;
        }

        private static List<GameObject> Load(string[] paths)
        {
            var result = new List<GameObject>(paths.Length);

            foreach (string path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) result.Add(prefab);
            }

            return result;
        }

        /// <summary>
        /// Bitkiyi oyuna zararsız hâle getirir: <b>çarpıştırıcı yok, statik değil</b>.
        ///
        /// <para><b>Materyale DOKUNULMAZ</b> — <c>ArtIntegration.StripForDecor</c>'dan
        /// tek farkı bu, ve tek gerekçe: Toby paketinin shader'ı zaten URP ve rüzgâr,
        /// yaprak saydamlığı, alfa kesme onun içinde. <c>URP/Lit</c>'e dönüştürmek
        /// ağaçları donuk, yaprakları dikdörtgen yapardı.</para>
        ///
        /// <para><b>NavMesh'e girmemesi şart:</b> zombinin yolunu kapatan bir ağaç,
        /// oyun testinde "zombiler gelmiyor" olarak okunur.</para>
        /// </summary>
        private static void StripForFoliage(GameObject instance)
        {
            GameObjectUtility.SetStaticEditorFlags(instance, (StaticEditorFlags)0);

            var colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders) Object.DestroyImmediate(c);

            // Cim ve calilar golge yazmaz: yuzlerce kucuk golge cizeni, kazandirdigi
            // seyin cok ustunde bir bedel (shader-graphics.md: golge bir butce
            // kalemidir).
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.bounds;
                if (bounds.size.y > 1.5f) continue;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ========================================================= kara bulutlar

        /// <summary>
        /// <b>Kara bulut tavanı</b>: haritanın üstünü kapatan, yavaşça akan iki
        /// katman. 2026-09-08.
        ///
        /// <para><b>Neden bir düzlem, gökyüzü materyali değil</b> (geliştirici:
        /// <i>"dışarıda hava kara bulutlarla kapanmış gibi gotik bir ortam
        /// hissettirsin"</i>): <c>Skybox/Procedural</c>'ın bulutu yok — yalnızca bir
        /// renk gradyanı ve bir güneş. Bulutlu bir gökyüzü ya bir cubemap ister (altı
        /// doku, MB'larca dosya) ya da bir bulut shader'ı. İkisi de bu prototipin
        /// bütçesinin dışında. <b>Alçak bir tavan düzlemi ise doğru cevap:</b> oyuncu
        /// zaten yerde ve yukarı bakınca gördüğü tek şey o düzlem; ufka yakın kısmı
        /// gökyüzünün rengiyle karışıyor.</para>
        ///
        /// <para><b>İki katman, farklı hızda:</b> tek katman düz bir tavan gibi durur.
        /// Farklı hızda kayan iki saydam katman, aralarında sürekli değişen bir
        /// yoğunluk üretir — bulutun "hareket ediyor" okunmasının en ucuz yolu.</para>
        ///
        /// <para><b>Gölge yok, ışık yok:</b> unlit ve saydam. Buluttan gölge almak ya
        /// da vermek, bir gri kutunun ödeyeceği bir bedel değil.</para>
        /// </summary>
        private static int BuildStormCeiling(BlockoutSettings s, Transform group)
        {
            Material clouds = StormClouds.EnsureMaterial();
            if (clouds == null) return 0;

            Transform host = new GameObject("StormCeiling").transform;
            host.SetParent(group, false);

            float apron = s.ApronWidth;
            float span = Mathf.Max(s.East - s.West, s.North - s.South) + apron * 2f;

            // Genis tutulur: kenarlari ufkun ARKASINDA kalmali, yoksa oyuncu tavanin
            // bittigi yeri gorur ve "bu bir plaka" der.
            float size = span * 6f;

            Vector3 center = new Vector3((s.West + s.East) * 0.5f, 0f, (s.South + s.North) * 0.5f);

            int placed = 0;
            placed += BuildCloudLayer(host, clouds, center, size, height: 42f,
                                      tiling: 9f, speed: new Vector2(0.006f, 0.0035f));
            placed += BuildCloudLayer(host, clouds, center, size, height: 56f,
                                      tiling: 5f, speed: new Vector2(-0.0035f, 0.005f));

            return placed;
        }

        private static int BuildCloudLayer(Transform parent, Material material, Vector3 center,
                                           float size, float height, float tiling, Vector2 speed)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Clouds_{Mathf.RoundToInt(height)}m";
            quad.transform.SetParent(parent, false);
            quad.transform.position = center + Vector3.up * height;

            // Yuzu ASAGI baksin: normal -Y, yani forward = -Y.
            quad.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            quad.transform.localScale = new Vector3(size, size, 1f);

            Object.DestroyImmediate(quad.GetComponent<Collider>());

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Akis calisma aninda: materyal ofseti editorde yazilsaydi bulutlar
            // dururdu ve sahne dosyasi her kaydetmede degisirdi.
            var drift = quad.AddComponent<Bunker.Gameplay.CloudDrift>();
            drift.Configure(tiling, speed);

            GameObjectUtility.SetStaticEditorFlags(quad, (StaticEditorFlags)0);

            return 1;
        }
    }
}
