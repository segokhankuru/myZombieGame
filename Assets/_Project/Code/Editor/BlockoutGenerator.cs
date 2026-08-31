using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// LVL-01 gri kutu haritasını <see cref="BlockoutSettings"/> içindeki ölçülerden üretir
    /// (`design/levels/LVL-01-greybox.md`).
    ///
    /// <para><b>Idempotent:</b> iki kez çalıştırmak bir kez çalıştırmakla aynı sonucu verir
    /// — mevcut kök silinip yeniden kurulur (editor-tools.md). Tamamı tek Undo adımıdır.</para>
    ///
    /// <para>Üretilen şey bir <b>başlangıç noktasıdır</b>. Ölçüleri ayar varlığından
    /// oynatıp yeniden üretmek saniyeler sürer; asıl level design o döngüde yapılır.</para>
    /// </summary>
    public static class BlockoutGenerator
    {
        private const string RootName = "LVL-01_Blockout";
        private const string SettingsPath = "Assets/_Project/Settings/BlockoutSettings.asset";

        [MenuItem("Bunker/Level/Ayarlari Ac", false, 90)]
        public static void OpenSettings()
        {
            Selection.activeObject = LoadOrCreateSettings();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Bunker/Level/LVL-01 Gri Kutu Uret", false, 100)]
        public static void GenerateFromMenu()
        {
            Generate(LoadOrCreateSettings());
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

        /// <summary>
        /// Ayarları denetler. Sorun varsa <b>ne yapılacağını söyleyen</b> mesajlar döner —
        /// "gecersiz deger" demek bir hata mesaji degildir (editor-tools.md).
        /// </summary>
        public static List<string> Validate(BlockoutSettings s)
        {
            var problems = new List<string>();

            if (s.East <= s.West)
                problems.Add("East, West'ten buyuk olmali.");
            if (s.North <= s.South)
                problems.Add("North, South'tan buyuk olmali.");
            if (s.Divider <= s.West || s.Divider >= s.East)
                problems.Add("Divider, West ile East arasinda olmali (A|B ic duvari).");

            if (s.RampRun <= 0.1f)
                problems.Add("RampEndZ, RampStartZ'den buyuk olmali.");
            else if (s.RampAngleDegrees > 45f)
                problems.Add($"Rampa egimi {s.RampAngleDegrees:F0} derece. NavMesh varsayilani " +
                             $"45 derecede kesiliyor; zombiler cikamaz. Cozum: RampEndZ'yi " +
                             $"buyut (rampayi uzat) ya da UpperFloorY'yi kucult.");
            else if (s.RampAngleDegrees > 35f)
                problems.Add($"Rampa egimi {s.RampAngleDegrees:F0} derece - bake gecer ama dik " +
                             $"hissettirir. 25-30 derece daha rahat.");

            if (s.RampHeadroom < 2f)
                problems.Add($"Rampada kafa payi {s.RampHeadroom:F2} m. Oyuncu 1.8 m; " +
                             $"zemine kafa atar. Cozum: RampHoleStartZ'yi kucult (acikligi " +
                             $"one cek) ya da UpperFloorY'yi buyut.");

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

            return problems;
        }

        public static void Generate(BlockoutSettings s)
        {
            List<string> problems = Validate(s);
            foreach (string problem in problems)
            {
                Debug.LogWarning($"[Blockout] {problem}");
            }

            GameObject existing = GameObject.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "LVL-01 Gri Kutu Uret");

            BuildZoneA(root.transform, s);
            BuildZoneB(root.transform, s);
            BuildZoneC(root.transform, s);
            BuildMarkers(root.transform, s);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            string verdict = problems.Count == 0
                ? "Ayarlar tutarli."
                : $"{problems.Count} uyari var - yukariya bak.";

            Debug.Log(
                $"[Blockout] LVL-01 uretildi. {verdict}\n" +
                $"  rampa egimi : {s.RampAngleDegrees:F1} derece\n" +
                $"  kafa payi   : {s.RampHeadroom:F2} m\n" +
                $"  SIRADAKI ADIM: koke 'NavMesh Surface' ekleyip Bake'e bas.");
        }

        // ---------------------------------------------------------------- bolgeler

        private static void BuildZoneA(Transform parent, BlockoutSettings s)
        {
            Transform zone = Group("Zone_A", parent);
            float midZ = (s.South + s.North) / 2f;

            Slab(zone, "Floor_A", s, s.West, s.Divider, s.South, s.North, -s.FloorThickness / 2f);

            WallAlongZ(zone, "Wall_A_West", s, s.West, s.South, s.North, 0f,
                Window(s, midZ - 2.5f), Window(s, midZ + 2.5f));

            WallAlongX(zone, "Wall_A_South", s, s.South, s.West, s.Divider, 0f,
                Window(s, (s.West + s.Divider) / 2f));

            WallAlongX(zone, "Wall_A_North", s, s.North, s.West, s.Divider, 0f,
                Window(s, (s.West + s.Divider) / 2f));

            // A|B ic duvari: kapi bosluğu
            WallAlongZ(zone, "Wall_A_Divider", s, s.Divider, s.South, s.North, 0f,
                new Gap(midZ, s.DoorWidth, 0f, s.DoorHeight));
        }

        private static void BuildZoneB(Transform parent, BlockoutSettings s)
        {
            Transform zone = Group("Zone_B", parent);
            float midZ = (s.South + s.North) / 2f;

            Slab(zone, "Floor_B", s, s.Divider, s.East, s.South, s.North, -s.FloorThickness / 2f);

            WallAlongZ(zone, "Wall_B_East", s, s.East, s.South, s.North, 0f,
                Window(s, midZ - 2.5f), Window(s, midZ + 2.5f));

            WallAlongX(zone, "Wall_B_South", s, s.South, s.Divider, s.East, 0f,
                Window(s, s.Divider + 3f));

            WallAlongX(zone, "Wall_B_North", s, s.North, s.Divider, s.East, 0f);

            BuildRamp(zone, s);
        }

        private static void BuildRamp(Transform zone, BlockoutSettings s)
        {
            float run = s.RampRun;
            float rise = s.UpperFloorY;
            if (run <= 0.1f) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ramp_B_to_C";
            go.transform.SetParent(zone, false);

            float length = Mathf.Sqrt(run * run + rise * rise);
            go.transform.position = new Vector3(s.RampX, rise / 2f, (s.RampStartZ + s.RampEndZ) / 2f);
            go.transform.rotation = Quaternion.Euler(-s.RampAngleDegrees, 0f, 0f);
            go.transform.localScale = new Vector3(s.RampWidth, 0.3f, length);
        }

        private static void BuildZoneC(Transform parent, BlockoutSettings s)
        {
            Transform zone = Group("Zone_C", parent);
            float slabY = s.UpperFloorY - s.FloorThickness / 2f;
            float midZ = (s.South + s.North) / 2f;

            // Ust kat zemini IKI aciklik birakir:
            //   1) DUSME DELIGI (A'nin uzerinde) -- dongunun kapanma noktasi
            //   2) RAMPA AGZI   (B'nin uzerinde) -- rampanin yuzeye ciktigi yer
            // Zemin X bantlarina bolunerek kuruluyor.
            Slab(zone, "Floor_C_Band1", s, s.West, s.DropHoleMinX, s.South, s.North, slabY);
            Slab(zone, "Floor_C_Band2_S", s, s.DropHoleMinX, s.DropHoleMaxX, s.South, s.DropHoleMinZ, slabY);
            Slab(zone, "Floor_C_Band2_N", s, s.DropHoleMinX, s.DropHoleMaxX, s.DropHoleMaxZ, s.North, slabY);
            Slab(zone, "Floor_C_Band3", s, s.DropHoleMaxX, s.RampHoleMinX, s.South, s.North, slabY);
            Slab(zone, "Floor_C_Band4_S", s, s.RampHoleMinX, s.RampHoleMaxX, s.South, s.RampHoleStartZ, slabY);
            Slab(zone, "Floor_C_Band4_N", s, s.RampHoleMinX, s.RampHoleMaxX, s.RampHoleMaxZ, s.North, slabY);
            Slab(zone, "Floor_C_Band5", s, s.RampHoleMaxX, s.East, s.South, s.North, slabY);

            WallAlongZ(zone, "Wall_C_West", s, s.West, s.South, s.North, s.UpperFloorY,
                Window(s, midZ));
            WallAlongZ(zone, "Wall_C_East", s, s.East, s.South, s.North, s.UpperFloorY,
                Window(s, midZ));
            WallAlongX(zone, "Wall_C_North", s, s.North, s.West, s.East, s.UpperFloorY,
                Window(s, s.West + 2f));
            WallAlongX(zone, "Wall_C_South", s, s.South, s.West, s.East, s.UpperFloorY);

            if (!s.DropHoleLips) return;

            float dropMidZ = (s.DropHoleMinZ + s.DropHoleMaxZ) / 2f;
            float dropLength = s.DropHoleMaxZ - s.DropHoleMinZ;

            Box(zone, "DropLip_West",
                new Vector3(s.DropHoleMinX, s.UpperFloorY + 0.25f, dropMidZ),
                new Vector3(0.2f, 0.5f, dropLength));
            Box(zone, "DropLip_East",
                new Vector3(s.DropHoleMaxX, s.UpperFloorY + 0.25f, dropMidZ),
                new Vector3(0.2f, 0.5f, dropLength));
        }

        // ---------------------------------------------------------------- isaretler

        private static void BuildMarkers(Transform parent, BlockoutSettings s)
        {
            Transform markers = Group("Markers", parent);
            float midZ = (s.South + s.North) / 2f;
            float aMidX = (s.West + s.Divider) / 2f;
            float sill = s.WindowSill + 0.6f;

            Marker(markers, "PlayerSpawn", new Vector3(aMidX, 0.1f, midZ));

            Transform windows = Group("Windows", markers);
            Marker(windows, "Window_A1", new Vector3(s.West, sill, midZ - 2.5f));
            Marker(windows, "Window_A2", new Vector3(s.West, sill, midZ + 2.5f));
            Marker(windows, "Window_A3", new Vector3(aMidX, sill, s.South));
            Marker(windows, "Window_A4", new Vector3(aMidX, sill, s.North));
            Marker(windows, "Window_B1", new Vector3(s.East, sill, midZ - 2.5f));
            Marker(windows, "Window_B2", new Vector3(s.East, sill, midZ + 2.5f));
            Marker(windows, "Window_B3", new Vector3(s.Divider + 3f, sill, s.South));
            Marker(windows, "Window_C1", new Vector3(s.West, s.UpperFloorY + sill, midZ));
            Marker(windows, "Window_C2", new Vector3(s.East, s.UpperFloorY + sill, midZ));
            Marker(windows, "Window_C3", new Vector3(s.West + 2f, s.UpperFloorY + sill, s.North));

            Transform doors = Group("Doors", markers);
            Marker(doors, "Door_A_to_B", new Vector3(s.Divider, 1.25f, midZ));
            Marker(doors, "Door_B_to_Ramp", new Vector3(s.RampX, 1.25f, s.RampStartZ - 0.5f));

            Transform buys = Group("Purchases", markers);
            Marker(buys, "WallBuy_A_Cheap", new Vector3(s.West + 0.5f, 1.4f, s.South + 1f));
            Marker(buys, "WallBuy_B_Mid", new Vector3(s.East - 0.5f, 1.4f, s.North - 2f));
            Marker(buys, "MysteryBox", new Vector3(s.Divider + 2f, s.UpperFloorY + 0.5f, midZ));

            // Zombi dogum noktalari pencerelerin DISINDA: zombinin gorunur sekilde
            // hiclikten belirmesi PILLAR-04'u cigner.
            Transform spawns = Group("SpawnPoints", markers);
            Marker(spawns, "Spawn_W1", new Vector3(s.West - 3f, 0.1f, midZ - 2.5f));
            Marker(spawns, "Spawn_W2", new Vector3(s.West - 3f, 0.1f, midZ + 2.5f));
            Marker(spawns, "Spawn_S1", new Vector3(aMidX, 0.1f, s.South - 3f));
            Marker(spawns, "Spawn_S2", new Vector3(s.Divider + 3f, 0.1f, s.South - 3f));
            Marker(spawns, "Spawn_N1", new Vector3(aMidX, 0.1f, s.North + 3f));
            Marker(spawns, "Spawn_E1", new Vector3(s.East + 3f, 0.1f, midZ - 2.5f));
            Marker(spawns, "Spawn_E2", new Vector3(s.East + 3f, 0.1f, midZ + 2.5f));
        }

        // ---------------------------------------------------------------- yardimcilar

        private readonly struct Gap
        {
            public readonly float Center, Width, Bottom, Top;

            public Gap(float center, float width, float bottom, float top)
            {
                Center = center; Width = width; Bottom = bottom; Top = top;
            }
        }

        private static Gap Window(BlockoutSettings s, float center)
            => new Gap(center, s.WindowWidth, s.WindowSill, s.WindowTop);

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
                                       params Gap[] gaps)
        {
            BuildWall(parent, name, s, gaps, baseY, minZ, maxZ,
                (a, y) => new Vector3(x, y, a),
                (len, h) => new Vector3(s.WallThickness, h, len));
        }

        private static void WallAlongX(Transform parent, string name, BlockoutSettings s,
                                       float z, float minX, float maxX, float baseY,
                                       params Gap[] gaps)
        {
            BuildWall(parent, name, s, gaps, baseY, minX, maxX,
                (a, y) => new Vector3(a, y, z),
                (len, h) => new Vector3(len, h, s.WallThickness));
        }

        private static void BuildWall(Transform parent, string name, BlockoutSettings s,
                                      Gap[] gaps, float baseY, float minAxis, float maxAxis,
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
    /// <see cref="BlockoutSettings"/> için Inspector: ölçülerin altına Üret / Sil
    /// düğmeleri ve canlı denetim ekler. Amaç, seviye düzenlemeyi kod düzenlemekten
    /// çıkarıp "sayıyı oynat, düğmeye bas, koş" döngüsüne indirmek.
    /// </summary>
    [CustomEditor(typeof(BlockoutSettings))]
    public sealed class BlockoutSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var settings = (BlockoutSettings)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hesaplanan", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Rampa egimi", $"{settings.RampAngleDegrees:F1} derece");
            EditorGUILayout.LabelField("Rampa kafa payi", $"{settings.RampHeadroom:F2} m");
            EditorGUILayout.LabelField("A odasi", $"{settings.Divider - settings.West:F1} x " +
                                                  $"{settings.North - settings.South:F1} m");
            EditorGUILayout.LabelField("B odasi", $"{settings.East - settings.Divider:F1} x " +
                                                 $"{settings.North - settings.South:F1} m");

            var problems = BlockoutGenerator.Validate(settings);
            if (problems.Count > 0)
            {
                EditorGUILayout.Space(6);
                foreach (string problem in problems)
                {
                    EditorGUILayout.HelpBox(problem, MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            if (GILButton("HARITAYI URET", Color.green))
            {
                BlockoutGenerator.Generate(settings);
            }

            if (GILButton("Haritayi Sil", Color.white))
            {
                BlockoutGenerator.Clear();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Uretimden sonra koke 'NavMesh Surface' ekleyip Bake'e basmayi unutma. " +
                "Yeniden uretim eski objeyi sildigi icin bilesen de gider.",
                MessageType.Info);
        }

        private static bool GILButton(string label, Color tint)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool clicked = GUILayout.Button(label, GUILayout.Height(30));
            GUI.backgroundColor = previous;
            return clicked;
        }
    }
}
