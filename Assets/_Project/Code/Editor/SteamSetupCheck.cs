using System;
using System.IO;
using System.Reflection;
using System.Text;
using Bunker.Net;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Steam kurulumunu denetler ve <b>bulursa kendisi bağlar</b>. ADR-0007.
    ///
    /// <para><b>Neden yansıma (reflection) kullanıyor:</b> Facepunch ve FizzyFacepunch
    /// henüz projede yok. Onların tiplerine derleme zamanında başvuran bir araç,
    /// paketler gelene kadar <b>projeyi derlenemez</b> yapardı — yani kurulumu kontrol
    /// etmesi gereken araç, kurulum olmadan çalışamazdı. Yansıma bu düğümü çözer:
    /// tipler yoksa araç yine çalışır ve eksiği söyler.</para>
    ///
    /// <para><b>Neden bir araç, bir tarif değil</b> (CLAUDE.md: "bir paragraf yerine bir
    /// betik"): kurulumun beş adımından biri atlandığında hata <b>sessizdir</b> — oyun
    /// açılır, "oda aç" çalışır gibi görünür ve arkadaş hiç bağlanamaz. Araç her adımı
    /// tek tek kontrol edip eksiği adıyla söyler.</para>
    /// </summary>
    public static class SteamSetupCheck
    {
        private const string AppIdFileName = "steam_appid.txt";
        private const string DevelopmentAppId = "480";

        [MenuItem("Bunker/Steam/Kurulumu Kontrol Et", false, 40)]
        public static void CheckMenu()
        {
            var report = new StringBuilder();

            bool facepunch = HasFacepunch(out string facepunchAssembly);
            bool fizzy = TryFindFizzyTransport(out Type fizzyType);
            bool appId = EnsureAppIdFile(report);

            report.AppendLine();
            report.AppendLine("STEAM KURULUM DURUMU (ADR-0007)");
            report.AppendLine("--------------------------------");
            report.AppendLine(Line("1) Facepunch.Steamworks", facepunch,
                facepunch ? facepunchAssembly : "YOK - wiki.facepunch.com/steamworks/Installing_For_Unity"));
            report.AppendLine(Line("2) FizzyFacepunch (Mirror tasimasi)", fizzy,
                fizzy ? fizzyType.FullName : "YOK - github.com/Chykary/FizzyFacepunch/releases"));
            report.AppendLine(Line("3) steam_appid.txt", appId, AppIdPath()));

            if (!facepunch || !fizzy)
            {
                report.AppendLine();
                report.AppendLine("EKSIK VAR - baglama adimi ATLANDI.");
                report.AppendLine("Paketleri kurduktan sonra bu araci tekrar calistir; " +
                                  "menu sahnesindeki tasimayi kendisi baglar.");
                Debug.LogWarning(report.ToString());
                return;
            }

            report.AppendLine();
            report.AppendLine(AttachTransport(fizzyType));

            Debug.Log(report.ToString());
        }

        /// <summary>Başsız giriş: <c>-executeMethod</c> için.</summary>
        public static void CheckBatch()
        {
            CheckMenu();
            EditorApplication.Exit(HasFacepunch(out _) && TryFindFizzyTransport(out _) ? 0 : 1);
        }

        // ---------------------------------------------------------------- kontroller

        /// <summary>
        /// Facepunch yüklenmiş mi. <b>Tip adıyla aranır, dosya adıyla değil:</b> DLL'in
        /// nereye konduğu kurulumdan kuruluma değişir, tipin adı değişmez.
        /// </summary>
        private static bool HasFacepunch(out string assemblyName)
        {
            assemblyName = string.Empty;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("Steamworks.SteamClient", false);
                if (type == null) continue;

                assemblyName = assembly.GetName().Name;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fizzy taşımasını bulur.
        ///
        /// <para><b>Ad alanı değil, MİRAS aranıyor:</b> <c>Transport</c>'tan türeyen ve
        /// adında "Fizzy" geçen tip. Paketin ad alanı sürümden sürüme değişti
        /// (<c>Mirror.FizzySteam</c> vs global); mirası değişmedi.</para>
        /// </summary>
        private static bool TryFindFizzyTransport(out Type found)
        {
            found = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    // Yarim yuklenmis bir assembly butun taramayi dusurmemeli.
                    types = e.Types;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(Transport).IsAssignableFrom(type)) continue;
                    if (type.Name.IndexOf("Fizzy", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    found = type;
                    return true;
                }
            }

            return false;
        }

        private static string AppIdPath() =>
            Path.Combine(Directory.GetCurrentDirectory(), AppIdFileName);

        /// <summary>
        /// <c>steam_appid.txt</c> yoksa oluşturur.
        ///
        /// <para>Bu dosya olmadan Steam, editörde başlatıldığında hangi oyun olduğunu
        /// bilemez ve <c>SteamClient.Init</c> hata verir. Depo dosyayı bilerek yok
        /// sayıyor (makineye özel), o yüzden oluşturmak bu aracın işi.</para>
        /// </summary>
        private static bool EnsureAppIdFile(StringBuilder report)
        {
            string path = AppIdPath();

            if (File.Exists(path)) return true;

            try
            {
                File.WriteAllText(path, DevelopmentAppId);
                report.AppendLine($"[Steam] {AppIdFileName} olusturuldu (App ID {DevelopmentAppId} - " +
                                  "Valve'in herkese acik gelistirme kimligi).");
                return true;
            }
            catch (Exception e)
            {
                report.AppendLine($"[Steam] {AppIdFileName} yazilamadi: {e.Message}");
                return false;
            }
        }

        // ---------------------------------------------------------------- baglama

        /// <summary>
        /// Menü sahnesindeki <c>NetworkManager</c>'a Fizzy taşımasını takar ve
        /// <c>transport</c> alanına bağlar.
        ///
        /// <para><b>KCP silinmez</b> (ADR-0007): Steam kapalıyken test edebilmek için
        /// duruyor. Değişen tek şey hangisinin <i>bağlı</i> olduğu.</para>
        ///
        /// <para>Alan adları yansımayla aranıyor — paketin alan adı ("SteamAppID",
        /// "steamAppId", ...) sürümden sürüme değişti ve bir sabit yazmak, sessizce
        /// yanlış App ID ile çalışan bir kurulum bırakırdı.</para>
        /// </summary>
        private static string AttachTransport(Type fizzyType)
        {
            if (!File.Exists(MenuSceneGenerator.MenuScenePath))
            {
                return $"[Steam] Menu sahnesi yok ({MenuSceneGenerator.MenuScenePath}). " +
                       "Once 'Bunker/Menu/Ana Menuyu Kur' calistir.";
            }

            EditorSceneManager.OpenScene(MenuSceneGenerator.MenuScenePath, OpenSceneMode.Single);

            GameObject host = GameObject.Find("NetworkManager");

            if (host == null)
            {
                return "[Steam] Menu sahnesinde 'NetworkManager' yok. " +
                       "'Bunker/Menu/Ana Menuyu Kur' calistir.";
            }

            var transport = host.GetComponent(fizzyType) as Transport;

            if (transport == null)
            {
                transport = host.AddComponent(fizzyType) as Transport;
                Undo.RegisterCreatedObjectUndo(host, "Steam tasimasi");
            }

            var manager = host.GetComponent<NetworkManager>();

            if (manager == null) return "[Steam] NetworkManager bileseni yok.";

            var serialized = new SerializedObject(manager);
            serialized.FindProperty("transport").objectReferenceValue = transport;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string appIdNote = WriteAppIdField(transport);

            // Lobi bileseni AYNI nesnede: NetworkManager sahneler arasi yasiyor
            // (DontDestroyOnLoad), yani lobi de oyun sahnesine gecerken hayatta kalir.
            // Ayri bir nesnede olsaydi menuden oyuna gecisde yok olur ve davet
            // sessizce calismazdi.
            if (host.GetComponent<SteamLobby>() == null) host.AddComponent<SteamLobby>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            return $"[Steam] TAMAM: {fizzyType.Name} menu sahnesine baglandi. {appIdNote}\n" +
                   "KCP bileseni SILINMEDI - Steam kapaliyken adresle katilma yolu duruyor.\n" +
                   "Simdi: Steam acikken oyunu iki makinede calistir, birinde 'ODA AC' de.";
        }

        private static string WriteAppIdField(Transport transport)
        {
            var serialized = new SerializedObject(transport);
            SerializedProperty property = serialized.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.name.IndexOf("appid", StringComparison.OrdinalIgnoreCase) < 0) continue;

                switch (property.propertyType)
                {
                    case SerializedPropertyType.String:
                        property.stringValue = DevelopmentAppId;
                        break;

                    case SerializedPropertyType.Integer:
                        property.intValue = int.Parse(DevelopmentAppId);
                        break;

                    default:
                        continue;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                return $"App ID alani ('{property.name}') {DevelopmentAppId} yazildi.";
            }

            return "App ID alani bulunamadi - tasimanin Inspector'unda elle yaz (480).";
        }

        private static string Line(string label, bool ok, string detail) =>
            $"  [{(ok ? "TAMAM" : "EKSIK")}] {label}  -  {detail}";
    }
}
