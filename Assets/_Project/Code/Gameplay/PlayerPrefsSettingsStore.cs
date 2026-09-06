using Bunker.Systems.Settings;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Ayarların diskteki hâli: <c>PlayerPrefs</c>. M-04.
    ///
    /// <para><b>Neden burada, <c>Bunker.Systems</c>'te değil:</b> <c>Systems</c> Unity'yi
    /// bilmez (<c>noEngineReferences</c>) — ayarların kuralı orada, <i>saklanması</i>
    /// burada. Ters çevirme sayesinde ayar mantığı EditMode testlerinde Unity açmadan
    /// sınanabilir (systems-code.md).</para>
    ///
    /// <para><b><c>PlayerPrefs</c> neden yeterli:</b> hepsi skaler tercihler, hiçbiri
    /// oyunun dengesine dokunmuyor ve kaybolması bir run'a mal olmuyor. Save sistemine
    /// (versiyon, migrasyon, atomik yazma) ihtiyaç duyan şey ilerlemedir, tercihler
    /// değil.</para>
    /// </summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        /// <summary>
        /// Depoyu <b>hiçbir sahne nesnesi uyanmadan önce</b> bağlar.
        ///
        /// <para><c>RoundSignalsBootstrap</c> ile aynı desen ve aynı gerekçe: bir
        /// <c>Awake</c>'e bağlanmış olsaydı, ondan önce uyanan oyuncu kontrolü
        /// kaydedilmiş hassasiyeti değil varsayılanı okurdu.</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Attach()
        {
            // Once temizle: Domain Reload kapaliyken statik olay aboneleri Play
            // oturumlari arasinda YASAR ve ikinci oturumda her sey iki kez uygulanirdi.
            GameSettings.Clear();
            GameSettings.AttachStore(new PlayerPrefsSettingsStore());
        }

        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public bool GetBool(string key, bool fallback) =>
            PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;

        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

        public void Flush() => PlayerPrefs.Save();
    }
}
