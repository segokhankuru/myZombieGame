using System.Collections.Generic;
using Bunker.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Bunker.Editor
{
    /// <summary>
    /// Gri kutuya okunabilirlik ve atmosfer verir: renk dili, ışık, gökyüzü, sis ve
    /// post-processing.
    ///
    /// <para><b>Bu bir sanat geçişi değildir.</b> Gerçek görsel dil
    /// <c>/art-direction</c>'ın işi ve stil kilidi henüz yok. Buradaki palet bir
    /// <i>okunabilirlik aracı</i>: PILLAR-04 "okunabilirlik bir cila işi değil, bir
    /// tasarım kısıtıdır" diyor ve reddettikleri arasında <b>"sanat gelince okunur hale
    /// gelir" gerekçesiyle ertelenen kararlar</b> var. Kapıyı duvardan ayırt edemiyorsan
    /// bu bir asset eksikliği değil, açık bir tasarım borcudur.</para>
    ///
    /// <para><b>Bilgi renkle tek başına taşınmıyor.</b> Her rolün rengi <i>ve</i> parlaklık
    /// değeri farklı; ekran gri tonlamaya düşse bile zemin, duvar, rampa ve etkileşilebilir
    /// yüzey ayrılır. Renk körlüğü ve düşük kaliteli ekran ikisi de gerçek.</para>
    ///
    /// <para><b>Atmosfer katmanı ayrı ve kapatılabilir</b> (<see cref="AtmosphereToggle"/>,
    /// F10). Sebebi ÇK-17: atmosfer açıkken alınan bir "evet", döngünün mü görselliğin mi
    /// taşıdığını söylemez.</para>
    ///
    /// <para><b>Idempotent</b> (editor-tools.md): varlıklar sabit yollarda aranır, yoksa
    /// yaratılır, varsa güncellenir. İki kez çalıştırmak bir kez çalıştırmakla aynı
    /// sonucu verir — bu araç sahneye yinelenen nesne <b>bırakmaz</b>.</para>
    /// </summary>
    public static class GreyboxLook
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Greybox";
        private const string ProfilePath = "Assets/_Project/Settings/AtmosphereProfile.asset";
        private const string SkyboxPath = "Assets/_Project/Art/Materials/Greybox/mat_sky_greybox.mat";

        private const string AtmosphereObjectName = "_Atmosphere";
        private const string LightObjectName = "_KeyLight";
        private const string ToggleHostName = "_CombatHud";

        // --------------------------------------------------------------- palet

        /// <summary>
        /// Gri kutu paleti. <b>Denge sayısı değil</b> — okunabilirlik ayarı, bu yüzden
        /// config'de değil burada (config-data.md config'i <i>denge</i> için ayırır).
        ///
        /// <para>İkinci sütun kasıtlı: her rolün parlaklık değeri de farklı, böylece
        /// bilgi renge tek başına yaslanmıyor.</para>
        /// </summary>
        private readonly struct Role
        {
            public readonly string Prefix;
            public readonly Color Color;
            public readonly float Smoothness;
            public readonly string Why;

            public Role(string prefix, Color color, float smoothness, string why)
            {
                Prefix = prefix; Color = color; Smoothness = smoothness; Why = why;
            }
        }

        private static readonly Role[] Palette =
        {
            // Zemin koyu ve mat: uzerinde duran her sey ondan ayrilsin.
            new Role("Floor_Ground", new Color(0.20f, 0.19f, 0.18f), 0.05f,
                     "Zemin. En koyu yuzey - ustundeki her sey ondan ayrilir."),
            new Role("Floor_C", new Color(0.28f, 0.27f, 0.25f), 0.05f,
                     "Ust kat zemini. Alt kattan ACIK: hangi kattasin, bakmadan bilinir."),

            // Duvar notr ve orta deger: arka plan olmali, dikkat cekmemeli.
            new Role("Wall_", new Color(0.42f, 0.42f, 0.44f), 0.10f,
                     "Duvar. Notr orta deger - sahnenin arka plani, dikkat cekmez."),

            // Rampa mavi-gri: gecilebilir yuzey duvardan ayrilmali.
            new Role("Ramp_", new Color(0.33f, 0.40f, 0.52f), 0.15f,
                     "Rampa. Gecilebilir yuzey duvardan ayrilir - kacis yolu okunur olmali."),

            // Disarisi: zombilerin dogdugu yer. Ic mekandan ayrilmali, yoksa
            // pencereden bakarken "disarisi" diye bir yer yok gibi okunur.
            new Role("Apron_", new Color(0.13f, 0.14f, 0.13f), 0.02f,
                     "Dis zemin. Zombilerin geldigi yer - ic mekandan koyu, mat ve ayri."),

            // Delik kenari: gorulmeyen bir delikten dusmek PILLAR-04 ihlalidir.
            // Zeminden belirgin acik, boylece kenar kendini soyler.
            new Role("DropLip_", new Color(0.62f, 0.58f, 0.30f), 0.20f,
                     "Dusme deliginin kenari. Zeminden ACIK - kenar kendini soylemeli, " +
                     "gorulmeyen bir delikten dusmek PILLAR-04 ihlalidir."),
        };

        // Etkilesilebilir yuzeyler UNLIT cizilir (bkz. LoadOrCreate): karanlik bir
        // kosede satin alma noktasini bulamamak M1-09 ve M1-10.u oynanamaz yapar.
        private static readonly Color DoorColor = new Color(0.78f, 0.45f, 0.12f);
        private static readonly Color WallBuyColor = new Color(0.16f, 0.62f, 0.68f);

        // --------------------------------------------------------------- giris

        [MenuItem("Bunker/Gorunum/Gri Kutu Gorunumunu Uygula")]
        public static void ApplyMenu() => Apply();

        /// <returns>Gerçekten uygulandıysa <c>true</c>.</returns>
        public static bool Apply()
        {
            // Yanlis sahnede calismak SESSIZ bir basarisizliktir: arac hicbir sey
            // bulamaz, hicbir sey yapmaz ve "tamam" der. Bu projedeki bes hatanin
            // dordu tam olarak bu sinifti - once kontrol et, yuksek sesle patla.
            if (GameObject.Find("LVL-01_Blockout") == null)
            {
                Debug.LogError("[Gorunum] 'LVL-01_Blockout' aktif sahnede yok - hicbir sey " +
                               "uygulanmadi. Yanlis sahne acik olabilir; beklenen: " +
                               "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity. " +
                               "Harita hic uretilmediyse: BlockoutSettings -> HARITAYI URET.");
                return false;
            }

            int changed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                Dictionary<string, Material> materials = EnsureMaterials(ref changed);
                changed += ApplyToBlockout(materials);
                changed += EnsureWallBuyVisuals(materials);
                changed += EnsureDoorLeafLook(materials);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            // Isik, gokyuzu ve Volume sahne nesneleridir - toplu varlik duzenlemesinin
            // DISINDA kalmali, yoksa yeni yaratilan nesneler dogru kaydedilmez.
            changed += EnsureLighting();
            changed += EnsureAtmosphere();

            // SART: yukaridaki SaveAssets yalnizca materyal blogunun finally'sindeydi
            // ve atmosfer ondan SONRA kosuyor. Bu satir olmadan VolumeProfile'a
            // yazilan her sey (bloom, vinyet, tonemapping, kontrast) yalnizca
            // "dirty" isaretlenip DISKE HIC YAZILMIYORDU: profil dosyasi 19 satir
            // ve icinde tek efekt yoktu, yani atmosfer katmani kuruldugundan beri
            // hicbir sey yapmiyordu. Sessiz basarisizligin ders kitabi ornegi -
            // arac "uygulandi" diyordu, git temizdi, ekranda hicbir sey yoktu.
            AssetDatabase.SaveAssets();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[Gorunum] Gri kutu gorunumu uygulandi. {changed} nesne/varlik " +
                      "guncellendi. Atmosferi F10 ile kapatabilirsin (CK-17 icin temiz olcum).");

            return true;
        }

        /// <summary>
        /// Başsız çalıştırma girişi: <c>-executeMethod</c> için.
        ///
        /// <para><b>Sahneyi kendisi açar.</b> Başsız Unity boş bir sahneyle başlar;
        /// açmadan çalıştırılan bir araç hiçbir şey bulamaz ve <b>başarıyla</b> biter —
        /// tam olarak bu projenin en pahalı hata türü. <c>ZombieSetup</c> ile aynı
        /// desen.</para>
        /// </summary>
        public static void ApplyBatch()
        {
            const string scenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                EditorApplication.Exit(Apply() ? 0 : 1);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Gorunum] Toplu uygulama basarisiz: {e}");
                EditorApplication.Exit(1);
            }
        }

        // --------------------------------------------------------------- materyal

        private static Dictionary<string, Material> EnsureMaterials(ref int changed)
        {
            EnsureFolder(MaterialFolder);

            var result = new Dictionary<string, Material>();

            for (int i = 0; i < Palette.Length; i++)
            {
                Role role = Palette[i];
                string key = role.Prefix.TrimEnd('_').ToLowerInvariant();

                result[role.Prefix] = LoadOrCreate(ref changed, $"mat_{key}_greybox",
                                                   role.Color, role.Smoothness);
            }

            // Etkilesim yuzeyleri UNLIT: sahne isigindan bagimsiz, her kosede ayni
            // parlaklikta okunur. Karanlik bir kosede gorunmeyen bir etkilesim
            // noktasi, olmayan bir etkilesim noktasidir (BUG-004'un dersi).
            //
            // Once emisyonlu Lit denendi ve CALISMADI: _EMISSION anahtari her
            // yuklemede kapali okunuyordu (teshis loglandi), yani kapi hic parlamiyor
            // ama materyal her kosuda kirleniyordu. Unlit'in kirilacak parcasi yok -
            // anahtar yok, GI bayragi yok, bloom esigine bagimlilik yok.
            result["Door"] = LoadOrCreate(ref changed, "mat_door_greybox", DoorColor, unlit: true);
            result["DoorOpen"] = LoadOrCreate(ref changed, "mat_door_open_greybox",
                                              DoorColor * 0.35f, unlit: true);
            result["WallBuy"] = LoadOrCreate(ref changed, "mat_wallbuy_greybox", WallBuyColor,
                                             unlit: true);

            return result;
        }

        /// <summary>
        /// Materyali yükler ya da yaratır ve değerlerini yazar.
        /// <para><b>Zaten doğru olan bir varlığı kirletmez</b> (editor-tools.md): her
        /// alan yazmadan önce karşılaştırılır. Aksi halde araç her çalıştığında
        /// dosyalar değişmiş görünür ve diff okunamaz hale gelir.</para>
        /// </summary>
        /// <param name="unlit">
        /// Etkileşim yüzeyleri için. <b>Unlit malzeme sahne ışığını hiç dinlemez</b> —
        /// karanlık bir köşede de aynı parlaklıkta okunur, ki etkileşim noktalarından
        /// istenen tam olarak budur.
        ///
        /// <para>Önce emisyonlu <c>Lit</c> denendi ve <b>çalışmadı</b>: <c>_EMISSION</c>
        /// anahtarı her yüklemede kapalı okunuyordu (teşhis logu), yani kapı hiç
        /// parlamıyordu — ama materyal her koşuda kirleniyordu, çünkü araç anahtarı
        /// her seferinde yeniden açmaya çalışıyordu. Tek sebep, iki hata: görünmeyen
        /// bir etkileşim noktası ve bozulan idempotency. <c>git diff</c> olmasa
        /// ikisi de görünmezdi.</para>
        /// </param>
        private static Material LoadOrCreate(ref int changed, string name, Color color,
                                             float smoothness = 0f, bool unlit = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            string shaderName = unlit
                ? "Universal Render Pipeline/Unlit"
                : "Universal Render Pipeline/Lit";

            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                // Sessiz varsayilan yok: URP yoksa bunu bilmek gerekir, cunku
                // materyaller pembe cikar ve sebebi hic soylenmez.
                Debug.LogError($"[Gorunum] '{shaderName}' shader'i bulunamadi. Render " +
                               "pipeline ayari bozuk olabilir.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool dirty = false;

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
                dirty = true;
            }
            else if (material.shader != shader)
            {
                // Rol Lit'ten Unlit'e (ya da tersine) gectiyse mevcut varlik
                // guncellenir, YENISI YARATILMAZ - GUID sabit kalir ve ona bakan
                // her sahne referansi ayakta kalir.
                material.shader = shader;
                dirty = true;
            }

            if (!Same(material.GetColor("_BaseColor"), color))
            {
                material.SetColor("_BaseColor", color);
                dirty = true;
            }

            // Unlit'te parlaklik diye bir sey yok; olmayan bir ozellige yazmak
            // her kosuda gereksiz bir kirlenme uretirdi.
            if (!unlit && !Mathf.Approximately(material.GetFloat("_Smoothness"), smoothness))
            {
                material.SetFloat("_Smoothness", smoothness);
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(material);

                // Sayaca DAHIL: ilk surumde materyal degisiklikleri sayilmiyordu ve
                // arac dosyalari yazarken bile "0 guncellendi" diyordu. Dogru olmayan
                // bir "hicbir sey degismedi" raporu, idempotency iddiasini
                // dogrulanamaz yapar - nitekim yapmisti.
                changed++;
            }

            return material;
        }

        // --------------------------------------------------------------- uygulama

        private static int ApplyToBlockout(Dictionary<string, Material> materials)
        {
            GameObject root = GameObject.Find("LVL-01_Blockout");

            if (root == null)
            {
                Debug.LogWarning("[Gorunum] 'LVL-01_Blockout' sahnede yok. Once haritayi " +
                                 "uret (BlockoutSettings -> HARITAYI URET).");
                return 0;
            }

            int changed = 0;
            var unmatched = new List<string>();

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material match = MatchByName(renderer.transform, materials);

                if (match == null)
                {
                    // Kapi kanadi ve satin alma levhasi kendi gecislerinde boyaniyor;
                    // burada raporlanirlarsa uyari HER kosuda bagirir ve gercek bir
                    // eslesmeme oldugunda kimse bakmaz.
                    if (renderer.name == "Leaf" || renderer.name == "Plate") continue;

                    // Eslesmeyen nesne SESSIZCE varsayilan gri kalir - yani arac
                    // "tamam" der ama sahnenin bir kismi hala gri corba olur. Rol
                    // oneki degistiginde bunu ogrenmenin tek yolu bu liste.
                    unmatched.Add(renderer.name);
                    continue;
                }

                if (renderer.sharedMaterial == match) continue;

                Undo.RecordObject(renderer, "Gri kutu gorunumu");
                renderer.sharedMaterial = match;
                EditorUtility.SetDirty(renderer);
                changed++;
            }

            if (unmatched.Count > 0)
            {
                Debug.LogWarning($"[Gorunum] {unmatched.Count} nesne bir role eslesmedi ve " +
                                 "varsayilan gri kaldi: " +
                                 string.Join(", ", unmatched.GetRange(0, Mathf.Min(8, unmatched.Count))) +
                                 (unmatched.Count > 8 ? " ..." : string.Empty) +
                                 "  (BlockoutGenerator'daki rol oneki degismis olabilir.)");
            }

            return changed;
        }

        /// <summary>
        /// Nesnenin rolünü <b>adından</b> bulur. <c>BlockoutGenerator</c> her şeyi rol
        /// önekiyle adlandırıyor (<c>Floor_</c>, <c>Wall_</c>, <c>Ramp_</c>) — bu araç
        /// o sözleşmeye yaslanır. Önek değişirse burası sessizce hiçbir şey yapmaz,
        /// bu yüzden eşleşmeyen nesne sayısı raporlanıyor.
        /// </summary>
        private static Material MatchByName(Transform transform,
                                            Dictionary<string, Material> materials)
        {
            string name = transform.name;

            // SIRA ONEMLI ve bu yuzden bir sozluk dongusu DEGIL, acik bir zincir:
            // sozlukte gezinme sirasi garanti degildir ve "Floor_C" ile "Floor_Ground"
            // hangisinin once eslestigi calisma calisma degisirdi. editor-tools.md
            // deterministik olmayi sart kosuyor - ayni girdi, ayni cikti.
            // Uzun onek once.
            if (name.StartsWith("WallBuy", System.StringComparison.Ordinal))
                return Get(materials, "WallBuy");

            if (name.StartsWith("Floor_C", System.StringComparison.Ordinal))
                return Get(materials, "Floor_C");

            if (name.StartsWith("Floor", System.StringComparison.Ordinal))
                return Get(materials, "Floor_Ground");

            if (name.StartsWith("Wall", System.StringComparison.Ordinal))
                return Get(materials, "Wall_");

            if (name.StartsWith("Ramp", System.StringComparison.Ordinal))
                return Get(materials, "Ramp_");

            if (name.StartsWith("Apron", System.StringComparison.Ordinal))
                return Get(materials, "Apron_");

            if (name.StartsWith("DropLip", System.StringComparison.Ordinal))
                return Get(materials, "DropLip_");

            // Ic bolme bir duvardir ve duvar gibi okunmali - ayri bir renk vermek,
            // olmayan bir ayrimi varmis gibi gosterirdi.
            if (name.StartsWith("Partition", System.StringComparison.Ordinal))
                return Get(materials, "Wall_");

            return null;
        }

        /// <summary>
        /// İki rengi <b>toleransla</b> karşılaştırır.
        ///
        /// <para><c>Color</c>'ın kendi <c>==</c>'i bunun için yeterli değil: bir
        /// materyalden geri okunan renk, yazılan değerle bit bit aynı olmayabilir
        /// (serileştirme yuvarlaması, HDR emisyonda renk uzayı dönüşümü). Tam eşitlik
        /// arayan bir kontrol <b>her koşuda "değişti" der</b>, materyali boş yere
        /// kirletir ve "hiçbir şey değişmedi" raporunu güvenilmez yapar.</para>
        /// </summary>
        private static bool Same(Color a, Color b)
        {
            const float epsilon = 0.002f;

            return Mathf.Abs(a.r - b.r) < epsilon
                && Mathf.Abs(a.g - b.g) < epsilon
                && Mathf.Abs(a.b - b.b) < epsilon
                && Mathf.Abs(a.a - b.a) < epsilon;
        }

        /// <summary>Materyal rengini <b>yalnızca farklıysa</b> yazar.</summary>
        private static bool SetIfDifferent(Material material, string property, Color value)
        {
            if (Same(material.GetColor(property), value)) return false;

            material.SetColor(property, value);
            return true;
        }

        /// <summary>Materyal sayısını <b>yalnızca farklıysa</b> yazar.</summary>
        private static bool SetIfDifferent(Material material, string property, float value)
        {
            if (Mathf.Approximately(material.GetFloat(property), value)) return false;

            material.SetFloat(property, value);
            return true;
        }

        private static Material Get(Dictionary<string, Material> materials, string key)
        {
            return materials.TryGetValue(key, out Material material) ? material : null;
        }

        /// <summary>
        /// Duvar silahı satın alma noktalarına <b>görünür bir levha</b> verir.
        ///
        /// <para>Şu an bu işaretler <b>hiçbir görsele sahip değil</b> — boş bir
        /// <c>GameObject</c>. Yani oyuncu mermiyi nereden alacağını göremiyor. Bu,
        /// BUG-004'ün ("kapı diye bir nesne yoktu") birebir aynı sınıfı ve M1-10'u
        /// oynanamaz yapıyor.</para>
        /// </summary>
        private static int EnsureWallBuyVisuals(Dictionary<string, Material> materials)
        {
            int changed = 0;

            foreach (string name in new[] { "WallBuy_A_Cheap", "WallBuy_B_Mid" })
            {
                GameObject marker = GameObject.Find(name);
                if (marker == null) continue;

                Transform plate = marker.transform.Find("Plate");

                if (plate == null)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Plate";
                    go.transform.SetParent(marker.transform, false);
                    go.transform.localScale = new Vector3(1.1f, 0.7f, 0.12f);

                    // Levha CARPISMAZ: satin alma tetikleyicisi zaten var, ikinci bir
                    // collider oyuncuyu duvara yapistirir ve sebebi anlasilmaz olur.
                    Object.DestroyImmediate(go.GetComponent<Collider>());

                    Undo.RegisterCreatedObjectUndo(go, "Duvar silahi levhasi");
                    plate = go.transform;
                    changed++;
                }

                var renderer = plate.GetComponent<MeshRenderer>();

                if (renderer != null && materials.TryGetValue("WallBuy", out Material mat)
                    && renderer.sharedMaterial != mat)
                {
                    renderer.sharedMaterial = mat;
                    EditorUtility.SetDirty(renderer);
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>Kapı kanadına kilitli-kapı rengini verir (M1-09 okunabilirliği).</summary>
        private static int EnsureDoorLeafLook(Dictionary<string, Material> materials)
        {
            if (!materials.TryGetValue("Door", out Material doorMaterial)) return 0;

            int changed = 0;

            foreach (string name in new[] { "Door_A_to_B", "Door_To_Ramp" })
            {
                GameObject marker = GameObject.Find(name);
                if (marker == null) continue;

                Transform leaf = marker.transform.Find("Leaf");
                if (leaf == null) continue;

                foreach (MeshRenderer renderer in leaf.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterial == doorMaterial) continue;

                    renderer.sharedMaterial = doorMaterial;
                    EditorUtility.SetDirty(renderer);
                    changed++;
                }
            }

            return changed;
        }

        // --------------------------------------------------------------- isik

        /// <summary>
        /// Işık, gökyüzü ve sis.
        ///
        /// <para><b>Unity'nin varsayılanları bu oyun için yanlış:</b> tek beyaz bir
        /// yönlü ışık, düz gri gökyüzü ve sissiz bir sahne, gri kutuyu düz bir yüzeye
        /// çevirir — derinlik ipucu kalmaz ve mesafe okunamaz. Mesafe okunamayan bir
        /// nişancı oyununda tehdit de okunamaz (PILLAR-04).</para>
        /// </summary>
        private static int EnsureLighting()
        {
            int changed = 0;

            // --- gokyuzu
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);

            if (sky == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural");

                if (shader != null)
                {
                    sky = new Material(shader) { name = "mat_sky_greybox" };
                    AssetDatabase.CreateAsset(sky, SkyboxPath);
                    changed++;
                }
            }

            if (sky != null)
            {
                // Materyallerde oldugu gibi ONCE KARSILASTIR: kosulsuz SetDirty,
                // "iki kez calistirmak bir kez calistirmakla ayni" iddiasini koda
                // degil sansa birakir. Materyal tarafinda bir kez isirilmistik.
                bool skyDirty = false;
                skyDirty |= SetIfDifferent(sky, "_SkyTint", new Color(0.22f, 0.24f, 0.30f));
                skyDirty |= SetIfDifferent(sky, "_GroundColor", new Color(0.10f, 0.10f, 0.11f));
                skyDirty |= SetIfDifferent(sky, "_AtmosphereThickness", 0.75f);
                skyDirty |= SetIfDifferent(sky, "_Exposure", 0.85f);

                if (skyDirty) { EditorUtility.SetDirty(sky); changed++; }

                if (RenderSettings.skybox != sky)
                {
                    RenderSettings.skybox = sky;
                    changed++;
                }
            }

            // --- anahtar isik
            Light key = FindKeyLight();

            if (key == null)
            {
                var go = new GameObject(LightObjectName);
                key = go.AddComponent<Light>();
                key.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(go, "Anahtar isik");
                changed++;
            }

            // Alcak ve yandan: uzun golgeler mesafe ipucu verir. Tepeden gelen isik
            // her yuzeyi ayni parlaklikta gosterir ve sahne duzlesir.
            // Isikta da ONCE KARSILASTIR: kosulsuz SetDirty sahneyi her kosuda
            // kirletir ve "hicbir sey degismedi" raporunu dogrulanamaz yapar.
            var keyRotation = Quaternion.Euler(38f, 145f, 0f);
            var keyColor = new Color(1f, 0.94f, 0.84f);
            bool keyDirty = key.transform.rotation != keyRotation
                            || key.color != keyColor
                            || !Mathf.Approximately(key.intensity, 1.15f)
                            || key.shadows != LightShadows.Soft;

            if (keyDirty)
            {
                key.transform.rotation = keyRotation;
                key.color = keyColor;
                key.intensity = 1.15f;
                key.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(key);
                changed++;
            }

            // --- ortam ve sis
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.26f, 0.28f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.18f, 0.20f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.09f, 0.10f);

            // Sis mesafeyi okutur ama YAKINI kapatmamali: 18 m'den once hicbir sey
            // solmuyor, cunku tehdidin okunmasi gereken mesafe orasi.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.16f, 0.17f, 0.21f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 75f;

            return changed;
        }

        private static Light FindKeyLight()
        {
            GameObject named = GameObject.Find(LightObjectName);
            if (named != null) return named.GetComponent<Light>();

            // Sahnede zaten bir yonlu isik varsa YENISINI YARATMA - iki yonlu isik,
            // iki golge yonu demektir ve nereden geldigi okunmaz olur.
            // Kapali nesneler de dahil: kapali bir yonlu isik atlanirsa arac IKINCI
            // bir tane yaratir ve iki golge yonu olusur.
            foreach (Light light in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) return light;
            }

            return null;
        }

        // --------------------------------------------------------------- atmosfer

        private static int EnsureAtmosphere()
        {
            int changed = 0;

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                changed++;
            }

            if (ConfigureProfile(profile))
            {
                EditorUtility.SetDirty(profile);
                changed++;
            }

            GameObject host = GameObject.Find(AtmosphereObjectName);

            if (host == null)
            {
                host = new GameObject(AtmosphereObjectName);
                Undo.RegisterCreatedObjectUndo(host, "Atmosfer");
                changed++;
            }

            var volume = host.GetComponent<Volume>();
            if (volume == null) volume = host.AddComponent<Volume>();

            if (!volume.isGlobal || !Mathf.Approximately(volume.priority, 0f)
                || volume.sharedProfile != profile)
            {
                volume.isGlobal = true;
                volume.priority = 0f;
                volume.sharedProfile = profile;
                EditorUtility.SetDirty(volume);
                changed++;
            }

            // F10 anahtari: HUD nesnesinde yasar, cunku o nesne her kurulumda zaten
            // var ve ayri bir nesne unutulmaya acik.
            GameObject toggleHost = GameObject.Find(ToggleHostName);

            if (toggleHost != null)
            {
                var toggle = toggleHost.GetComponent<AtmosphereToggle>();

                if (toggle == null)
                {
                    toggle = toggleHost.AddComponent<AtmosphereToggle>();
                    changed++;
                }

                SerializedObject so = new SerializedObject(toggle);
                SerializedProperty field = so.FindProperty("atmosphere");

                if (field != null && field.objectReferenceValue != host)
                {
                    field.objectReferenceValue = host;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }
            }
            else
            {
                Debug.LogWarning($"[Gorunum] '{ToggleHostName}' sahnede yok, F10 anahtari " +
                                 "baglanamadi. Once 'Bunker/Zombi/Test Alanini Kur' calistir.");
            }

            return changed;
        }

        /// <summary>
        /// Post-processing yığını.
        ///
        /// <para><b>Her efekt bir bütçe kalemidir</b> (shader-graphics.md: post-processing
        /// her piksele, her karede uygulanır). Buradaki dördü bilinçli seçildi ve hiçbiri
        /// tam ekran bulanıklık değil — okunabilirliği düşüren bir atmosfer, atmosfer
        /// değil hasardır.</para>
        /// </summary>

        private static bool ConfigureProfile(VolumeProfile profile)
        {
            bool dirty = false;

            // Tonemapping: HDR renkleri ekrana makul indirir. Olmadan emisyonlu kapi
            // ve satin alma levhasi patlar ve yanindaki her seyi yutar.
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile, ref dirty);

            dirty |= SetActive(tonemapping);
            dirty |= Set(tonemapping.mode, TonemappingMode.Neutral);

            // Bloom: yalnizca emisyonlu yuzeyleri parlatir. Esik yuksek tutuldu -
            // dusuk esik butun sahneyi sisler ve tehdidi gizler (PILLAR-04'un
            // reddettigi sey).
            Bloom bloom = GetOrAdd<Bloom>(profile, ref dirty);

            dirty |= SetActive(bloom);
            dirty |= Set(bloom.threshold, 1.1f);
            dirty |= Set(bloom.intensity, 0.55f);
            dirty |= Set(bloom.scatter, 0.62f);

            // Vinyet: gozu ekranin ortasina, nisangaha toplar. Hafif - agir bir vinyet
            // cevre gorusunu keser ve arkadan gelen zombiyi gizler.
            Vignette vignette = GetOrAdd<Vignette>(profile, ref dirty);

            dirty |= SetActive(vignette);
            dirty |= Set(vignette.intensity, 0.28f);
            dirty |= Set(vignette.smoothness, 0.45f);

            // Renk derecelendirme: hafif soguk ve kontrastli. Kontrast okunabilirlige
            // HIZMET eder - duz gri bir goruntude silueti ayirmak zordur.
            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile, ref dirty);

            dirty |= SetActive(color);
            dirty |= Set(color.postExposure, 0.15f);
            dirty |= Set(color.contrast, 12f);
            dirty |= Set(color.saturation, -8f);

            return dirty;
        }

        /// <summary>
        /// Profildeki efekti bulur, yoksa ekler <b>ve varlığın alt nesnesi yapar</b>.
        ///
        /// <para><b>`AddObjectToAsset` olmadan efekt diske hiç yazılmaz.</b> URP'de
        /// <c>VolumeProfile.Add&lt;T&gt;()</c> bileşeni bellekte yaratır; profil
        /// varlığının bir parçası olması için ayrıca alt nesne olarak eklenmesi
        /// gerekir. Bu satır eksikken <c>AtmosphereProfile.asset</c> <b>19 satır ve
        /// içinde tek efekt yoktu</b> — yani bloom, vinyet, tonemapping ve kontrast
        /// hiç çalışmıyordu. Araç "uygulandı" diyordu, git temizdi, ekranda hiçbir
        /// şey yoktu; ve her koşuda yeniden ekleyip yeniden kaybediyordu.</para>
        /// </summary>
        private static T GetOrAdd<T>(VolumeProfile profile, ref bool dirty)
            where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;

            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;

            AssetDatabase.AddObjectToAsset(component, profile);

            dirty = true;
            return component;
        }

        /// <summary>
        /// Bir <c>Volume</c> geçersiz kılmasını <b>yalnızca farklıysa</b> yazar.
        ///
        /// <para>Koşulsuz yazmak profili her koşuda kirletirdi ve aracın "iki kez
        /// çalıştırmak bir kez çalıştırmakla aynı" iddiasını doğrulanamaz yapardı.
        /// Materyal tarafında bu hatadan bir kez ısırılmıştık; burada tekrarlanmıyor.</para>
        /// </summary>
        private static bool Set<T>(VolumeParameter<T> parameter, T value)
        {
            if (parameter.overrideState && EqualityComparer<T>.Default.Equals(parameter.value, value))
            {
                return false;
            }

            parameter.overrideState = true;
            parameter.value = value;
            return true;
        }

        private static bool SetActive(VolumeComponent component)
        {
            if (component.active) return false;

            component.active = true;
            return true;
        }

        // --------------------------------------------------------------- yardimci

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
