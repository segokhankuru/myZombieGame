using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// LVL-01 gri kutu haritasının ölçüleri. Bir varlık (asset) olarak diskte durur,
    /// Inspector'dan düzenlenir, git'te diff'lenir.
    ///
    /// <para><b>Neden kod yerine varlık:</b> level design bir <i>his</i> işidir ve
    /// deneyerek bulunur — "bu oda dar mı" sorusunun cevabı ancak içinde koşarak
    /// çıkar. Sayıları C# sabiti yapmak her denemeyi kod düzenlemeye çevirir. Varlık
    /// olarak durunca döngü şu hâle gelir: sayıyı oynat, Üret'e bas, koş, tekrar.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BlockoutSettings", menuName = "Bunker/Blockout Settings")]
    public sealed class BlockoutSettings : ScriptableObject
    {
        [Header("Ayak izi (metre)")]
        [Tooltip("Binanin bati siniri. Kucultursen A odasi daralir.")]
        public float West = -6f;
        [Tooltip("Binanin dogu siniri. Buyutursen B odasi genisler.")]
        public float East = 14f;
        public float South = -5f;
        public float North = 5f;
        [Tooltip("A ile B'yi ayiran ic duvarin X konumu. Sola kaydirirsan A kucuk B buyuk olur.")]
        public float Divider = 4f;

        [Header("Yukseklikler")]
        [Tooltip("Zemin kat tavan yuksekligi. 3 m kapali ama sikisik olmayan bir his verir.")]
        public float WallHeight = 3f;
        [Tooltip("Ust katin ust yuzeyinin yuksekligi.")]
        public float UpperFloorY = 3.5f;
        public float FloorThickness = 0.5f;
        public float WallThickness = 0.3f;

        [Header("Kapi ve pencere")]
        public float DoorWidth = 1.6f;
        public float DoorHeight = 2.5f;
        public float WindowWidth = 1.5f;
        [Tooltip("Pencerenin alt kenari. Zombi buradan tirmanir.")]
        public float WindowSill = 1f;
        public float WindowTop = 2.2f;

        [Header("Rampa")]
        [Tooltip("Rampanin merkez ekseninin X konumu.")]
        public float RampX = 12f;
        public float RampWidth = 2.5f;
        [Tooltip("Rampanin basladigi Z (zemin katta).")]
        public float RampStartZ = -4.5f;
        [Tooltip("Rampanin bittigi Z. UST KATIN ICINDE olmali ki oyuncu cikinca " +
                 "ileri adim atip zemine bassin.")]
        public float RampEndZ = 2f;
        [Tooltip("Rampa agzinin rampadan her iki yana tasma payi.")]
        public float RampHoleMargin = 0.5f;
        [Tooltip("Ust kat zeminindeki rampa acikliginin basladigi Z. Erken baslamali, " +
                 "yoksa oyuncu rampada ilerlerken zemine kafa atar.")]
        public float RampHoleStartZ = -3f;

        [Header("Dusme deligi (C'den A'ya)")]
        [Tooltip("Dongunun kapanma noktasi. Oyuncu buradan asagi atlar.")]
        public float DropHoleMinX = -4f;
        public float DropHoleMaxX = -1f;
        public float DropHoleMinZ = -2f;
        public float DropHoleMaxZ = 1f;
        [Tooltip("Delik kenarindaki alcak korkuluk. Kazara dusmeyi engeller, " +
                 "atlamak bilincli bir hareket olur.")]
        public bool DropHoleLips = true;

        // --- turetilmis degerler ---

        public float RampHoleMinX => RampX - RampWidth / 2f - RampHoleMargin;
        public float RampHoleMaxX => RampX + RampWidth / 2f + RampHoleMargin;
        public float RampHoleMaxZ => RampEndZ;
        public float RampRun => RampEndZ - RampStartZ;
        public float RampAngleDegrees => Mathf.Atan2(UpperFloorY, RampRun) * Mathf.Rad2Deg;

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
    }
}
