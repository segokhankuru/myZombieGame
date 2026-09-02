using System.Collections.Generic;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Zombilerin kovalayabileceği hedeflerin kaydı.
    ///
    /// <para><b>Neden bir kayıt, doğrudan oyuncuyu bulmak değil:</b> zombi oyuncunun
    /// transform'unu okumaz — kendisine söylenen hedefi bilir (ai-code.md). Ayrıca
    /// <c>Bunker.AI</c>, <c>Bunker.Gameplay</c>'e bağımlı değildir ve olmamalıdır; oyuncu
    /// nesnesi buraya <see cref="ZombieTargetBeacon"/> ile kendini <i>kaydeder</i>,
    /// zombiler onu <i>arar</i>. Bağımlılık böylece ters çevrilmiş olur.</para>
    ///
    /// <para>Kare başına <c>GameObject.Find</c> ya da <c>FindObjectsOfType</c> yasaktır
    /// (csharp-code.md); 40 zombinin her biri için oyuncu aramak bunun en pahalı hâlidir.</para>
    /// </summary>
    public static class ZombieTargets
    {
        private static readonly List<ZombieTargetBeacon> Beacons = new List<ZombieTargetBeacon>(8);

        public static int Count => Beacons.Count;

        public static void Register(ZombieTargetBeacon beacon)
        {
            if (beacon == null || Beacons.Contains(beacon)) return;
            Beacons.Add(beacon);
        }

        public static void Unregister(ZombieTargetBeacon beacon)
        {
            if (beacon == null) return;
            Beacons.Remove(beacon);
        }

        /// <summary>
        /// Verilen noktaya en yakın canlı hedef. Yoksa <c>null</c> — zombi bu durumda
        /// hedefsiz kalır, çökmez.
        /// </summary>
        public static ZombieTargetBeacon Nearest(Vector3 from)
        {
            ZombieTargetBeacon best = null;
            float bestSqr = float.MaxValue;

            // Ters dongu: sirasi bozulan (yok olan) hedefler guvenle temizlenebilsin.
            for (int i = Beacons.Count - 1; i >= 0; i--)
            {
                ZombieTargetBeacon b = Beacons[i];

                if (b == null)
                {
                    Beacons.RemoveAt(i);
                    continue;
                }

                if (!b.IsTargetable) continue;

                float sqr = (b.Position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = b;
            }

            return best;
        }
    }
}
