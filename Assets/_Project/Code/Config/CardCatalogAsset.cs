using System.Collections.Generic;
using Bunker.Systems.Cards;
using UnityEngine;

namespace Bunker.Config
{
    /// <summary>
    /// Kart havuzunun çalışma anı hâli. <c>config/content/cards.json</c>'dan
    /// <b>üretilir</b> (Bunker/Config/Kartlari Ice Aktar).
    ///
    /// <para><b>Neden elle yazılmış, üretilmiş değil:</b> config importer düz skaler
    /// alanlar üretir (bir sayı, bir aralık, bir alan). Kart havuzu bir <i>liste</i>;
    /// aynı kalıba sokulamaz. Yön yine tek: JSON kaynak, bu varlık çıktı.
    /// <b>Elle düzenlenmez</b> — bir sonraki içe aktarma üzerine yazar.</para>
    ///
    /// <para><b>Çalışma anında JSON okunmaz</b> (systems-code.md). Oyun bu varlığı
    /// okur; <c>config/</c> klasörü build'e hiç girmez.</para>
    /// </summary>
    public sealed class CardCatalogAsset : ScriptableObject
    {
        /// <summary>Diske serileşebilen kart satırı. <c>CardDefinition</c> bir struct
        /// ve Unity onu serileştiremez; bu sınıf o köprüdür.</summary>
        [System.Serializable]
        public sealed class Entry
        {
            public string id;
            public string displayName;
            [TextArea] public string text;
            public CardTag tag;
            public CardStat stat;
            public float value;
            public bool soloValid = true;
            public bool coopValid = true;
        }

        [SerializeField] private int version = 1;
        [SerializeField] private List<Entry> cards = new List<Entry>();

        public int Version => version;
        public int Count => cards?.Count ?? 0;

        /// <summary>
        /// Saf C# karşılığını üretir. <b>Boot bir kez çağırır</b> — kare başına değil,
        /// çünkü liste her çağrıda yeniden ayrılır.
        /// </summary>
        public List<CardDefinition> ToRuntime()
        {
            var result = new List<CardDefinition>(Count);

            if (cards == null) return result;

            for (int i = 0; i < cards.Count; i++)
            {
                Entry e = cards[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;

                result.Add(new CardDefinition(e.id, e.displayName, e.text,
                                              e.tag, e.stat, e.value,
                                              e.soloValid, e.coopValid));
            }

            return result;
        }

        /// <summary>İçe aktarıcının yazdığı yer. <b>Yalnızca editörden çağrılır.</b></summary>
        public void Fill(int newVersion, List<Entry> entries)
        {
            version = newVersion;
            cards = entries ?? new List<Entry>();
        }
    }
}
