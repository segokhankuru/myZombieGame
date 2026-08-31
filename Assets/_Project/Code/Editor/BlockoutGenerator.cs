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

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log(
                $"[Blockout] LVL-01 uretildi.\n" +
                $"  ayak izi    : {s.FootprintWidth:F0} x {s.FootprintDepth:F0} m, tavan {s.WallHeight:F1} m\n" +
                $"  rampa       : {s.RampAngleDegrees:F1} derece, kafa payi {s.RampHeadroom:F2} m\n" +
                $"  pencere     : {Windows.Count} adet\n" +
                $"  ic bolme    : {(s.Partitions?.Length ?? 0)} adet\n" +
                $"  SIRADAKI ADIM: koke 'NavMesh Surface' ekleyip Bake'e bas.");
        }

        private static void BuildGroundFloor(Transform parent, BlockoutSettings s)
        {
            Transform zone = Group("GroundFloor", parent);

            Slab(zone, "Floor_Ground", s, s.West, s.East, s.South, s.North, -s.FloorThickness / 2f);

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

                if (p.Axis == PartitionAxis.AlongZ)
                {
                    WallAlongZ(zone, $"Partition_{i}", s, p.Position, p.From, p.To, 0f,
                        Vector3.zero, new[] { gap });
                }
                else
                {
                    WallAlongX(zone, $"Partition_{i}", s, p.Position, p.From, p.To, 0f,
                        Vector3.zero, new[] { gap });
                }
            }
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
                Marker(windows, $"Window_{i:D2}", w.Position);

                // Zombi dogum noktalari pencerelerin DISINDA: zombinin gorunur sekilde
                // hiclikten belirmesi PILLAR-04'u cigner. Yalnizca zemin kat
                // pencereleri icin - ust kata disaridan tirmanmak yok.
                if (!w.GroundFloor) continue;

                Vector3 outside = w.Position + w.Outward * 3f;
                outside.y = 0.1f;
                Marker(spawns, $"Spawn_{i:D2}", outside);
            }

            Transform doors = Group("Doors", markers);
            Marker(doors, "Door_A_to_B", new Vector3(s.Divider, 1.25f, midZ));
            Marker(doors, "Door_To_Ramp", new Vector3(s.RampX, 1.25f, s.RampStartZ - 0.5f));

            Transform buys = Group("Purchases", markers);
            Marker(buys, "WallBuy_A_Cheap", new Vector3(s.West + 0.5f, 1.4f, s.South + 2f));
            Marker(buys, "WallBuy_B_Mid", new Vector3(s.East - 0.5f, 1.4f, s.North - 2f));
            Marker(buys, "MysteryBox", new Vector3(s.Divider + 2f, s.UpperFloorY + 0.5f, midZ));
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

        private static void Marker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
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
