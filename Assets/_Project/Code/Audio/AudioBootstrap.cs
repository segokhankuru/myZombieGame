using Bunker.Systems.Settings;
using UnityEngine;

namespace Bunker.Audio
{
    /// <summary>
    /// Ses kataloğunu servise bağlar ve <b>menü müziğini</b> yönetir (2026-09-09).
    ///
    /// <para><b>Neden bir sahne bileşeni:</b> <see cref="GameAudio"/> statik bir cephe
    /// ve statik kod bir varlığa referans tutamaz. <c>Resources.Load</c> yeni kodda
    /// yasak (csharp-code.md) ve haklı bir sebeple: yanlış yazılmış bir yol çalışma
    /// anında <b>sessizce</b> null döner — yani ses hiç çıkmaz ve kimse sebebini
    /// bilmez. Serileşmiş bir referans, dosya taşınsa bile bozulmaz.
    /// <c>RoundSignalsBootstrap</c> ve <c>CombatLogBootstrap</c> ile aynı desen.</para>
    ///
    /// <para><b>Müzik SAHNEYE bağlı, oyuna değil</b> (geliştirici: <i>"ana menüde çal...
    /// oyuna katılınca bu ses çalmasın, sadece menü müziği olacak"</i>). Bu bileşen
    /// <see cref="playMusic"/> açıkken çalar, kapalıyken <b>susturur</b>. Yani müziği
    /// durdurma işi menüden çıkan koda değil, oyun sahnesinin kendisine ait — hangi
    /// yoldan girilirse girilsin (menü, doğrudan sahne, yeniden bağlanma) sonuç aynı.
    /// Durdurmayı çağıran tarafa bırakmak, unutulan bir yolun müziği çatışmanın
    /// üstünde bırakması demekti.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Audio Bootstrap")]
    [DefaultExecutionOrder(-500)]
    public sealed class AudioBootstrap : MonoBehaviour
    {
        [Tooltip("Magaza ses dosyalarinin katalogu. Bunker > Gorunum > Ses Dosyalarini " +
                 "Bagla ile uretilir. Bos birakilirsa sesler eskisi gibi sentezlenir.")]
        [SerializeField] private AudioCatalogAsset catalog;

        [Tooltip("Bu sahnede menu muzigi calsin mi. ANA MENUDE acik, oyun sahnelerinde " +
                 "KAPALI - kapaliyken muzigi ayrica susturur.")]
        [SerializeField] private bool playMusic;

        private void Awake()
        {
            if (catalog == null)
            {
                // Sessiz kalmaz ama oyunu da durdurmaz: katalog bir YUKSELTME.
                // Yoksa butun sesler eskisi gibi sentezlenir (ADR-0009).
                Debug.LogWarning("[Ses] Ses katalogu atanmamis - sesler sentezlenecek. " +
                                 "'Bunker/Gorunum/Ses Dosyalarini Bagla' calistir.", this);
            }

            GameAudio.AttachCatalog(catalog);
        }

        private void OnEnable()
        {
            GameSettings.Changed += OnSettingsChanged;
            Apply();
        }

        private void OnDisable()
        {
            // OnEnable'in kurdugunu OnDisable bozar (csharp-code.md).
            GameSettings.Changed -= OnSettingsChanged;
        }

        private void OnSettingsChanged() => GameAudio.RefreshMusicVolume();

        private void Apply()
        {
            if (playMusic && catalog != null && catalog.MenuMusic != null)
            {
                GameAudio.PlayMusic(catalog.MenuMusic);
                return;
            }

            // Bu sahnede muzik YOK: caliyorsa sustur. Menuden oyuna gecerken muzigi
            // kesen sey burasi - menuden cikan kod degil.
            GameAudio.StopMusic();
        }
    }
}
