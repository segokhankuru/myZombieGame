using System;
using System.Reflection;
using System.Text;
using Bunker.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Bunker.Editor
{
    /// <summary>
    /// Dış dekoru <b>NavMesh bake'inin dışında</b> tutar. 2026-09-09.
    ///
    /// <para><b>Neden gerekti</b> (geliştirici: <i>"apron_south yüzeyinde ilk alan için
    /// doğan zombiler olduğu yerde sıkışıp kalıyor, engel olmadığı hâlde"</i>). Cümlenin
    /// iki yarısı da doğruydu ve çelişmiyorlardı: <b>fiziksel</b> engel gerçekten yok —
    /// dekorun ve bitkilerin çarpıştırıcıları kuruluşta siliniyor. Ama
    /// <c>NavMeshSurface</c>'in varsayılan geometri kaynağı <b>Render Meshes</b>'tir,
    /// yani bake çarpıştırıcıya değil <i>çizilen mesh'e</i> bakar. 222 parça ot, çalı,
    /// taş ve ağaç apronun NavMesh'ini delik deşik ediyor; 14 metre dışarıda doğan zombi
    /// küçük bir adanın üstüne düşüyor ve pencereye giden bir yol bulamıyor.</para>
    ///
    /// <para><b>Ders (kayda değer):</b> çarpıştırıcıyı silmek bir nesneyi navigasyondan
    /// çıkarmaz. İkisi ayrı sistem ve ayrı sorular — "oyuncu çarpar mı" ile "ajan
    /// yürüyebilir mi". Dekorun ilk kuralı oynanışa dokunmamaktı ve bu kural yalnızca
    /// yarısı uygulanmış hâlde yazılmıştı.</para>
    ///
    /// <para><b>Neden <c>NavMeshModifier</c>, katman ya da geometri kaynağı değil:</b>
    /// (1) <c>useGeometry</c>'yi <c>PhysicsColliders</c> yapmak bütün haritayı etkiler
    /// ve çarpıştırıcısı olmayan her blokout parçası sessizce NavMesh'ten düşerdi.
    /// (2) Ayrı bir katman, proje ayarlarına yeni bir gizli bağımlılık demek. Modifier
    /// nesnenin <b>kendi üstünde</b> duruyor ve hiyerarşisinin tamamına uygulanıyor —
    /// yani niyet, uygulandığı yerde okunuyor.</para>
    ///
    /// <para><b>Bu araç haritaya DOKUNMAZ.</b> Yalnızca dekor gruplarına bir bileşen
    /// ekler ve NavMesh'i yeniden bake eder; blokout geometrisi, elle yapılmış her düzen
    /// olduğu gibi kalır (2026-09-08'in kuralı).</para>
    /// </summary>
    public static class NavMeshDecor
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

        /// <summary>
        /// Bake dışında kalması gereken gruplar. <b>Üreteçlerin verdiği adlar</b>
        /// (<c>ArtIntegration.DecorateOutside</c>, <c>OutdoorScenery.Decorate</c>);
        /// ad değişirse burası sessizce hiçbir şey yapmaz, o yüzden rapor bulunan grup
        /// sayısını yazıyor.
        /// </summary>
        private static readonly string[] DecorGroups = { "Decor_Outside", "Scenery_Outside" };

        // ================================================================= giris

        [MenuItem("Bunker/Level/Dis Dekoru NavMesh Disina Al (+ bake)", false, 111)]
        public static void FixFromMenu()
        {
            if (!Apply()) return;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Başsız giriş: sahneyi açar, düzeltir, bake eder, kaydeder.</summary>
        public static void FixBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                bool ok = Apply();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();

                EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NavMesh dekor] Basarisiz: {e}");
                EditorApplication.Exit(2);
            }
        }

        /// <summary>
        /// Başsız <b>teşhis</b>: hiçbir şeyi değiştirmez, yalnızca ölçer.
        /// Düzeltmeden önce ve sonra aynı sayıları karşılaştırmak için.
        /// </summary>
        public static void ReportBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Report();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NavMesh dekor] Rapor alinamadi: {e}");
                EditorApplication.Exit(2);
            }
        }

        // ================================================================= rapor

        /// <summary>
        /// Ne olduğunu <b>sayıyla</b> söyler: bake'in geometri kaynağı, dekorun kaç
        /// renderer'ı bake'e giriyor, ve doğum noktalarından pencereye yol var mı.
        /// </summary>
        [MenuItem("Bunker/Level/NavMesh Dekor Raporu", false, 112)]
        public static void Report()
        {
            var report = new StringBuilder(1024);
            report.Append("[NavMesh dekor] RAPOR\n");

            AppendSurfaceSettings(report);
            AppendDecorCounts(report);
            AppendSpawnPaths(report);

            Debug.Log(report.ToString());
        }

        private static void AppendSurfaceSettings(StringBuilder report)
        {
            Type surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");

            if (surfaceType == null)
            {
                report.Append("  YUZEY        : NavMeshSurface tipi yok (AI Navigation paketi?)\n");
                return;
            }

            UnityEngine.Object[] surfaces = UnityEngine.Object.FindObjectsByType(
                surfaceType, FindObjectsInactive.Include, FindObjectsSortMode.None);

            report.Append($"  yuzey sayisi : {surfaces.Length}\n");

            for (int i = 0; i < surfaces.Length; i++)
            {
                var so = new SerializedObject(surfaces[i]);

                // Alan adlari NavMeshSurface'in serilesmis hali; property yerine
                // SerializedObject kullaniliyor cunku reflection'la enum degerini
                // okumak paket surumune bagli bir tur cozumlemesi gerektirir.
                string geometry = ReadEnum(so, "m_UseGeometry", "0=RenderMeshes, 1=PhysicsColliders");
                string collect = ReadEnum(so, "m_CollectObjects", "0=All, 1=Volume, 2=Children");
                string layers = ReadInt(so, "m_LayerMask");

                report.Append($"    [{i}] {surfaces[i].name}: geometri {geometry}, " +
                              $"toplama {collect}, katman maskesi {layers}\n");
            }
        }

        private static string ReadEnum(SerializedObject so, string field, string legend)
        {
            SerializedProperty p = so.FindProperty(field);
            return p == null ? "?" : $"{p.intValue} ({legend})";
        }

        private static string ReadInt(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            return p == null ? "?" : p.intValue.ToString();
        }

        private static void AppendDecorCounts(StringBuilder report)
        {
            Type modifierType = FindType("Unity.AI.Navigation.NavMeshModifier");

            foreach (string name in DecorGroups)
            {
                GameObject group = GameObject.Find(name);

                if (group == null)
                {
                    report.Append($"  {name,-16}: sahnede YOK\n");
                    continue;
                }

                int renderers = group.GetComponentsInChildren<MeshRenderer>(true).Length;
                bool ignored = modifierType != null && IsIgnored(group, modifierType);

                report.Append($"  {name,-16}: {renderers} renderer, " +
                              $"bake disinda mi = {(ignored ? "EVET" : "HAYIR")}\n");
            }
        }

        /// <summary>
        /// <b>Asıl ölçüm:</b> her zemin kat penceresi için doğum noktasından pencerenin
        /// dış bekleme noktasına yol var mı.
        ///
        /// <para><b>Neden bu, mevcut bağlantı kontrolünden farklı:</b>
        /// <see cref="LevelConnectivityCheck"/> <c>OutsidePoint</c>'in altında NavMesh
        /// olup olmadığına bakıyordu — <i>var</i>. Zombi ise 14 metre daha dışarıda
        /// doğuyor ve oraya <b>yürümek</b> zorunda. Aradaki apronu delik deşik eden bir
        /// bake, "NavMesh var ama ada" durumunu üretir ve o kontrol bunu göremez. Bu
        /// satır tam olarak geliştiricinin gördüğü şeyi ölçüyor.</para>
        /// </summary>
        private static void AppendSpawnPaths(StringBuilder report)
        {
            WindowEntry[] windows = UnityEngine.Object.FindObjectsByType<WindowEntry>(
                FindObjectsSortMode.None);

            if (windows.Length == 0)
            {
                report.Append("  dogum yolu   : WindowEntry yok - harita uretilmemis olabilir\n");
                return;
            }

            int noMeshAtSpawn = 0;
            int noPath = 0;
            int ok = 0;
            var broken = new StringBuilder(256);

            for (int i = 0; i < windows.Length; i++)
            {
                WindowEntry w = windows[i];

                if (!NavMesh.SamplePosition(w.SpawnPoint, out NavMeshHit spawnHit, 2f,
                                            NavMesh.AllAreas))
                {
                    noMeshAtSpawn++;
                    Append(broken, w.name, "dogum noktasinda NavMesh YOK");
                    continue;
                }

                if (!NavMesh.SamplePosition(w.OutsidePoint, out NavMeshHit outsideHit, 2f,
                                            NavMesh.AllAreas))
                {
                    noPath++;
                    Append(broken, w.name, "pencere onunde NavMesh YOK");
                    continue;
                }

                var path = new NavMeshPath();
                bool found = NavMesh.CalculatePath(spawnHit.position, outsideHit.position,
                                                   NavMesh.AllAreas, path);

                if (!found || path.status != NavMeshPathStatus.PathComplete)
                {
                    noPath++;
                    Append(broken, w.name,
                           found ? $"YOL YARIM ({path.status})" : "YOL YOK");
                    continue;
                }

                ok++;
            }

            report.Append($"  dogum -> pencere yolu ({windows.Length} giris):\n");
            report.Append($"    tam yol             : {ok}\n");
            report.Append($"    dogumda NavMesh yok : {noMeshAtSpawn}\n");
            report.Append($"    yol yok / yarim     : {noPath}\n");

            if (broken.Length > 0) report.Append(broken);

            report.Append(noMeshAtSpawn + noPath == 0
                ? "  SONUC: her dogum noktasindan pencereye yol var."
                : "  SONUC: KOPUK - dogan zombi oldugu yerde kalir.");
        }

        private static void Append(StringBuilder builder, string window, string why)
        {
            builder.Append("      ").Append(window).Append(": ").Append(why).Append('\n');
        }

        // ============================================================== duzeltme

        /// <summary>
        /// Dekor gruplarını bake dışına alır ve NavMesh'i yeniden bake eder.
        /// </summary>
        /// <returns>Bir şey değiştiyse ve bake edildiyse <c>true</c>.</returns>
        public static bool Apply()
        {
            Type modifierType = FindType("Unity.AI.Navigation.NavMeshModifier");

            if (modifierType == null)
            {
                Debug.LogError("[NavMesh dekor] NavMeshModifier tipi bulunamadi " +
                               "(AI Navigation paketi yuklu mu?). Hicbir sey yapilmadi.");
                return false;
            }

            int touched = 0;
            var log = new StringBuilder(256);

            foreach (string name in DecorGroups)
            {
                GameObject group = GameObject.Find(name);

                if (group == null)
                {
                    log.Append($"  {name}: sahnede yok, atlandi\n");
                    continue;
                }

                if (IsIgnored(group, modifierType))
                {
                    log.Append($"  {name}: zaten bake disinda\n");
                    continue;
                }

                MarkIgnored(group, modifierType);
                touched++;

                int renderers = group.GetComponentsInChildren<MeshRenderer>(true).Length;
                log.Append($"  {name}: bake disina alindi ({renderers} renderer)\n");
            }

            // ONCE RAPOR, SONRA BAKE, SONRA YINE RAPOR: "duzeldi mi" sorusunun cevabi
            // bir kanaat degil, iki sayi arasindaki fark olmali.
            Debug.Log($"[NavMesh dekor] {touched} grup isaretlendi.\n{log}");

            bool baked = ZombieSetup.BakeNavMeshPublic();

            if (!baked)
            {
                Debug.LogError("[NavMesh dekor] Bake edilemedi - isaretleme yazildi ama " +
                               "NavMesh eski hâlinde. 'Bunker/Zombi/NavMesh Bake' dene.");
                return false;
            }

            Report();
            return true;
        }

        /// <summary>
        /// Bir grubu bake dışına alır. <b>Modifier grubun KÖKÜNE konur</b> — NavMesh
        /// oluşturucu hiyerarşiyi dolaşırken üstteki modifier'ı bütün alt nesnelere
        /// uygular. 222 nesneye ayrı bileşen koymak aynı işi yapar ve sahneyi 222 satır
        /// şişirirdi.
        /// </summary>
        public static void MarkIgnored(GameObject group, Type modifierType)
        {
            Component modifier = group.GetComponent(modifierType);
            if (modifier == null) modifier = Undo.AddComponent(group, modifierType);

            var so = new SerializedObject(modifier);

            SetBool(so, "m_IgnoreFromBuild", true);

            // Butun ajan tipleri icin gecerli: tek bir ajan tipi var ama "yalnizca
            // su ajan" demek, ikinci bir ajan tipi eklendigi gun dekorun sessizce
            // geri gelmesi demekti.
            SetBool(so, "m_OverrideArea", false);
            SetBool(so, "m_ApplyToChildren", true);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(modifier);
        }

        /// <summary>
        /// Üreteçlerin çağırdığı hâli: tip yoksa <b>sessizce geçer</b>.
        ///
        /// <para><b>Neden burada sessizlik doğru</b> (projenin "sessiz return yazma"
        /// kuralının istisnası, ve gerekçesi yazılı olmak zorunda): AI Navigation
        /// paketi yoksa <c>BakeNavMesh</c> zaten kendi yüksek sesli uyarısını basıyor
        /// ve NavMesh hiç üretilmiyor. Aynı eksiklik için ikinci bir hata, haritayı
        /// üreten aracın çıktısını gerçek sorunun görünmediği bir gürültüye çevirir.</para>
        /// </summary>
        public static void MarkIgnoredIfPossible(GameObject group)
        {
            if (group == null) return;

            Type modifierType = FindType("Unity.AI.Navigation.NavMeshModifier");
            if (modifierType == null) return;

            MarkIgnored(group, modifierType);
        }

        /// <summary>Grup zaten bake dışında mı.</summary>
        private static bool IsIgnored(GameObject group, Type modifierType)
        {
            Component modifier = group.GetComponent(modifierType);
            if (modifier == null) return false;

            var so = new SerializedObject(modifier);
            SerializedProperty p = so.FindProperty("m_IgnoreFromBuild");

            return p != null && p.boolValue;
        }

        /// <summary>
        /// Alan varsa yazar. <b>Yoksa sessiz geçer ve bu bilinçli:</b> paket sürümleri
        /// arasında <c>m_ApplyToChildren</c> gibi alanlar gelip gidiyor; olmayan bir
        /// alan için patlamak, aracın çalıştığı tek şeyi de yapmamasına yol açardı.
        /// </summary>
        private static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.Boolean) return;

            p.boolValue = value;
        }

        /// <summary>
        /// Tipi yüklü assembly'lerde arar. <c>Bunker.Editor</c> AI Navigation
        /// paketine referans vermiyor (<c>ZombieSetup</c> ile aynı desen ve aynı
        /// gerekçe: asmdef'e referans eklemek bütün derlemeyi o pakete bağlar).
        /// </summary>
        internal static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }
    }
}
