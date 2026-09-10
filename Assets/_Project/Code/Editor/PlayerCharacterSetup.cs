using System.Text;
using Bunker.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Oyuncuya <b>gerçek bir gövde</b> verir: Human Basic Motions modeli, animatör
    /// denetleyicisi ve hareket blend ağacı (2026-09-09).
    ///
    /// <para><b>Neden bir araç, elle kurulum değil</b> (CLAUDE.md #10 ve
    /// editor-tools.md): bir animatör denetleyicisi elle kurulduğunda YAML olarak
    /// diske yazılır, diff'i okunamaz ve bir daha üretilemez. Buradaki her düğüm ve
    /// her geçiş koddan çıkıyor — yani <i>neden</i> öyle olduğu okunabiliyor ve
    /// yeniden çalıştırmak aynı sonucu veriyor.</para>
    ///
    /// <para><b>Idempotent</b>: iki kez çalıştırmak ikinci bir gövde takmaz, ikinci bir
    /// blend ağacı kurmaz. Append eden bir authoring aracı, bir sahneyi kırk kopya
    /// ışıkla dolduran şeydir.</para>
    ///
    /// <para><b>Erkek model seçildi</b> çünkü paket iki tane veriyor ve oyunda karakter
    /// seçimi <i>yok</i> (CONTEXT.md: kozmetik ekonomisi v1'de yok). İkisini birden
    /// bağlamak, kullanılmayan bir seçim mekanizması kodlamak olurdu. Kadın model
    /// pakette duruyor; karakter seçimi geldiğinde buraya bir satır yazılır.</para>
    /// </summary>
    public static class PlayerCharacterSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";
        private const string OutputFolder = "Assets/_Project/Art/ThirdParty/Models";
        private const string ControllerPath = OutputFolder + "/player_locomotion.controller";
        private const string ModelCopyPath = OutputFolder + "/player_body.prefab";

        private const string Root = "Assets/Kevin Iglesias/Human Animations";
        private const string ModelSource = Root + "/Models/HumanM_Model.fbx";
        private const string Male = Root + "/Animations/Male";

        /// <summary>Gövdenin prefab içindeki adı. Idempotentliğin anahtarı.</summary>
        private const string BodyObjectName = "Body";

        /// <summary>
        /// Blend ağacına giren klipler ve <b>ağaçtaki yerleri</b>.
        ///
        /// <para>Eksenler: <c>Speed</c> ileri/geri (-1 geri, 0 dur, 1 yürüme, 2 koşu),
        /// <c>Strafe</c> yanal (-1 sol, 1 sağ). İki eksenli bir ağaç seçildi çünkü tek
        /// eksenli bir ağaçta geri geri yürüyen oyuncu ileri yürüyor görünür — co-op'ta
        /// arkadaşının ne yaptığını okumanın en temel parçası bu.</para>
        /// </summary>
        private static readonly (string Path, float Speed, float Strafe)[] Clips =
        {
            (Male + "/Idles/HumanM@Idle01.fbx",                    0f,  0f),

            (Male + "/Movement/Walk/HumanM@Walk01_Forward.fbx",    1f,  0f),
            (Male + "/Movement/Walk/HumanM@Walk01_Backward.fbx",  -1f,  0f),
            (Male + "/Movement/Walk/HumanM@Walk01_Left.fbx",       0f, -1f),
            (Male + "/Movement/Walk/HumanM@Walk01_Right.fbx",      0f,  1f),

            (Male + "/Movement/Sprint/HumanM@Sprint01_Forward.fbx", 2f,  0f),
            (Male + "/Movement/Sprint/HumanM@Sprint01_Left.fbx",    2f, -1f),
            (Male + "/Movement/Sprint/HumanM@Sprint01_Right.fbx",   2f,  1f)
        };

        /// <summary>Komut satırı girişi. <b>Kendisi çıkar</b> (unity-exec -quit geçmiyor).</summary>
        public static void SetupBatch()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Karakter] Toplu kosu basarisiz: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Bunker/Oyuncu/Karakteri Kur", false, 140)]
        public static void Setup()
        {
            var log = new StringBuilder(512);

            // --- 1. modelin insansi (humanoid) olarak ice aktarildigindan emin ol
            if (!EnsureHumanoid(ModelSource, log)) return;

            foreach ((string path, _, _) in Clips) EnsureHumanoid(path, log);

            // --- 2. animator denetleyicisi
            AnimatorController controller = BuildController(log);
            if (controller == null) return;

            // --- 3. modelin URP kopyasi
            var modelOriginal = AssetDatabase.LoadAssetAtPath<GameObject>(ModelSource);

            if (modelOriginal == null)
            {
                Debug.LogError($"[Karakter] Model yok: {ModelSource}");
                return;
            }

            GameObject bodyPrefab = ArtIntegration.MakeUrpCopyPublic(
                modelOriginal, ModelCopyPath, stripColliders: true);

            if (bodyPrefab == null)
            {
                Debug.LogError("[Karakter] Modelin URP kopyasi uretilemedi.");
                return;
            }

            // --- 4. oyuncu prefab'ina tak
            if (!AttachToPlayer(bodyPrefab, controller, log)) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Karakter] Kuruldu.\n{log}");
        }

        /// <summary>
        /// FBX'in <b>insansı</b> olarak içe aktarıldığından emin olur.
        ///
        /// <para>Paket klipleri varsayılan olarak insansı geliyor ama <b>varsaymak
        /// yetmez</b>: genel (generic) içe aktarılmış tek bir klip, blend ağacında
        /// sessizce hareketsiz durur — ve bu, teşhisi en pahalı animasyon hatasıdır
        /// (hata yok, uyarı yok, karakter sadece kaymaya devam eder).</para>
        ///
        /// <para><b>Zaten doğruysa dosyaya dokunmaz</b> (editor-tools.md): idempotent
        /// olmayan bir araç her koşuda kırk FBX'i yeniden içe aktartır.</para>
        /// </summary>
        private static bool EnsureHumanoid(string path, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                log.Append($"  EKSIK  {path}\n");
                return false;
            }

            if (importer.animationType == ModelImporterAnimationType.Human) return true;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();

            log.Append($"  insansi yapildi: {System.IO.Path.GetFileName(path)}\n");
            return true;
        }

        /// <summary>
        /// Hareket denetleyicisini kurar: tek durum, <b>iki eksenli blend ağacı</b>.
        ///
        /// <para><b>Neden tek durum, durum makinesi değil:</b> yürüme/koşma arasındaki
        /// geçiş bir <i>eşik</i> değil bir <i>karışım</i>. Ayrı durumlar ve geçişler
        /// olsaydı, hızlanan oyuncu belirli bir hızda "atlayan" bir animasyon
        /// gösterirdi. Zıplama ve düşme geldiğinde <b>o</b> ayrı bir durum olacak —
        /// çünkü zıplamak gerçekten ayrı bir hâl.</para>
        /// </summary>
        private static AnimatorController BuildController(StringBuilder log)
        {
            // IDEMPOTENT: var olan denetleyici bastan kurulur. Uzerine eklemek, her
            // kosuda blend agacina bir kopya daha koymak olurdu.
            AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            if (controller == null)
            {
                Debug.LogError($"[Karakter] Denetleyici uretilemedi: {ControllerPath}");
                return null;
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Strafe", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "Strafe",
                blendParameterY = "Speed"
            };

            // Agac VARLIGIN ICINE yazilir: ayri bir dosya olsaydi denetleyiciyi silmek
            // yetim bir blend agaci birakirdi.
            AssetDatabase.AddObjectToAsset(tree, controller);

            int added = 0;

            foreach ((string path, float speed, float strafe) in Clips)
            {
                AnimationClip clip = LoadClip(path);

                if (clip == null)
                {
                    log.Append($"  EKSIK KLIP  {path}\n");
                    continue;
                }

                tree.AddChild(clip, new Vector2(strafe, speed));
                added++;
            }

            if (added == 0)
            {
                Debug.LogError("[Karakter] Hicbir klip bulunamadi - denetleyici bos kalirdi. " +
                               "Paket yolu degismis olabilir: " + Male);
                return null;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.AddState("Locomotion");
            state.motion = tree;
            machine.defaultState = state;

            EditorUtility.SetDirty(controller);
            log.Append($"  denetleyici: {added} klip -> {ControllerPath}\n");

            return controller;
        }

        /// <summary>
        /// FBX'in içindeki animasyon klibini bulur.
        ///
        /// <para><c>LoadAssetAtPath&lt;AnimationClip&gt;</c> <b>yetmez</b>: bir FBX
        /// birden fazla alt varlık taşır ve ana varlık <c>GameObject</c>'tir. Ayrıca
        /// Unity her modele "__preview__" adlı gizli bir klip ekler — onu alan bir araç,
        /// karakteri hiç kıpırdamayan bir hâlde bırakır.</para>
        /// </summary>
        private static AnimationClip LoadClip(string path)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (Object asset in all)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            return null;
        }

        /// <summary>
        /// Gövdeyi oyuncu prefab'ına takar ve <see cref="PlayerAvatar"/>'ı bağlar.
        ///
        /// <para><b>Model KAPSULUN ICINE oturur, onun yerine geçmez:</b> çarpışma
        /// <c>CharacterController</c>'ın işi ve o kapsül hareketin tamamının dayandığı
        /// şey. Modeli çarpışma gövdesi yapmak, animasyonun fiziği belirlemesi olurdu —
        /// gameplay-code.md'nin tam tersi.</para>
        /// </summary>
        private static bool AttachToPlayer(GameObject bodyPrefab, AnimatorController controller,
                                           StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogError($"[Karakter] Oyuncu prefab'i yok: {PlayerPrefabPath}");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                // IDEMPOTENT: ikinci kosuda ikinci govde takilmaz.
                Transform existing = contents.transform.Find(BodyObjectName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    bodyPrefab, contents.transform);

                if (instance == null)
                {
                    Debug.LogError("[Karakter] Govde prefab'i sahneye konamadi.");
                    return false;
                }

                instance.name = BodyObjectName;

                // Kapsulun TABANINA oturur. CharacterController'in merkezi govdenin
                // ortasinda; model ise ayaklarindan baslar.
                var body = contents.GetComponent<CharacterController>();
                float bottom = body != null ? body.center.y - body.height * 0.5f : -0.9f;

                instance.transform.localPosition = new Vector3(0f, bottom, 0f);
                instance.transform.localRotation = Quaternion.identity;

                Animator animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;

                // KOK HAREKETI KAPALI: konumu CharacterController belirliyor. Acik
                // olsaydi animasyon karakteri surukler ve iki ayri hareket kaynagi
                // birbiriyle yarisirdi - "karakter kayiyor" hatasinin klasik sebebi.
                animator.applyRootMotion = false;

                // Gorunmezken de animasyon isler: kapali olsaydi uzak oyuncunun
                // govdesi ekrana girdigi an yanlis pozda belirirdi.
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var avatar = contents.GetComponent<PlayerAvatar>();
                if (avatar == null) avatar = contents.AddComponent<PlayerAvatar>();

                var serialized = new SerializedObject(avatar);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.FindProperty("model").objectReferenceValue = instance;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);

                log.Append($"  govde takildi: {BodyObjectName} (y={bottom:F2})\n");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
