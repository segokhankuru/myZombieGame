using System.Collections.Generic;
using System.Text;
using Bunker.Config;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Asset Store paketlerini oyuna bağlayan araç. 2026-09-07.
    ///
    /// <para><b>Neden bir araç, elle sürükle-bırak değil</b> (CLAUDE.md 10): dört
    /// paketten gelen silah ve karakter modelleri farklı ölçekte, farklı eksende ve
    /// <b>Built-in shader</b>'la geliyor. Proje URP; dönüştürülmemiş bir materyal
    /// oyunda pembe görünür. Bunu elle yapmak yirmi tıklama ve bir sonraki paket
    /// geldiğinde yirmi tıklama daha demek. Araç ölçer, dönüştürür, kaydeder.</para>
    ///
    /// <para><b>Üçüncü parti klasörlerine hiç dokunmaz</b> (asset-art.md): kaynak
    /// prefab'lar ve materyaller olduğu gibi kalır; oyunun kullandığı her şey
    /// <c>Assets/_Project/Art/ThirdParty/</c> altında <i>üretilmiş bir kopya</i>dır.
    /// Paket güncellenirse aracı yeniden çalıştırmak yeter.</para>
    ///
    /// <para><b>Idempotent</b> (editor-tools.md): iki kez çalıştırmak bir kez
    /// çalıştırmakla aynı sonucu verir. Aynı yollara yazar, aynı ölçüleri üretir.</para>
    /// </summary>
    public static class ArtIntegration
    {
        // --- uretilen ciktilar (_Project'in ICINDE, ucuncu parti dokunulmaz) ---

        private const string OutputRoot = "Assets/_Project/Art/ThirdParty";
        private const string MaterialFolder = OutputRoot + "/Materials";
        private const string ModelFolder = OutputRoot + "/Models";
        private const string CatalogPath = "Assets/_Project/Config/art.asset";
        private const string SurfaceFolder = "Assets/_Project/Art/Materials/Surfaces";

        // --- kaynaklar (paketlerin diskteki yolu) ---

        private const string ZombieSource =
            "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/" +
            "(P) Characters_Zombie_SuitMan_1.prefab";

        private const string BloodyWoodFolder = "Assets/Tim's Substances/Bloody_Wood_graph_0";
        private const string WastelandPrefabs = "Assets/The Wasteland LITE/Prefabs";

        /// <summary>
        /// Takılı bir aksesuarın prefab'daki çocuk nesne adının öneki: <c>Att_&lt;ad&gt;</c>.
        ///
        /// <para><b>Ateşli silahların modeli, yönü, eldeki boyu ve aksesuarları artık
        /// burada bir tablo değil</b> (2026-09-10, geliştirici: <i>"import ettiği tüm
        /// silahları ve attachmentları ekleyip düzenleyebileceğim yapı kur"</i>):
        /// <c>config/content/weapon-art.json</c>, <c>Bunker &gt; Silah Atolyesi</c> yazar.
        /// Yönün neden elle seçildiği — namlunun hangi uçta olduğu geometriden
        /// çıkarılamaz, 2026-09-07'de dört silahın ikisi ters çıktı, 2026-09-09'da
        /// Low Poly paketi <c>-Z</c> çıktı — o dosyanın notunda; pencere yönü
        /// görerek seçtiriyor.</para>
        /// </summary>
        internal const string AttachmentPrefix = "Att_";

        /// <summary>Bir çocuk nesne takılı bir aksesuar mı.</summary>
        internal static bool IsAttachmentChild(Transform child) =>
            child != null && child.name.StartsWith(AttachmentPrefix, System.StringComparison.Ordinal);

        /// <summary><paramref name="node"/>, <paramref name="root"/>'a takılı bir aksesuarın içinde mi.</summary>
        internal static bool IsUnderAttachment(Transform node, Transform root)
        {
            while (node != null && node != root)
            {
                if (node.parent == root) return IsAttachmentChild(node);
                node = node.parent;
            }

            return false;
        }

        private const string BlinkWeapons = "Assets/Blink/Art/Weapons/Stylized";

        /// <summary>
        /// Yakın dövüş modelleri (FREE - Stylized Weapons). 2026-09-08.
        ///
        /// <para><b>Yön burada tabloda DEĞİL, ölçülüyor</b> — ateşli silahların
        /// tersine. Sebep: bir namlunun hangi uçta olduğu geometriden çıkarılamaz
        /// (dipçik namludan ince olabilir, dürbün kütleyi kaydırır) ve tam bu yüzden
        /// tablo yazıldı. Bir <b>bıçakta</b> ise ucun hangi tarafta olduğu tek
        /// anlamlıdır: kabza orijinde, ağız uzun eksen boyunca uzanıyor. Bunu ölçmek,
        /// üç satır tahmin yazmaktan güvenilir — ve dördüncü bıçak eklendiğinde
        /// kendiliğinden doğru çalışır.</para>
        ///
        /// <para><b>Hedef boy oyunun kararı:</b> hançer bir el bıçağı, kılıç bir kol
        /// boyu, balta ikisinin arası ama kalın. Üçü aynı elin içinde yaşayacak.</para>
        /// </summary>
        private static readonly (string Id, string PrefabPath, float LengthMeters)[] Melee =
        {
            ("melee.dagger", BlinkWeapons + "/Daggers/_PrefabsDaggers/Dagger1_3_5.prefab", 0.34f),
            ("melee.sword",  BlinkWeapons + "/Swords/_PrefabsSwords/Sword1_1_3.prefab",    0.88f),
            ("melee.axe",    BlinkWeapons + "/Axes/PrefabsAxes/AxeBasic1_2.prefab",        0.72f)
        };

        // ================================================================= giris

        /// <summary>
        /// Komut satırı girişi. <b>Kendisi çıkar</b> — <c>unity-exec.ps1</c>
        /// <c>-quit</c> geçmiyor, çıkmayan bir toplu metot Unity'yi sonsuza kadar açık
        /// bırakır ve proje kilidi bir daha açılmaz.
        /// </summary>
        public static void LinkStoreArtBatch()
        {
            try
            {
                LinkStoreArt();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Magaza sanati] Toplu kosu basarisiz: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Bunker/Gorunum/Magaza Modellerini Bagla", false, 120)]
        public static void LinkStoreArt()
        {
            var log = new System.Text.StringBuilder(512);
            int problems = 0;

            // Silah gorunum dosyasi bozuksa HICBIR SEY uretilmeden patlar: yarim bir
            // kosu modelsiz ya da durbunsuz prefab'lar yazar ve oyunda "silah
            // kayboldu" diye okunur.
            List<WeaponArtData> weapons = WeaponWorkshopData.LoadArt();

            EnsureFolder(OutputRoot);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ModelFolder);
            EnsureFolder(SurfaceFolder);

            // Bu kosuda uretilen materyaller.
            //
            // <b>Neden StartAssetEditing YOK:</b> toplu duzenleme kipinde
            // <c>CreateAsset</c> ertelenir ama <c>LoadAssetAtPath</c> henuz yazilmamis
            // varligi bulamaz - ayni materyali ikinci kez yaratmaya calisip "bu yolda
            // zaten bir varlik var" hatasi alirdik. On bes varlik icin toplu kip zaten
            // olcuulebilir bir kazanc degil; dogruluk once gelir.
            _converted.Clear();

            try
            {
                // --- silahlar (config/content/weapon-art.json)
                var models = new List<ArtCatalogAsset.WeaponModel>(weapons.Count);

                foreach (WeaponArtData art in weapons)
                {
                    var original = AssetDatabase.LoadAssetAtPath<GameObject>(art.ModelPath);
                    if (original == null)
                    {
                        problems++;
                        log.Append($"  EKSIK  {art.WeaponId}: {art.ModelPath} bulunamadi\n");
                        continue;
                    }

                    string copyPath = $"{ModelFolder}/{Sanitize(art.WeaponId)}.prefab";
                    GameObject copy = MakeUrpCopy(original, copyPath, stripColliders: true);
                    if (copy == null)
                    {
                        problems++;
                        log.Append($"  HATA   {art.WeaponId}: kopya uretilemedi\n");
                        continue;
                    }

                    // AKSESUARLAR olcumden ONCE takilir: namlu ucu (alevin yeri) takili
                    // bir susturucunun ucunda olmali. Boy ve merkez ise aksesuarlari
                    // SAYMAZ (Measure) - parca takmak silahi kucultmesin.
                    if (!AttachAccessories(art, copyPath, log)) problems++;

                    // Kopya diskte degisti; olcum icin yeniden okunuyor.
                    copy = AssetDatabase.LoadAssetAtPath<GameObject>(copyPath);

                    ArtCatalogAsset.WeaponModel model = Measure(art.WeaponId, copy, art);
                    models.Add(model);

                    // Sinirlar da yazilir: yon elle secildi ve YANLIS olabilir. Uc sayiya
                    // bakmak, hangi eksenin gercekten uzun oldugunu tartismasiz soyler
                    // (2026-09-07).
                    log.Append($"  {art.WeaponId,-16} olcek {model.localScale:F3}  " +
                               $"namlu z={model.muzzleLocal.z:F3}  " +
                               $"olculen sinirlar={LastMeasuredSize}  " +
                               $"aksesuar {art.Attachments.Count}\n");
                }

                // --- yakin dovus (2026-09-08)
                var meleeModels = new List<ArtCatalogAsset.WeaponModel>(Melee.Length);

                foreach ((string id, string prefabPath, float lengthMeters) in Melee)
                {
                    var original = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (original == null)
                    {
                        problems++;
                        log.Append($"  EKSIK  {id}: {prefabPath} bulunamadi\n");
                        continue;
                    }

                    string copyPath = $"{ModelFolder}/{Sanitize(id)}.prefab";
                    GameObject copy = MakeUrpCopy(original, copyPath, stripColliders: true);
                    if (copy == null)
                    {
                        problems++;
                        log.Append($"  HATA   {id}: kopya uretilemedi\n");
                        continue;
                    }

                    ArtCatalogAsset.WeaponModel model = MeasureMelee(id, copy, lengthMeters);
                    meleeModels.Add(model);

                    // Sinirlarin MIN/MAX'i da yazilir, yalnizca boyutu degil: agzin
                    // hangi ucta oldugu tam olarak bu iki sayidan cikariliyor
                    // (MeasureMelee) ve hipotezin dogrulanabilir olmasi sart -
                    // silahlarda tahmine guvenildigi icin dordun ikisi ters cikmisti.
                    log.Append($"  {id,-16} olcek {model.localScale:F3}  " +
                               $"uc z={model.muzzleLocal.z:F3}  " +
                               $"boyut={LastMeasuredSize}  " +
                               $"min={LastMeasuredMin}  max={LastMeasuredMax}  " +
                               $"agiz: {LastBladeReason}\n");
                }

                // --- zombi
                GameObject zombieCopy = null;
                Vector3 zombieOffset = Vector3.zero;
                float zombieScale = 1f;

                var zombieOriginal = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieSource);
                if (zombieOriginal == null)
                {
                    problems++;
                    log.Append($"  EKSIK  zombi: {ZombieSource} bulunamadi\n");
                }
                else
                {
                    zombieCopy = MakeUrpCopy(zombieOriginal, $"{ModelFolder}/zombie_suitman.prefab",
                                             stripColliders: true);

                    if (zombieCopy != null)
                    {
                        MeasureCharacter(zombieCopy, 1.8f, out zombieScale, out zombieOffset);
                        log.Append($"  zombi            olcek {zombieScale:F3}  " +
                                   $"ayak kaydirmasi y={zombieOffset.y:F3}\n");
                    }
                }

                WriteCatalog(models.ToArray(), meleeModels.ToArray(),
                             zombieCopy, zombieOffset, zombieScale);
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (problems > 0)
            {
                Debug.LogError($"[Magaza sanati] {problems} sorun var - eksik model yerine " +
                               $"GRI KUTU gorunecek:\n{log}");
            }
            else
            {
                Debug.Log($"[Magaza sanati] Baglandi:\n{log}\n" +
                          $"  katalog: {CatalogPath}\n" +
                          "  Simdi: Bunker > Zombi > Test Alanini Kur (prefab'lar yeniden kurulur).");
            }
        }

        // ================================================== URP kopyasi uretme

        /// <summary>
        /// Bir üçüncü parti prefab'ın <b>URP karşılığını</b> üretir.
        ///
        /// <para><b>Neden kopya, neden yerinde dönüştürme değil:</b> paket klasörü
        /// düzenlenmez (asset-art.md). Paket güncellenirse ya da yeniden indirilirse
        /// yaptığımız her değişiklik kaybolurdu — ve bunu kimse fark etmezdi, ta ki
        /// oyunda her şey pembe görünene kadar.</para>
        /// </summary>
        /// <summary>
        /// Silaha aksesuarlarını takar (<c>weapon-art.json</c>, serbest liste).
        ///
        /// <para><b>Idempotent</b> (editor-tools.md): önceki koşudan kalan bütün
        /// <c>Att_</c> çocukları önce silinir. Olmasaydı aracı iki kez çalıştırmak
        /// silaha iki dürbün takardı — "append eden authoring aracı" hatasının ders
        /// kitabı örneği.</para>
        ///
        /// <para><b>Önce hepsi takılır, sonra hepsi yerleştirilir.</b> Silahın çerçevesi
        /// aksesuarsız ölçüldüğü için sıra sonucu değiştirmez; ama bir parçayı henüz
        /// takılmamış diğerlerine göre yerleştirmek bu garantiyi sessizce bozardı.</para>
        ///
        /// <para>Aynı paket parçası iki silaha takılırsa URP kopyası tektir
        /// (<c>att_&lt;prefab adı&gt;</c>).</para>
        /// </summary>
        /// <returns>Sorun çıkmadıysa <c>true</c>.</returns>
        private static bool AttachAccessories(WeaponArtData art, string weaponPrefabPath, StringBuilder log)
        {
            if (art.Attachments.Count == 0) return true;

            bool ok = true;

            // URP kopyalari prefab ICERIGI acilmadan once uretilir: acik bir prefab
            // duzenleme sahnesinin yaninda baska bir prefab kaydetmek, Unity'nin hangi
            // sahneye yazdigini tartismaya acar.
            var copies = new List<(AttachmentData Fit, GameObject Copy)>(art.Attachments.Count);

            foreach (AttachmentData fit in art.Attachments)
            {
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(fit.PrefabPath);

                if (original == null)
                {
                    log.Append($"  EKSIK  {art.WeaponId} aksesuari '{fit.Name}': {fit.PrefabPath} bulunamadi\n");
                    ok = false;
                    continue;
                }

                string name = Sanitize(System.IO.Path.GetFileNameWithoutExtension(fit.PrefabPath)).ToLowerInvariant();
                GameObject copy = MakeUrpCopy(original, $"{ModelFolder}/att_{name}.prefab", stripColliders: true);

                if (copy == null)
                {
                    log.Append($"  HATA   {art.WeaponId} aksesuari '{fit.Name}': URP kopyasi uretilemedi\n");
                    ok = false;
                    continue;
                }

                copies.Add((fit, copy));
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(weaponPrefabPath);

            try
            {
                for (int i = contents.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = contents.transform.GetChild(i);
                    if (IsAttachmentChild(child)) Object.DestroyImmediate(child.gameObject);
                }

                var placed = new List<(Transform Node, AttachmentData Fit)>(copies.Count);

                foreach ((AttachmentData fit, GameObject copy) in copies)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(copy, contents.transform);

                    if (instance == null)
                    {
                        log.Append($"  HATA   {art.WeaponId} aksesuari '{fit.Name}': silaha konamadi\n");
                        ok = false;
                        continue;
                    }

                    instance.name = AttachmentPrefix + fit.Name;
                    placed.Add((instance.transform, fit));
                }

                // Yerlesim Silah Atolyesi ile ORTAK (FitAccessory): pencerenin gosterdigi
                // yer ile burada uretilen yer ayni fonksiyondan cikiyor. Iki ayri hesap
                // "pencerede oturuyordu, oyunda havada" demek olurdu.
                foreach ((Transform node, AttachmentData fit) in placed)
                {
                    if (FitAccessory(art, contents, node, fit)) continue;

                    log.Append($"  HATA   {art.WeaponId} aksesuari '{fit.Name}': silahin olcusu alinamadi (mesh yok)\n");
                    ok = false;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, weaponPrefabPath);
                log.Append($"  {art.WeaponId,-16} {placed.Count} aksesuar takildi\n");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return ok;
        }

        /// <summary>Disaridan cagrilabilen URP kopyalayici (PlayerCharacterSetup kullanir).</summary>
        public static GameObject MakeUrpCopyPublic(GameObject original, string path,
                                                   bool stripColliders) =>
            MakeUrpCopy(original, path, stripColliders);

        private static GameObject MakeUrpCopy(GameObject original, string path, bool stripColliders)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
            if (instance == null) return null;

            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                                                  InteractionMode.AutomatedAction);

                if (stripColliders)
                {
                    var colliders = instance.GetComponentsInChildren<Collider>(true);
                    foreach (Collider c in colliders) Object.DestroyImmediate(c);
                }

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    Material[] shared = renderer.sharedMaterials;
                    var converted = new Material[shared.Length];


                    for (int i = 0; i < shared.Length; i++)
                    {
                        converted[i] = ToUrp(shared[i]);

                        // BOS MATERYAL SESSIZ KALMAZ (2026-09-07: "silahlar bembeyaz").
                        //
                        // Materyali olmayan bir renderer, Unity'nin varsayilan BEYAZ
                        // materyaliyle cizilir - yani "materyal yok" ile "materyal
                        // beyaz" ekranda ayni gorunur. Adini yazmak, hangi paketin
                        // hangi parcasinin bagsiz geldigini soyleyen tek sey.
                        if (converted[i] != null) continue;

                        Debug.LogWarning(
                            $"[Magaza sanati] '{original.name}/{renderer.name}' " +
                            $"materyal yuvasi {i} BOS - varsayilan beyazla cizilecek. " +
                            "Paketin materyali eksik ya da FBX'in icine gomulu olabilir.",
                            original);
                    }

                    renderer.sharedMaterials = converted;
                }

                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Built-in bir materyalin URP karşılığını üretir (ya da zaten üretilmişse
        /// onu döner).
        ///
        /// <para><b>Neden elle, Render Pipeline Converter ile değil:</b> Converter
        /// üçüncü parti klasörünü <i>yerinde</i> değiştirir. Bize gereken, kaynağa
        /// dokunmayan bir kopya.</para>
        /// </summary>
        /// <summary>Bu koşuda zaten dönüştürülmüş materyaller (kaynak → URP kopyası).</summary>
        private static readonly Dictionary<Material, Material> _converted =
            new Dictionary<Material, Material>();

        /// <summary>Disaridan cagrilabilen URP donusturucu (asset rafi kullanir).</summary>
        public static Material ToUrpPublic(Material source) => ToUrp(source);

        private static Material ToUrp(Material source)
        {
            if (source == null) return null;

            if (_converted.TryGetValue(source, out Material cached)) return cached;

            // ZATEN URP VE DOKUSU YERINDEYSE dokunma: kendi urettigimiz materyalleri
            // ikinci kez kopyalamak her calistirmada yeni dosya uretirdi (idempotent
            // degil).
            //
            // <b>"Dokusu yerinde" sarti 2026-09-07'de eklendi</b> ("silahlar
            // bembeyaz"): Unity, PolyOne paketinin Built-in materyalini ice aktarirken
            // URP/Lit'e YUKSELTIYOR ama <c>_MainTex</c>'i <c>_BaseMap</c>'e
            // TASIMIYOR. Sonuc, shader'i dogru ama dokusu bos bir materyal - ekranda
            // Unity'nin varsayilan beyazindan ayirt edilemez. Sadece shader adina
            // bakan bir kontrol bunu "hallolmus" sayar ve sessizce beyaz birakir.
            if (IsUsableUrp(source))
            {
                if (source.GetTexture("_BaseMap") == null)
                {
                    // Dokusuz bir URP materyali duz beyaz cizer ve ekranda "materyal
                    // yok"tan ayirt edilemez. Hangi materyalin dokusuz geldigini
                    // soylemeyen bir arac, sorunu gizler.
                    Debug.LogWarning(
                        $"[Magaza sanati] '{source.name}' zaten URP ({source.shader.name}) " +
                        "ama _BaseMap BOS ve pakette bir _MainTex de bulunamadi - " +
                        "model duz renk cizilecek.", source);
                }

                return source;
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return source;

            string path = $"{MaterialFolder}/{Sanitize(source.name)}.mat";
            var target = AssetDatabase.LoadAssetAtPath<Material>(path);

            bool created = false;
            if (target == null)
            {
                target = new Material(lit) { name = source.name };
                created = true;
            }

            target.shader = lit;
            CopyBuiltInToUrp(source, target);

            if (created) AssetDatabase.CreateAsset(target, path);
            else EditorUtility.SetDirty(target);

            _converted[source] = target;
            return target;
        }

        /// <summary>
        /// Materyalin <b>diskteki</b> doku girdisini okur — shader'a hiç sormadan.
        ///
        /// <para>Shader'ı projede olmayan bir materyalde <c>GetTexture</c> ve
        /// <c>HasProperty</c> boş döner, ama <c>m_SavedProperties</c> hâlâ dolu.
        /// Üçüncü parti paketlerde bu sık: pakete ait shader ayrı satılıyor ya da
        /// başka bir sürüme ait oluyor.</para>
        /// </summary>
        private static Texture SavedTexture(Material material, string propertyName)
        {
            var serialized = new SerializedObject(material);
            SerializedProperty textures =
                serialized.FindProperty("m_SavedProperties.m_TexEnvs");

            if (textures == null) return null;

            for (int i = 0; i < textures.arraySize; i++)
            {
                SerializedProperty entry = textures.GetArrayElementAtIndex(i);
                if (entry.displayName != propertyName
                    && entry.FindPropertyRelative("first")?.stringValue != propertyName)
                {
                    continue;
                }

                return entry.FindPropertyRelative("second.m_Texture")?.objectReferenceValue
                       as Texture;
            }

            return null;
        }

        private static void CopyColor(Material from, string fromName, Material to, string toName)
        {
            if (from.HasProperty(fromName) && to.HasProperty(toName))
            {
                to.SetColor(toName, from.GetColor(fromName));
            }
        }

        private static void CopyTexture(Material from, string fromName, Material to, string toName)
        {
            if (!from.HasProperty(fromName) || !to.HasProperty(toName)) return;

            Texture texture = from.GetTexture(fromName);
            if (texture == null) return;

            to.SetTexture(toName, texture);
            to.SetTextureScale(toName, from.GetTextureScale(fromName));
            to.SetTextureOffset(toName, from.GetTextureOffset(fromName));
        }

        /// <summary>Son olculen modelin sinirlar boyutu - yalnizca gunluge yazmak icin.</summary>
        private static Vector3 LastMeasuredSize;

        /// <summary>Son olculen modelin sinirlar min/max degerleri - yalnizca gunluk icin.</summary>
        private static Vector3 LastMeasuredMin;
        private static Vector3 LastMeasuredMax;

        // ====================================================== durbun yerlesimi

        /// <summary>
        /// Silahın <b>kendi kök uzayındaki</b> çerçevesi: namlu, üst ve sağ yönleri ile
        /// aksesuarsız sınırları. 2026-09-10.
        ///
        /// <para><b>Yönler <c>weapon-art.json</c>'dan gelir</b> (pencerede görülerek
        /// seçilir), sınırlardan tahmin edilmez. Önceki yerleşim "en uzun kenar namludur, Y yukarıdır"
        /// diyordu; tablonun var olma sebebi tam olarak bu tür bir tahminin dört
        /// silahın ikisinde yanlış çıkmasıydı.</para>
        /// </summary>
        internal readonly struct WeaponFrame
        {
            public readonly Bounds Bounds;
            public readonly Vector3 Forward;
            public readonly Vector3 Up;
            public readonly Vector3 Right;

            public WeaponFrame(Bounds bounds, Vector3 forward, Vector3 up)
            {
                Bounds = bounds;
                Forward = forward.normalized;
                Up = up.normalized;
                Right = Vector3.Cross(Up, Forward);   // Unity: sag = yukari x ileri
            }

            public float Length => Mathf.Abs(Vector3.Dot(Bounds.size, Forward));
            public float Height => Mathf.Abs(Vector3.Dot(Bounds.size, Up));
            public float Width => Mathf.Abs(Vector3.Dot(Bounds.size, Right));
        }

        /// <summary>
        /// Silahın çerçevesi. Takılı aksesuarlar (<see cref="AttachmentPrefix"/>) ölçüye girmez.
        /// </summary>
        internal static bool TryWeaponFrame(WeaponArtData art, GameObject weaponRoot, out WeaponFrame frame)
        {
            frame = default;

            if (!CollectVertices(weaponRoot, true, out List<Vector3> points)) return false;

            frame = new WeaponFrame(BoundsOf(points), art.ForwardVector, art.UpVector);
            return true;
        }

        /// <summary>
        /// Aksesuarı silaha yerleştirir. <b>Tek yerleşim hesabı</b>: hem
        /// "Mağaza Modellerini Bağla" hem Silah Atölyesi bunu çağırır. 2026-09-10.
        ///
        /// <para><b>Aksesuarın kendi ölçüsü de ölçülür, pivotu kullanılmaz</b>
        /// (2026-09-10 oyun testi: <i>"silahla scope arasında boşluk var, çok havada
        /// duruyor"</i>). Pivotun modelin neresinde olduğu üçüncü parti paketin kararı
        /// ve bu iki dürbünde gövdenin epey altındaydı; pivot varsayımı satın alınmış
        /// bir modelde asla doğrulanamaz. Bu yüzden: namlu boyunca dürbünün
        /// <b>merkezi</b>, dikeyde <b>alt kenarı</b>, yanda yine <b>merkezi</b>
        /// hizalanır.</para>
        ///
        /// <para>Ölçüler mesh köşelerinden alınır, <c>Renderer.bounds</c>'tan değil:
        /// o dünya uzayında ve eksene hizalı; döndürülmüş bir kökte kutuyu şişirir ve
        /// pencerede (silah el konumunda, dönük) başka, prefab'da başka bir yer
        /// verirdi.</para>
        ///
        /// <para><paramref name="accessory"/> silah kökünün <b>doğrudan</b> çocuğu
        /// olmalı. Dönüşü veriden gelir: başka bir paketten gelen parça ters yönde
        /// çizilmiş olabilir.</para>
        /// </summary>
        internal static bool FitAccessory(WeaponArtData art, GameObject weaponRoot,
                                          Transform accessory, AttachmentData fit)
        {
            if (accessory.parent != weaponRoot.transform) return false;
            if (!TryWeaponFrame(art, weaponRoot, out WeaponFrame frame)) return false;

            // Aksesuarin KENDI uzayinda, olceksiz (kokun olcegi matriste geri aliniyor),
            // sonra DONUSUYLE cevrilmis: cevrilmis bir parcanin alt kenari da cevrilmis
            // haliyle olculmeli, yoksa yan yatan bir lazer govdeye gomulur.
            if (!CollectVertices(accessory.gameObject, out List<Vector3> own)) return false;

            Quaternion rotation = Quaternion.Euler(fit.EulerDegrees);
            for (int i = 0; i < own.Count; i++) own[i] = rotation * own[i];

            Bounds self = BoundsOf(own);

            float scale = fit.ScaleMultiplier;
            Vector3 center = frame.Bounds.center;

            // Namlu: agiz ucundan dipcige dogru oran.
            float muzzle = Vector3.Dot(center, frame.Forward) + frame.Length * 0.5f;
            float along = muzzle - frame.Length * fit.AlongBarrel01
                          - scale * Vector3.Dot(self.center, frame.Forward);

            // Ust: aksesuarin ALT kenari govdenin tepesine, arti bosluk.
            float top = Vector3.Dot(center, frame.Up) + frame.Height * 0.5f;
            float selfBottom = Vector3.Dot(self.center, frame.Up)
                               - Mathf.Abs(Vector3.Dot(self.size, frame.Up)) * 0.5f;
            float up = top + frame.Height * fit.GapOfWeaponHeight - scale * selfBottom;

            // Yan: silahin ortasi, arti kaydirma.
            float side = Vector3.Dot(center, frame.Right) + frame.Width * fit.SideOfWeaponWidth
                         - scale * Vector3.Dot(self.center, frame.Right);

            accessory.localRotation = rotation;
            accessory.localScale = Vector3.one * scale;
            accessory.localPosition = frame.Forward * along + frame.Up * up + frame.Right * side;

            return true;
        }

        /// <summary>
        /// Silahı oyunun el modeli için ölçer (ölçek, dönüş, konum, namlu ucu) — katalog
        /// yazılırken yapılan ölçümün aynısı. Silah Atölyesi, yön, boy ve el konumu
        /// değiştikçe oyuncunun gözünde ne göreceğini canlı göstermek için çağırır.
        /// </summary>
        internal static ArtCatalogAsset.WeaponModel MeasureWeapon(WeaponArtData art, GameObject weaponRoot) =>
            Measure(art.WeaponId, weaponRoot, art);

        /// <summary>
        /// Önizleme için paketin Built-in materyallerini <b>diske yazmadan</b> URP'ye
        /// çevirir. Oluşturulan materyaller <paramref name="created"/>'a eklenir;
        /// çağıran yok eder.
        ///
        /// <para><b>Neden gerekli:</b> kaynak paket prefab'ı doğrudan gösterilirse URP'de
        /// <b>pembe</b> çizilir ve "model bozuk" diye okunur. Kaydet'in yapacağı
        /// dönüşüm (<see cref="ToUrp"/>) ise materyal dosyası üretiyor — sadece bakmak
        /// için diske yazmak olmaz. Kopyalama kuralı ikisinde de aynı fonksiyon
        /// (<see cref="CopyBuiltInToUrp"/>).</para>
        /// </summary>
        internal static void PreviewMaterials(GameObject root, List<Material> created)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < shared.Length; i++)
                {
                    Material source = shared[i];
                    if (source == null || IsUsableUrp(source)) continue;

                    var copy = new Material(lit) { name = source.name + " (onizleme)", hideFlags = HideFlags.HideAndDontSave };
                    CopyBuiltInToUrp(source, copy);

                    created.Add(copy);
                    shared[i] = copy;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = shared;
            }
        }

        /// <summary>
        /// Materyal zaten URP ve dokusu yerinde mi — o zaman dokunulmaz.
        ///
        /// <para><b>"Dokusu yerinde" şartı 2026-09-07'de eklendi</b> ("silahlar
        /// bembeyaz"): Unity, PolyOne paketinin Built-in materyalini içe aktarırken
        /// URP/Lit'e YÜKSELTİYOR ama <c>_MainTex</c>'i <c>_BaseMap</c>'e TAŞIMIYOR.
        /// Yalnızca shader adına bakan bir kontrol bunu "hallolmuş" sayar ve sessizce
        /// beyaz bırakır.</para>
        /// </summary>
        private static bool IsUsableUrp(Material source) =>
            source.shader != null
            && source.shader.name.StartsWith("Universal Render Pipeline")
            && (source.GetTexture("_BaseMap") != null || SavedTexture(source, "_MainTex") == null);

        /// <summary>Built-in materyalin renk, doku ve yüzey değerlerini bir URP/Lit materyaline kopyalar.</summary>
        private static void CopyBuiltInToUrp(Material source, Material target)
        {
            CopyColor(source, "_Color", target, "_BaseColor");
            CopyTexture(source, "_MainTex", target, "_BaseMap");
            CopyTexture(source, "_BumpMap", target, "_BumpMap");
            CopyTexture(source, "_OcclusionMap", target, "_OcclusionMap");
            CopyTexture(source, "_MetallicGlossMap", target, "_MetallicGlossMap");

            // TEK RENK KALDIYSA DOKUYU SERILESMIS VERIDEN AL (2026-09-07 bulgusu:
            // "silahlar bembeyaz").
            //
            // <c>HasProperty</c> materyalin SHADER'ina sorar. PolyOne paketinin
            // materyali projede olmayan bir toon shader'a bakiyor; Unity onu eksik
            // shader'a dusuruyor ve o shader'in hicbir ozelligi yok - yani
            // <c>_MainTex</c> "yok" cikiyor ve doku sessizce kayboluyor. Materyalin
            // DISKTEKI verisi ise dokuyu hala tasiyor.
            if (target.GetTexture("_BaseMap") == null)
            {
                Texture saved = SavedTexture(source, "_MainTex") ?? SavedTexture(source, "_BaseMap");
                if (saved != null) target.SetTexture("_BaseMap", saved);
            }

            if (source.HasProperty("_Glossiness") && target.HasProperty("_Smoothness"))
            {
                target.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
            }

            if (source.HasProperty("_Metallic") && target.HasProperty("_Metallic"))
            {
                target.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            }
        }

        // ================================================================ olcum

        /// <summary>
        /// Bir silah modelini <b>ölçer</b>: ne kadar büyütülecek, nasıl ortalanacak,
        /// namlu ucu nerede.
        ///
        /// <para><b>Yön ölçülmez, verilir</b> (2026-09-07). Önceki sürüm namlunun hangi
        /// uçta olduğunu geometriden çıkarmaya çalışıyordu ve dört silahın ikisinde
        /// yanılıyordu. Yön artık <c>weapon-art.json</c>'da, Silah
        /// Atölyesi'nde <i>görülerek</i> seçilmiş. Burada kalan iş ölçek, merkez ve namlu ucu —
        /// üçü de yön bilindiğinde tek anlamlı.</para>
        ///
        /// <para><b>Neden ölçek yine de hesaplanıyor:</b> paketler farklı birimlerde
        /// geliyor ve elle yazılmış bir ölçek, paket güncellendiğinde sessizce yanlış
        /// olur. Ölçek modelin kendi boyundan türüyor; yön gibi bir "hangisi" sorusu
        /// değil, bir bölme işlemi.</para>
        /// </summary>
        private static ArtCatalogAsset.WeaponModel Measure(string id, GameObject prefab, WeaponArtData art)
        {
            var model = new ArtCatalogAsset.WeaponModel { id = id, prefab = prefab };

            float targetLength = art.LengthMeters;
            Vector3 nativeForward = art.ForwardVector;
            Vector3 nativeUp = art.UpVector;

            // GOVDE olculur, aksesuarlar SAYILMAZ (2026-09-10): sayilsaydi namluya
            // susturucu takmak butun silahi kucultur, uste durbun koymak asagi
            // kaydirirdi. Boy ve merkez silahin kendisinin.
            if (!CollectVertices(prefab, true, out List<Vector3> points))
            {
                model.localScale = 1f;
                return model;
            }

            Bounds bounds = BoundsOf(points);
            LastMeasuredSize = bounds.size;

            int longAxis = AxisIndexOf(nativeForward);
            float frontSign = Axis(nativeForward, longAxis) >= 0f ? 1f : -1f;

            // LookRotation'in verdigi Q, DUNYADAN modele donusturur (Q * +Z = forward).
            // Bize tersi lazim: modelin namlusunu +Z'ye getiren donus.
            Quaternion rotation = Quaternion.LookRotation(nativeForward, nativeUp);
            rotation = Quaternion.Inverse(rotation);

            float length = Axis(bounds.size, longAxis);
            float scale = length > 0.0001f ? targetLength / length : 1f;

            model.localEulerAngles = rotation.eulerAngles;
            model.localScale = scale;

            // ORIJIN KABZADA, ORTADA DEGIL (2026-09-07 bulgusu).
            //
            // İlk sürüm sınırlar kutusunun merkezini orijine alıyordu, yani silahın
            // yarısı elin <b>arkasında</b> kalıyordu. El konumu kameradan 0.42 m
            // ötede; 0.78 m'lik pompalının arka ucu 0.03 m'ye, 0.92 m'lik tüfeğinki
            // kameranın <i>arkasına</i> düşüyordu. Sonuç: ekranı kaplayan bir yüzey.
            //
            // Gri kutu siluetlerinde de oran buydu: tabancanın namlusu +0.235'e
            // uzanırken dipçiği -0.06'da duruyordu — yaklaşık %20 arkada, %80 önde.
            // Aynı oran burada tek bir sayı; el konumunun (GunHome) değişmesi
            // gerekmiyor.
            const float behindHand01 = 0.18f;

            // El konumu duzeltmesi en sonda, EL uzayinda (sag, yukari, ileri): otomatik
            // yerlesim bir modelde yanlis oturuyorsa pencereden goz karariyla duzeltilir.
            model.localPosition = -(rotation * bounds.center) * scale
                                  + Vector3.forward * ((0.5f - behindHand01) * targetLength)
                                  + art.HandOffsetMeters;

            // --- namlu ucu: on %8'lik dilimin ortalamasi.
            //
            // Sinirlar kutusunun on yuzunun MERKEZI degil: dürbünlü bir tüfekte o
            // nokta havada kalir. Gercek koselerin ortalamasi namlunun ekseninde durur.
            //
            // Burada aksesuarlar DAHIL: namluya susturucu takiliysa alev onun ucunda
            // patlamali, govdenin icinde degil.
            CollectVertices(prefab, false, out List<Vector3> all);
            Vector3 tipLocal = FrontSlice(all, BoundsOf(all), longAxis, frontSign, 0.08f);
            model.muzzleLocal = model.localPosition + rotation * tipLocal * scale;

            return model;
        }

        /// <summary>
        /// Bir bıçağı ölçer: <b>ağzı +Z'ye çevir, kabzayı orijine getir</b>.
        /// 2026-09-08.
        ///
        /// <para><b>Yön ölçülüyor, tabloda yazmıyor</b> — ateşli silahların tam
        /// tersine, ve sebebi <see cref="Melee"/>'de. Uzun eksen ağzın ekseni; işaret
        /// ise <i>orijine göre hangi tarafın daha uzun olduğu</i>. Kabza modelin
        /// orijinindedir (bu paketlerin evrensel kuralı: silah elde tutulacak),
        /// dolayısıyla kütlenin uzandığı taraf ağızdır.</para>
        ///
        /// <para><b>Neden kabza orijine oturuyor, merkez değil:</b> el modeli bıçağı
        /// <i>kabzasından</i> tutar. Merkezi orijine almak, 0.88 m'lik bir kılıcın
        /// yarısını elin arkasında bırakırdı — tüfeklerde birebir yaşanan hata.</para>
        /// </summary>
        private static ArtCatalogAsset.WeaponModel MeasureMelee(string id, GameObject prefab,
                                                                float targetLength)
        {
            var model = new ArtCatalogAsset.WeaponModel { id = id, prefab = prefab };

            if (!CollectVertices(prefab, out List<Vector3> points))
            {
                model.localScale = 1f;
                return model;
            }

            Bounds bounds = BoundsOf(points);
            LastMeasuredSize = bounds.size;
            LastMeasuredMin = bounds.min;
            LastMeasuredMax = bounds.max;

            // Uzun eksen = agzin ekseni. Bir bicakta bu tek anlamli.
            int longAxis = 0;
            if (bounds.size.y >= bounds.size.x && bounds.size.y >= bounds.size.z) longAxis = 1;
            else if (bounds.size.z >= bounds.size.x && bounds.size.z >= bounds.size.y) longAxis = 2;

            float min = Axis(bounds.min, longAxis);
            float max = Axis(bounds.max, longAxis);

            float bladeSign = BladeSign(points, bounds, longAxis, min, max);

            Vector3 nativeForward = Vector3.zero;
            if (longAxis == 0) nativeForward.x = bladeSign;
            else if (longAxis == 1) nativeForward.y = bladeSign;
            else nativeForward.z = bladeSign;

            // Yukari yon: uzun eksene DIK olan iki eksenden kalin olani. Bir baltada
            // bu, agzin duzlemidir - yani balta yanindan degil, yuzunden gorunur.
            Vector3 nativeUp = PerpendicularUp(longAxis, bounds.size);

            Quaternion rotation = Quaternion.Inverse(Quaternion.LookRotation(nativeForward, nativeUp));

            float length = max - min;
            float scale = length > 0.0001f ? targetLength / length : 1f;

            model.localEulerAngles = rotation.eulerAngles;
            model.localScale = scale;

            // KABZA ELDE: once merkez orijine alinir, sonra model ILERI itilir.
            // Boy artik dunya olcusunde targetLength; %12'si elin ARKASINDA kalir
            // (kabza avucun icinde), %88'i onde. Tufeklerdeki 'behindHand01' ile ayni
            // fikir, ayni sebeple: merkezi orijine almak 0.88 m'lik kilicin yarisini
            // kameranin arkasinda birakirdi.
            const float behindHand01 = 0.12f;

            model.localPosition = -(rotation * bounds.center) * scale
                                  + Vector3.forward * ((0.5f - behindHand01) * targetLength);

            // Ucun yeri: savurus izi ve carpma efekti oraya konur.
            Vector3 tipLocal = FrontSlice(points, bounds, longAxis, bladeSign, 0.08f);
            model.muzzleLocal = model.localPosition + rotation * tipLocal * scale;

            return model;
        }

        /// <summary>
        /// Ağzın hangi uçta olduğu. <b>İki ölçüt, sırayla</b> — çünkü tek bir ölçüt
        /// üç bıçağın üçünde birden doğru çıkmıyor. 2026-09-08.
        ///
        /// <para><b>1) Orijine göre asimetri.</b> Bu paketlerin modelleri elde
        /// tutulacak şekilde kuruluyor, yani orijin genellikle kabzada ve kütle bir
        /// tarafa uzanıyor. Hançer (−0.48 / +0.15) ve balta (−0.13 / +0.79) bunu net
        /// söylüyor. <b>Ama kılıç söylemiyor</b> (−0.58 / +0.59): orijini tam
        /// ortasında ve ölçü bir yazı tura. Ölçütü tek başına bırakmak, kılıcın
        /// %50 ihtimalle kabzasından tutulup ters savrulması demekti.</para>
        ///
        /// <para><b>2) Uç incelir.</b> Asimetri kararsızsa uçların <i>kesitine</i>
        /// bakılır: bir kılıcın kabza ucunda balçak ve topuz vardır (bu modelde X
        /// yayılımı 0.22, ağzın kalınlığı ise ~0.04), ağız ucu ise sivrilir. <b>Dar
        /// olan uç ağızdır.</b></para>
        ///
        /// <para><b>Neden bu sira:</b> ikinci ölçüt baltada yanılırdı — baltanın
        /// GENIS ucu ağzıdır. Birinci ölçüt orada zaten kararlı (6:1) ve ikinciye hiç
        /// sıra gelmiyor. Yani sıra bir tercih değil, ölçütlerin geçerlilik
        /// alanı.</para>
        ///
        /// <para><b>Her ikisi de günlüğe yazılıyor</b> (<see cref="LastBladeReason"/>):
        /// bir sonraki bıçak eklendiğinde hangi ölçütün karar verdiği görünmeli —
        /// silahlarda tam olarak bu kanıt eksik olduğu için dördün ikisi ters
        /// çıkmıştı.</para>
        /// </summary>
        private static float BladeSign(List<Vector3> points, Bounds bounds, int longAxis,
                                       float min, float max)
        {
            float positive = Mathf.Abs(max);
            float negative = Mathf.Abs(min);

            const float decisiveRatio = 1.5f;

            if (positive > negative * decisiveRatio)
            {
                LastBladeReason = $"asimetri +{positive:F2} / -{negative:F2}";
                return 1f;
            }

            if (negative > positive * decisiveRatio)
            {
                LastBladeReason = $"asimetri -{negative:F2} / +{positive:F2}";
                return -1f;
            }

            // Kararsiz: BALCAGI bul. Kilicin en genis kesiti balcaktir ve balcak
            // kabzanin hemen ustundedir - yani agiz, balcaktan UZAGA uzanir.
            float guard01 = WidestSlice01(points, bounds, longAxis);

            float sign = guard01 < 0.5f ? 1f : -1f;

            LastBladeReason = $"balcak {guard01:F2} (0=alt uc, 1=ust uc); agiz ters yonde";

            return sign;
        }

        /// <summary>Son ölçümün ağız yönü gerekçesi — yalnızca günlüğe yazmak için.</summary>
        private static string LastBladeReason = string.Empty;

        /// <summary>
        /// Uzun eksen boyunca <b>en geniş kesitin</b> yeri (0 = alt uç, 1 = üst uç).
        ///
        /// <para>Bir kılıçta ya da hançerde bu nokta <b>balçaktır</b>: kabzanın hemen
        /// üstünde duran, ağızdan kat kat geniş olan parça. Ağız ondan uzağa uzanır.
        /// Uçların kalınlığını karşılaştırmaktan çok daha keskin bir sinyal, çünkü
        /// balçak ile ağız arasındaki fark üç-dört kat; iki ucun kalınlığı arasındaki
        /// fark ise milimetrelerle ölçülüyor ve gürültüye açık.</para>
        /// </summary>
        private static float WidestSlice01(List<Vector3> points, Bounds bounds, int longAxis)
        {
            const int slices = 24;

            float min = Axis(bounds.min, longAxis);
            float max = Axis(bounds.max, longAxis);
            float span = max - min;

            if (span < 0.0001f) return 0.5f;

            int a = longAxis == 0 ? 1 : 0;
            int b = longAxis == 2 ? 1 : 2;

            var aMin = new float[slices];
            var aMax = new float[slices];
            var bMin = new float[slices];
            var bMax = new float[slices];

            for (int i = 0; i < slices; i++)
            {
                aMin[i] = bMin[i] = float.MaxValue;
                aMax[i] = bMax[i] = float.MinValue;
            }

            for (int i = 0; i < points.Count; i++)
            {
                float t = (Axis(points[i], longAxis) - min) / span;
                int index = Mathf.Clamp((int)(t * slices), 0, slices - 1);

                float va = Axis(points[i], a);
                float vb = Axis(points[i], b);

                if (va < aMin[index]) aMin[index] = va;
                if (va > aMax[index]) aMax[index] = va;
                if (vb < bMin[index]) bMin[index] = vb;
                if (vb > bMax[index]) bMax[index] = vb;
            }

            int widest = slices / 2;
            float widestValue = -1f;

            for (int i = 0; i < slices; i++)
            {
                if (aMin[i] > aMax[i]) continue;

                float width = Mathf.Max(aMax[i] - aMin[i], bMax[i] - bMin[i]);
                if (width <= widestValue) continue;

                widestValue = width;
                widest = i;
            }

            return (widest + 0.5f) / slices;
        }

        /// <summary>Uzun eksene dik iki eksenden <b>kalın</b> olanı — bıçağın yüzü.</summary>
        private static Vector3 PerpendicularUp(int longAxis, Vector3 size)
        {
            int a = longAxis == 0 ? 1 : 0;
            int b = longAxis == 2 ? 1 : 2;

            int thick = Axis(size, a) >= Axis(size, b) ? a : b;

            return thick == 0 ? Vector3.right : thick == 1 ? Vector3.up : Vector3.forward;
        }

        /// <summary>
        /// Karakter modelini ölçer: <b>ayakları sıfıra oturt, boyu hedefe getir</b>.
        /// Zombinin <c>NavMeshAgent</c> yüksekliği 1.8 m; model bundan uzunsa kafası
        /// tavana girer, kısaysa havada yürür.
        /// </summary>
        private static void MeasureCharacter(GameObject prefab, float targetHeight,
                                             out float scale, out Vector3 offset)
        {
            scale = 1f;
            offset = Vector3.zero;

            if (!CollectVertices(prefab, out List<Vector3> points)) return;

            Bounds bounds = BoundsOf(points);
            if (bounds.size.y > 0.0001f) scale = targetHeight / bounds.size.y;

            // Ayak tabani ebeveynin orijinine gelsin.
            offset = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale,
                                 -bounds.center.z * scale);
        }

        /// <summary>
        /// Modelin bütün köşe noktalarını prefab'ın <b>kök yerel uzayında</b> toplar.
        ///
        /// <para><c>Renderer.bounds</c> yetmez: o dünya uzayında ve eksene hizalıdır,
        /// döndürülmüş bir alt nesnede yalan söyler. Köşeler tek doğru kaynak.</para>
        /// </summary>
        private static bool CollectVertices(GameObject prefab, out List<Vector3> points) =>
            CollectVertices(prefab, false, out points);

        /// <summary>
        /// Aynısı; <paramref name="excludeAttachments"/> ise takılı aksesuarlar
        /// (<see cref="AttachmentPrefix"/>) sayılmaz — silahı <b>kendi gövdesiyle</b>
        /// ölçmek için.
        /// </summary>
        private static bool CollectVertices(GameObject prefab, bool excludeAttachments,
                                            out List<Vector3> points)
        {
            points = new List<Vector3>(4096);
            Transform root = prefab.transform;

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (excludeAttachments && IsUnderAttachment(filter.transform, root)) continue;

                Append(points, mesh, root, filter.transform);
            }

            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in skinned)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                if (excludeAttachments && IsUnderAttachment(renderer.transform, root)) continue;

                Append(points, mesh, root, renderer.transform);
            }

            return points.Count > 0;
        }

        private static void Append(List<Vector3> points, Mesh mesh, Transform root, Transform node)
        {
            Vector3[] vertices = mesh.vertices;
            Matrix4x4 toRoot = root.worldToLocalMatrix * node.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i++)
            {
                points.Add(toRoot.MultiplyPoint3x4(vertices[i]));
            }
        }

        private static Bounds BoundsOf(List<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++) bounds.Encapsulate(points[i]);
            return bounds;
        }

        /// <summary>
        /// Uzun eksenin ön ucundaki ince dilimin ortalama noktası — <b>namlu ucu</b>.
        ///
        /// <para><b>Sınırlar kutusunun ön yüzünün merkezi değil:</b> dürbünlü bir
        /// tüfekte o nokta havada kalır ve namlu alevi silahın üstünde patlardı.
        /// Gerçek köşelerin ortalaması namlunun ekseninde durur.</para>
        /// </summary>
        private static Vector3 FrontSlice(List<Vector3> points, Bounds bounds,
                                          int longAxis, float frontSign, float fraction)
        {
            float min = Axis(bounds.min, longAxis);
            float max = Axis(bounds.max, longAxis);
            float span = max - min;

            float threshold = frontSign > 0f ? max - span * fraction : min + span * fraction;

            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < points.Count; i++)
            {
                float t = Axis(points[i], longAxis);
                bool inSlice = frontSign > 0f ? t >= threshold : t <= threshold;
                if (!inSlice) continue;

                sum += points[i];
                count++;
            }

            return count == 0 ? bounds.center : sum / count;
        }

        private static float Axis(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;

        /// <summary>
        /// Eksene hizalı bir vektörün hangi eksen olduğu (0=X, 1=Y, 2=Z).
        /// <c>weapon-art.json</c>'daki yönler her zaman eksene hizalıdır (şemada bir enum).
        /// </summary>
        private static int AxisIndexOf(Vector3 direction)
        {
            float x = Mathf.Abs(direction.x);
            float y = Mathf.Abs(direction.y);
            float z = Mathf.Abs(direction.z);

            if (x >= y && x >= z) return 0;
            return y >= z ? 1 : 2;
        }

        // ============================================================== katalog

        private static void WriteCatalog(ArtCatalogAsset.WeaponModel[] models,
                                         ArtCatalogAsset.WeaponModel[] meleeModels,
                                         GameObject zombie,
                                         Vector3 zombieOffset, float zombieScale)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalogAsset>(CatalogPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArtCatalogAsset>();
                catalog.EditorFill(models, meleeModels, zombie, zombieOffset, zombieScale);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                return;
            }

            catalog.EditorFill(models, meleeModels, zombie, zombieOffset, zombieScale);
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Sahnedeki ve prefab'lardaki bileşenlerin bağlanacağı katalog.</summary>
        public static ArtCatalogAsset LoadCatalog() =>
            AssetDatabase.LoadAssetAtPath<ArtCatalogAsset>(CatalogPath);

        // ====================================================== bunker yuzeyleri

        /// <summary>
        /// Bunkerin duvar malzemesi: <b>kanlı ahşap</b> (2026-09-07, geliştirici
        /// isteği).
        ///
        /// <para><b>Neden yeni bir materyal, paketinki değil:</b> paketinki Built-in
        /// shader ile geliyor ve üçüncü parti klasöründe. Dokuları kullanıp URP
        /// materyalini kendimiz yazıyoruz — böylece kiremitleme (tiling) oyunun
        /// ölçüsüne göre ayarlanabiliyor. 4 m'lik bir duvara 1x1 kiremitlenen bir
        /// ahşap dokusu, ahşap gibi değil duvar kâğıdı gibi görünür.</para>
        /// </summary>
        public static Material EnsureBloodyWood() =>
            EnsureWoodVariant("mat_bunker_wood", Color.white, 3f);

        /// <summary>
        /// Zeminin ahşabı: <b>aynı doku, koyultulmuş</b>. 2026-09-07 (geliştirici:
        /// <i>"tahtaları duvara döşemişsin, yere de döşe"</i>).
        ///
        /// <para><b>Neden aynı doku değil de koyu bir varyantı:</b> gri kutu paletinin
        /// kuralı hâlâ geçerli — zemin sahnedeki en koyu yüzey olmalı ki üstünde duran
        /// her şey (zombi, eşya, oyuncu) ondan ayrılsın (PILLAR-04). Duvarla zemin
        /// birebir aynı malzeme olsaydı, karanlık bir odada nerede durduğunu ayırt
        /// etmek zorlaşırdı — düşme deliğinin kenarı dâhil.</para>
        ///
        /// <para><b>Kiremitleme daha sık:</b> zemin duvardan çok daha geniş bir yüzey;
        /// aynı kiremit sayısı tahta desenini bir halıya çevirirdi.</para>
        /// </summary>
        public static Material EnsureBloodyWoodFloor() =>
            EnsureWoodVariant("mat_bunker_wood_floor", new Color(0.45f, 0.44f, 0.42f), 6f);

        /// <summary>Wasteland duvar dokularının klasörü.</summary>
        private const string WastelandTextures = "Assets/The Wasteland LITE/Textures";

        /// <summary>
        /// Bunkerin duvarı: <b>beton</b> (2026-09-08, geliştirici: <i>"bunker
        /// binasının duvarlarını wasteland asset paketindeki bina için olan wall'u
        /// kullan, beton görünümü verecek"</i>).
        ///
        /// <para><b>Neden doğru bir değişiklik, sadece bir zevk tercihi değil:</b>
        /// duvar ve zemin şimdiye kadar <i>aynı</i> kanlı ahşaptı — yalnızca biri
        /// koyultulmuştu. İki yüzeyi aynı malzemeden yapmak, karanlık bir odada
        /// duvarın nerede bittiğini okunmaz kılar (PILLAR-04). Beton duvar + ahşap
        /// zemin, siluetin okunması için gereken kontrastı malzemeden veriyor.</para>
        ///
        /// <para><b>Paketin materyali değil, dokusundan üretilen kendi materyalimiz</b>
        /// — kanlı ahşapla aynı gerekçe: paketinki Built-in shader ile geliyor ve
        /// üçüncü parti klasöründe (asset-art.md). Ayrıca kiremitleme oyunun ölçüsüne
        /// göre ayarlanabilmeli.</para>
        /// </summary>
        public static Material EnsureConcreteWall()
        {
            EnsureFolder(SurfaceFolder);

            const string name = "mat_bunker_concrete";
            string path = $"{SurfaceFolder}/{name}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return material;

            bool created = false;
            if (material == null)
            {
                material = new Material(lit) { name = name };
                created = true;
            }

            material.shader = lit;

            SetTexture(material, "_BaseMap", $"{WastelandTextures}/Walls_Map_1A.png");
            SetTexture(material, "_BumpMap", $"{WastelandTextures}/Walls_Map_1A_normal.png");
            SetTexture(material, "_OcclusionMap", $"{WastelandTextures}/Walls_Map_1A_occlusion.png");

            if (material.GetTexture("_BaseMap") == null)
            {
                Debug.LogWarning("[Gorunum] Wasteland duvar dokusu bulunamadi " +
                                 $"({WastelandTextures}/Walls_Map_1A.png). Duvarlar duz " +
                                 "renk cizilecek.");
            }

            if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");

            // Kiremitleme ahsaptan SEYREK: beton bloklarin deseni buyuk. 3x3'te
            // duvar bir tugla duvarina, 1.5'te bir DOKUM betona benziyor - istenen
            // ikincisi. (Ayni yaklasiklik uyarisi ahsapta yazili: kup UV'si yuz
            // basina 0..1, yani uzun duvarda doku yatay gerilir.)
            material.SetTextureScale("_BaseMap", new Vector2(1.5f, 1.5f));
            material.SetTextureScale("_BumpMap", new Vector2(1.5f, 1.5f));

            // Beton ne parlar ne metaldir; hafif gri bir taban rengi dokuyu
            // sogutuyor - bunker "yeni dokulmus" degil "terk edilmis" gorunmeli.
            material.SetColor("_BaseColor", new Color(0.78f, 0.78f, 0.76f));
            material.SetFloat("_Smoothness", 0.06f);
            material.SetFloat("_Metallic", 0f);

            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);

            return material;
        }

        private static Material EnsureWoodVariant(string name, Color tint, float tiling)
        {
            EnsureFolder(SurfaceFolder);

            string path = $"{SurfaceFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return material;

            bool created = false;
            if (material == null)
            {
                material = new Material(lit) { name = name };
                created = true;
            }

            material.shader = lit;

            SetTexture(material, "_BaseMap", $"{BloodyWoodFolder}/Bloody_Wood_basecolor.tga");
            SetTexture(material, "_BumpMap", $"{BloodyWoodFolder}/Bloody_Wood_normal.tga");
            SetTexture(material, "_OcclusionMap", $"{BloodyWoodFolder}/Bloody_Wood_AO.tga");

            if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");

            // Kiremitleme 3x3.
            //
            // <b>Bu bir yaklasiklik ve oyle kalmali:</b> duvarlar paylasilan bir kup
            // mesh'inden geliyor ve kupun UV'si yuz basina 0..1. Yani 10 m'lik bir
            // duvar ile 4 m'lik bir duvar ayni sayida kiremit gosterir - uzun duvarda
            // doku yatay gerilir. Duzgun cozum her duvara ayri UV ya da triplanar bir
            // shader; ikisi de ya cizim cagrisi ya da bir ADR demek. 3x3, tek
            // kiremete gore cok daha okunur ve hicbir seye mal olmuyor.
            material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            material.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);

            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);

            return material;
        }

        private static void SetTexture(Material material, string property, string texturePath)
        {
            if (!material.HasProperty(property)) return;

            CapTextureSize(texturePath, property == "_BumpMap");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null) material.SetTexture(property, texture);
        }

        /// <summary>
        /// Üçüncü parti dokuyu <b>bütçeye sokar</b>: en fazla 1024, sıkıştırılmış.
        /// 2026-09-07.
        ///
        /// <para><b>Neden gerekti:</b> ilk build 289 MB çıktı ve bunun <b>63 MB'ı</b>
        /// tek bir ahşap malzemenin üç TGA'sıydı (her biri 21 MB, 4096×4096,
        /// sıkıştırmasız). Bir gri kutu prototipi için bu, indirmesi dakikalar süren
        /// bir dosya demek — arkadaş testinin en can sıkıcı adımı. Duvar dokusu 3×3
        /// kiremitleniyor; 1024 ile 4096 arasındaki farkı ekranda kimse göremez.</para>
        ///
        /// <para><b>Üçüncü parti klasörü düzenlenmiyor mu?</b> Dosyanın kendisine
        /// dokunulmuyor — değişen yalnızca <i>içe aktarma ayarı</i>. Ve kural
        /// (asset-art.md) zaten bunu istiyor: "içe aktarma ayarları koddur, disiplin
        /// değil". Paket yeniden indirilse bu araç ayarı tekrar uygular.</para>
        /// </summary>
        private static void CapTextureSize(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;

            if (importer.maxTextureSize > 1024)
            {
                importer.maxTextureSize = 1024;
                dirty = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Compressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                dirty = true;
            }

            TextureImporterType wanted = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

            if (importer.textureType != wanted)
            {
                importer.textureType = wanted;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        // ======================================================== dis dekorasyon

        /// <summary>
        /// Tahkimat duvarlarını <b>çevre duvarının üstüne dizer</b> — dört kenar
        /// boyunca, eşit aralıkla, geçitler açık kalacak şekilde. 2026-09-07.
        ///
        /// <para><b>Geçitler neden atlanıyor:</b> çeper duvarında zombilerin girdiği
        /// açıklıklar var ("bir yoldan geliyorlar" hissi oradan geliyor). Onların önüne
        /// bir duvar parçası koymak, hem o hissi hem de zombilerin geldiği yönü
        /// okunmaz yapardı — ve dekorun ilk kuralı oynanışa dokunmamak.</para>
        ///
        /// <para><b>Ölçek modele göre değil, aralığa göre:</b> parçanın kendi genişliği
        /// ölçülüp bir sonraki parçanın adımı ondan türetiliyor. Sabit bir adım
        /// yazsaydık paket güncellenip parça büyüdüğünde duvarlar birbirinin içine
        /// girerdi.</para>
        /// </summary>
        private static int LineThePerimeter(BlockoutSettings s, Transform group,
                                            GameObject wallPrefab)
        {
            if (wallPrefab == null || s.PerimeterWallHeight <= 0.1f) return 0;

            float width = PrefabWidth(wallPrefab);
            if (width < 0.5f) return 0;

            // Parca genisligi yazilir: "dizildi" demek yetmez, ARALIK dogru mu
            // sorusunun cevabi bu sayida. 13 m'lik bir parca ile 2 m'lik bir parca
            // ayni koddan cok farkli bir cephe uretir.
            Debug.Log($"[Dis dekor] Tahkimat parcasi genisligi {width:F2} m; " +
                      $"cepere bu adimla diziliyor.");

            float apron = s.ApronWidth;
            float minX = s.West - apron;
            float maxX = s.East + apron;
            float minZ = s.South - apron;
            float maxZ = s.North + apron;

            float midX = (minX + maxX) * 0.5f;
            float midZ = (minZ + maxZ) * 0.5f;
            float halfGate = s.GateWidth * 0.5f + width * 0.5f;

            int placed = 0;

            // Guney ve kuzey kenarlar (X boyunca).
            placed += LineRun(group, wallPrefab, width, minX, maxX, midX, halfGate,
                              along: true, fixedValue: minZ, yaw: 0f);
            placed += LineRun(group, wallPrefab, width, minX, maxX, midX, halfGate,
                              along: true, fixedValue: maxZ, yaw: 180f);

            // Bati ve dogu kenarlar (Z boyunca).
            placed += LineRun(group, wallPrefab, width, minZ, maxZ, midZ, halfGate,
                              along: false, fixedValue: minX, yaw: 90f);
            placed += LineRun(group, wallPrefab, width, minZ, maxZ, midZ, halfGate,
                              along: false, fixedValue: maxX, yaw: -90f);

            return placed;
        }

        /// <summary>Tek bir kenar boyunca dizer; geçidin denk geldiği yeri atlar.</summary>
        private static int LineRun(Transform group, GameObject prefab, float width,
                                   float from, float to, float gateCenter, float halfGate,
                                   bool along, float fixedValue, float yaw)
        {
            int count = Mathf.FloorToInt((to - from) / width);
            if (count <= 0) return 0;

            // Artan bosluk iki uca esit dagitilir: bir kenarda toplanan bosluk,
            // "buraya sigmamis" diye okunur.
            float step = (to - from) / count;
            int placed = 0;

            for (int i = 0; i < count; i++)
            {
                float t = from + step * (i + 0.5f);

                // Gecidin onunu kapatma.
                if (Mathf.Abs(t - gateCenter) < halfGate) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                if (instance == null) continue;

                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                                                   InteractionMode.AutomatedAction);

                instance.transform.position = along
                    ? new Vector3(t, 0f, fixedValue)
                    : new Vector3(fixedValue, 0f, t);

                instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                StripForDecor(instance);
                placed++;
            }

            return placed;
        }

        /// <summary>Prefab'ın en geniş yatay ölçüsü — dizme adımı bundan türüyor.</summary>
        private static float PrefabWidth(GameObject prefab)
        {
            if (!CollectVertices(prefab, out List<Vector3> points)) return 0f;

            Bounds bounds = BoundsOf(points);
            return Mathf.Max(bounds.size.x, bounds.size.z);
        }

        /// <summary>
        /// Dekoru oyuna zararsız hâle getirir: çarpıştırıcı yok, statik değil, URP
        /// materyalli.
        ///
        /// <para><b>NavMesh'e girmemesi şart:</b> zombinin yolunu kapatan bir dekor,
        /// oyun testinde "zombiler gelmiyor" olarak okunur ve sebebi haftalarca
        /// bulunamaz.</para>
        /// </summary>
        private static void StripForDecor(GameObject instance)
        {
            GameObjectUtility.SetStaticEditorFlags(instance, (StaticEditorFlags)0);

            var colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders) Object.DestroyImmediate(c);

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                var converted = new Material[shared.Length];
                for (int m = 0; m < shared.Length; m++) converted[m] = ToUrp(shared[m]);
                renderer.sharedMaterials = converted;
            }
        }

        /// <summary>
        /// Dış alanı The Wasteland LITE parçalarıyla süsler (2026-09-07).
        ///
        /// <para><b>Neden dekorasyon bir araç işi:</b> dışarısı zombilerin geldiği yer
        /// ve şu an düz, boş, gri bir zemin — yani "dışarısı" diye bir yer yok gibi
        /// okunuyor. Ama süs <b>oynanışa dokunmamalı</b>: her parça
        /// <c>NavMesh</c>'in dışında kalacak şekilde çeper duvarının dibine ve
        /// köşelere konur, pencereye giden yol açık bırakılır.</para>
        ///
        /// <para><b>Deterministik</b> (editor-tools.md): sabit tohumlu bir rastgele
        /// akış. Aynı harita, aynı süs — iki kez çalıştırmak sahneyi ikinci bir
        /// kopyayla doldurmaz, önce eskisini siler.</para>
        /// </summary>
        public static int DecorateOutside(BlockoutSettings s, Transform parent)
        {
            // TAHKIMAT DUVARLARI CEPERE DIZILIR, tarlaya serpilmez (2026-09-07,
            // gelistirici: "fortified wall diye ekledigin duvari haritanin en dis
            // duvarina diz, alanda rastgele durmasina gerek yok").
            //
            // <b>Neden dogru:</b> tahkimat duvari bir SINIR parcasi - ortada tek basina
            // duran bir duvar parcasi hicbir sey soylemez, sinirin uzerinde duran bir
            // dizi ise "dunyanin kenari burasi" der. Cevre duvari zaten oradaydi ve
            // ciplak gri bir kutuydu; tahkimat onu kapliyor.
            GameObject wallPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    WastelandPrefabs + "/Fortified_Walls/Fortified_Wall_1A.prefab");

            // Kucuk parcalar hala serpilir: bariyerler ve tahta panolar bir SINIR
            // degil, dagiklik. Onlarin duzenli dizilmesi "birileri burayi tertiplemis"
            // der - istenen tam tersi.
            string[] propPaths =
            {
                WastelandPrefabs + "/Props, Misc/Barrier_1A.prefab",
                WastelandPrefabs + "/Props, Misc/Barrier_1B.prefab",
                WastelandPrefabs + "/Props, Misc/Barrier_1C.prefab",
                WastelandPrefabs + "/Props, Misc/Shanty_Wall_Board_1A.prefab",
                WastelandPrefabs + "/Props, Misc/Shanty_Wall_Board_1B.prefab"
            };

            var props = new List<GameObject>(propPaths.Length);
            foreach (string path in propPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) props.Add(prefab);
            }

            if (props.Count == 0 && wallPrefab == null)
            {
                Debug.LogWarning("[Dis dekor] The Wasteland LITE parcalari bulunamadi; " +
                                 "disarisi ciplak kaldi.");
                return 0;
            }

            Transform group = new GameObject("Decor_Outside").transform;
            group.SetParent(parent, false);

            // BAKE DISINDA (2026-09-09). Carpistirici silmek bir nesneyi navigasyondan
            // CIKARMAZ - ikisi ayri sistem ve ayri soru ("oyuncu carpar mi" ile "ajan
            // yuruyebilir mi"). StripForDecor carpistiriciyi siliyordu ve dekorun
            // "oynanisa dokunmama" kurali yalnizca yarisi uygulanmis halde yaziliydi.
            NavMeshDecor.MarkIgnoredIfPossible(group.gameObject);

            // Sabit tohum: ayni harita her seferinde ayni suslenir.
            var random = new System.Random(20260907);

            float apron = s.ApronWidth;
            int placed = LineThePerimeter(s, group, wallPrefab);

            // Kucuk parcalar: binanin DORT KENARI boyunca, cepere yakin serit.
            // Pencerenin onunde durmamak icin duvardan en az 4 m uzakta.
            for (int i = 0; i < 24; i++)
            {
                if (props.Count == 0) break;

                GameObject prefab = props[random.Next(props.Count)];

                bool alongX = random.Next(2) == 0;
                bool positive = random.Next(2) == 0;

                float t = (float)random.NextDouble();
                float band = 4f + (float)random.NextDouble() * Mathf.Max(0.5f, apron - 7f);

                Vector3 position;
                if (alongX)
                {
                    float x = Mathf.Lerp(s.West - apron * 0.7f, s.East + apron * 0.7f, t);
                    float z = positive ? s.North + band : s.South - band;
                    position = new Vector3(x, 0f, z);
                }
                else
                {
                    float z = Mathf.Lerp(s.South - apron * 0.7f, s.North + apron * 0.7f, t);
                    float x = positive ? s.East + band : s.West - band;
                    position = new Vector3(x, 0f, z);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                if (instance == null) continue;

                // Bagi koparilir: prefab ornegi uzerinden bilesen silinemez, ve bir
                // ucuncu parti prefab'a gecirme (override) yazmak paket guncellendigi
                // gun sessizce kaybolur.
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                                                   InteractionMode.AutomatedAction);

                instance.transform.position = position;
                instance.transform.rotation = Quaternion.Euler(0f, random.Next(0, 360), 0f);

                StripForDecor(instance);
                placed++;
            }

            return placed;
        }

        // ============================================================= yardimci

        private static string Sanitize(string name)
        {
            var builder = new System.Text.StringBuilder(name.Length);

            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                          || (c >= '0' && c <= '9') || c == '_' || c == '-';
                builder.Append(ok ? c : '_');
            }

            return builder.ToString();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
