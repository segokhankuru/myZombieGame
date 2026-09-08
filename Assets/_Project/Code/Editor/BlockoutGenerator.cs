using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// LVL-01 gri kutu haritasını <see cref="BlockoutSettings"/> ölçülerinden üretir
    /// (`design/levels/LVL-01-greybox.md`).
    ///
    /// <para><b>Idempotent:</b> iki kez çalıştırmak bir kez çalıştırmakla aynı sonucu
    /// verir; mevcut kök silinip yeniden kurulur (editor-tools.md). Tek Undo adımı.</para>
    ///
    /// <para><b>Pencereler tek yerden hesaplanır.</b> Delikler ve işaretleri aynı
    /// hesaptan çıkar — önceki sürümde ikisi ayrı hesaplanıyordu ve ayak izi değişince
    /// birbirinden kayıyorlardı. Pencereler duvar boyunca eşit aralıkla dağıtılır,
    /// sabit ofsetlerle değil; böylece bina büyüyünce dağılım bozulmaz.</para>
    /// </summary>
    public static class BlockoutGenerator
    {
        private const string RootName = "LVL-01_Blockout";
        private const string SettingsPath = "Assets/_Project/Settings/BlockoutSettings.asset";

        /// <summary>Üretilen bir pencere ve dışa bakan yönü (doğum noktası için).</summary>
        private readonly struct WindowRecord
        {
            public readonly Vector3 Position;
            public readonly Vector3 Outward;
            public readonly bool GroundFloor;

            public WindowRecord(Vector3 position, Vector3 outward, bool groundFloor)
            {
                Position = position; Outward = outward; GroundFloor = groundFloor;
            }
        }

        private static readonly List<WindowRecord> Windows = new List<WindowRecord>();

        // ---------------------------------------------------------------- menu

        [MenuItem("Bunker/Level/Ayarlari Ac", false, 90)]
        public static void OpenSettings()
        {
            Selection.activeObject = LoadOrCreateSettings();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Bunker/Level/LVL-01 Gri Kutu Uret", false, 100)]
        public static void GenerateFromMenu() => Generate(LoadOrCreateSettings());

        /// <summary>
        /// Dış alan düzenini ayar varlığına uygular (2026-09-04).
        ///
        /// <para><b>Neden ayrı bir adım gerekiyor:</b> C# içindeki varsayılanı
        /// değiştirmek <b>var olan bir <c>ScriptableObject</c> varlığını
        /// değiştirmez</b> — Unity diskte yazılı değeri okur. <c>WindowSpacingMeters</c>
        /// varlıkta <c>8</c> olarak duruyordu ve sınıftaki yeni varsayılan (<c>12</c>)
        /// hiçbir şey yapmıyordu. Harita yeniden üretildi, pencere sayısı değişmedi,
        /// araç da "üretildi" dedi — sessiz başarısızlığın bir başka kılığı.</para>
        ///
        /// <para><b>Yalnızca bu oturumda değişen alanı yazar.</b> Elle ayarlanmış başka
        /// hiçbir ölçüye dokunmaz.</para>
        /// </summary>
        [MenuItem("Bunker/Level/Dis Alan Duzenini Uygula", false, 102)]
        public static void ApplyOutdoorLayout()
        {
            BlockoutSettings settings = LoadOrCreateSettings();
            var defaults = ScriptableObject.CreateInstance<BlockoutSettings>();

            var so = new SerializedObject(settings);
            int changed = 0;

            // Yalnizca 2026-09-04'te degisen alan. Digerleri (ApronWidth,
            // PerimeterWallHeight, GateWidth, SpawnStandoffMeters) varlikta hic
            // yazili olmadigi icin sinif varsayilanini zaten aliyor.
            changed += SyncFloat(so, "WindowSpacingMeters", defaults.WindowSpacingMeters);

            if (changed > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            Object.DestroyImmediate(defaults);

            Debug.Log($"[Blockout] Dis alan duzeni uygulandi. {changed} alan guncellendi. " +
                      "Simdi 'Bunker/Zombi/Test Alanini Kur' calistir - harita yeniden " +
                      "uretilmeli ve NavMesh yeniden bake edilmeli.");
        }

        private static int SyncFloat(SerializedObject so, string field, float wanted)
        {
            SerializedProperty p = so.FindProperty(field);

            if (p == null)
            {
                Debug.LogError($"[Blockout] '{field}' alani bulunamadi - alan adi degismis olabilir.");
                return 0;
            }

            if (Mathf.Approximately(p.floatValue, wanted)) return 0;

            Debug.Log($"[Blockout] {field}: {p.floatValue} -> {wanted}");
            p.floatValue = wanted;
            return 1;
        }

        /// <summary>
        /// <b>Dünyayı sıfırdan kurar</b>: harita, yüzeyler, kurulum, NavMesh.
        /// 2026-09-08.
        ///
        /// <para><b>Neden tek bir giriş noktası</b> (<c>CLAUDE.md</c> 10, ve
        /// geliştiricinin çalışma biçimi): bu dört adım <i>ayrı ayrı</i> ve <b>doğru
        /// sırada</b> çalıştırılmak zorunda — harita üretilmeden yüzey uygulanamaz,
        /// yüzeyler uygulanmadan kurulum haritayı korumaya alır, NavMesh en sonda
        /// bake edilmeli çünkü üreteç kökün tamamını yeniden kuruyor. Dört menü
        /// maddesini sırayla tıklamak bir adım değil, hatırlanması gereken bir
        /// prosedürdür; ve unutulan bir adım sessizce yanlış bir harita üretir.</para>
        ///
        /// <para><b>YIKICIDIR ve öyle kalmalı:</b> haritayı sıfırdan üretir, yani elle
        /// yapılmış her düzenlemeyi siler. Bu yüzden <c>SetupTestbed</c> haritaya
        /// dokunmuyor ve bu ayrı bir giriş; yıkıcı olan şey, yıkıcı olduğunu söyleyen
        /// bir kapının arkasında durmalı.</para>
        /// </summary>
        [MenuItem("Bunker/Level/DUNYAYI SIFIRDAN KUR (harita + yuzey + kurulum)", false, 103)]
        public static void RebuildWorld()
        {
            Generate(LoadOrCreateSettings());

            // Yuzeyler haritadan SONRA: uretec kokun tamamini yeniden kurdugu icin
            // materyaller her uretimde yeniden atanmali.
            GreyboxLook.Apply();

            // Kurulum en sonda: haritayi zaten var buldugu icin ona dokunmaz, ama
            // zombi prefab'ini, HUD'u, barikatlari ve NavMesh'i tazeler.
            ZombieSetup.SetupTestbed();
        }

        /// <summary>Başsız giriş: <see cref="RebuildWorld"/>.</summary>
        public static void RebuildWorldBatch()
        {
            const string scenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RebuildWorld();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Blockout] Dunya kurulamadi: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Başsız giriş: ayar güncellemesi + harita üretimi.</summary>
        public static void ApplyOutdoorLayoutBatch()
        {
            const string scenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ApplyOutdoorLayout();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Blockout] Dis alan duzeni uygulanamadi: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Bunker/Level/LVL-01 Gri Kutuyu Sil", false, 101)]
        public static void Clear()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing == null)
            {
                Debug.Log("[Blockout] Silinecek bir sey yok.");
                return;
            }

            Undo.DestroyObjectImmediate(existing);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static BlockoutSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<BlockoutSettings>(SettingsPath);
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<BlockoutSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Blockout] Ayar varligi olusturuldu: {SettingsPath}");
            return settings;
        }

        // ---------------------------------------------------------------- denetim

        /// <summary>
        /// Ayarları denetler. Mesajlar <b>ne yapılacağını</b> söyler — "gecersiz deger"
        /// bir hata mesaji degildir (editor-tools.md).
        /// </summary>
        public static List<string> Validate(BlockoutSettings s)
        {
            var problems = new List<string>();

            if (s.East <= s.West) problems.Add("East, West'ten buyuk olmali.");
            if (s.North <= s.South) problems.Add("North, South'tan buyuk olmali.");
            if (s.Divider <= s.West || s.Divider >= s.East)
                problems.Add("Divider, West ile East arasinda olmali (A|B kapili duvari).");

            if (s.RampRun <= 0.1f)
            {
                problems.Add("RampEndZ, RampStartZ'den buyuk olmali.");
            }
            else if (s.RampAngleDegrees > 45f)
            {
                problems.Add($"Rampa egimi {s.RampAngleDegrees:F0} derece. NavMesh varsayilani " +
                             "45 derecede kesiliyor; zombiler cikamaz. Cozum: RampEndZ'yi " +
                             "buyut (rampayi uzat) ya da UpperFloorY'yi kucult.");
            }
            else if (s.RampAngleDegrees > 35f)
            {
                problems.Add($"Rampa egimi {s.RampAngleDegrees:F0} derece - bake gecer ama dik " +
                             "hissettirir. 25-30 derece daha rahat.");
            }

            if (s.RampHeadroom < 2f)
                problems.Add($"Rampada kafa payi {s.RampHeadroom:F2} m. Oyuncu 1.8 m; zemine " +
                             "kafa atar. Cozum: RampHoleStartZ'yi kucult (acikligi one cek) " +
                             "ya da UpperFloorY'yi buyut.");

            if (s.RampEndZ >= s.North)
                problems.Add("RampEndZ kuzey duvarinda ya da disinda. Rampa ust katin ICINDE " +
                             "bitmeli ki oyuncu cikinca zemine bassin.");

            if (s.RampX < s.Divider)
                problems.Add("RampX, Divider'in batisinda - rampa B bolgesinde olmali.");

            if (s.DropHoleMinX < s.West || s.DropHoleMaxX > s.Divider)
                problems.Add("Dusme deligi A bolgesinin icinde olmali (West ile Divider arasi).");

            if (s.WindowTop > s.WallHeight)
                problems.Add("WindowTop, WallHeight'tan buyuk olamaz.");
            if (s.DoorHeight > s.WallHeight)
                problems.Add("DoorHeight, WallHeight'tan buyuk olamaz.");

            for (int i = 0; i < (s.Partitions?.Length ?? 0); i++)
            {
                Partition p = s.Partitions[i];
                if (p.To <= p.From)
                    problems.Add($"Bolme {i}: To, From'dan buyuk olmali.");
                if (p.GapWidth < 1f)
                    problems.Add($"Bolme {i}: gecis {p.GapWidth:F1} m - oyuncu sigmaz, " +
                                 "en az 1 m olmali.");
                if (p.GapCenter - p.GapWidth / 2f < p.From ||
                    p.GapCenter + p.GapWidth / 2f > p.To)
                    problems.Add($"Bolme {i}: gecis duvarin disinda kaliyor, duvar kapali olur.");
            }

            return problems;
        }

        // ---------------------------------------------------------------- uretim

        public static void Generate(BlockoutSettings s)
        {
            foreach (string problem in Validate(s))
            {
                Debug.LogWarning($"[Blockout] {problem}");
            }

            GameObject existing = GameObject.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            Windows.Clear();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "LVL-01 Gri Kutu Uret");

            BuildGroundFloor(root.transform, s);
            BuildUpperFloor(root.transform, s);
            BuildPartitions(root.transform, s);
            BuildMarkers(root.transform, s);

            // "Icerisi neresi" sorusunun calisma anindaki cevabi (2026-09-07). Esya
            // dusme kurali bunu okur; ayak izi burada, ayarlarla ayni sayilardan
            // kuruluyor - ikinci bir yere kopyalanmis sinir, harita degistigi gun
            // sessizce yanlis cevap verirdi.
            var interior = root.AddComponent<Bunker.Config.BunkerInterior>();
            interior.Configure(s.West, s.East, s.South, s.North,
                               -0.5f, s.UpperFloorY + s.WallHeight + 1f);

            // Pencere tikaclari: zombi girer, oyuncu cikamaz (2026-09-07).
            int blockers = InstallWindowBlockers(root.transform, s);

            // Disarisi: The Wasteland LITE parcalari. NavMesh'in disinda kalir.
            int decor = ArtIntegration.DecorateOutside(s, root.transform);

            // ...ve uzerine bitki ortusu, toprak yollar, kara bulut tavani
            // (2026-09-08). AYRI bir adim: Wasteland parcalari SINIRI kuruyor
            // (tahkimat duvarlari, bariyerler), Toby parcalari ise ARAZIYI. Ikisini
            // tek metotta toplamak, birini kapatmak istediginde digerini de
            // kapatmak demekti.
            //
            // Yollar GERCEK pencere konumlarindan turuyor: ayri hesaplanmis bir yol,
            // ayak izi degistigi gun barikatin yanindan gecerdi.
            var groundWindows = new List<(Vector3, Vector3)>(Windows.Count);

            for (int i = 0; i < Windows.Count; i++)
            {
                if (!Windows[i].GroundFloor) continue;

                groundWindows.Add((Windows[i].Position, Windows[i].Outward));
            }

            int scenery = OutdoorScenery.Decorate(s, root.transform, groundWindows);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log(
                $"[Blockout] LVL-01 uretildi.\n" +
                $"  ayak izi    : {s.FootprintWidth:F0} x {s.FootprintDepth:F0} m, tavan {s.WallHeight:F1} m\n" +
                $"  rampa       : {s.RampAngleDegrees:F1} derece, kafa payi {s.RampHeadroom:F2} m\n" +
                $"  pencere     : {Windows.Count} adet\n" +
                $"  ic bolme    : {(s.Partitions?.Length ?? 0)} adet\n" +
                $"  disarida serit: {s.ApronWidth:F0} m (zombiler pencereye buradan yurur)\n" +
                $"  pencere tikaci: {blockers} adet (oyuncu cikamaz, mermi gecer)\n" +
                $"  dis dekor   : {decor} parca (Wasteland: sinir)\n" +
                $"  dis arazi   : {scenery} parca (Toby: yol, bitki, bulut)\n" +
                $"  SIRADAKI ADIM: 'Bunker/Zombi/NavMesh Bake' - uretec kokun tamamini " +
                $"yeniden kurdugu icin eski bake gecersizdir.");
        }

        /// <summary>
        /// Her zemin kat penceresine <b>oyuncu tıkacı</b> koyar. 2026-09-07.
        ///
        /// <para><b>Neden gerekti</b> (geliştirici): <i>"barikat penceresinden dışarı
        /// çıkabiliyorum, bunu engelle."</i> Pencere duvarda gerçek bir delik ve oyuncu
        /// zıplayınca dışarı düşüyor. Dışarısı savunulacak yer değil — orada oyuncunun
        /// arkası, yanı ve önü açık, ve bütün tur tasarımı "içeride tutunmak" üzerine
        /// kurulu.</para>
        ///
        /// <para><b>Neden özel bir katman</b> (<c>GameLayers.PlayerBlocker</c>): sıradan
        /// bir kutu mermiyi de durdururdu, yani pencereden ateş etmek imkânsız olurdu —
        /// çözdüğünden büyük bir hata. Bu katman her nişan sorgusunun maskesinin
        /// dışında; yalnızca <c>CharacterController</c> ona çarpar. Zombiler ışın
        /// kullanmadan, <c>NavMeshAgent</c> ve elle sürülen tırmanışla geçtiği için
        /// onları hiç etkilemez.</para>
        ///
        /// <para><b>Görünmez:</b> Renderer yok. Oyuncunun gördüğü şey barikat tahtaları
        /// ve duvar; tıkaç bir fizik nesnesi, bir görsel değil.</para>
        /// </summary>
        private static int InstallWindowBlockers(Transform root, BlockoutSettings s)
        {
            int layer = EnsurePlayerBlockerLayer();

            // HER pencere, yalnizca zemin kat degil: ust kattaki bir pencereden
            // dusmek de disari cikmaktir, ustelik dusme hasariyla birlikte.
            Transform windows = root.Find("Markers/Windows");
            if (windows == null) return 0;

            float height = Mathf.Max(0.4f, s.WindowTop - s.WindowSill);
            int count = 0;

            foreach (Transform window in windows)
            {
                var blocker = new GameObject("PlayerBlocker");
                blocker.transform.SetParent(window, false);
                blocker.transform.localPosition = Vector3.zero;
                blocker.transform.localRotation = Quaternion.identity;

                if (layer >= 0) blocker.layer = layer;

                var box = blocker.AddComponent<BoxCollider>();

                // Pencere agzindan GENIS ve KALIN: kenarindan sizmak, tikacin hic
                // olmamasiyla ayni sey. Kalinlik duvarin iki kati - yuksek hizda
                // hareket eden bir CharacterController ince bir kutunun icinden gecer.
                box.size = new Vector3(s.WindowWidth + 0.4f, height + 0.3f,
                                       Mathf.Max(0.6f, s.WallThickness * 2f));

                count++;
            }

            return count;
        }

        /// <summary>
        /// <c>PlayerBlocker</c> katmanını proje ayarlarında garantiler.
        ///
        /// <para><b>Neden araç açıyor, elle değil:</b> katman yoksa tıkaçlar mermiyi de
        /// durdurur ve bu <i>sessiz</i> bir hatadır — oyun çalışır, sadece pencereden
        /// ateş edilemez ve kimse sebebini bulamaz. Kurulumun bir adımının insan
        /// hafızasına bırakılması, bu projedeki hataların en sık kaynağı.</para>
        /// </summary>
        private static int EnsurePlayerBlockerLayer()
        {
            int existing = LayerMask.NameToLayer(Bunker.Config.GameLayers.PlayerBlockerName);
            if (existing >= 0) return existing;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[Blockout] TagManager.asset okunamadi; " +
                               $"'{Bunker.Config.GameLayers.PlayerBlockerName}' katmani acilamadi.");
                return -1;
            }

            var tagManager = new SerializedObject(asset[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // 0-7 Unity'nin ayirdigi katmanlar; kullanicinin ilki 8.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = Bunker.Config.GameLayers.PlayerBlockerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                Debug.Log($"[Blockout] '{slot.stringValue}' katmani acildi (index {i}).");
                return i;
            }

            Debug.LogError("[Blockout] Bos katman yuvasi kalmamis; pencere tikaclari " +
                           "mermiyi de durduracak.");
            return -1;
        }

        private static void BuildGroundFloor(Transform parent, BlockoutSettings s)
        {
            Transform zone = Group("GroundFloor", parent);

            Slab(zone, "Floor_Ground", s, s.West, s.East, s.South, s.North, -s.FloorThickness / 2f);

            BuildApron(zone, s);

            WallAlongZ(zone, "Wall_West", s, s.West, s.South, s.North, 0f, Vector3.left,
                EvenWindows(s, s.South, s.North));

            WallAlongZ(zone, "Wall_East", s, s.East, s.South, s.North, 0f, Vector3.right,
                EvenWindows(s, s.South, s.North));

            // Guney ve kuzey duvarlari Divider'da ikiye ayriliyor ki A ve B'nin
            // pencereleri kendi duvar boylarina gore dagitilsin.
            WallAlongX(zone, "Wall_South_A", s, s.South, s.West, s.Divider, 0f, Vector3.back,
                EvenWindows(s, s.West, s.Divider));
            WallAlongX(zone, "Wall_South_B", s, s.South, s.Divider, s.East, 0f, Vector3.back,
                EvenWindows(s, s.Divider, s.East));

            WallAlongX(zone, "Wall_North_A", s, s.North, s.West, s.Divider, 0f, Vector3.forward,
                EvenWindows(s, s.West, s.Divider));
            WallAlongX(zone, "Wall_North_B", s, s.North, s.Divider, s.East, 0f, Vector3.forward,
                EvenWindows(s, s.Divider, s.East));

            // A|B kapili duvari: satin alinacak kapi burada
            float midZ = (s.South + s.North) / 2f;
            WallAlongZ(zone, "Wall_Divider_A_B", s, s.Divider, s.South, s.North, 0f, Vector3.zero,
                new[] { new Gap(midZ, s.DoorWidth, 0f, s.DoorHeight, false) });

            BuildRamp(zone, s);
            BuildRampShaft(zone, s);
        }

        /// <summary>
        /// Rampayı iki yan duvarla <b>kapalı bir merdiven boşluğuna</b> çevirir.
        ///
        /// <para><b>Neden gerekliydi:</b> üst kat kapısı rampanın ağzında duran serbest
        /// bir bloktu ve açık alanda duruyordu — oyuncu (ve zombi) yanından dolaşıp üst
        /// kata çıkabiliyordu. Yani satın alınan kapı hiçbir şeyi kapatmıyordu; harita
        /// açma ekonomisinin (SYS-ekonomi) tamamı o kapının bir <i>engel</i> olmasına
        /// dayanıyor.</para>
        ///
        /// <para><b>Çözüm kapıyı büyütmek değil, boşluğu daraltmaktır.</b> Bir kapı
        /// ancak tek geçit olduğunda kapıdır. Yan duvarlar zemin katından üst kat
        /// döşemesine kadar çıkar; tavanı zaten üst katın kendi döşemesi
        /// (<c>Floor_C_Band4_S</c>) kapatır — geliştiricinin önerdiği çözüm buydu ve
        /// doğrusu da bu.</para>
        ///
        /// <para>Duvarlar rampa açıklığının kenarlarında durur, yani <b>rampanın kendisi
        /// hiç dokunulmadan</b> koridorun içinde kalır. Rampa ölçüleri Inspector'dan
        /// değişince koridor da onunla birlikte kayar.</para>
        /// </summary>
        private static void BuildRampShaft(Transform zone, BlockoutSettings s)
        {
            if (s.RampRun <= 0.1f) return;

            float entranceZ = RampDoorZ(s);
            float closedUntilZ = s.RampHoleStartZ;

            // Ust kat dosemesi rampanin agzindan ONCE bitiyorsa kapatilacak bir tavan
            // yok demektir; sessizce yarim bir koridor uretmek yerine soylenir.
            if (closedUntilZ <= entranceZ)
            {
                Debug.LogWarning("[Blockout] RampHoleStartZ kapinin gerisinde: rampa " +
                                 "koridoru kapanmiyor ve UST KAT KAPISI ATLANABILIR. " +
                                 "RampHoleStartZ'yi buyut ya da RampStartZ'yi kucult.");
                return;
            }

            float length = closedUntilZ - entranceZ;
            float centerZ = (entranceZ + closedUntilZ) / 2f;
            float height = s.UpperFloorY;

            Box(zone, "Wall_RampShaft_West",
                new Vector3(s.RampHoleMinX, height / 2f, centerZ),
                new Vector3(s.WallThickness, height, length));

            Box(zone, "Wall_RampShaft_East",
                new Vector3(s.RampHoleMaxX, height / 2f, centerZ),
                new Vector3(s.WallThickness, height, length));
        }

        /// <summary>
        /// Üst kat kapısının durduğu Z: rampanın <b>başlangıcından bir metre ileride</b>.
        ///
        /// <para><b>Tek kaynak:</b> kapı, kanadı ve merdiven boşluğunun ağzı aynı
        /// sayıdan türer — üçü ayrı hesaplansaydı biri kayınca kapı duvarın içinde ya da
        /// bir karış önünde kalırdı.</para>
        ///
        /// <para><b>Neden rampanın önünde değil, bir metre içinde:</b> ilk deneme kapıyı
        /// rampa ağzına, güney duvarının 35 cm önüne koydu. Sonuç bir bağlantı hatası
        /// oldu — <c>NavMesh</c> ajanı 70 cm çapında ve 35 cm'lik bir şeride sığmıyor,
        /// yani kapının önünde <i>durulacak yer</i> kalmıyordu. Bağlantı kontrolü bunu
        /// "zemin -&gt; ust kat: KOPUK" olarak yakaladı. Bir metre içeri alınca kapının
        /// önünde 1.85 m'lik gerçek bir yaklaşma alanı kalıyor.</para>
        /// </summary>
        private static float RampDoorZ(BlockoutSettings s) => s.RampStartZ + 1f;

        /// <summary>
        /// Binanın çevresindeki dış zemin. <b>Zombiler pencereden girer</b> (M1-04) ve
        /// bunun için önce dışarıda yürüyebilecekleri bir NavMesh olmalı. Zemin plakası
        /// duvarların tam altında bittiği için dışarısı bake'te boşluktu; bu şerit onu
        /// kapatır.
        ///
        /// <para>Dört ayrı bant olarak kuruluyor, tek büyük plaka olarak değil: tek
        /// plaka iç zeminle üst üste biner ve z-fighting üretir.</para>
        /// </summary>
        private static void BuildApron(Transform zone, BlockoutSettings s)
        {
            if (s.ApronWidth <= 0.1f) return;

            Transform apron = Group("Apron_Outside", zone);
            float y = -s.FloorThickness / 2f;
            float w = s.ApronWidth;

            Slab(apron, "Apron_South", s, s.West - w, s.East + w, s.South - w, s.South, y);
            Slab(apron, "Apron_North", s, s.West - w, s.East + w, s.North, s.North + w, y);
            Slab(apron, "Apron_West", s, s.West - w, s.West, s.South, s.North, y);
            Slab(apron, "Apron_East", s, s.East, s.East + w, s.South, s.North, y);

            BuildPerimeter(zone, s);
        }

        /// <summary>
        /// Yaklaşma bölgesini çevreleyen dış duvar ve geçitleri.
        ///
        /// <para><b>Neden var</b> (geliştirici, 2026-09-04): <i>"Dışarıya bakınca bir
        /// ortam görelim ve zombiler barikata gelirken bir yoldan doğru geldiği
        /// gözüksün."</i> Sonsuz düz bir zemin, dışarısı diye bir yer olmadığını söyler;
        /// duvar dünyaya bir kenar verir ve geçitler zombinin nereden geleceğini
        /// <b>önceden</b> okunur kılar.</para>
        ///
        /// <para><b>Duvar NavMesh'i keser, geçitler açar.</b> Yani zombi rastgele bir
        /// yönden değil, dört geçidin birinden gelir — dışarısını savunmak artık bir
        /// anlam taşır (PILLAR-04: neyin nereden geldiğini bilmek).</para>
        ///
        /// <para><b>Geçitler kenarların ORTASINDA.</b> Köşeye koymak iki geçidi
        /// birbirine yakınlaştırır ve sürüyü tek noktaya yığar.</para>
        /// </summary>
        private static void BuildPerimeter(Transform zone, BlockoutSettings s)
        {
            if (s.PerimeterWallHeight <= 0.1f) return;

            Transform group = Group("Perimeter_Outside", zone);

            float w = s.ApronWidth;
            float h = s.PerimeterWallHeight;
            float t = s.WallThickness;
            float y = h / 2f;

            float minX = s.West - w;
            float maxX = s.East + w;
            float minZ = s.South - w;
            float maxZ = s.North + w;

            float midX = (minX + maxX) / 2f;
            float midZ = (minZ + maxZ) / 2f;
            float half = s.GateWidth / 2f;

            // Guney ve kuzey: X boyunca, ortada gecit.
            Box(group, "Perimeter_South_A", new Vector3((minX + midX - half) / 2f, y, minZ),
                new Vector3(midX - half - minX, h, t));
            Box(group, "Perimeter_South_B", new Vector3((midX + half + maxX) / 2f, y, minZ),
                new Vector3(maxX - midX - half, h, t));

            Box(group, "Perimeter_North_A", new Vector3((minX + midX - half) / 2f, y, maxZ),
                new Vector3(midX - half - minX, h, t));
            Box(group, "Perimeter_North_B", new Vector3((midX + half + maxX) / 2f, y, maxZ),
                new Vector3(maxX - midX - half, h, t));

            // Bati ve dogu: Z boyunca, ortada gecit.
            Box(group, "Perimeter_West_A", new Vector3(minX, y, (minZ + midZ - half) / 2f),
                new Vector3(t, h, midZ - half - minZ));
            Box(group, "Perimeter_West_B", new Vector3(minX, y, (midZ + half + maxZ) / 2f),
                new Vector3(t, h, maxZ - midZ - half));

            Box(group, "Perimeter_East_A", new Vector3(maxX, y, (minZ + midZ - half) / 2f),
                new Vector3(t, h, midZ - half - minZ));
            Box(group, "Perimeter_East_B", new Vector3(maxX, y, (midZ + half + maxZ) / 2f),
                new Vector3(t, h, maxZ - midZ - half));
        }

        private static void BuildRamp(Transform zone, BlockoutSettings s)
        {
            if (s.RampRun <= 0.1f) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ramp_To_Upper";
            go.transform.SetParent(zone, false);

            float length = Mathf.Sqrt(s.RampRun * s.RampRun + s.UpperFloorY * s.UpperFloorY);
            go.transform.position = new Vector3(s.RampX, s.UpperFloorY / 2f,
                                                (s.RampStartZ + s.RampEndZ) / 2f);
            go.transform.rotation = Quaternion.Euler(-s.RampAngleDegrees, 0f, 0f);
            go.transform.localScale = new Vector3(s.RampWidth, 0.3f, length);
        }

        private static void BuildUpperFloor(Transform parent, BlockoutSettings s)
        {
            Transform zone = Group("UpperFloor", parent);
            float slabY = s.UpperFloorY - s.FloorThickness / 2f;

            // Zemin IKI aciklik birakir: dusme deligi (dongunun kapanma noktasi) ve
            // rampa agzi. X bantlarina bolunerek kuruluyor.
            Slab(zone, "Floor_C_Band1", s, s.West, s.DropHoleMinX, s.South, s.North, slabY);
            Slab(zone, "Floor_C_Band2_S", s, s.DropHoleMinX, s.DropHoleMaxX, s.South, s.DropHoleMinZ, slabY);
            Slab(zone, "Floor_C_Band2_N", s, s.DropHoleMinX, s.DropHoleMaxX, s.DropHoleMaxZ, s.North, slabY);
            Slab(zone, "Floor_C_Band3", s, s.DropHoleMaxX, s.RampHoleMinX, s.South, s.North, slabY);
            Slab(zone, "Floor_C_Band4_S", s, s.RampHoleMinX, s.RampHoleMaxX, s.South, s.RampHoleStartZ, slabY);
            Slab(zone, "Floor_C_Band4_N", s, s.RampHoleMinX, s.RampHoleMaxX, s.RampHoleMaxZ, s.North, slabY);
            Slab(zone, "Floor_C_Band5", s, s.RampHoleMaxX, s.East, s.South, s.North, slabY);

            WallAlongZ(zone, "Wall_C_West", s, s.West, s.South, s.North, s.UpperFloorY, Vector3.left,
                EvenWindows(s, s.South, s.North, ground: false));
            WallAlongZ(zone, "Wall_C_East", s, s.East, s.South, s.North, s.UpperFloorY, Vector3.right,
                EvenWindows(s, s.South, s.North, ground: false));
            WallAlongX(zone, "Wall_C_South", s, s.South, s.West, s.East, s.UpperFloorY, Vector3.back,
                EvenWindows(s, s.West, s.East, ground: false));
            WallAlongX(zone, "Wall_C_North", s, s.North, s.West, s.East, s.UpperFloorY, Vector3.forward,
                EvenWindows(s, s.West, s.East, ground: false));

            if (!s.DropHoleLips) return;

            float midZ = (s.DropHoleMinZ + s.DropHoleMaxZ) / 2f;
            float len = s.DropHoleMaxZ - s.DropHoleMinZ;

            Box(zone, "DropLip_West",
                new Vector3(s.DropHoleMinX, s.UpperFloorY + 0.25f, midZ),
                new Vector3(0.2f, 0.5f, len));
            Box(zone, "DropLip_East",
                new Vector3(s.DropHoleMaxX, s.UpperFloorY + 0.25f, midZ),
                new Vector3(0.2f, 0.5f, len));
        }

        private static void BuildPartitions(Transform parent, BlockoutSettings s)
        {
            if (s.Partitions == null || s.Partitions.Length == 0) return;

            Transform zone = Group("Partitions", parent);

            for (int i = 0; i < s.Partitions.Length; i++)
            {
                Partition p = s.Partitions[i];
                var gap = new Gap(p.GapCenter, p.GapWidth, 0f, s.DoorHeight, false);

                Gap[] gaps = WithRampOpening(s, p, gap, i);

                if (p.Axis == PartitionAxis.AlongZ)
                {
                    WallAlongZ(zone, $"Partition_{i}", s, p.Position, p.From, p.To, 0f,
                        Vector3.zero, gaps);
                }
                else
                {
                    WallAlongX(zone, $"Partition_{i}", s, p.Position, p.From, p.To, 0f,
                        Vector3.zero, gaps);
                }
            }
        }

        /// <summary>
        /// Bir iç bölme rampanın üstünden geçiyorsa, rampa hizasında <b>tavana kadar</b>
        /// bir açıklık açar.
        ///
        /// <para><b>Neden otomatik:</b> rampa eğik olduğu için duvarı 3 metre yükseklikte
        /// keser — kapı boşluğunun çok üstünde. Sonuç, oyuncunun haritaya bakınca fark
        /// etmediği ama zombilerin tam olarak takıldığı bir tıkaç olur. Ölçüler
        /// Inspector'dan denenerek bulunuyor; her denemede "rampa duvara giriyor mu"
        /// diye kontrol etmeyi insana bırakmak, er geç unutulacak bir kontroldür.
        /// Üreteç biliyorsa hiç unutulmaz.</para>
        /// </summary>
        private static Gap[] WithRampOpening(BlockoutSettings s, Partition p, Gap designerGap, int index)
        {
            if (s.RampRun <= 0.1f) return new[] { designerGap };

            float rampMinX = s.RampX - s.RampWidth / 2f - s.RampHoleMargin;
            float rampMaxX = s.RampX + s.RampWidth / 2f + s.RampHoleMargin;

            float openingCenter;
            float openingWidth;

            if (p.Axis == PartitionAxis.AlongX)
            {
                // Duvar sabit Z'de, X boyunca uzaniyor. Rampa bu Z'yi kesiyor mu?
                if (p.Position < s.RampStartZ || p.Position > s.RampEndZ) return new[] { designerGap };
                if (p.To <= rampMinX || p.From >= rampMaxX) return new[] { designerGap };

                openingCenter = s.RampX;
                openingWidth = rampMaxX - rampMinX;
            }
            else
            {
                // Duvar sabit X'te, Z boyunca uzaniyor. Rampanin bandinda mi?
                if (p.Position < rampMinX || p.Position > rampMaxX) return new[] { designerGap };
                if (p.To <= s.RampStartZ || p.From >= s.RampEndZ) return new[] { designerGap };

                float from = Mathf.Max(p.From, s.RampStartZ);
                float to = Mathf.Min(p.To, s.RampEndZ);
                openingCenter = (from + to) / 2f;
                openingWidth = to - from;
            }

            Debug.Log($"[Blockout] Bolme {index} rampanin ustunden geciyor; rampa hizasinda " +
                      $"tavana kadar {openingWidth:F1} m aciklik acildi. Bu bolmenin " +
                      "darbogaz gorevi zayifladi - istemiyorsan rampayi veya bolmeyi kaydir.");

            float openMin = openingCenter - openingWidth / 2f;
            float openMax = openingCenter + openingWidth / 2f;
            float doorMin = designerGap.Center - designerGap.Width / 2f;
            float doorMax = designerGap.Center + designerGap.Width / 2f;

            // Kapi boslugu rampa acikligiyla cakisiyorsa TEK bir aciklikta birlestirilir.
            // Ayri birakilirsa kapinin lentosu (2.6 m ustu) rampanin tam gectigi
            // yukseklikte durur ve tikac aynen yerinde kalir - gozle gorunmeyen cinsten.
            if (doorMin < openMax && doorMax > openMin)
            {
                float min = Mathf.Min(doorMin, openMin);
                float max = Mathf.Max(doorMax, openMax);
                return new[] { new Gap((min + max) / 2f, max - min, 0f, s.WallHeight, false) };
            }

            var opening = new Gap(openingCenter, openingWidth, 0f, s.WallHeight, false);

            // Duvar parcalari soldan saga tek geciste kuruluyor; bosluklar sirali olmali,
            // yoksa aralarindaki parca ters uzunlukta cikar.
            return designerGap.Center <= opening.Center
                ? new[] { designerGap, opening }
                : new[] { opening, designerGap };
        }

        // ---------------------------------------------------------------- isaretler

        private static void BuildMarkers(Transform parent, BlockoutSettings s)
        {
            Transform markers = Group("Markers", parent);
            float midZ = (s.South + s.North) / 2f;
            float aMidX = (s.West + s.Divider) / 2f;

            Marker(markers, "PlayerSpawn", new Vector3(aMidX, 0.1f, midZ));

            // Pencere isaretleri GERCEK deliklerden turetiliyor. Ayri hesaplanirsa
            // ayak izi degisince birbirinden kayarlar - onceki surumdeki hata buydu.
            Transform windows = Group("Windows", markers);
            Transform spawns = Group("SpawnPoints", markers);

            for (int i = 0; i < Windows.Count; i++)
            {
                WindowRecord w = Windows[i];
                GameObject marker = Marker(windows, $"Window_{i:D2}", w.Position);

                // Isaretin forward'i binanin DISINI gosterir - WindowEntry'nin yon
                // sozlesmesi bu. Ic ve dis bekleme noktalari buradan turer, o yuzden
                // yonu burada kurmak zorundayiz: pencere kendi yonunu bilmeli.
                if (w.Outward.sqrMagnitude > 0.0001f)
                {
                    marker.transform.rotation = Quaternion.LookRotation(w.Outward);
                }

                // Zombi dogum noktalari pencerelerin DISINDA: zombinin gorunur sekilde
                // hiclikten belirmesi PILLAR-04'u cigner. Yalnizca zemin kat
                // pencereleri icin - ust kata disaridan tirmanmak yok.
                if (!w.GroundFloor) continue;

                // Zemin kat penceresi = zombi girisi (M1-04). Duvar kalinliginin
                // yarisindan buyuk bir standoff sart, yoksa bekleme noktasi duvarin
                // icinde kalir ve NavMesh orada yoktur.
                var entry = marker.AddComponent<Bunker.AI.WindowEntry>();
                entry.Configure(s.WindowSill, Mathf.Max(1f, s.WallThickness * 2f + 0.8f),
                                Mathf.Clamp(s.SpawnStandoffMeters, 2f, Mathf.Max(2f, s.ApronWidth - 2f)));

                // Isaret GERCEK dogum noktasinda durur. Onceki surumde sabit 3 m
                // ilerideydi ve yonetmen bambaska bir yer kullaniyordu - yani
                // sahnedeki isaret yalan soyluyordu.
                Marker(spawns, $"Spawn_{i:D2}", entry.SpawnPoint + Vector3.up * 0.1f);
            }

            Transform doors = Group("Doors", markers);

            // Kapinin KENDISI. Onceki surumde yalnizca bir isaret vardi ve duvarda
            // zaten bir bosluk duruyordu - yani "kapiyi satin aldim" dedigin an
            // gorunur hicbir sey degismiyordu, cunku kapanmis bir sey yoktu.
            GameObject dividerDoor = Marker(doors, "Door_A_to_B", new Vector3(s.Divider, 1.25f, midZ));
            BuildDoorLeaf(dividerDoor, s,
                new Vector3(s.Divider, s.DoorHeight / 2f, midZ),
                new Vector3(s.WallThickness * 1.5f, s.DoorHeight, s.DoorWidth));

            // Ust kat kapisi: merdiven boslugunun agzini TAMAMEN kapatir. Kanat
            // koridorun genisligi kadar genis ve ust kat dosemesine kadar yuksek -
            // kapi yuksekliginde biraksaydik ustunden gorunen bosluk kalirdi ve
            // "kapali" olan sey oyuncuya kapali gorunmezdi.
            float shaftWidth = s.RampHoleMaxX - s.RampHoleMinX;
            float shaftCenterX = (s.RampHoleMinX + s.RampHoleMaxX) / 2f;
            float doorZ = RampDoorZ(s);

            GameObject rampDoor = Marker(doors, "Door_To_Ramp",
                new Vector3(shaftCenterX, 1.25f, doorZ));
            BuildDoorLeaf(rampDoor, s,
                new Vector3(shaftCenterX, s.UpperFloorY / 2f, doorZ),
                new Vector3(shaftWidth, s.UpperFloorY, s.WallThickness * 1.5f));

            Transform buys = Group("Purchases", markers);

            // Isaretler DUVARA DONUK duruyor (2026-09-05). Onceki halde donmemis
            // bos nesnelerdi ve levhalari - ince yuzu +Z'ye bakan kutular - bati/dogu
            // duvarinin ICINE giriyordu: oyun testinde "tezgah ve mermi panosu
            // gorunmuyor" olarak okundu. Bir etkilesim noktasi gorunmuyorsa yoktur
            // (BUG-004'un dersi).
            var faceEast = Quaternion.Euler(0f, 90f, 0f);   // bati duvari -> odaya bakar
            var faceWest = Quaternion.Euler(0f, -90f, 0f);  // dogu duvari -> odaya bakar

            BuyMarker(buys, "WallBuy_A_Cheap",
                      new Vector3(s.West + 0.5f, 1.4f, s.South + 2f), faceEast);
            BuyMarker(buys, "WallBuy_B_Mid",
                      new Vector3(s.East - 0.5f, 1.4f, s.North - 2f), faceWest);

            // Tezgah (M-03): UST KATTA (2026-09-05, gelistirici karari).
            //
            // Baslangicta zemin kattaydi ve baslangic odasindan cikmadan ulasilabiliyordu -
            // yani yukseltme almak icin haritayi acmak gerekmiyordu. Ust kata tasinmasi
            // tezgahi kapinin ARKASINA koyar: once 1250 puanla ust kati ac, sonra
            // yukselt. Bu, kapinin fiyatina bir sebep verir ve tezgahi bir hedef yapar.
            //
            // Dagiticidan (MysteryBox) uzak bir duvarda: yan yana olsalardi hangi tusun
            // ne actigi karisirdi.
            // SILAH TEZGAHI: ust katta (2026-09-06, gelistirici karari).
            //
            // Onceki hal uc AYRI duvar noktasiydi (WallBuy_C_Rifle bunlardan biri) ve
            // her biri tek bir silah satiyordu. Gelistirici: "silah secimini yukarida
            // tezgah gibi ekle, oradan alalim; alt katta sadece mermi alinsin."
            //
            // Dogru ayrim: duvardaki nokta bir MUSLUK (mermin bitti, kostun, aldin,
            // dondun - dusunmedin), tezgah bir KARAR (dort secenek yan yana). Uc ayri
            // duvara dagilmis silah, karsilastirilamayan silahtir.
            // IKI TEZGAH YAN YANA (2026-09-06, gelistirici karari). Onceki hal onlari
            // haritanin iki ucuna koyuyordu ("yan yana olsalardi hangi tusun ne actigi
            // karisirdi") - ama oyun testi bunun tersini gosterdi: mola 20 saniye ve
            // iki tezgah arasinda kosmak, molanin yarisini yurumeye harciyordu.
            // Ikisi de MOLADA acilan harcama noktalari; ayni durak olmalari dogru.
            // Karisiklik riski, ipuclarinin farkli metin yazmasiyla cozuluyor.
            BuyMarker(buys, "Shop_Station",
                      new Vector3(s.West + 0.5f, s.UpperFloorY + 1.4f, s.North - 3f), faceEast);

            BuyMarker(buys, "Weapon_Station",
                      new Vector3(s.West + 0.5f, s.UpperFloorY + 1.4f, s.North - 5f), faceEast);
            Marker(buys, "MysteryBox", new Vector3(s.Divider + 2f, s.UpperFloorY + 0.5f, midZ));
        }

        /// <summary>
        /// Kapı kanadı: satın alınana kadar geçişi kapatan katı bir blok.
        ///
        /// <para><b>Navigasyonu <c>NavMeshObstacle</c> ile keser, bake ile değil.</b>
        /// Bake edilmiş bir NavMesh çalışma anında değişmez; kanat bake'e girseydi
        /// kapıyı satın almak geçidi <i>açmazdı</i> — kapı görünmez olur, zombiler yine
        /// geçemezdi. Oyma (carving) yapan bir engel ise kapatılınca NavMesh'i geri
        /// verir. Bu yüzden <c>ZombieSetup</c> bake'i kanatlar <b>kapalıyken</b>
        /// yapar.</para>
        /// </summary>
        private static void BuildDoorLeaf(GameObject parent, BlockoutSettings s,
                                          Vector3 center, Vector3 size)
        {
            GameObject leaf = Box(parent.transform, "Leaf", center, size);

            var obstacle = leaf.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;          // kutu zaten olcekli
            obstacle.center = Vector3.zero;
        }

        // ---------------------------------------------------------------- yardimcilar

        private readonly struct Gap
        {
            public readonly float Center, Width, Bottom, Top;
            public readonly bool IsWindow;

            public Gap(float center, float width, float bottom, float top, bool isWindow)
            {
                Center = center; Width = width; Bottom = bottom; Top = top; IsWindow = isWindow;
            }
        }

        /// <summary>
        /// Bir duvar açıklığına pencereleri <b>eşit aralıkla</b> dağıtır. Duvarı n eşit
        /// parçaya böler ve her parçanın ortasına bir pencere koyar — sonuç her zaman
        /// simetriktir ve bina büyüyünce dağılım bozulmaz.
        /// </summary>
        private static Gap[] EvenWindows(BlockoutSettings s, float min, float max, bool ground = true)
        {
            float length = max - min;
            if (length <= s.WindowWidth + 1f) return System.Array.Empty<Gap>();

            int count = s.WindowCountFor(length);
            var gaps = new Gap[count];
            float segment = length / count;

            for (int i = 0; i < count; i++)
            {
                float center = min + segment * (i + 0.5f);
                gaps[i] = new Gap(center, s.WindowWidth, s.WindowSill, s.WindowTop, true);
            }

            return gaps;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            return go;
        }

        private static void Slab(Transform parent, string name, BlockoutSettings s,
                                 float minX, float maxX, float minZ, float maxZ, float y)
        {
            if (maxX - minX <= 0.01f || maxZ - minZ <= 0.01f) return;

            Box(parent, name,
                new Vector3((minX + maxX) / 2f, y, (minZ + maxZ) / 2f),
                new Vector3(maxX - minX, s.FloorThickness, maxZ - minZ));
        }

        private static void WallAlongZ(Transform parent, string name, BlockoutSettings s,
                                       float x, float minZ, float maxZ, float baseY,
                                       Vector3 outward, Gap[] gaps)
        {
            BuildWall(parent, name, s, gaps, baseY, minZ, maxZ, outward,
                (a, y) => new Vector3(x, y, a),
                (len, h) => new Vector3(s.WallThickness, h, len));
        }

        private static void WallAlongX(Transform parent, string name, BlockoutSettings s,
                                       float z, float minX, float maxX, float baseY,
                                       Vector3 outward, Gap[] gaps)
        {
            BuildWall(parent, name, s, gaps, baseY, minX, maxX, outward,
                (a, y) => new Vector3(a, y, z),
                (len, h) => new Vector3(len, h, s.WallThickness));
        }

        private static void BuildWall(Transform parent, string name, BlockoutSettings s,
                                      Gap[] gaps, float baseY, float minAxis, float maxAxis,
                                      Vector3 outward,
                                      System.Func<float, float, Vector3> toCenter,
                                      System.Func<float, float, Vector3> toSize)
        {
            Transform group = Group(name, parent);
            float h = s.WallHeight;

            if (gaps == null || gaps.Length == 0)
            {
                Box(group, name + "_Solid",
                    toCenter((minAxis + maxAxis) / 2f, baseY + h / 2f),
                    toSize(maxAxis - minAxis, h));
                return;
            }

            float cursor = minAxis;
            int index = 0;

            foreach (Gap gap in gaps)
            {
                float gapMin = gap.Center - gap.Width / 2f;
                float gapMax = gap.Center + gap.Width / 2f;

                if (gapMin > cursor)
                {
                    Box(group, $"{name}_Seg{index++}",
                        toCenter((cursor + gapMin) / 2f, baseY + h / 2f),
                        toSize(gapMin - cursor, h));
                }

                if (gap.Bottom > 0.01f)
                {
                    Box(group, $"{name}_Sill{index}",
                        toCenter(gap.Center, baseY + gap.Bottom / 2f),
                        toSize(gap.Width, gap.Bottom));
                }

                float lintel = h - gap.Top;
                if (lintel > 0.01f)
                {
                    Box(group, $"{name}_Lintel{index}",
                        toCenter(gap.Center, baseY + gap.Top + lintel / 2f),
                        toSize(gap.Width, lintel));
                }

                if (gap.IsWindow)
                {
                    Vector3 pos = toCenter(gap.Center, baseY + (gap.Bottom + gap.Top) / 2f);
                    Windows.Add(new WindowRecord(pos, outward, baseY < 0.01f));
                }

                cursor = gapMax;
            }

            if (maxAxis > cursor)
            {
                Box(group, $"{name}_Seg{index}",
                    toCenter((cursor + maxAxis) / 2f, baseY + h / 2f),
                    toSize(maxAxis - cursor, h));
            }
        }

        private static GameObject Marker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }

        /// <summary>
        /// Satın alma işareti: dönük bir işaret <b>ve görünür bir levha</b>.
        ///
        /// <para><b>Levha burada üretiliyor, görünüm aracında değil</b> (2026-09-05).
        /// Önceden yalnızca <c>GreyboxLook</c> üretiyordu ve harita yeniden
        /// üretildiğinde levhalar kayboluyordu: sahnede tezgâh ve mermi noktası
        /// <i>hiçbir görsele sahip olmayan boş nesnelerdi</i>. Oyuncunun mermiyi
        /// nereden alacağını göremediği bir harita, o mekaniğin olmadığı bir haritadır.
        /// Görünüm aracı hâlâ levhanın <b>rengini</b> verir; varlığı artık haritanın
        /// kendi işi.</para>
        ///
        /// <para>Levha <b>çarpışmaz</b>: etkileşim tetikleyicisini <c>ZombieSetup</c>
        /// işaretin kendisine koyuyor, ikinci bir katı yüzey oyuncuyu duvara
        /// yapıştırırdı.</para>
        /// </summary>
        private static GameObject BuyMarker(Transform parent, string name,
                                            Vector3 position, Quaternion rotation)
        {
            GameObject go = Marker(parent, name, position);
            go.transform.rotation = rotation;

            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            plate.transform.SetParent(go.transform, false);
            plate.transform.localPosition = Vector3.zero;
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = new Vector3(1.1f, 0.7f, 0.12f);

            Object.DestroyImmediate(plate.GetComponent<Collider>());

            return go;
        }
    }

    /// <summary>
    /// <see cref="BlockoutSettings"/> için Inspector: ölçülerin altına canlı hesaplar,
    /// denetim uyarıları ve Üret / Sil düğmeleri ekler.
    /// </summary>
    [CustomEditor(typeof(BlockoutSettings))]
    public sealed class BlockoutSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var s = (BlockoutSettings)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hesaplanan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Ayak izi",
                $"{s.FootprintWidth:F0} x {s.FootprintDepth:F0} m  (tavan {s.WallHeight:F1} m)");
            EditorGUILayout.LabelField("A bolgesi",
                $"{s.Divider - s.West:F0} x {s.FootprintDepth:F0} m");
            EditorGUILayout.LabelField("B bolgesi",
                $"{s.East - s.Divider:F0} x {s.FootprintDepth:F0} m");
            EditorGUILayout.LabelField("Rampa egimi", $"{s.RampAngleDegrees:F1} derece");
            EditorGUILayout.LabelField("Rampa kafa payi", $"{s.RampHeadroom:F2} m");
            EditorGUILayout.LabelField("Ic bolme", $"{(s.Partitions?.Length ?? 0)} adet");

            var problems = BlockoutGenerator.Validate(s);
            if (problems.Count > 0)
            {
                EditorGUILayout.Space(6);
                foreach (string problem in problems)
                {
                    EditorGUILayout.HelpBox(problem, MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            if (TintedButton("HARITAYI URET", Color.green))
            {
                BlockoutGenerator.Generate(s);
            }

            if (TintedButton("Haritayi Sil", Color.white))
            {
                BlockoutGenerator.Clear();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Uretimden sonra koke 'NavMesh Surface' ekleyip Bake'e basmayi unutma. " +
                "Yeniden uretim eski objeyi sildigi icin bilesen de gider.",
                MessageType.Info);
        }

        private static bool TintedButton(string label, Color tint)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool clicked = GUILayout.Button(label, GUILayout.Height(30));
            GUI.backgroundColor = previous;
            return clicked;
        }
    }
}
