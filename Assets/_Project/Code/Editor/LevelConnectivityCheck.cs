using System;
using System.Text;
using Bunker.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Bunker.Editor
{
    /// <summary>
    /// NavMesh'in <b>gerçekten</b> bağlı olup olmadığını ölçer: pencereden içeri,
    /// içeriden oyuncuya, aşağıdan yukarı, yukarıdan aşağı.
    ///
    /// <para><b>Neden bir araç:</b> LVL-01 spec'i "bake sonrası döngüyü doğrula" diyor
    /// ve bunun elle yolu, Scene görünümünde mavi katmana bakıp göz kararı karar
    /// vermek. Göz kararı, rampanın üstünden geçen bir duvarı ya da iki metrelik bir
    /// kopukluğu yakalamaz — <c>NavMesh.CalculatePath</c> yakalar. Kopukluğun bedeli
    /// oyun içinde "zombiler yukarı gelmiyor" olarak ödeniyor, ve orada teşhisi
    /// pahalı.</para>
    /// </summary>
    public static class LevelConnectivityCheck
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/M0-Sandbox.unity";

        [MenuItem("Bunker/Level/NavMesh Baglanti Kontrolu", false, 110)]
        public static void CheckFromMenu() => Run();

        /// <summary>Komut satırı girişi. Kopukluk varsa sıfırdan farklı çıkar.</summary>
        public static void CheckBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.Exit(Run() ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Baglanti] Kontrol calistirilamadi: {e}");
                EditorApplication.Exit(2);
            }
        }

        /// <summary>Her yol tamamsa true.</summary>
        public static bool Run()
        {
            GameObject playerSpawn = GameObject.Find("PlayerSpawn");
            GameObject upstairs = GameObject.Find("MysteryBox");
            WindowEntry[] windows = UnityEngine.Object.FindObjectsByType<WindowEntry>(
                FindObjectsSortMode.None);

            if (playerSpawn == null)
            {
                Debug.LogError("[Baglanti] 'PlayerSpawn' isareti yok. Gri kutu uretilmemis olabilir.");
                return false;
            }

            var report = new StringBuilder(1024);
            report.Append("[Baglanti] NAVMESH BAGLANTI KONTROLU\n");

            bool allOk = true;

            if (!TrySnap(playerSpawn.transform.position, out Vector3 ground, 3f))
            {
                Debug.LogError("[Baglanti] Oyuncu dogum noktasinin altinda NavMesh yok. " +
                               "Bake edilmemis olabilir: 'Bunker/Zombi/NavMesh Bake'.");
                return false;
            }

            // --- ust kat: rampanin gercekten yurunebilir olmasi
            if (upstairs != null && TrySnap(upstairs.transform.position, out Vector3 upper, 4f))
            {
                allOk &= Line(report, "zemin -> ust kat (rampa)", ground, upper);
                allOk &= Line(report, "ust kat -> zemin", upper, ground);
            }
            else
            {
                report.Append("  ust kat      : ISARET/NAVMESH YOK - rampa dogrulanamadi\n");
                allOk = false;
            }

            // --- pencereler: disarisi ve iceri ayri adalar olmali (aralarini zombi
            //     tirmanarak gecer), ama HER IKISI de var olmali.
            int outsideMissing = 0, insideMissing = 0, insideUnreachable = 0;

            for (int i = 0; i < windows.Length; i++)
            {
                WindowEntry w = windows[i];

                if (!TrySnap(w.OutsidePoint, out Vector3 _, 2f)) { outsideMissing++; continue; }

                if (!TrySnap(w.InsidePoint, out Vector3 inside, 2f)) { insideMissing++; continue; }

                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(inside, ground, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    insideUnreachable++;
                }
            }

            report.Append($"  pencere      : {windows.Length} giris\n");
            report.Append($"    disarida NavMesh yok : {outsideMissing}\n");
            report.Append($"    iceride NavMesh yok  : {insideMissing}\n");
            report.Append($"    icerden oyuncuya yol yok : {insideUnreachable}\n");

            if (outsideMissing > 0 || insideMissing > 0 || insideUnreachable > 0) allOk = false;

            report.Append(allOk
                ? "  SONUC: BAGLI - zombiler her yere ulasabilir."
                : "  SONUC: KOPUK - yukaridaki sifir olmayan satirlar duzeltilmeli.");

            if (allOk) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());

            return allOk;
        }

        private static bool Line(StringBuilder report, string label, Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            bool found = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);

            string status = !found ? "YOL YOK"
                : path.status == NavMeshPathStatus.PathComplete ? "tam"
                : path.status == NavMeshPathStatus.PathPartial ? "YARIM (kopuk)"
                : "GECERSIZ";

            report.Append("  ").Append(label.PadRight(26)).Append(": ").Append(status);

            if (found && path.status == NavMeshPathStatus.PathComplete)
            {
                report.Append("  (").Append(Length(path).ToString("F1")).Append(" m)");
            }

            report.Append('\n');
            return found && path.status == NavMeshPathStatus.PathComplete;
        }

        private static float Length(NavMeshPath path)
        {
            float total = 0f;
            Vector3[] corners = path.corners;

            for (int i = 1; i < corners.Length; i++)
            {
                total += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return total;
        }

        private static bool TrySnap(Vector3 point, out Vector3 snapped, float radius)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                snapped = hit.position;
                return true;
            }

            snapped = point;
            return false;
        }
    }
}
