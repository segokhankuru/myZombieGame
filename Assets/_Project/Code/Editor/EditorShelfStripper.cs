using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bunker.Editor
{
    /// <summary>
    /// Sahnedeki <b>editör raflarını build'den çıkarır</b>. 2026-09-10.
    ///
    /// <para><b>Neden</b> (geliştirici: <i>"build alırken kullanılmayan objeler vs
    /// build'in içinde olmamalı"</i>): oyun sahnesinde <c>_AssetPalette (cogaltmak
    /// icin)</c> duruyor — Wasteland paketinin bütün prefab'larından birer tane,
    /// haritanın 200 m uzağında, yalnızca editörde Ctrl+D ile çoğaltılmak için. Oyunda
    /// hiç görünmüyor ama sahnede olduğu için build'e giriyordu: mesh'leri, malzemeleri
    /// ve dokularıyla birlikte.</para>
    ///
    /// <para><b>Neden silmek yerine build anında çıkarmak:</b> raf geliştiricinin
    /// kendi çalışma aracı (<i>"bu assetleri kendim çoğaltıp haritayı canlı hâle
    /// getireyim"</i>). Sahneden silmek onu elinden alırdı; build'e girmemesi yeter.</para>
    ///
    /// <para><b>Yalnızca build'de</b>: Play'e basıldığında da çağrılır ama o zaman
    /// <c>report</c> boştur ve hiçbir şeye dokunulmaz. Sahne dosyası değişmez — işlem
    /// build'in kopyasında olur.</para>
    /// </summary>
    public sealed class EditorShelfStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;   // Play modu: raf editorde kalsin

            int removed = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != AssetPalette.RootName) continue;

                int parts = root.transform.childCount;
                Object.DestroyImmediate(root);
                removed++;

                Debug.Log($"[Build] {scene.name}: '{AssetPalette.RootName}' build'den " +
                          $"cikarildi ({parts} parca). Sahne dosyasi degismedi.");
            }

            if (removed == 0) return;
        }
    }
}
