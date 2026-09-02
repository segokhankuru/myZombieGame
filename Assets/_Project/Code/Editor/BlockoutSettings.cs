using System;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>Bir iç bölmenin uzandığı eksen.</summary>
    public enum PartitionAxis
    {
        /// <summary>Sabit X'te, Z boyunca uzanan duvar (kuzey-güney).</summary>
        AlongZ,
        /// <summary>Sabit Z'de, X boyunca uzanan duvar (doğu-batı).</summary>
        AlongX
    }

    /// <summary>
    /// Bir bölgenin içindeki ayırıcı duvar. Odaları bölerek darboğaz üretir —
    /// LVL-01 spec'i her bölgede en az bir darboğaz istiyor, çünkü "burayı tutabilirim"
    /// diyebilmek bu türün temel taktiğidir.
    /// </summary>
    [Serializable]
    public struct Partition
    {
        [Tooltip("Duvarin hangi eksende uzandigi.")]
        public PartitionAxis Axis;

        [Tooltip("AlongZ ise duvarin X konumu, AlongX ise Z konumu.")]
        public float Position;

        [Tooltip("Duvarin baslangici (diger eksende).")]
        public float From;
        [Tooltip("Duvarin bitisi (diger eksende).")]
        public float To;

        [Tooltip("Gecis boslugunun merkezi. Komsu bolmelerde farkli deger vermek " +
                 "duz gorus hattini kirar ve oyuncuyu donmeye zorlar.")]
        public float GapCenter;

        [Tooltip("Gecis genisligi. Daraltmak darbogaz uretir.")]
        public float GapWidth;
    }

    /// <summary>
    /// LVL-01 gri kutu haritasının ölçüleri. Bir varlık olarak diskte durur,
    /// Inspector'dan düzenlenir, git'te diff'lenir.
    ///
    /// <para><b>Neden kod yerine varlık:</b> level design bir <i>his</i> işidir ve
    /// deneyerek bulunur. Sayıları C# sabiti yapmak her denemeyi kod düzenlemeye
    /// çevirir. Varlık olunca döngü şu hâle gelir: sayıyı oynat, Üret'e bas, koş.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BlockoutSettings", menuName = "Bunker/Blockout Settings")]
    public sealed class BlockoutSettings : ScriptableObject
    {
        [Header("Ayak izi (metre)")]
        public float West = -8f;
        public float East = 22f;
        public float South = -8f;
        public float North = 8f;
        [Tooltip("A ile B'yi ayiran kapili duvar. Sola cekersen A kucuk B buyuk olur.")]
        public float Divider = 4f;

        [Tooltip("Binanin cevresindeki disarida yurunecek serit (metre). Zombiler " +
                 "burada dogar ve pencereye buradan yurur - NavMesh binanin disinda da " +
                 "olmak zorunda. Sifir yaparsan zombilerin duracagi zemin kalmaz. " +
                 "Cok genis yapmak bake suresini ve NavMesh boyutunu bosuna buyutur.")]
        public float ApronWidth = 6f;

        [Header("Yukseklikler")]
        [Tooltip("Zemin kat tavan yuksekligi. 3 m basik hissettirir; 4 m nefes aldirir.")]
        public float WallHeight = 4f;
        public float UpperFloorY = 4.5f;
        public float FloorThickness = 0.5f;
        public float WallThickness = 0.3f;

        [Header("Kapi ve pencere")]
        public float DoorWidth = 1.8f;
        public float DoorHeight = 2.6f;
        public float WindowWidth = 1.5f;
        [Tooltip("Pencerenin alt kenari. Zombi buradan tirmanir.")]
        public float WindowSill = 1f;
        public float WindowTop = 2.3f;
        [Tooltip("Kac metrede bir pencere. Pencereler duvar boyunca ESIT araliklarla " +
                 "dagitilir, sabit konumlarla degil - ayak izi degisince kaymasinlar diye. " +
                 "Kucultursen pencere sayisi artar ve savunma zorlasir.")]
        public float WindowSpacingMeters = 8f;

        [Header("Ic bolmeler (odalari boler, darbogaz uretir)")]
        public Partition[] Partitions = new Partition[]
        {
            // B'yi ikiye bolen duvar, gecis guneyde
            new Partition { Axis = PartitionAxis.AlongZ, Position = 12f,
                            From = -8f, To = 8f, GapCenter = -5f, GapWidth = 2f },
            // Dogu yariyi ikiye bolen duvar, gecis kuzeyde -- kaydirilmis bosluklar
            // duz gorus hattini kirar ve oyuncuyu donmeye zorlar
            new Partition { Axis = PartitionAxis.AlongX, Position = 0f,
                            From = 12f, To = 22f, GapCenter = 18f, GapWidth = 2f },
            // A'da bir nis: baslangic odasina sekil verir
            new Partition { Axis = PartitionAxis.AlongX, Position = 3f,
                            From = -8f, To = -2f, GapCenter = -5f, GapWidth = 2.5f }
        };

        [Header("Rampa")]
        public float RampX = 19f;
        public float RampWidth = 2.5f;
        public float RampStartZ = -7f;
        [Tooltip("Rampanin bittigi Z. UST KATIN ICINDE olmali ki oyuncu cikinca " +
                 "ileri adim atip zemine bassin.")]
        public float RampEndZ = 3f;
        public float RampHoleMargin = 0.6f;
        [Tooltip("Ust kat zeminindeki rampa acikliginin basladigi Z. Erken baslamali, " +
                 "yoksa oyuncu rampada ilerlerken zemine kafa atar.")]
        public float RampHoleStartZ = -4f;

        [Header("Dusme deligi (C'den A'ya)")]
        public float DropHoleMinX = -6f;
        public float DropHoleMaxX = -2f;
        public float DropHoleMinZ = -2f;
        public float DropHoleMaxZ = 2f;
        [Tooltip("Delik kenarindaki alcak korkuluk. Kazara dusmeyi engeller, " +
                 "atlamak bilincli bir hareket olur.")]
        public bool DropHoleLips = true;

        // --- turetilmis ---

        public float RampHoleMinX => RampX - RampWidth / 2f - RampHoleMargin;
        public float RampHoleMaxX => RampX + RampWidth / 2f + RampHoleMargin;
        public float RampHoleMaxZ => RampEndZ;
        public float RampRun => RampEndZ - RampStartZ;
        public float RampAngleDegrees => Mathf.Atan2(UpperFloorY, RampRun) * Mathf.Rad2Deg;

        public float FootprintWidth => East - West;
        public float FootprintDepth => North - South;

        /// <summary>Rampa üst kat zemininin altından geçerken kalan en dar kafa payı.</summary>
        public float RampHeadroom
        {
            get
            {
                float slabBottom = UpperFloorY - FloorThickness;
                float rampYAtHoleStart = RampRun <= 0f
                    ? 0f
                    : UpperFloorY * ((RampHoleStartZ - RampStartZ) / RampRun);
                return slabBottom - rampYAtHoleStart;
            }
        }

        /// <summary>Bir duvar açıklığına kaç pencere düşeceği. En az bir tane.</summary>
        public int WindowCountFor(float wallLength)
        {
            if (WindowSpacingMeters <= 0.1f) return 1;
            int count = Mathf.FloorToInt(wallLength / WindowSpacingMeters);
            return Mathf.Max(1, count);
        }
    }
}
