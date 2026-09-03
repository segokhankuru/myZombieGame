using System;
using System.Reflection;
using Bunker.AI;
using Bunker.Gameplay;
using Bunker.Net;
using Bunker.UI;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Bunker.Editor
{
    /// <summary>
    /// M1-04 zombisini <b>çalışır hâlde kurar</b>: prefab'ı üretir, oyuncuyu hedef
    /// olarak işaretler, deneme tezgâhını sahneye koyar ve NavMesh'i bake eder.
    ///
    /// <para><b>Neden bir araç, tarif değil:</b> bu projede elle yapılan mekanik iş
    /// (sahne kurulumu, prefab bağlama, dört ayrı ölçüm) tekrar tekrar hataya düştü;
    /// araca dönüşünce iş açıldı. Prefab'ı elle kurmak yirmi tıklama ve her tıklamada
    /// bir unutma ihtimalidir — burada tek menü, tekrar çalıştırılabilir.</para>
    ///
    /// <para><b>Idempotent</b> (editor-tools.md): iki kez çalıştırmak bir kez
    /// çalıştırmakla aynı sonucu verir. Prefab yerinde güncellenir, sahnedeki tezgâh
    /// nesnesi çoğaltılmaz.</para>
    /// </summary>
    public static class ZombieSetup
    {
        private const string ZombiePrefabPath = "Assets/_Project/Prefabs/Gameplay/Zombie.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const string MaterialPath = "Assets/_Project/Art/Materials/mat_zombie_greybox.mat";
        private const string SandboxObjectName = "_ZombieSandbox";
        private const string DirectorObjectName = "_ZombieDirector";
        private const string HudObjectName = "_CombatHud";
        private const string TracerObjectName = "Tracer";
        private const string TracerMaterialPath = "Assets/_Project/Art/Materials/mat_tracer_greybox.mat";

        // ---------------------------------------------------------------- menu

        [MenuItem("Bunker/Zombi/Test Alanini Kur", false, 200)]
        public static void SetupTestbed()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[Zombi] Acik bir sahne yok. Once bir sahne ac.");
                return;
            }

            // 1) Gri kutuyu yeniden uret: pencerelere WindowEntry ve binanin cevresine
            //    disarida yurunecek serit bu adimda geliyor.
            BlockoutSettings settings = BlockoutGenerator.LoadOrCreateSettings();
            BlockoutGenerator.Generate(settings);

            // 2) Zombi prefab'i
            GameObject zombiePrefab = BuildZombiePrefab();

            // 3) Oyuncu hedef olarak isaretlensin (zombiler yalnizca isaretliyi kovalar)
            PatchPlayerPrefab();

            // 4) Deneme tezgahi sahnede
            InstallSandbox(zombiePrefab);
            InstallHud();

            // 4b) Pencerelere barikat (M1-08)
            int barricades = InstallBarricades();

            // 4c) Kapilar ve duvar silahlari (M1-09, M1-10)
            (int doors, int wallWeapons) = InstallPurchasables();

            // 5) NavMesh bake - apron eklendigi icin eski bake gecersiz
            bool baked = BakeNavMesh();

            // 6) Sahne Build Settings'te olsun. Unity, Library klasoru sifirlandiginda
            //    (klonlama, temizlik, baska makine) listedeki ILK sahneyi acar - bu
            //    olmazsa gelistirici bos bir hiyerarsi ile karsilasir.
            EnsureSceneInBuildSettings(scene.path);

            // 7) Silinmis betiklerden kalan bos bilesenleri temizle. M0-04'un yuk testi
            //    (AgentLoadTest) kaldirildi; onun gibi her silinen betik sahnede
            //    "Missing script" birakir ve o uyari zamanla gercek hatalari gizler.
            int stripped = StripMissingScripts(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[Zombi] Test alani hazir.\n" +
                $"  prefab      : {ZombiePrefabPath}\n" +
                $"  pencere      : {UnityEngine.Object.FindObjectsByType<WindowEntry>(FindObjectsSortMode.None).Length} giris noktasi\n" +
                $"  NavMesh      : {(baked ? "bake edildi" : "BAKE EDILEMEDI - asagidaki uyariya bak")}\n" +
                $"  barikat      : {barricades} pencere\n" +
                $"  satin alma   : {doors} kapi, {wallWeapons} duvar silahi\n" +
                $"  temizlik     : {stripped} bos bilesen kaldirildi\n" +
                "  SIRADAKI ADIM: Play'e bas. Sol tik ates, R dolum, V bicak, " +
                "E tamir/satin al; F7/F8 tur, F9 sahayi temizle.");
        }

        /// <summary>
        /// Komut satırı girişi: sandbox sahnesini açar, kurulumu çalıştırır, kaydeder.
        ///
        /// <para>CI'ın ve bu projede geliştiricinin ihtiyacı olan her şey
        /// <c>-executeMethod</c> ile çağrılabilir olmalı ve <b>hiçbir editör penceresinin
        /// açık olmasına bağlı olmamalı</b> (editor-tools.md). Kurulumun Unity açmadan
        /// koşabilmesinin sebebi bu: "şu menüye tıkla" bir adım değil, bir borçtur.</para>
        /// </summary>
        public static void SetupTestbedBatch()
        {
            const string scenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                SetupTestbed();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Zombi] Toplu kurulum basarisiz: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Bunker/Zombi/Sadece Prefab Uret", false, 201)]
        public static void RebuildPrefabOnly()
        {
            BuildZombiePrefab();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Zombi] Prefab guncellendi: {ZombiePrefabPath}");
        }

        [MenuItem("Bunker/Zombi/NavMesh Bake", false, 202)]
        public static void BakeNavMeshMenu()
        {
            if (BakeNavMesh())
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
            }
        }

        // ---------------------------------------------------------------- prefab

        /// <summary>
        /// Zombi prefab'ını sıfırdan kurar ve diske yazar. Var olanı <b>yerinde</b>
        /// günceller — GUID korunur, sahnedeki ve prefab'a bakan her referans yaşamaya
        /// devam eder.
        /// </summary>
        public static GameObject BuildZombiePrefab()
        {
            Material material = LoadOrCreateMaterial();

            var root = new GameObject("Zombie");

            try
            {
                // --- govde carpismasi: isinlarin isabet ettigi yer
                var body = root.AddComponent<CapsuleCollider>();
                body.height = 1.8f;
                body.radius = 0.35f;
                body.center = new Vector3(0f, 0.9f, 0f);

                // --- navigasyon
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.baseOffset = 0f;
                agent.speed = 1.4f;              // dogumda turdan gelen degerle degisir
                agent.angularSpeed = 720f;
                agent.acceleration = 20f;
                agent.stoppingDistance = 1.2f;   // saldiri menzilinin biraz altinda
                agent.autoBraking = false;

                // Pencere tirmanisi elle surulur (ZombieAgent), ama bake'in kendi
                // urettigi baglantilar (ust kattan atlama) otomatik gecilsin - kapatmak
                // zombiyi orada dondurur.
                agent.autoTraverseOffMeshLink = true;

                // Kalabalik onlemesi zombide ORTA: yuksek kalite 40 ajanda pahali,
                // kapali olursa sürü ust uste biner ve tek bir zombi gorunur
                // (PILLAR-04, kaosta okunabilirlik).
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

                // --- gorsel (gri kutu)
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

                var visualRenderer = visual.GetComponent<Renderer>();
                visualRenderer.sharedMaterial = material;

                // --- kafa: hem gorsel yon ipucu hem kafa vurusu kutusu
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
                head.transform.localScale = new Vector3(0.36f, 0.36f, 0.36f);
                head.GetComponent<Renderer>().sharedMaterial = material;

                // --- burun: gri kapsul hangi yone baktigini soylemez. Zombinin
                //     nereye dondugu telegrafin yarisidir.
                GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nose.name = "FacingMarker";
                nose.transform.SetParent(root.transform, false);
                nose.transform.localPosition = new Vector3(0f, 1.72f, 0.22f);
                nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.18f);
                UnityEngine.Object.DestroyImmediate(nose.GetComponent<Collider>());
                nose.GetComponent<Renderer>().sharedMaterial = material;

                // --- beyin
                var zombie = root.AddComponent<ZombieAgent>();
                SetPrivateField(zombie, "bodyRenderer", visualRenderer);
                SetPrivateField(zombie, "debugVisuals", true);

                // --- vurus kutulari
                var bodyHitbox = root.AddComponent<ZombieHitbox>();
                SetPrivateField(bodyHitbox, "head", false);
                SetPrivateField(bodyHitbox, "owner", zombie);

                // Gelistirme araci: kafanin ustunde can bari (yayin oncesi kapatilir).
                root.AddComponent<ZombieHealthBar>();

                var headHitbox = head.AddComponent<ZombieHitbox>();
                SetPrivateField(headHitbox, "head", true);
                SetPrivateField(headHitbox, "owner", zombie);

                return PrefabUtility.SaveAsPrefabAsset(root, ZombiePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material LoadOrCreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "mat_zombie_greybox" };
            material.SetColor("_BaseColor", new Color(0.45f, 0.20f, 0.20f));

            // MaterialPropertyBlock ile renk degistirebilmek icin materyal PAYLASILIR
            // kalir: her zombiye ayri materyal ornegi vermek zombi basina bir cizim
            // cagrisi demektir (shader-graphics.md).
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // ---------------------------------------------------------------- oyuncu

        /// <summary>
        /// Oyuncu prefab'ına hedef işaretini ve geçici canı ekler. Zaten varsa
        /// dokunmaz — prefab'ı gereksiz yere kirletmek diff'i ve sürüm geçmişini bozar.
        /// </summary>
        private static void PatchPlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogWarning($"[Zombi] Oyuncu prefab'i bulunamadi: {PlayerPrefabPath}. " +
                                 "Zombiler kovalayacak hedef bulamaz.");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                bool changed = false;

                // Once bozuk bilesenler: bir betik dosyasi .meta'si olmadan tasinirsa
                // Unity ona YENI bir GUID uretir ve o betige bakan her prefab "missing
                // script" tasimaya baslar. Bir kez yasandi (BUG-003); temizligi
                // kuruluma bagladik ki elle ugrasilmasin.
                // Ozyinelemeli: RemoveMonoBehavioursWithMissingScript TEK bir
                // GameObject'e bakar. Yalnizca koke uygulamak, kamera ve govde gibi
                // alt nesnelerdeki bozuk bilesenleri geride birakir - ilk denemede
                // tam olarak bu oldu.
                int stripped = 0;
                foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                {
                    stripped += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }

                if (stripped > 0)
                {
                    changed = true;
                    Debug.LogWarning($"[Zombi] Oyuncu prefab'indan {stripped} bozuk bilesen " +
                                     "temizlendi (betik GUID'i degismis).");
                }

                ReportRemainingMissing(contents);

                if (contents.GetComponent<ZombieTargetBeacon>() == null)
                {
                    contents.AddComponent<ZombieTargetBeacon>();
                    changed = true;
                }

                // M1-11: gecici DebugPlayerHealth'in yerini gercek can aldi. Eski
                // bilesen prefab'da kalmis olabilir - betik silindigi icin Unity onu
                // "Missing Script" olarak gosterir ve ReportRemainingMissing yakalar.
                var health = contents.GetComponent<PlayerHealth>();
                if (health == null) { health = contents.AddComponent<PlayerHealth>(); changed = true; }

                SetPrivateField(health, "playerConfig", LoadConfigAsset("player"));

                changed |= PatchWeapon(contents);

                if (!changed) return;

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                Debug.Log("[Zombi] Oyuncu prefab'ina hedef isareti ve can eklendi.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Gri kutudaki kapı ve duvar silahı işaretlerini <b>satın alınabilir</b> hâle
        /// getirir (M1-09, M1-10).
        ///
        /// <para>İşaretler <c>BlockoutGenerator</c> tarafından üretiliyor; burada
        /// üstlerine bileşen, çarpıştırıcı ve fiyat bandı bağlanıyor. Kapılar
        /// <c>NetworkIdentity</c> alır çünkü açılma durumu geç katılan istemciye de
        /// gitmeli (ADR-0004).</para>
        /// </summary>
        private static (int doors, int weapons) InstallPurchasables()
        {
            UnityEngine.Object economy = LoadConfigAsset("economy");
            int doors = 0;
            int weapons = 0;

            // --- kapilar
            foreach (string name in new[] { "Door_A_to_B", "Door_To_Ramp" })
            {
                GameObject marker = GameObject.Find(name);
                if (marker == null) continue;

                var door = marker.GetComponent<PurchasableDoor>();
                if (door == null) door = marker.AddComponent<PurchasableDoor>();

                if (marker.GetComponent<NetworkIdentity>() == null)
                {
                    marker.AddComponent<NetworkIdentity>();
                }

                EnsureInteractionTrigger(marker, new Vector3(2.2f, 2.6f, 1.2f));

                // Kapinin kanadi: satin alinca kapanacak nesne. Bu bagli degilse
                // "kapiyi aldim ama hicbir sey olmadi" olur - tam olarak oyun
                // testinde gorulen sey.
                Transform leaf = marker.transform.Find("Leaf");
                if (leaf != null)
                {
                    SetPrivateField(door, "blockers", new[] { leaf.gameObject });
                }
                else
                {
                    Debug.LogWarning($"[Zombi] '{name}' kapisinin kanadi yok; satin almak " +
                                     "gorunur bir sey degistirmeyecek. Gri kutu yeniden " +
                                     "uretilmeli.");
                }

                SetPrivateField(door, "economyConfig", economy);
                SetPrivateField(door, "displayName", name == "Door_A_to_B" ? "YAN KANAT" : "UST KAT");

                // Ucuz bant A->B, orta bant ust kat: ust kat dagiticiyi barindiriyor
                // ve dongunun uzak ucu, o yuzden daha pahali (LVL-01).
                SetPrivateField(door, "priceTier",
                                name == "Door_A_to_B" ? DoorPriceTier.Cheap : DoorPriceTier.Mid);

                doors++;
            }

            // --- duvar silahlari
            foreach (string name in new[] { "WallBuy_A_Cheap", "WallBuy_B_Mid" })
            {
                GameObject marker = GameObject.Find(name);
                if (marker == null) continue;

                var wall = marker.GetComponent<WallWeaponPurchase>();
                if (wall == null) wall = marker.AddComponent<WallWeaponPurchase>();

                EnsureInteractionTrigger(marker, new Vector3(1.4f, 1.4f, 0.8f));

                SetPrivateField(wall, "economyConfig", economy);
                SetPrivateField(wall, "midTier", name.EndsWith("Mid"));
                SetPrivateField(wall, "displayName", "MERMI");

                weapons++;
            }

            return (doors, weapons);
        }

        /// <summary>
        /// Etkileşim için tetikleyici bir kutu. İşaretler boş <c>GameObject</c>;
        /// ışının çarpacağı bir yüzey olmadan hiçbir etkileşim bulunamaz — barikat
        /// tamirinde tam olarak bu unutulmuştu.
        /// </summary>
        private static void EnsureInteractionTrigger(GameObject target, Vector3 size)
        {
            var box = target.GetComponent<BoxCollider>();
            if (box == null) box = target.AddComponent<BoxCollider>();

            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
        }

        /// <summary>
        /// Temizlikten sonra <b>hâlâ</b> bozuk bileşen kaldıysa hangi nesnede olduğunu
        /// söyler. Unity'nin temizleyemediği bir kalıntı varsa onu sessizce bırakmak,
        /// her Play'de tekrarlanan ve kimsenin sebebini bilmediği bir uyarı demektir.
        /// </summary>
        private static void ReportRemainingMissing(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                Component[] components = t.GetComponents<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null) continue;

                    Debug.LogWarning($"[Zombi] '{t.name}' uzerinde temizlenemeyen bozuk " +
                                     $"bilesen var (sira {i}). Prefab'i Unity'de acip " +
                                     "elle kaldirmak gerekebilir.");
                }
            }
        }

        /// <summary>
        /// Oyuncuya silahı, puanı ve mermi izini ekler (M1-06). Zaten varsa dokunmaz.
        /// </summary>
        private static bool PatchWeapon(GameObject player)
        {
            bool changed = false;

            var weapon = player.GetComponent<PlayerWeapon>();
            if (weapon == null) { weapon = player.AddComponent<PlayerWeapon>(); changed = true; }

            var score = player.GetComponent<PlayerScore>();
            if (score == null) { score = player.AddComponent<PlayerScore>(); changed = true; }

            // Mermi izi: gri kutuda atisin nereye gittigini gosteren tek sey.
            Transform tracerTransform = player.transform.Find(TracerObjectName);
            LineRenderer line;

            if (tracerTransform == null)
            {
                var go = new GameObject(TracerObjectName);
                go.transform.SetParent(player.transform, false);
                line = go.AddComponent<LineRenderer>();
                changed = true;
            }
            else
            {
                line = tracerTransform.GetComponent<LineRenderer>();
                if (line == null) { line = tracerTransform.gameObject.AddComponent<LineRenderer>(); changed = true; }
            }

            line.positionCount = 2;
            line.startWidth = 0.02f;
            line.endWidth = 0.005f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = LoadOrCreateTracerMaterial();
            line.enabled = false;

            // --- bicak (M1-07) ve barikat tamiri (M1-08)
            var melee = player.GetComponent<PlayerMelee>();
            if (melee == null) { melee = player.AddComponent<PlayerMelee>(); changed = true; }

            var repair = player.GetComponent<PlayerRepair>();
            if (repair == null) { repair = player.AddComponent<PlayerRepair>(); changed = true; }

            var interact = player.GetComponent<PlayerInteract>();
            if (interact == null) { interact = player.AddComponent<PlayerInteract>(); changed = true; }

            // Oyuncu kamerasi "MainCamera" etiketli olmali. Camera.main yalnizca o
            // etikete bakar; etiketsiz kalirsa null doner ve ona guvenen her sey
            // sessizce calismaz - zombi can barlari tam olarak boyle hic
            // guncellenmedi.
            Camera playerCamera = player.GetComponentInChildren<Camera>(true);
            if (playerCamera != null && !playerCamera.CompareTag("MainCamera"))
            {
                playerCamera.tag = "MainCamera";
                changed = true;
                Debug.Log("[Zombi] Oyuncu kamerasi 'MainCamera' olarak etiketlendi.");
            }

            SetPrivateField(weapon, "weaponConfig", LoadConfigAsset("weapon"));
            SetPrivateField(weapon, "tracer", line);
            SetPrivateField(score, "economyConfig", LoadConfigAsset("economy"));
            SetPrivateField(score, "weapon", weapon);
            SetPrivateField(score, "melee", melee);
            SetPrivateField(melee, "knifeConfig", LoadConfigAsset("knife"));
            SetPrivateField(repair, "barricadeConfig", LoadConfigAsset("barricade"));
            SetPrivateField(repair, "score", score);
            SetPrivateField(repair, "interact", interact);
            SetPrivateField(interact, "score", score);
            SetPrivateField(interact, "weapon", weapon);

            return changed;
        }

        /// <summary>
        /// Her zemin kat penceresine barikat takar (M1-08). Pencereler her üretimde
        /// yeniden kurulduğu için bu adım da her seferinde koşar.
        /// </summary>
        private static int InstallBarricades()
        {
            UnityEngine.Object config = LoadConfigAsset("barricade");
            WindowEntry[] windows = UnityEngine.Object.FindObjectsByType<WindowEntry>(
                FindObjectsSortMode.None);

            int count = 0;

            for (int i = 0; i < windows.Length; i++)
            {
                var barricade = windows[i].GetComponent<WindowBarricade>();
                if (barricade == null) barricade = windows[i].gameObject.AddComponent<WindowBarricade>();

                SetPrivateField(barricade, "barricadeConfig", config);
                count++;
            }

            return count;
        }

        private static Material LoadOrCreateTracerMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(TracerMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "mat_tracer_greybox" };
            material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f));

            AssetDatabase.CreateAsset(material, TracerMaterialPath);
            return material;
        }

        /// <summary>Savaş HUD'unu sahneye koyar (nişangâh, isabet işareti, şarjör, puan).</summary>
        private static void InstallHud()
        {
            GameObject host = GameObject.Find(HudObjectName);

            if (host == null)
            {
                host = new GameObject(HudObjectName);
                Undo.RegisterCreatedObjectUndo(host, "Savas HUD");
            }

            if (host.GetComponent<CombatHud>() == null) host.AddComponent<CombatHud>();

            // M1-11: skor ekrani ayni sahne nesnesinde yasar. Ayri bir nesne olsaydi
            // kurulum araci ikisini ayri ayri bulmak zorunda kalirdi ve biri eksik
            // kaldiginda hata sessiz olurdu - olen oyuncu bos ekrana bakar.
            if (host.GetComponent<GameOverHud>() == null) host.AddComponent<GameOverHud>();
        }

        // ---------------------------------------------------------------- sahne

        private static void InstallSandbox(GameObject zombiePrefab)
        {
            ZombieAgent agent = zombiePrefab != null ? zombiePrefab.GetComponent<ZombieAgent>() : null;

            // --- yonetmen: dogum, havuz, tur akisi (M1-05)
            GameObject directorHost = GameObject.Find(DirectorObjectName);

            if (directorHost == null)
            {
                directorHost = new GameObject(DirectorObjectName);
                Undo.RegisterCreatedObjectUndo(directorHost, "Zombi yonetmeni");
            }

            var director = directorHost.GetComponent<ZombieDirector>();
            if (director == null) director = directorHost.AddComponent<ZombieDirector>();

            SetPrivateField(director, "zombiePrefab", agent);

            // Ayarlar enjekte edilir, statikten cekilmez (config-protocol.md). Varlik
            // yoksa uyari: sessiz varsayilanla calisan bir yonetmen, yanlis sayilarla
            // yapilmis bir oyun testi demektir.
            SetPrivateField(director, "roundsConfig", LoadConfigAsset("rounds"));
            SetPrivateField(director, "zombieConfig", LoadConfigAsset("zombie"));

            // --- ag seam'i: zombi konumlarinin TEK gecidi (ADR-0004)
            if (directorHost.GetComponent<NetworkIdentity>() == null)
            {
                directorHost.AddComponent<NetworkIdentity>();
            }

            var relay = directorHost.GetComponent<ZombieNetworkRelay>();
            if (relay == null) relay = directorHost.AddComponent<ZombieNetworkRelay>();

            SetPrivateField(relay, "director", director);

            // --- tezgah: gecici silah ve ekran (M1-06 ve M1-11 silecek)
            GameObject sandboxHost = GameObject.Find(SandboxObjectName);

            if (sandboxHost == null)
            {
                sandboxHost = new GameObject(SandboxObjectName);
                Undo.RegisterCreatedObjectUndo(sandboxHost, "Zombi test alani");
            }

            var sandbox = sandboxHost.GetComponent<ZombieSandbox>();
            if (sandbox == null) sandbox = sandboxHost.AddComponent<ZombieSandbox>();

            SetPrivateField(sandbox, "director", director);
        }

        private static UnityEngine.Object LoadConfigAsset(string domain)
        {
            string path = $"Assets/_Project/Config/{domain}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (asset == null)
            {
                Debug.LogWarning($"[Zombi] Config varligi yok: {path}. " +
                                 "'Bunker/Config/Ice Aktar' calistirilmali.");
            }

            return asset;
        }

        /// <summary>
        /// Sahneyi Build Settings listesinin <b>başına</b> koyar. Zaten baştaysa
        /// dokunmaz — gereksiz yere ProjectSettings dosyasını kirletmek diff üretir.
        /// </summary>
        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;

            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;

            if (current.Length > 0 && current[0].path == scenePath && current[0].enabled) return;

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(current.Length + 1)
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].path == scenePath) continue;
                list.Add(current[i]);
            }

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[Zombi] Build Settings'e eklendi (ilk sahne): {scenePath}");
        }

        /// <summary>
        /// Sahnedeki "Missing script" bileşenlerini temizler ve kaç tane olduğunu döner.
        /// Bir betik silindiğinde sahnede kalan boş kabuk, Console'u kalıcı bir uyarıyla
        /// doldurur; birikince gerçek hataları gizler.
        /// </summary>
        private static int StripMissingScripts(Scene scene)
        {
            int removed = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);

                for (int i = 0; i < all.Length; i++)
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(all[i].gameObject);
                }
            }

            return removed;
        }

        // ---------------------------------------------------------------- navmesh

        /// <summary>
        /// Sahnedeki <c>NavMeshSurface</c>'i bulur ve bake eder.
        ///
        /// <para><b>Neden yansıma (reflection):</b> <c>NavMeshSurface</c> bir paket
        /// tipidir (<c>com.unity.ai.navigation</c>). Doğrudan referans vermek
        /// <c>Bunker.Editor</c>'ün asmdef'ine paket bağımlılığı eklemek demek; adı bir
        /// gün değişirse <b>bütün editör derlemesi</b> derlenmez ve proje kilitlenir.
        /// Bake bir kolaylıktır, o riski hak etmiyor: burada başarısız olursa yalnızca
        /// bu satır uyarı verir, gerisi çalışmaya devam eder.</para>
        /// </summary>
        private static bool BakeNavMesh()
        {
            Type surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");

            if (surfaceType == null)
            {
                Debug.LogWarning("[Zombi] NavMeshSurface tipi bulunamadi (AI Navigation " +
                                 "paketi yuklu mu?). NavMesh elle bake edilmeli.");
                return false;
            }

            UnityEngine.Object[] surfaces =
                UnityEngine.Object.FindObjectsByType(surfaceType, FindObjectsSortMode.None);

            if (surfaces.Length == 0)
            {
                GameObject root = GameObject.Find("LVL-01_Blockout");
                if (root == null)
                {
                    Debug.LogWarning("[Zombi] Sahnede NavMeshSurface yok ve " +
                                     "'LVL-01_Blockout' bulunamadi; bake atlandi.");
                    return false;
                }

                Component added = root.AddComponent(surfaceType);
                surfaces = new UnityEngine.Object[] { added };
                Debug.Log("[Zombi] NavMeshSurface bulunamadi, koke eklendi.");
            }

            // Kapi kanatlari bake sirasinda KAPALI: gecidin NavMesh'i bastan var olsun,
            // kapali kapiyi calisma aninda NavMeshObstacle kessin. Kanat bake'e
            // girseydi kapiyi satin almak gecidi acmazdi - bake edilmis NavMesh
            // calisma aninda degismez.
            GameObject[] leaves = FindDoorLeaves();
            for (int i = 0; i < leaves.Length; i++) leaves[i].SetActive(false);

            // TETIKLEYICILER DE KAPATILIR. Etkilesim icin eklenen kutular (kapi, duvar
            // silahi) sahnede duruyor ve bake onlari GEOMETRI sayabiliyor - kapinin
            // 2.2 x 2.6 m'lik tetikleyicisi tam boslugun icinde oturdugu icin gecidi
            // tikadi ve "zemin -> ust kat KOPUK" olarak olculdu. Tetikleyici asla
            // yurunemez engel degildir; bake'te bulunmamali.
            Collider[] triggers = UnityEngine.Object.FindObjectsByType<Collider>(
                FindObjectsSortMode.None);
            var disabledTriggers = new System.Collections.Generic.List<Collider>(16);

            for (int i = 0; i < triggers.Length; i++)
            {
                if (!triggers[i].isTrigger || !triggers[i].enabled) continue;
                triggers[i].enabled = false;
                disabledTriggers.Add(triggers[i]);
            }

            Debug.Log($"[Zombi] Bake hazirligi: {leaves.Length} kapi kanadi ve " +
                      $"{disabledTriggers.Count} tetikleyici gecici olarak kapatildi.");

            MethodInfo build = surfaceType.GetMethod("BuildNavMesh",
                BindingFlags.Public | BindingFlags.Instance);

            if (build == null)
            {
                Debug.LogWarning("[Zombi] NavMeshSurface.BuildNavMesh bulunamadi; " +
                                 "NavMesh elle bake edilmeli.");
                return false;
            }

            PropertyInfo dataProperty = surfaceType.GetProperty("navMeshData",
                BindingFlags.Public | BindingFlags.Instance);

            try
            {
                for (int i = 0; i < surfaces.Length; i++)
                {
                    build.Invoke(surfaces[i], null);
                    PersistNavMeshData(surfaces[i], dataProperty, i);
                    EditorUtility.SetDirty(surfaces[i]);
                }

                // Baglanti olcumu TAM BURADA yapilir: bake yeni, kanatlar hala kapali,
                // yani olculen sey haritanin topolojisi. Ayri bir oturumda olcmek
                // kapali kapilarin oymasini geri almayi gerektiriyor ve o geri alma
                // toplu calistirmada hic olmuyor.
                LevelConnectivityCheck.Measure(leaves.Length);
            }
            finally
            {
                // Kanatlar ve tetikleyiciler her durumda geri acilir - bake patlasa
                // bile kapilar acik, etkilesimler olu kalmamali.
                for (int i = 0; i < leaves.Length; i++) leaves[i].SetActive(true);
                for (int i = 0; i < disabledTriggers.Count; i++) disabledTriggers[i].enabled = true;
            }

            return true;
        }

        /// <summary>
        /// Bake sonucu bellekte durur; varlık olarak kaydedilmezse sahne kapanınca
        /// kaybolur ve oyun NavMesh'siz açılır.
        /// </summary>
        private static void PersistNavMeshData(UnityEngine.Object surface, PropertyInfo dataProperty, int index)
        {
            if (dataProperty?.GetValue(surface) is not NavMeshData data) return;
            if (AssetDatabase.Contains(data)) return;

            Scene scene = EditorSceneManager.GetActiveScene();
            string folder = System.IO.Path.GetDirectoryName(scene.path)?.Replace('\\', '/');
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scene.path);

            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogWarning("[Zombi] Sahne kaydedilmemis; NavMesh varligi yazilamadi.");
                return;
            }

            string dataFolder = $"{folder}/{sceneName}";
            if (!AssetDatabase.IsValidFolder(dataFolder))
            {
                AssetDatabase.CreateFolder(folder, sceneName);
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{dataFolder}/NavMesh-{sceneName}-{index}.asset");

            AssetDatabase.CreateAsset(data, path);
        }

        // ---------------------------------------------------------------- yardimcilar

        /// <summary>Sahnedeki bütün kapı kanatları.</summary>
        private static GameObject[] FindDoorLeaves()
        {
            var leaves = new System.Collections.Generic.List<GameObject>(4);

            foreach (PurchasableDoor door in
                     UnityEngine.Object.FindObjectsByType<PurchasableDoor>(FindObjectsSortMode.None))
            {
                Transform leaf = door.transform.Find("Leaf");
                if (leaf != null) leaves.Add(leaf.gameObject);
            }

            return leaves.ToArray();
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                Type t = assemblies[i].GetType(fullName, false);
                if (t != null) return t;
            }

            return null;
        }

        /// <summary>
        /// <c>[SerializeField] private</c> bir alanı doldurur. Alanları <c>public</c>
        /// yapmak yerine bu yol seçildi: bir alanı yalnızca kurulum aracı doldurabiliyor
        /// diye herkese açmak, tasarlanmamış bir API üretir (csharp-code.md).
        /// </summary>
        private static void SetPrivateField(UnityEngine.Object target, string fieldName, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[Zombi] '{target.GetType().Name}' uzerinde '{fieldName}' " +
                               "alani yok. Alan adi degistiyse bu araci da guncelle.");
                return;
            }

            // Her tip acikca ele alinir. Onceki surum yalnizca bool ve nesne
            // referansi biliyordu; enum, metin ve dizi alanlari SESSIZCE bos
            // kaliyordu - kapinin fiyat bandi ve adi bu yuzden hic yazilmadi.
            switch (value)
            {
                case bool b:
                    property.boolValue = b;
                    break;

                case int i:
                    property.intValue = i;
                    break;

                case float f:
                    property.floatValue = f;
                    break;

                case string s:
                    property.stringValue = s;
                    break;

                case System.Enum e:
                    property.enumValueIndex = Convert.ToInt32(e);
                    break;

                case UnityEngine.Object[] array:
                    property.arraySize = array.Length;
                    for (int n = 0; n < array.Length; n++)
                    {
                        property.GetArrayElementAtIndex(n).objectReferenceValue = array[n];
                    }
                    break;

                case UnityEngine.Object o:
                    property.objectReferenceValue = o;
                    break;

                case null:
                    property.objectReferenceValue = null;
                    break;

                default:
                    Debug.LogError($"[Zombi] '{fieldName}' icin desteklenmeyen tip: " +
                                   $"{value.GetType().Name}. SetPrivateField genisletilmeli.");
                    return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
