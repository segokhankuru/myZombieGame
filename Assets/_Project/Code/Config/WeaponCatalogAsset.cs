using System.Collections.Generic;
using Bunker.Systems.Combat;
using UnityEngine;

namespace Bunker.Config
{
    /// <summary>
    /// Silah kataloğunun çalışma anı hâli. <c>config/content/weapons.json</c>'dan
    /// <b>üretilir</b> (Bunker/Config/Silahlari Ice Aktar).
    ///
    /// <para><b>Neden elle yazılmış, üretilmiş değil:</b> config importer düz skaler
    /// alanlar üretir (bir sayı, bir aralık). Silah listesi bir <i>liste</i>; kart
    /// kataloğuyla aynı kalıp ve aynı gerekçe. Yön yine tek: JSON kaynak, bu varlık
    /// çıktı. <b>Elle düzenlenmez.</b></para>
    /// </summary>
    public sealed class WeaponCatalogAsset : ScriptableObject
    {
        /// <summary>Diske serileşebilen silah satırı.</summary>
        [System.Serializable]
        public sealed class Entry
        {
            public string id;
            public string displayName;
            [TextArea] public string text;

            public float damage = 30f;
            public float roundsPerMinute = 400f;
            public float headshotMultiplier = 2f;
            public float rangeMeters = 60f;
            public float spreadDegrees = 1f;
            public int pelletCount = 1;

            public int magazineCapacity = 12;
            public int reserveCapacity = 300;
            public int startingReserve = 120;
            public float reloadSeconds = 1.6f;

            public float recoilPitchPerShot = 1f;
            public float recoilYawPerShot = 0.3f;
            public float recoilRecoveryPerSecond = 14f;
            public float recoilMaxPitch = 6f;

            public int price;
            public int ammoPrice = 250;

            /// <summary>Dolum mermi mermi mi ilerliyor (pompali).</summary>
            public bool reloadPerShell;

            /// <summary>Durbun buyutmesi. 0 ya da 1 = durbun yok (WeaponDefinition.ScopeMagnification).</summary>
            public float scopeMagnification;

            /// <summary>Nisan alirken ekranda ne gorundugu (WeaponDefinition.ScopeStyle).</summary>
            public ScopeStyle scopeStyle = ScopeStyle.None;
        }

        [SerializeField] private int version = 1;
        [SerializeField] private List<Entry> weapons = new List<Entry>();

        public int Version => version;
        public int Count => weapons?.Count ?? 0;

        /// <summary>
        /// Saf C# karşılığını üretir. <b>Boot bir kez çağırır</b> — kare başına değil,
        /// çünkü liste her çağrıda yeniden ayrılır.
        /// </summary>
        /// <param name="feelTracerSeconds">
        /// His ayarları (iz, isabet işareti, girdi tamponu) <b>silaha göre değil oyuna
        /// göre</b>dir ve <c>weapon.json</c>'da yaşar. Katalogda tekrar edilmeleri, aynı
        /// sayının beş yerde durması olurdu (config-data.md).
        /// </param>
        public List<WeaponDefinition> ToRuntime(float feelTracerSeconds,
                                                float feelHitMarkerSeconds,
                                                float feelInputBufferSeconds)
        {
            var result = new List<WeaponDefinition>(Count);

            if (weapons == null) return result;

            for (int i = 0; i < weapons.Count; i++)
            {
                Entry e = weapons[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;

                result.Add(new WeaponDefinition(
                    e.id, e.displayName, e.damage, e.roundsPerMinute, e.headshotMultiplier,
                    e.rangeMeters, e.spreadDegrees, e.pelletCount,
                    e.magazineCapacity, e.reserveCapacity, e.startingReserve, e.reloadSeconds,
                    e.recoilPitchPerShot, e.recoilYawPerShot,
                    e.recoilRecoveryPerSecond, e.recoilMaxPitch,
                    feelTracerSeconds, feelHitMarkerSeconds, feelInputBufferSeconds,
                    e.price, e.ammoPrice, e.reloadPerShell, e.scopeMagnification,
                    e.scopeStyle));
            }

            return result;
        }

        /// <summary>İçe aktarıcının yazdığı yer. <b>Yalnızca editörden çağrılır.</b></summary>
        public void Fill(int newVersion, List<Entry> entries)
        {
            version = newVersion;
            weapons = entries ?? new List<Entry>();
        }
    }
}
