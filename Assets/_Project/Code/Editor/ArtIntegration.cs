using System.Collections.Generic;
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
        /// Silah eşleşmesi. <b>Hedef boy oyunun kararı, paketin değil:</b> dört ayrı
        /// paketten gelen dört silah aynı dünyada yaşamalı — biri diğerinin iki katı
        /// olursa el modeli ekranı kaplar. Boy, gri kutu siluetlerinin boyudur
        /// (<c>WeaponShape</c>), yani oyunun zaten alıştığı ölçü.
        /// </summary>
        private readonly struct WeaponSource
        {
            public readonly string Id;
            public readonly string PrefabPath;
            public readonly float LengthMeters;

            /// <summary>Modelin KENDI uzayinda namlunun baktigi yon.</summary>
            public readonly Vector3 NativeForward;

            /// <summary>Modelin KENDI uzayinda silahin ustu.</summary>
            public readonly Vector3 NativeUp;

            public readonly string Why;

            public WeaponSource(string id, string prefabPath, float lengthMeters,
                                Vector3 nativeForward, Vector3 nativeUp, string why)
            {
                Id = id; PrefabPath = prefabPath; LengthMeters = lengthMeters;
                NativeForward = nativeForward; NativeUp = nativeUp; Why = why;
            }
        }

        /// <summary>
        /// Silahların kaynağı ve <b>ölçülmüş nativ yönü</b>.
        ///
        /// <para><b>Yön neden tabloda, koddan çıkarılmıyor</b> (2026-09-07): ilk sürüm
        /// namlunun hangi uçta olduğunu mesh köşelerinden <i>tahmin ediyordu</i> —
        /// "ince olan uç namludur", "kütle merkezinin kaydığı taraf kabzadır". Dört
        /// silahın ikisinde yanlış çıktı ve geliştirici oyunda gördü:
        /// <i>"silahlar ters, bana doğru dönük."</i> M16'nın dipçiği namlusundan ince,
        /// kabzası da az köşe taşıyor; sezgi modele bağlı, model pakete bağlı.</para>
        ///
        /// <para><b>Yerine kanıt kondu:</b> her modelin sınırlar kutusu günlüğe
        /// yazılıyor (<i>olculen sinirlar</i>) ve eksen çubuklu önizlemesi çekiliyor
        /// (<c>ArtPreview</c> → <c>Logs/art-preview/raw_*.png</c>). Üç sayı hangi
        /// eksenin uzun olduğunu tartışmasız söylüyor; resim ise yönün işaretini.
        /// <b>Yalnızca resme bakmak yetmedi</b> — 3/4 açıdan bakan bir kamerada X ve Z
        /// çubukları benzer yöne düşüyor ve ilk okuma iki silahta yanlış çıktı. Sayı
        /// ile resim birlikte.</para>
        ///
        /// <para>İki paket iki farklı düzende geliyor: Low Poly paketleri (tabanca,
        /// pompalı) <b>+Z</b>, PolyOne (MP5, M16) <b>-X</b>. Yani "hepsi aynıdır" diye
        /// bir kural yok; beşinci silah geldiğinde önizlemeye bakılır ve buraya bir
        /// satır yazılır.</para>
        ///
        /// <para><b>Hedef boy oyunun kararı, paketin değil:</b> dört ayrı paketten
        /// gelen dört silah aynı dünyada yaşamalı. Boy, gri kutu siluetlerinin boyu
        /// (<c>WeaponShape</c>) — yani oyunun zaten alıştığı ölçü.</para>
        /// </summary>
        private static readonly WeaponSource[] Weapons =
        {
            new WeaponSource("weapon.pistol",
                "Assets/Low Poly Pistol Weapon Pack 2/Prefabs/Weapons/Pistol_F.prefab",
                0.26f, Vector3.forward, Vector3.up,
                "Tabanca: en kisa siluet, referans silah."),

            new WeaponSource("weapon.smg",
                "Assets/PolyOne/Free Gun/Prefabs/SM_HK_MP5.prefab",
                0.52f, Vector3.left, Vector3.up,
                "Taramali (MP5): kisa gövde, sarkan sarjor - tabancadan ilk " +
                "bakista ayrilir."),

            new WeaponSource("weapon.shotgun",
                "Assets/Low Poly ShotGun Weapon Pack 1/Prefabs/Weapons/ShotGun_A.prefab",
                0.78f, Vector3.forward, Vector3.up,
                "Pompali: kalin namlu, altinda pompa kolu - yakin mesafenin cevabi."),

            new WeaponSource("weapon.rifle",
                "Assets/PolyOne/Free Gun/Prefabs/SM_M16A1.prefab",
                0.92f, Vector3.left, Vector3.up,
                "Tufek: en uzun namlu - uzaktan is gordugu silüetinden okunmali.")
        };

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
                // --- silahlar
                var models = new List<ArtCatalogAsset.WeaponModel>(Weapons.Length);

                foreach (WeaponSource source in Weapons)
                {
                    var original = AssetDatabase.LoadAssetAtPath<GameObject>(source.PrefabPath);
                    if (original == null)
                    {
                        problems++;
                        log.Append($"  EKSIK  {source.Id}: {source.PrefabPath} bulunamadi\n");
                        continue;
                    }

                    string copyPath = $"{ModelFolder}/{Sanitize(source.Id)}.prefab";
                    GameObject copy = MakeUrpCopy(original, copyPath, stripColliders: true);
                    if (copy == null)
                    {
                        problems++;
                        log.Append($"  HATA   {source.Id}: kopya uretilemedi\n");
                        continue;
                    }

                    ArtCatalogAsset.WeaponModel model = Measure(source.Id, copy, source.LengthMeters,
                                                                source.NativeForward,
                                                                source.NativeUp);
                    models.Add(model);

                    // Sinirlar da yazilir: yon tablodan geliyor ve tablo YANLIS
                    // olabilir. Uc sayiya bakmak, hangi eksenin gercekten uzun
                    // oldugunu tartismasiz soyler - resme bakip 3/4 acidan eksen
                    // tahmin etmekten cok daha guvenilir (2026-09-07).
                    log.Append($"  {source.Id,-16} olcek {model.localScale:F3}  " +
                               $"namlu z={model.muzzleLocal.z:F3}  " +
                               $"olculen sinirlar={LastMeasuredSize}\n");
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
            if (source.shader != null
                && source.shader.name.StartsWith("Universal Render Pipeline")
                && (source.GetTexture("_BaseMap") != null || SavedTexture(source, "_MainTex") == null))
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

        // ================================================================ olcum

        /// <summary>
        /// Bir silah modelini <b>ölçer</b>: ne kadar büyütülecek, nasıl ortalanacak,
        /// namlu ucu nerede.
        ///
        /// <para><b>Yön ölçülmez, verilir</b> (2026-09-07). Önceki sürüm namlunun hangi
        /// uçta olduğunu geometriden çıkarmaya çalışıyordu ve dört silahın ikisinde
        /// yanılıyordu. Yön artık <see cref="Weapons"/> tablosunda, eksen çubuklu
        /// önizlemelerden <i>okunarak</i>. Burada kalan iş ölçek, merkez ve namlu ucu —
        /// üçü de yön bilindiğinde tek anlamlı.</para>
        ///
        /// <para><b>Neden ölçek yine de hesaplanıyor:</b> paketler farklı birimlerde
        /// geliyor ve elle yazılmış bir ölçek, paket güncellendiğinde sessizce yanlış
        /// olur. Ölçek modelin kendi boyundan türüyor; yön gibi bir "hangisi" sorusu
        /// değil, bir bölme işlemi.</para>
        /// </summary>
        private static ArtCatalogAsset.WeaponModel Measure(string id, GameObject prefab,
                                                           float targetLength,
                                                           Vector3 nativeForward,
                                                           Vector3 nativeUp)
        {
            var model = new ArtCatalogAsset.WeaponModel { id = id, prefab = prefab };

            if (!CollectVertices(prefab, out List<Vector3> points))
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

            model.localPosition = -(rotation * bounds.center) * scale
                                  + Vector3.forward * ((0.5f - behindHand01) * targetLength);

            // --- namlu ucu: on %8'lik dilimin ortalamasi.
            //
            // Sinirlar kutusunun on yuzunun MERKEZI degil: dürbünlü bir tüfekte o
            // nokta havada kalir. Gercek koselerin ortalamasi namlunun ekseninde durur.
            Vector3 tipLocal = FrontSlice(points, bounds, longAxis, frontSign, 0.08f);
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
        private static bool CollectVertices(GameObject prefab, out List<Vector3> points)
        {
            points = new List<Vector3>(4096);
            Transform root = prefab.transform;

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Append(points, mesh, root, filter.transform);
            }

            var skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in skinned)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;

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
        /// <see cref="Weapons"/> tablosundaki yönler her zaman eksene hizalıdır.
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
