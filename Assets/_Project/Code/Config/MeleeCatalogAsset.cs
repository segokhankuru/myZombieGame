using System.Collections.Generic;
using Bunker.Systems.Combat;
using UnityEngine;

namespace Bunker.Config
{
    /// <summary>
    /// Yakın dövüş kataloğunun çalışma anı hâli. <c>config/content/melee.json</c>'dan
    /// <b>üretilir</b> (<i>Bunker/Config/Yakin Dovus Ice Aktar</i>). 2026-09-08.
    ///
    /// <para><b>Neden elle yazılmış, üretilmiş değil:</b> config importer düz skaler
    /// alanlar üretir; bu bir <i>liste</i>. Silah ve kart kataloğuyla aynı kalıp ve
    /// aynı gerekçe. Yön tek: JSON kaynak, bu varlık çıktı — <b>elle düzenlenmez</b>
    /// (config-protocol.md).</para>
    ///
    /// <para><b>Çarpanları taşır, sonucu değil.</b> Taban savuruş <c>knife.json</c>'da;
    /// <see cref="ToRuntime"/> ikisini birleştirir. Buraya mutlak sayı yazmak, bıçağın
    /// dengesini iki dosyada tutmak olurdu.</para>
    /// </summary>
    public sealed class MeleeCatalogAsset : ScriptableObject
    {
        /// <summary>Diske serileşebilen katalog satırı.</summary>
        [System.Serializable]
        public sealed class Entry
        {
            public string id;
            public string displayName;
            [TextArea] public string text;

            public float damageMultiplier = 1f;
            public float swingTimeMultiplier = 1f;
            public float rangeMultiplier = 1f;

            public int price;
        }

        [SerializeField] private int version = 1;
        [SerializeField] private List<Entry> melee = new List<Entry>();

        public int Version => version;
        public int Count => melee == null ? 0 : melee.Count;

        /// <summary>
        /// Saf C# karşılığını üretir. <b>Boot bir kez çağırır</b> — liste her çağrıda
        /// yeniden ayrılır, kare başına çağrılamaz.
        /// </summary>
        /// <param name="baseSwing">
        /// Taban savuruş (<c>knife.json</c>). Çarpanların üstüne bineceği tek kaynak.
        /// </param>
        public List<MeleeDefinition> ToRuntime(Bunker.Systems.Config.KnifeConfig baseSwing)
        {
            var result = new List<MeleeDefinition>(Count);

            if (melee == null || baseSwing == null) return result;

            for (int i = 0; i < melee.Count; i++)
            {
                Entry e = melee[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;

                result.Add(MeleeDefinition.Resolve(
                    e.id, e.displayName, e.text,
                    baseSwing.SwingDamage, baseSwing.SwingRangeMeters,
                    baseSwing.SwingArcDegrees, baseSwing.SwingCooldownSeconds,
                    baseSwing.SwingWindupSeconds,
                    e.damageMultiplier, e.swingTimeMultiplier, e.rangeMultiplier,
                    e.price));
            }

            return result;
        }

        /// <summary>İçe aktarıcının yazdığı yer. <b>Yalnızca editörden çağrılır.</b></summary>
        public void Fill(int newVersion, List<Entry> entries)
        {
            version = newVersion;
            melee = entries ?? new List<Entry>();
        }
    }
}
