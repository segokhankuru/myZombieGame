using UnityEngine;

namespace Bunker.Config
{
    /// <summary>
    /// Çalışma anında ihtiyaç duyulan <b>modellerin tek adresi</b>. 2026-09-07.
    ///
    /// <para><b>Neden var:</b> silah ve zombi görselleri şimdiye kadar koddan ilkel
    /// şekillerle kuruluyordu (<c>WeaponShape</c>, <c>ZombieSetup</c>) — sanat yönü
    /// kilitlenmediği için doğru karardı. Artık Asset Store'dan gelen gerçek modeller
    /// var ve bir yerden <i>bulunmaları</i> gerekiyor. <c>Resources.Load</c> yasak
    /// (csharp-code.md); doğru yol serileşmiş bir referans.</para>
    ///
    /// <para><b>Neden tek varlık, prefab başına alan değil:</b> silahı hem el modeli
    /// hem duvardaki teşhir kuruyor. İki ayrı referans, gün gelip birbirinden
    /// ayrılırdı — duvarda gördüğün pompalı ile eline aldığın pompalı farklı olurdu.
    /// Tek kaynak, iki tüketici.</para>
    ///
    /// <para><b>Yerleşim burada hazır duruyor, çalışma anında hesaplanmıyor.</b> Dört
    /// silah dört ayrı paketten geliyor: farklı ölçekte, farklı eksende, farklı
    /// merkezde. Bunu düzelten ölçüm (<i>hangi eksen namlu, hangi uç ince, ne kadar
    /// büyük</i>) mesh köşelerini taramayı gerektirir — kare içinde yapılacak iş
    /// değil. Üreteç bir kez ölçer, sonuç burada saklanır, oyun yalnızca uygular.</para>
    ///
    /// <para><b>Bu varlık üretilir, elle doldurulmaz:</b>
    /// <i>Bunker &gt; Gorunum &gt; Magaza Modellerini Bagla</i>. Üreteç idempotenttir;
    /// iki kez çalıştırmak bir kez çalıştırmakla aynı sonucu verir
    /// (editor-tools.md).</para>
    /// </summary>
    public sealed class ArtCatalogAsset : ScriptableObject
    {
        /// <summary>Bir silahın modeli ve ölçülmüş yerleşimi.</summary>
        [System.Serializable]
        public sealed class WeaponModel
        {
            [Tooltip("Katalog id'si: weapon.pistol, weapon.smg, weapon.shotgun, weapon.rifle")]
            public string id;

            [Tooltip("Modelin prefab'i (ucuncu parti paketten uretilmis URP kopyasi).")]
            public GameObject prefab;

            [Tooltip("Uretecin olctugu yerlesim. ELLE DEGISTIRME - yeniden uretilince " +
                     "uzerine yazilir.")]
            public Vector3 localPosition;

            public Vector3 localEulerAngles;

            [Tooltip("Tek sayilik olcek: model hedef boya buradan gelir.")]
            public float localScale = 1f;

            [Tooltip("Namlu ucunun yerel konumu. Namlu alevi tam buraya konur - " +
                     "sabit bir nokta, pompalida alevin govdenin icinde patlamasi olurdu.")]
            public Vector3 muzzleLocal;

            [Tooltip("Silahin RENGI. Beyaz = paketten geldigi gibi. Bunu ELLE " +
                     "ayarlarsin ve uretec KORUR - 'Magaza Modellerini Bagla' yeniden " +
                     "calissa da rengin silinmez.")]
            public Color tint = Color.white;
        }

        [SerializeField] private WeaponModel[] weapons = new WeaponModel[0];

        [Tooltip("Yakin dovus modelleri: hancer, kilic, balta (2026-09-08). Ayni " +
                 "yerlesim yapisi - bir bicagin 'namlu ucu' UCUDUR ve savurus izinin " +
                 "cizildigi yer odur.")]
        [SerializeField] private WeaponModel[] meleeWeapons = new WeaponModel[0];

        [Tooltip("Zombinin gorsel modeli. Carpistirici tasimaz - vurus kutulari " +
                 "ZombieSetup'in kurdugu ilkel sekillerde kalir.")]
        [SerializeField] private GameObject zombiePrefab;

        [Tooltip("Zombi modelinin dikey kaydirmasi ve olcegi (uretec olcer).")]
        [SerializeField] private Vector3 zombieLocalPosition;
        [SerializeField] private float zombieScale = 1f;

        [Tooltip("Modelin Y ekseninde donusu. FBX karakterleri genelde +Z'ye bakar, " +
                 "ama hepsi degil - zombi GERI GERI yuruyorsa burasi 180.")]
        [SerializeField] private float zombieYawDegrees;

        public GameObject ZombiePrefab => zombiePrefab;
        public Vector3 ZombieLocalPosition => zombieLocalPosition;
        public float ZombieScale => zombieScale;
        public float ZombieYawDegrees => zombieYawDegrees;

        /// <summary>Bir silahın yerleşimi; tanımlı değilse <c>null</c>.</summary>
        public WeaponModel Weapon(string weaponId) => Find(weapons, weaponId);

        /// <summary>Bir bıçağın yerleşimi; tanımlı değilse <c>null</c>.</summary>
        public WeaponModel Melee(string meleeId) => Find(meleeWeapons, meleeId);

        private static WeaponModel Find(WeaponModel[] list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < list.Length; i++)
            {
                WeaponModel entry = list[i];
                if (entry != null && entry.prefab != null && entry.id == id) return entry;
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Üretecin doldurduğu yer. <b>Yalnızca editörde.</b>
        ///
        /// <para><b>Elle verilen renkler KORUNUR.</b> Üreteç ölçüyü yeniden hesaplar
        /// (ölçek, eksen, namlu ucu) ama rengi hesaplamaz — renk bir sanat kararı ve
        /// sahibi geliştirici. Korunmasaydı, aracı her çalıştıran kişi farkında olmadan
        /// bütün renkleri sıfırlardı; yani ayarı yapmak <i>bir daha araç çalıştırmamak</i>
        /// anlamına gelirdi. Aynı gerekçeyle zombinin dönüş açısı da burada
        /// yazılmıyor.</para>
        /// </summary>
        public void EditorFill(WeaponModel[] weaponModels, WeaponModel[] meleeModels,
                               GameObject zombie,
                               Vector3 zombieOffset, float zombieUniformScale)
        {
            KeepTints(weaponModels, weapons);
            KeepTints(meleeModels, meleeWeapons);

            weapons = weaponModels;
            meleeWeapons = meleeModels;
            zombiePrefab = zombie;
            zombieLocalPosition = zombieOffset;
            zombieScale = zombieUniformScale;
        }

        /// <summary>Aynı id'nin elle verilmiş rengini yeni ölçüme taşır.</summary>
        private static void KeepTints(WeaponModel[] fresh, WeaponModel[] previous)
        {
            if (fresh == null || previous == null) return;

            foreach (WeaponModel entry in fresh)
            {
                if (entry == null) continue;

                foreach (WeaponModel old in previous)
                {
                    if (old == null || old.id != entry.id) continue;

                    entry.tint = old.tint;
                    break;
                }
            }
        }
#endif
    }
}
