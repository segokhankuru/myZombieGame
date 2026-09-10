using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// Paketlerdeki <b>bütün parçaları tek bir alana dizer</b>, çoğaltılmaya hazır.
    /// 2026-09-08.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"haritayı elimle düzenledim, bunu
    /// bozma. Sadece bir alana assetleri ekle ve bu assetleri kendim çoğaltıp haritayı
    /// daha canlı hâle getireyim — eklenmemiş assetler var, nasıl ekleyeceğimi
    /// bilemediğimden sen hepsini ekle, ben çoğaltarak kullanayım."</i></para>
    ///
    /// <para><b>Bu bir dekorasyon aracı değil, bir MALZEME RAFI.</b> Haritaya hiç
    /// dokunmuyor; oyun alanının uzağında, düzenli bir ızgarada her prefab'dan birer
    /// tane duruyor. Sen beğendiğini <c>Ctrl+D</c> ile çoğaltıp haritaya
    /// sürüklüyorsun.</para>
    ///
    /// <para><b>Neden sahnede, Project penceresinde değil:</b> Project'te bir prefab'ın
    /// adı var ama <i>neye benzediği</i> yok — 55 parçayı tek tek tıklayıp önizlemek,
    /// tam da "nasıl ekleyeceğimi bilemedim" cümlesinin sebebi. Sahnede yan yana
    /// durunca hepsini bir bakışta görüyorsun ve seçmek bir tıklama.</para>
    ///
    /// <para><b>Oyunu etkilemez:</b> raf <c>NavMesh</c>'in dışında, çarpıştırıcıları
    /// sökülmüş ve oyun alanından 200 m uzakta. Yanlışlıkla açık kalırsa bile
    /// zombiler oraya gitmez, oyuncu oraya düşmez.</para>
    ///
    /// <para><b>Idempotent</b> (editor-tools.md): iki kez çalıştırmak rafı çoğaltmaz,
    /// eskisini silip yeniden dizer. <b>Ama senin haritaya sürüklediğin kopyalara
    /// dokunmaz</b> — onlar rafın çocuğu değil.</para>
    /// </summary>
    public static class AssetPalette
    {
        /// <summary>Rafın kök adı. <c>EditorShelfStripper</c> build'de bununla bulur.</summary>
        internal const string RootName = "_AssetPalette (cogaltmak icin)";

        /// <summary>Rafın oyun alanından uzaklığı (metre).</summary>
        private const float OriginX = -200f;

        /// <summary>Izgara adımı. Parçaların en genişi bunun altında kalmalı.</summary>
        private const float Spacing = 9f;

        /// <summary>Bir satırda kaç parça.</summary>
        private const int Columns = 8;

        /// <summary>Prefab aranacak paket klasörleri.</summary>
        private static readonly string[] SourceFolders =
        {
            "Assets/The Wasteland LITE/Prefabs"
        };

        /// <summary>
        /// Komut satırı girişi. <b>Kendisi çıkar</b> — <c>unity-exec.ps1</c>
        /// <c>-quit</c> geçmiyor; çıkmayan bir toplu metot Unity'yi sonsuza kadar açık
        /// bırakır ve proje kilidi bir daha açılmaz.
        /// </summary>
        public static void BuildBatch()
        {
            const string scenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                Build();

                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Asset rafi] Toplu kosu basarisiz: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Bunker/Gorunum/Asset Rafini Kur (cogaltmak icin)", false, 122)]
        public static void Build()
        {
            var prefabs = new List<GameObject>(64);

            foreach (string folder in SourceFolders)
            {
                if (!Directory.Exists(folder))
                {
                    Debug.LogWarning($"[Asset rafi] Klasor yok: {folder}");
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab != null) prefabs.Add(prefab);
                }
            }

            if (prefabs.Count == 0)
            {
                Debug.LogError("[Asset rafi] Hic prefab bulunamadi. Paketler ice " +
                               "aktarilmis mi?");
                return;
            }

            // Ad sirasi: ayni paket her calistirmada AYNI duzende dizilsin. Rastgele
            // bir sira, "gecen sefer sagdaydi" diye aranan bir parca demek olurdu.
            prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            GameObject existing = GameObject.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Asset rafi");

            // EditorOnly: Unity bu etiketli nesneyi build'e HIC koymaz (2026-09-10,
            // "kullanilmayan objeler build'in icinde olmamali"). EditorShelfStripper
            // etiketsiz eski raflari da build aninda cikariyor; bu satir yenisini en
            // bastan dogru kuruyor.
            root.tag = "EditorOnly";
            root.transform.position = new Vector3(OriginX, 0f, 0f);

            int placed = 0;

            for (int i = 0; i < prefabs.Count; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i],
                                                                          root.transform);
                if (instance == null) continue;

                int column = i % Columns;
                int row = i / Columns;

                instance.transform.localPosition = new Vector3(column * Spacing, 0f,
                                                               row * Spacing);
                instance.transform.localRotation = Quaternion.identity;

                // PREFAB BAGI KORUNUR (dekordan farkli olarak): sen bunu cogaltip
                // haritaya koyacaksin ve prefab'a bagli kalmasi, paket guncellenirse
                // hepsinin birden guncellenmesi demek. Dekorda bagi koparmistik cunku
                // orada bilesen siliyorduk; burada silmiyoruz.
                MakeInert(instance);
                placed++;
            }

            Selection.activeGameObject = root;

            Debug.Log(
                $"[Asset rafi] {placed} parca dizildi: '{RootName}' (x={OriginX:F0}).\n" +
                "  Kullanim: raftan bir parcayi sec, Ctrl+D ile cogalt, haritaya surukle.\n" +
                "  Raf oyunu etkilemez (NavMesh disi, carpistiricisiz, 200 m uzakta).\n" +
                "  Isin bitince rafi silebilirsin; menuden tekrar kurulur.");
        }

        /// <summary>
        /// Raftaki parçayı <b>oyuna görünmez</b> kılar: NavMesh'e girmez, çarpışmaz.
        ///
        /// <para><b>Neden şart:</b> raf sahnede duruyor ve NavMesh bake'i bütün sahneyi
        /// tarar. Statik işaretli 55 parça, oyun alanının 200 m uzağında ikinci bir
        /// NavMesh adası üretir — zombiler oraya yol bulmaya çalışır ve "zombiler
        /// gelmiyor" diye okunan bir hataya döner.</para>
        ///
        /// <para><b>Çarpıştırıcılar bileşen olarak KAPATILIYOR, silinmiyor:</b> prefab
        /// bağı korunduğu için silme bir "kaldırılmış bileşen" geçersizi (override)
        /// yazardı ve sen çoğaltıp haritaya koyduğunda parça çarpışmasız kalırdı.
        /// Kapatmak, çoğaltılan kopyada <c>enabled</c>'ı geri açmakla düzelir.</para>
        /// </summary>
        private static void MakeInert(GameObject instance)
        {
            GameObjectUtility.SetStaticEditorFlags(instance, (StaticEditorFlags)0);

            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, (StaticEditorFlags)0);
            }

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            // Materyaller URP'ye cevrilir: raf pembe gorunurse hangi parcanin ne
            // oldugunu secmek imkansizlasir - rafin tek isi SECTIRMEK.
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                var converted = new Material[shared.Length];

                for (int i = 0; i < shared.Length; i++)
                {
                    converted[i] = ArtIntegration.ToUrpPublic(shared[i]);
                }

                renderer.sharedMaterials = converted;
            }
        }
    }
}
