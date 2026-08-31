using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// LVL-01 gri kutu haritasını üretir (`design/levels/LVL-01-greybox.md`).
    ///
    /// <para><b>Neden bir araç:</b> otuz küpü elle ölçeklemek mekanik, yavaş ve hataya
    /// açık bir iş. Üreteç deterministik: aynı girdiden aynı harita çıkar, sayıları
    /// değiştirip yeniden üretmek saniyeler sürer, ve sonuç diff'lenebilir.</para>
    ///
    /// <para><b>Idempotent:</b> iki kez çalıştırmak bir kez çalıştırmakla aynı sonucu
    /// verir — mevcut kök silinir ve yeniden kurulur (editor-tools.md). Tamamı tek bir
    /// Undo adımıdır.</para>
    ///
    /// <para>Üretilen şey bir <b>başlangıç noktasıdır</b>, son hâli değil. Oda oranlarını,
    /// darboğazları ve pencere yerlerini oynayarak ayarlamak level design işidir ve
    /// elle yapılır.</para>
    /// </summary>
    public static class BlockoutGenerator
    {
        private const string RootName = "LVL-01_Blockout";

        // --- olculer (LVL-01 spec'i) ---
        private const float WallThickness = 0.3f;
        private const float WallHeight = 3f;
        private const float FloorThickness = 0.5f;
        private const float UpperFloorY = 3.5f;

        private const float DoorWidth = 1.6f;
        private const float DoorHeight = 2.5f;
        private const float WindowWidth = 1.5f;
        private const float WindowSill = 1f;
        private const float WindowTop = 2.2f;

        // --- ayak izi ---
        private const float West = -6f;
        private const float East = 14f;
        private const float South = -5f;
        private const float North = 5f;
        private const float Divider = 4f;   // A ile B arasindaki ic duvar

        private const float RampX = 12f;    // rampanin merkez ekseni
        private const float RampWidth = 2.5f;

        [MenuItem("Bunker/Level/LVL-01 Gri Kutu Uret", false, 100)]
        public static void Generate()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "LVL-01 Gri Kutu Uret");

            BuildZoneA(root.transform);
            BuildZoneB(root.transform);
            BuildZoneC(root.transform);
            BuildMarkers(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log(
                "[Blockout] LVL-01 uretildi.\n" +
                "  SIRADAKI ADIM: koke bir 'NavMesh Surface' bileseni ekleyip Bake'e bas.\n" +
                "  Sonra dongunun kapali oldugunu dogrula: mavi katman A -> B -> rampa ->\n" +
                "  C -> delik -> A boyunca kesintisiz gitmeli.");
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

        // ---------------------------------------------------------------- bolgeler

        private static void BuildZoneA(Transform parent)
        {
            Transform zone = Group("Zone_A", parent);

            Box(zone, "Floor_A",
                center: new Vector3((West + Divider) / 2f, -FloorThickness / 2f, 0f),
                size: new Vector3(Divider - West, FloorThickness, North - South));

            // Bati dis duvari: iki pencere
            WallAlongZ(zone, "Wall_A_West", West, South, North,
                gaps: new[] { new Gap(-2.5f, WindowWidth, WindowSill, WindowTop),
                              new Gap(2.5f, WindowWidth, WindowSill, WindowTop) });

            // Guney duvari: bir pencere
            WallAlongX(zone, "Wall_A_South", South, West, Divider,
                gaps: new[] { new Gap(-1f, WindowWidth, WindowSill, WindowTop) });

            // Kuzey duvari: bir pencere
            WallAlongX(zone, "Wall_A_North", North, West, Divider,
                gaps: new[] { new Gap(-1f, WindowWidth, WindowSill, WindowTop) });

            // Ic duvar (A|B): kapi bosluğu
            WallAlongZ(zone, "Wall_A_Divider", Divider, South, North,
                gaps: new[] { new Gap(0f, DoorWidth, 0f, DoorHeight) });
        }

        private static void BuildZoneB(Transform parent)
        {
            Transform zone = Group("Zone_B", parent);

            Box(zone, "Floor_B",
                center: new Vector3((Divider + East) / 2f, -FloorThickness / 2f, 0f),
                size: new Vector3(East - Divider, FloorThickness, North - South));

            // Dogu dis duvari: iki pencere
            WallAlongZ(zone, "Wall_B_East", East, South, North,
                gaps: new[] { new Gap(-2.5f, WindowWidth, WindowSill, WindowTop),
                              new Gap(2.5f, WindowWidth, WindowSill, WindowTop) });

            // Guney duvari: bir pencere. Rampa kuzeye dogru ciktigi icin guney bos.
            WallAlongX(zone, "Wall_B_South", South, Divider, East,
                gaps: new[] { new Gap(7f, WindowWidth, WindowSill, WindowTop) });

            WallAlongX(zone, "Wall_B_North", North, Divider, East, gaps: null);

            // Rampa: B icinde, guneyden kuzeye yukselir. 8 m kosu / 3.5 m yukselti
            // = ~24 derece. NavMesh varsayilan 45 derece sinirinin altinda.
            BuildRamp(zone);
        }

        private static void BuildRamp(Transform zone)
        {
            float startZ = South + 0.5f;
            float endZ = North - 0.5f;
            float run = endZ - startZ;
            float rise = UpperFloorY;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ramp_B_to_C";
            go.transform.SetParent(zone, false);

            float length = Mathf.Sqrt(run * run + rise * rise);
            go.transform.position = new Vector3(RampX, rise / 2f, (startZ + endZ) / 2f);
            go.transform.rotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0f, 0f);
            go.transform.localScale = new Vector3(RampWidth, 0.3f, length);

            Undo.RegisterCreatedObjectUndo(go, "Blockout");
        }

        private static void BuildZoneC(Transform parent)
        {
            Transform zone = Group("Zone_C", parent);

            // Ust kat zemini, A'nin uzerinde bir DELIK birakarak. Delik dongunun
            // kapanma noktasi: oyuncu C'den A'ya asagi atlar.
            const float holeMinX = -4f, holeMaxX = -1f;
            const float holeMinZ = -2f, holeMaxZ = 1f;
            float slabY = UpperFloorY - FloorThickness / 2f;

            // Delik cevresinde dort parca
            Slab(zone, "Floor_C_West", West, holeMinX, South, North, slabY);
            Slab(zone, "Floor_C_East", holeMaxX, East, South, North, slabY);
            Slab(zone, "Floor_C_SouthStrip", holeMinX, holeMaxX, South, holeMinZ, slabY);
            Slab(zone, "Floor_C_NorthStrip", holeMinX, holeMaxX, holeMaxZ, North, slabY);

            // Ust kat dis duvarlari: uc pencere
            WallAlongZ(zone, "Wall_C_West", West, South, North, UpperFloorY,
                gaps: new[] { new Gap(0f, WindowWidth, WindowSill, WindowTop) });

            WallAlongZ(zone, "Wall_C_East", East, South, North, UpperFloorY,
                gaps: new[] { new Gap(0f, WindowWidth, WindowSill, WindowTop) });

            WallAlongX(zone, "Wall_C_North", North, West, East, UpperFloorY,
                gaps: new[] { new Gap(-6f, WindowWidth, WindowSill, WindowTop) });

            WallAlongX(zone, "Wall_C_South", South, West, East, UpperFloorY, gaps: null);

            // Delik kenarina alcak korkuluk: oyuncunun kazara dusmesini engeller,
            // atlamak bilincli bir hareket olsun.
            Box(zone, "HoleLip_West",
                new Vector3(holeMinX, UpperFloorY + 0.25f, (holeMinZ + holeMaxZ) / 2f),
                new Vector3(0.2f, 0.5f, holeMaxZ - holeMinZ));
            Box(zone, "HoleLip_East",
                new Vector3(holeMaxX, UpperFloorY + 0.25f, (holeMinZ + holeMaxZ) / 2f),
                new Vector3(0.2f, 0.5f, holeMaxZ - holeMinZ));
        }

        // ---------------------------------------------------------------- isaretler

        private static void BuildMarkers(Transform parent)
        {
            Transform markers = Group("Markers", parent);

            Marker(markers, "PlayerSpawn", new Vector3(-1f, 0.1f, 0f));

            Transform windows = Group("Windows", markers);
            Marker(windows, "Window_A1", new Vector3(West, 1.6f, -2.5f));
            Marker(windows, "Window_A2", new Vector3(West, 1.6f, 2.5f));
            Marker(windows, "Window_A3", new Vector3(-1f, 1.6f, South));
            Marker(windows, "Window_A4", new Vector3(-1f, 1.6f, North));
            Marker(windows, "Window_B1", new Vector3(East, 1.6f, -2.5f));
            Marker(windows, "Window_B2", new Vector3(East, 1.6f, 2.5f));
            Marker(windows, "Window_B3", new Vector3(7f, 1.6f, South));
            Marker(windows, "Window_C1", new Vector3(West, UpperFloorY + 1.6f, 0f));
            Marker(windows, "Window_C2", new Vector3(East, UpperFloorY + 1.6f, 0f));
            Marker(windows, "Window_C3", new Vector3(-6f, UpperFloorY + 1.6f, North));

            Transform doors = Group("Doors", markers);
            Marker(doors, "Door_A_to_B", new Vector3(Divider, 1.25f, 0f));
            Marker(doors, "Door_B_to_Ramp", new Vector3(RampX, 1.25f, South + 1f));

            Transform buys = Group("Purchases", markers);
            Marker(buys, "WallBuy_A_Cheap", new Vector3(-5.5f, 1.4f, -4f));
            Marker(buys, "WallBuy_B_Mid", new Vector3(13.5f, 1.4f, 3f));
            Marker(buys, "MysteryBox", new Vector3(8f, UpperFloorY + 0.5f, 0f));

            // Zombi dogum noktalari pencerelerin DISINDA: zombinin gorunur sekilde
            // hiclikten belirmesi PILLAR-04'u cigner.
            Transform spawns = Group("SpawnPoints", markers);
            Marker(spawns, "Spawn_W1", new Vector3(West - 3f, 0.1f, -2.5f));
            Marker(spawns, "Spawn_W2", new Vector3(West - 3f, 0.1f, 2.5f));
            Marker(spawns, "Spawn_S1", new Vector3(-1f, 0.1f, South - 3f));
            Marker(spawns, "Spawn_S2", new Vector3(7f, 0.1f, South - 3f));
            Marker(spawns, "Spawn_N1", new Vector3(-1f, 0.1f, North + 3f));
            Marker(spawns, "Spawn_E1", new Vector3(East + 3f, 0.1f, -2.5f));
            Marker(spawns, "Spawn_E2", new Vector3(East + 3f, 0.1f, 2.5f));
        }

        // ---------------------------------------------------------------- yardimcilar

        private readonly struct Gap
        {
            public readonly float Center;
            public readonly float Width;
            public readonly float Bottom;
            public readonly float Top;

            public Gap(float center, float width, float bottom, float top)
            {
                Center = center; Width = width; Bottom = bottom; Top = top;
            }
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

        private static void Slab(Transform parent, string name,
                                 float minX, float maxX, float minZ, float maxZ, float y)
        {
            if (maxX - minX <= 0.01f || maxZ - minZ <= 0.01f) return;

            Box(parent, name,
                new Vector3((minX + maxX) / 2f, y, (minZ + maxZ) / 2f),
                new Vector3(maxX - minX, FloorThickness, maxZ - minZ));
        }

        /// <summary>Z ekseni boyunca uzanan duvar (sabit X), boşluklarıyla.</summary>
        private static void WallAlongZ(Transform parent, string name,
                                       float x, float minZ, float maxZ,
                                       float baseY = 0f, Gap[] gaps = null)
        {
            BuildWall(parent, name, gaps, baseY,
                minAxis: minZ, maxAxis: maxZ,
                toCenter: (a, y, h) => new Vector3(x, y, a),
                toSize: (len, h) => new Vector3(WallThickness, h, len));
        }

        /// <summary>X ekseni boyunca uzanan duvar (sabit Z), boşluklarıyla.</summary>
        private static void WallAlongX(Transform parent, string name,
                                       float z, float minX, float maxX,
                                       float baseY = 0f, Gap[] gaps = null)
        {
            BuildWall(parent, name, gaps, baseY,
                minAxis: minX, maxAxis: maxX,
                toCenter: (a, y, h) => new Vector3(a, y, z),
                toSize: (len, h) => new Vector3(len, h, WallThickness));
        }

        private static void BuildWall(Transform parent, string name, Gap[] gaps, float baseY,
                                      float minAxis, float maxAxis,
                                      System.Func<float, float, float, Vector3> toCenter,
                                      System.Func<float, float, Vector3> toSize)
        {
            Transform group = Group(name, parent);

            if (gaps == null || gaps.Length == 0)
            {
                float len = maxAxis - minAxis;
                Box(group, name + "_Solid",
                    toCenter((minAxis + maxAxis) / 2f, baseY + WallHeight / 2f, WallHeight),
                    toSize(len, WallHeight));
                return;
            }

            // Bosluklar arasindaki dolu parcalar
            float cursor = minAxis;
            int index = 0;

            foreach (Gap gap in gaps)
            {
                float gapMin = gap.Center - gap.Width / 2f;
                float gapMax = gap.Center + gap.Width / 2f;

                if (gapMin > cursor)
                {
                    Box(group, $"{name}_Seg{index++}",
                        toCenter((cursor + gapMin) / 2f, baseY + WallHeight / 2f, WallHeight),
                        toSize(gapMin - cursor, WallHeight));
                }

                // Bosluğun alti (pencere esigi)
                if (gap.Bottom > 0.01f)
                {
                    Box(group, $"{name}_Sill{index}",
                        toCenter(gap.Center, baseY + gap.Bottom / 2f, gap.Bottom),
                        toSize(gap.Width, gap.Bottom));
                }

                // Bosluğun ustu (lento)
                float lintelHeight = WallHeight - gap.Top;
                if (lintelHeight > 0.01f)
                {
                    Box(group, $"{name}_Lintel{index}",
                        toCenter(gap.Center, baseY + gap.Top + lintelHeight / 2f, lintelHeight),
                        toSize(gap.Width, lintelHeight));
                }

                cursor = gapMax;
            }

            if (maxAxis > cursor)
            {
                Box(group, $"{name}_Seg{index}",
                    toCenter((cursor + maxAxis) / 2f, baseY + WallHeight / 2f, WallHeight),
                    toSize(maxAxis - cursor, WallHeight));
            }
        }

        private static void Marker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
        }
    }
}
