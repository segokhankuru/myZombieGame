using System.Collections.Generic;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Birinci şahıs <b>kol ve el mesh'ini üretir</b>. 2026-09-08.
    ///
    /// <para><b>Neden gerekti</b> (geliştirici): <i>"fps görünümüne eklediğin el kol
    /// bok gibi kutular eklemişsin, şöyle adam akıllı el kol ekle, kutuya benzemesin,
    /// polygon olsun yani çekinme."</i> Haklı: kutulardan yapılmış bir kol, silahı bir
    /// bedene bağlamak yerine ekrana ikinci bir arayüz öğesi ekliyordu.</para>
    ///
    /// <para><b>Neden üretiliyor, modellenmiş bir varlık değil:</b> elle modellenmiş
    /// bir çift FPS eli iskelet, ağırlık boyaması ve tutuş pozları demek — ve sanat
    /// yönü hâlâ kilitlenmedi. Üretilen mesh <i>parametrik</i>: kol kalınlığı, parmak
    /// sayısı ve büküm açıları birer sayı, yani sanat geldiğinde değiştirilecek tek yer
    /// hâlâ burası.</para>
    ///
    /// <para><b>Silüet önce, ayrıntı sonra:</b> kol bir <b>daralan boru</b> (omuzdan
    /// bileğe incelir), el bir avuç kütlesi ve dört parmak + başparmak. Yumuşak
    /// normaller kullanılıyor — düşük poligonlu bir borunun kutuya benzememesinin tek
    /// sebebi normallerin köşede kırılmaması.</para>
    ///
    /// <para><b>Tahsis bir kez</b> (csharp-code.md): mesh el modeli kurulurken bir kez
    /// üretilir ve paylaşılır. Kare başına mesh üretmek bir çöp toplama fırtınasıdır.</para>
    /// </summary>
    public static class ArmMesh
    {
        /// <summary>Borunun kaç kenarı olacağı. 8 yeterince yuvarlak, 8 üçgen ucuz.</summary>
        private const int Sides = 8;

        private static Mesh _armCached;
        private static Mesh _handCached;

        /// <summary>
        /// Ön kol: omuzdan bileğe <b>daralan</b> bir boru.
        ///
        /// <para>Uzunluk 1 birimdir; ölçeği çağıran verir. Böylece sağ ve sol kol aynı
        /// mesh'i paylaşır — iki ayrı mesh, iki ayrı çizim çağrısı demek olurdu.</para>
        /// </summary>
        public static Mesh Arm()
        {
            if (_armCached != null) return _armCached;

            _armCached = BuildTaperedTube(
                length: 1f,
                startRadius: 0.038f,
                endRadius: 0.029f,
                name: "mesh_fps_arm");

            return _armCached;
        }

        /// <summary>
        /// El: avuç + dört parmak + başparmak. <b>Kavramış duruşta</b> üretilir —
        /// parmaklar açık bir el, silahı tutmuyor <i>gösterir</i>.
        /// </summary>
        public static Mesh Hand()
        {
            if (_handCached != null) return _handCached;

            var vertices = new List<Vector3>(256);
            var triangles = new List<int>(768);

            // AVUC YASSI (2026-09-08, onizlemede olculdu): ilk surum yuvarlak bir
            // boruydu ve el degil TOP gibi okunuyordu. Gercek bir avuc genis ve ince;
            // yassilastirma orani 0.45, yani kalinligi genisliginin yarisindan az.
            AppendTaperedTube(vertices, triangles,
                              from: new Vector3(0f, 0f, 0f),
                              to: new Vector3(0f, 0f, 0.085f),
                              startRadius: 0.041f,
                              endRadius: 0.037f,
                              flatten: 0.45f);

            // Dort parmak: avucun ucundan asagi-ileri, kavramis gibi bukuk.
            for (int i = 0; i < 4; i++)
            {
                float x = -0.024f + i * 0.016f;

                Vector3 knuckle = new Vector3(x, -0.006f, 0.085f);
                Vector3 middle = knuckle + new Vector3(0f, -0.026f, 0.020f);
                Vector3 tip = middle + new Vector3(0f, -0.020f, -0.012f);

                AppendTaperedTube(vertices, triangles, knuckle, middle, 0.0105f, 0.0095f);
                AppendTaperedTube(vertices, triangles, middle, tip, 0.0095f, 0.0080f);
            }

            // Basparmak: DIGERLERININ TERSINE, karsidan kavriyor. Bir eli el yapan
            // sey bu - dort paralel parmak bir tarak gibi okunur.
            Vector3 thumbBase = new Vector3(0.032f, -0.004f, 0.036f);
            Vector3 thumbMid = thumbBase + new Vector3(0.006f, -0.014f, 0.030f);
            Vector3 thumbTip = thumbMid + new Vector3(-0.008f, -0.010f, 0.022f);

            AppendTaperedTube(vertices, triangles, thumbBase, thumbMid, 0.0125f, 0.0105f);
            AppendTaperedTube(vertices, triangles, thumbMid, thumbTip, 0.0105f, 0.0085f);

            _handCached = Finish(vertices, triangles, "mesh_fps_hand");
            return _handCached;
        }

        // ------------------------------------------------------------- uretim

        private static Mesh BuildTaperedTube(float length, float startRadius,
                                             float endRadius, string name)
        {
            var vertices = new List<Vector3>(64);
            var triangles = new List<int>(192);

            AppendTaperedTube(vertices, triangles,
                              Vector3.zero, new Vector3(0f, 0f, length),
                              startRadius, endRadius);

            return Finish(vertices, triangles, name);
        }

        /// <summary>
        /// İki nokta arasına daralan bir boru ekler (uçları kapalı).
        ///
        /// <para><b>Uçlar kapatılıyor:</b> açık bir boru içeriden bakıldığında delik
        /// görünür ve el modeli kameraya çok yakın durduğu için o delik <i>görünür</i>.
        /// Kapatmanın maliyeti iki üçgen yelpazesi.</para>
        /// </summary>
        private static void AppendTaperedTube(List<Vector3> vertices, List<int> triangles,
                                              Vector3 from, Vector3 to,
                                              float startRadius, float endRadius,
                                              float flatten = 1f)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-5f) return;

            axis /= length;

            // Eksene dik bir taban: eksen +Y'ye paralel oldugunda Cross sifir doner,
            // o yuzden yedek bir referans gerekiyor.
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;

            Vector3 right = Vector3.Normalize(Vector3.Cross(reference, axis));
            Vector3 up = Vector3.Cross(axis, right);

            int baseIndex = vertices.Count;

            for (int i = 0; i < Sides; i++)
            {
                float angle = i / (float)Sides * Mathf.PI * 2f;
                // Yassilastirma: kesitin bir ekseni kisaltilir. Avuc boyle "el" olur,
                // kol icin 1 (yuvarlak) kalir.
                Vector3 offset = right * Mathf.Cos(angle) + up * (Mathf.Sin(angle) * flatten);

                vertices.Add(from + offset * startRadius);
                vertices.Add(to + offset * endRadius);
            }

            for (int i = 0; i < Sides; i++)
            {
                int a = baseIndex + i * 2;
                int b = baseIndex + i * 2 + 1;
                int c = baseIndex + ((i + 1) % Sides) * 2;
                int d = baseIndex + ((i + 1) % Sides) * 2 + 1;

                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(b); triangles.Add(d);
            }

            // Uc kapaklari.
            int startCap = vertices.Count;
            vertices.Add(from);

            int endCap = vertices.Count;
            vertices.Add(to);

            for (int i = 0; i < Sides; i++)
            {
                int a = baseIndex + i * 2;
                int c = baseIndex + ((i + 1) % Sides) * 2;

                triangles.Add(startCap); triangles.Add(c); triangles.Add(a);

                int b = baseIndex + i * 2 + 1;
                int d = baseIndex + ((i + 1) % Sides) * 2 + 1;

                triangles.Add(endCap); triangles.Add(b); triangles.Add(d);
            }
        }

        /// <summary>
        /// Mesh'i kapatır. <b>Normaller hesaplanır, düz köşe bırakılmaz</b> — düşük
        /// poligonlu bir borunun kutuya benzememesinin tek sebebi bu.
        /// </summary>
        private static Mesh Finish(List<Vector3> vertices, List<int> triangles, string name)
        {
            var mesh = new Mesh { name = name };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
