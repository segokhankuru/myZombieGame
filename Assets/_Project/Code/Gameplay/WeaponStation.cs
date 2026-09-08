using Bunker.Systems.Rounds;
using Bunker.Systems.Ui;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Üst kattaki <b>silah tezgâhı</b>. <b>E</b> ile katalog açılır. 2026-09-06.
    ///
    /// <para><b>Neden bir tezgâh, üç ayrı duvar noktası değil</b> (geliştirici,
    /// 2026-09-06): <i>"silah seçimini yukarıda tezgâh gibi ekle, oradan alalım; alt
    /// katta sadece mermi alınsın."</i> Doğru ayrım. Duvardaki nokta bir <b>musluk</b>
    /// olmalı — mermin bitti, koşarak gittin, aldın, döndün; düşünmedin. Silah seçmek
    /// ise bir <b>karar</b>: dört seçenek, dört ritim, dört fiyat, ve elindekini
    /// bırakma bedeli. Kararın karşılaştırılabilmesi için seçeneklerin <b>yan yana
    /// görünmesi</b> gerekir. Üç ayrı duvara dağıtılmış silah, karşılaştırılamayan
    /// silahtır.</para>
    ///
    /// <para><b>Neden üst katta:</b> yükseltme tezgâhıyla aynı gerekçe — kapının
    /// arkasında olması, 1250 puanlık kapıya bir sebep verir ve silah değiştirmeyi bir
    /// hedef yapar. Alt kattaki noktalar mermi satmaya devam eder, yani başlangıç
    /// odasından çıkmayan oyuncu oyunu oynayabilir, sadece tabancayla oynar.</para>
    ///
    /// <para><b>Yalnızca molada açılır</b> — <see cref="ShopStation"/> ile aynı kural ve
    /// aynı sebep: menü açıkken oyuncunun girdisi kesiliyor ama dünya dönmeye devam
    /// ediyor. Tur ortasında katalog açmak, sürünün ortasında heykel olmak demek.</para>
    ///
    /// <para><b>Satın almayı kendisi yapmaz.</b> Menü <c>WeaponShopHud</c>'da, ödeme
    /// <see cref="WeaponShopController"/>'da. Bu bileşen yalnızca "burada bir silah
    /// tezgâhı var" der — <see cref="ShopStation"/> ile birebir aynı iş bölümü.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Weapon Station")]
    public sealed class WeaponStation : MonoBehaviour
    {
        [Tooltip("Oyuncuya gosterilen metin. M-05'te yerellestirme anahtari olur.")]
        [SerializeField] private string displayName = "SILAH TEZGAHI";

        /// <summary>Molada mı — katalog yalnızca o zaman açılır.</summary>
        public bool CanOpen => RoundSignals.IsBreather;

        /// <summary>HUD'un gösterdiği ipucu. Kapalıyken SEBEBİNİ söyler.</summary>
        public string Prompt => CanOpen
            ? $"{displayName}  -  silah sec"
            : $"{displayName}  -  yalnizca tur arasinda";

        /// <summary>Kataloğu açar. <c>PlayerInteract</c> çağırır.</summary>
        public void Open()
        {
            if (!CanOpen) return;

            MenuSignals.SetWeaponShopOpen(true);
        }
    }
}
