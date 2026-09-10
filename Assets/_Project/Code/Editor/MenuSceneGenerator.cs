using System;
using System.Collections.Generic;
using System.IO;
using Bunker.Net;
using Bunker.UI;
using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bunker.Editor
{
    /// <summary>
    /// Ana menü sahnesini üretir ve derleme sahne listesini kurar. M-04.
    ///
    /// <para><b>Neden bir araç, elle kurulmuş bir sahne değil</b> (editor-tools.md):
    /// menü sahnesi bir <c>NetworkManager</c>, bir taşıma bileşeni, bir oyuncu prefab'ı
    /// referansı ve iki sahne yolu taşıyor. Elle kurulmuş hâlinde bunlardan biri
    /// kopduğunda hata <b>sessiz</b> olur — menüdeki "OYNA" hiçbir şey yapmaz ve
    /// sebebi YAML'ın içinde saklanır.</para>
    ///
    /// <para><b>İki kez çalıştırmak bir kez çalıştırmakla aynı sonucu verir.</b> Var
    /// olan sahne yeniden yaratılmaz; eksik parçalar tamamlanır, doğru olanlara
    /// dokunulmaz.</para>
    /// </summary>
    public static class MenuSceneGenerator
    {
        public const string MenuScenePath = "Assets/_Project/Scenes/Menu/Menu.unity";
        public const string GameScenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Gameplay/Player.prefab";

        [MenuItem("Bunker/Menu/Ana Menuyu Kur", false, 10)]
        public static void SetupMenu()
        {
            if (Build())
            {
                Debug.Log("[Menu] Ana menu hazir. Derleme sahne listesinde Menu ILK sirada; " +
                          "Play'e menu sahnesinden bas.");
            }
        }

        /// <summary>Başsız giriş: <c>-executeMethod</c> için.</summary>
        public static void SetupMenuBatch() => EditorApplication.Exit(Build() ? 0 : 1);

        private static bool Build()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (playerPrefab == null)
            {
                // Yuksek sesle, duzeltmesi mesajin icinde (editor-tools.md).
                Debug.LogError($"[Menu] Oyuncu prefab'i yok: {PlayerPrefabPath}. " +
                               "Once 'Bunker/Zombi/Test Alanini Kur' calistir.");
                return false;
            }

            // OYUN SAHNESI ONCE (2026-09-10): bu adim sahne actigi icin en sonda
            // menu sahnesi acik kalmali - araci calistiran kisi Play'e basmaya hazir
            // olsun.
            EnsureGameSceneReturnsToMenu();

            Directory.CreateDirectory(Path.GetDirectoryName(MenuScenePath) ?? ".");

            Scene scene;
            bool created = !File.Exists(MenuScenePath);

            if (created)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            EnsureCamera();
            EnsureMenuHost();
            EnsureNetworkManager(playerPrefab);

            EditorSceneManager.MarkSceneDirty(scene);

            if (created) EditorSceneManager.SaveScene(scene, MenuScenePath);
            else EditorSceneManager.SaveOpenScenes();

            EnsureBuildSettings();

            return true;
        }

        // ---------------------------------------------------------------- parcalar

        /// <summary>
        /// Menünün kamerası. <b>Olmadan ekran "no cameras rendering" der</b> ve IMGUI
        /// çizilse bile arka plan bir hata mesajı olur.
        /// </summary>
        private static void EnsureCamera()
        {
            GameObject host = GameObject.Find("MenuCamera");

            if (host == null)
            {
                host = new GameObject("MenuCamera");
                Undo.RegisterCreatedObjectUndo(host, "Menu kamerasi");
            }

            var camera = host.GetComponent<Camera>();
            if (camera == null) camera = host.AddComponent<Camera>();

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            camera.orthographic = true;

            // Menude 3B bir sey yok: kamera hicbir katmani cizmesin, bos bir sahneyi
            // her kare taramanin bedeli sifir olmali.
            camera.cullingMask = 0;

            // MainCamera ETIKETI SART (2026-09-06): oyundan menuye donuldugunde oyuncu
            // ve kamerasi yok olur; etiketsiz bir menu kamerasi Camera.main'i null
            // birakir ve ona guvenen her sey sessizce calismaz. Bu projede ayni sey
            // bir kez zombi can barlarinda yasandi.
            if (!host.CompareTag("MainCamera")) host.tag = "MainCamera";

            if (host.GetComponent<AudioListener>() == null) host.AddComponent<AudioListener>();
        }

        private static void EnsureMenuHost()
        {
            GameObject host = GameObject.Find("MainMenu");

            if (host == null)
            {
                host = new GameObject("MainMenu");
                Undo.RegisterCreatedObjectUndo(host, "Ana menu");
            }

            if (host.GetComponent<MainMenu>() == null) host.AddComponent<MainMenu>();

            // MENU MUZIGI (2026-09-09, gelistirici: "Mystical Music Loops sesini ana
            // menude cal... oyuna katilinca bu ses calmasin, sadece menu muzigi
            // olacak"). playMusic ACIK yalnizca burada; oyun sahnesindeki ayni bilesen
            // kapali ve muzigi ayrica SUSTURUYOR - yani durdurma isi menuden cikan
            // koda degil, oyun sahnesinin kendisine ait.
            var audio = host.GetComponent<Bunker.Audio.AudioBootstrap>();
            if (audio == null) audio = host.AddComponent<Bunker.Audio.AudioBootstrap>();

            const string catalogPath = "Assets/_Project/Config/audio.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<Bunker.Audio.AudioCatalogAsset>(catalogPath);

            if (catalog == null)
            {
                Debug.LogWarning($"[Menu] Ses katalogu yok: {catalogPath} - menude " +
                                 "muzik CALMAYACAK. 'Bunker/Gorunum/Ses Dosyalarini " +
                                 "Bagla' calistir.");
            }

            SetPrivateField(audio, "catalog", catalog);
            SetPrivateField(audio, "playMusic", true);
        }

        /// <summary>
        /// Serileşmiş özel bir alanı yazar. <c>SerializedObject</c> üzerinden, çünkü
        /// doğrudan atama <c>private</c> alanlara ulaşamaz ve prefab/sahne kaydını
        /// kirletmez.
        /// </summary>
        private static void SetPrivateField(UnityEngine.Object target, string field, object value)
        {
            if (target == null) return;

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"[Menu] '{target.GetType().Name}' uzerinde '{field}' " +
                                 "alani yok - alan adi degismis olabilir.");
                return;
            }

            switch (value)
            {
                case bool b: property.boolValue = b; break;
                case UnityEngine.Object o: property.objectReferenceValue = o; break;
                case null: property.objectReferenceValue = null; break;
                default: return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// Menü sahnesinin <c>NetworkManager</c>'ı.
        ///
        /// <para><b>Oturumun sahibi budur:</b> Mirror onu sahneler arası yaşatır
        /// (<c>DontDestroyOnLoad</c>), oyun sahnesindeki ikinci manager kendini yok
        /// eder. Sandbox sahnesindeki manager bilerek duruyor — o sahneye doğrudan Play
        /// basmak, menüden geçmeden test etmenin yolu.</para>
        ///
        /// <para><b><c>autoStartSolo</c> burada KAPALI.</b> Açık olsaydı menü açılır
        /// açılmaz oyuna girerdi — düzeltilmek istenen davranışın ta kendisi.</para>
        /// </summary>
        private static void EnsureNetworkManager(GameObject playerPrefab)
        {
            GameObject host = GameObject.Find("NetworkManager");

            if (host == null)
            {
                host = new GameObject("NetworkManager");
                Undo.RegisterCreatedObjectUndo(host, "Ag yoneticisi");
            }

            // KCP HER ZAMAN DURUR: Steam kapaliyken adresle katilma yolu bu.
            var kcp = host.GetComponent<KcpTransport>();
            if (kcp == null) kcp = host.AddComponent<KcpTransport>();

            // ...AMA VARSA STEAM TASIMASI KORUNUR (2026-09-09).
            //
            // <b>Duzeltilen mayin:</b> bu metot `transport` alanina KOSULSUZ KcpTransport
            // yaziyordu. `SteamSetupCheck` menu sahnesine FizzyFacepunch'i baglayip
            // aktif tasima yapiyor; ondan SONRA 'Ana Menuyu Kur' calistiran herkes
            // Steam'i sessizce kapatiyordu. Hicbir hata cikmaz, davet dugmesi
            // gorunmez, katilma kodu KCP'ye adres diye gider ve "baglanilamadi" der -
            // yani iki Steam yolu birden, sebebi soylenmeden olur.
            //
            // `EnsureUsableTransport` bunu yakalayamaz: KCP her zaman Available()
            // doner, yani geri dusme mantigi hic devreye girmez.
            //
            // Tip YANSIMAYLA araniyor: FizzyFacepunch ucuncu partiden elle indiriliyor
            // ve `Bunker.Editor` ona referans vermiyor (SteamSetupCheck ile ayni desen).
            Transport transport = FindSteamTransport(host) ?? kcp;

            var manager = host.GetComponent<BunkerNetworkManager>();
            if (manager == null) manager = host.AddComponent<BunkerNetworkManager>();

            // Lobi denetleyicisi AYNI nesnede: NetworkManager sahneler arasi yasiyor,
            // yani lobi de oyuna gecerken hayatta kalir ve oyundan menuye donuldugunde
            // hala oradadir.
            if (host.GetComponent<LobbyController>() == null) host.AddComponent<LobbyController>();

            var serialized = new SerializedObject(manager);

            serialized.FindProperty("autoStartSolo").boolValue = false;
            serialized.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            serialized.FindProperty("transport").objectReferenceValue = transport;
            serialized.FindProperty("offlineScene").stringValue = MenuScenePath;
            serialized.FindProperty("dontDestroyOnLoad").boolValue = true;
            serialized.FindProperty("gameScene").stringValue = GameScenePath;

            // onlineScene BOS BIRAKILIR (2026-09-05). Doluyken Mirror, host olur olmaz
            // oyun sahnesine geciyordu - yani "oda ac" demek dogrudan oyuna dusmek
            // demekti ve lobi hic gorunmuyordu. Sahneyi artik lobi 'BASLAT' dedigi an
            // sunucu degistiriyor (BunkerNetworkManager.ServerStartGame).
            serialized.FindProperty("onlineScene").stringValue = string.Empty;

            // Oyuncu LOBIDE yaratilmaz: menu sahnesinin ortasina birinci sahis
            // karakterler koymak olurdu. Oyun sahnesine gecince OnServerReady yaratir.
            serialized.FindProperty("autoCreatePlayer").boolValue = false;

            // Dort oyuncu (GDD). Mirror varsayilani cok daha yuksek ve o sayi hicbir
            // yerde karar verilmis degil.
            SerializedProperty maxConnections = serialized.FindProperty("maxConnections");
            if (maxConnections != null) maxConnections.intValue = 4;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Hangi tasimanin secildigi SOYLENIR. Sessiz kalsaydi Steam'in kapandigi
            // an, ancak iki makineli bir testte fark edilirdi - ve orada teshis pahali.
            Debug.Log($"[Menu] Aktif tasima: {transport.GetType().Name}" +
                      (transport is KcpTransport
                          ? "  (Steam tasimasi yok - davet kapali. " +
                            "'Bunker/Steam/Kurulumu Kontrol Et' bagliyor.)"
                          : "  (Steam daveti acik; KCP yedek olarak duruyor.)"));

            EditorUtility.SetDirty(manager);
        }

        /// <summary>
        /// Sahnedeki Steam taşımasını bulur; yoksa <c>null</c>.
        ///
        /// <para><b>Yansımayla</b>, çünkü FizzyFacepunch üçüncü partiden elle
        /// indiriliyor ve projede olmayabilir — <c>Bunker.Editor</c>'ün asmdef'ine
        /// referans eklemek, paket yokken bütün editör derlemesini düşürürdü.</para>
        /// </summary>
        private static Transport FindSteamTransport(GameObject host)
        {
            foreach (Transport candidate in host.GetComponents<Transport>())
            {
                if (candidate is KcpTransport) continue;

                // Ad kontrolu: nesnede baska bir tasima daha olsa (ornegin bir test
                // tasimasi) onu Steam sanmayalim.
                if (candidate.GetType().Name.IndexOf("Fizzy", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        // Play'in menuden baslamasi PlayModeStartScene.cs'te (InitializeOnLoad):
        // o ayar editor OTURUMUNA ait ve bassiz bir kurulumdan gelistiricinin
        // editorune gecmiyor (2026-09-06 hatasi).

        /// <summary>
        /// Derleme sahne listesi: <b>menü ilk sırada</b>.
        ///
        /// <para>Sıra önemli: Unity açılışta listenin ilk sahnesini yükler. Sandbox ilk
        /// sırada kalsaydı, derlenen oyun menüyü hiç görmeden oyuna düşerdi — menü
        /// yazılmış ama hiç açılmayan bir sahne olurdu.</para>
        /// </summary>
        /// <summary>
        /// Oyun sahnesindeki <c>NetworkManager</c>'a <b>menüye dönüş yolunu</b> öğretir
        /// (2026-09-10, oyun testi: <i>"ana menü açılmıyor"</i> — iki belirti birden).
        ///
        /// <para><b>Belirti 1: Play doğrudan oyuna düşüyordu.</b> Derleme sahne
        /// listesinin başında oyun sahnesi duruyordu; <see cref="EnsureBuildSettings"/>
        /// bunu zaten menü lehine düzeltiyor ama araç bir süredir çalıştırılmamıştı.
        /// Liste elle sürüklenebildiği sürece bu belirti geri gelir — o yüzden düzeltme
        /// araca ait, hafızaya değil.</para>
        ///
        /// <para><b>Belirti 2: "ANA MENÜYE DÖN" hiçbir şey yapmıyordu.</b> Ayrılma
        /// isteği oturumu kapatıyor (<c>StopHost</c>) ve menüye dönüşü Mirror'ın
        /// <c>offlineScene</c>'i yapıyor. O alan yalnızca <b>menü sahnesindeki</b>
        /// yöneticide doluydu. Oyun sahnesinden başlatan oyuncu (Editor'de Play,
        /// ya da doğrudan bu sahneye düşen bir oturum) o yöneticiyi hiç görmüyor:
        /// oturum kapanıyor, sahne değişmiyor, ekranda hiçbir şey olmuyor.</para>
        ///
        /// <para><b>İki yöneticinin de aynı cümleyi kurması gerekiyor</b> — çünkü
        /// hangisinin hayatta olduğu, oyunun nereden başlatıldığına bağlı ve bu bir
        /// çalışma anı sorusu. Aynı değeri iki yerde tutmak burada bir kopya değil:
        /// tek kaynak <see cref="MenuScenePath"/> sabiti, ikisi de onu yazıyor.</para>
        /// </summary>
        private static void EnsureGameSceneReturnsToMenu()
        {
            if (!File.Exists(GameScenePath))
            {
                Debug.LogWarning($"[Menu] Oyun sahnesi yok: {GameScenePath} - menuye " +
                                 "donus yolu kurulamadi.");
                return;
            }

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var manager = UnityEngine.Object.FindFirstObjectByType<BunkerNetworkManager>();

            if (manager == null)
            {
                Debug.LogWarning("[Menu] Oyun sahnesinde NetworkManager yok - menuye " +
                                 "donus yolu kurulamadi. 'Bunker/Zombi/Test Alanini Kur' " +
                                 "calistir.");
                return;
            }

            var serialized = new SerializedObject(manager);

            // autoStartSolo KAPALI (2026-09-10, gelistirici istegi).
            //
            // Bu bayrak bir gelistirici kisayoluydu: oyun sahnesinden Play'e basinca
            // menuyu atlayip dogrudan solo oturum aciyordu. Ama menu artik oyunun
            // gercek girisi (lobi, ayarlar, davet oradan geciyor) ve bayrak, menuyu
            // ATLANABILIR bir sey yapiyordu - "ana menu acilmiyor"un ikinci sebebi.
            //
            // Kapali oldugunda M0-Sandbox'tan Play'e basmak BOS bir sahne verir
            // (oturum yok, oyuncu yok). Bu bir hata degil, bilgi: giris Menu.unity.
            SerializedProperty autoSolo = serialized.FindProperty("autoStartSolo");

            SerializedProperty offline = serialized.FindProperty("offlineScene");
            SerializedProperty gameScene = serialized.FindProperty("gameScene");

            bool changed = false;

            if (autoSolo != null && autoSolo.boolValue)
            {
                autoSolo.boolValue = false;
                changed = true;

                Debug.Log("[Menu] Oyun sahnesinde autoStartSolo KAPATILDI - giris artik " +
                          "her zaman Menu.unity.");
            }

            if (offline != null && offline.stringValue != MenuScenePath)
            {
                offline.stringValue = MenuScenePath;
                changed = true;
            }

            if (gameScene != null && gameScene.stringValue != GameScenePath)
            {
                gameScene.stringValue = GameScenePath;
                changed = true;
            }

            // ZATEN DOGRUYSA SAHNE KIRLETILMEZ (editor-tools.md): 3 MB'lik bir sahneyi
            // her calistirmada yeniden kaydetmek, hicbir seyi degistirmeyen bir diff
            // uretir ve o diff bir sonraki gercek degisikligi gizler.
            if (!changed)
            {
                Debug.Log("[Menu] Oyun sahnesi zaten menuye donuyor - degisiklik yok.");
                return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);

            Debug.Log($"[Menu] Oyun sahnesindeki NetworkManager artik menuye donuyor: " +
                      $"offlineScene = {MenuScenePath}");
        }

        private static void EnsureBuildSettings()
        {
            var wanted = new List<string> { MenuScenePath, GameScenePath };
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (string path in wanted)
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[Menu] Sahne bulunamadi, listeye eklenmedi: {path}");
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            // Listedeki diger sahneler KORUNUR (sonda): birinin test sahnesini sessizce
            // silmek, bir sonraki derlemede "sahne yok" hatasi olarak geri gelir.
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (wanted.Contains(existing.path)) continue;
                scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
