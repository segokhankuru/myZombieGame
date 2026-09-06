using Bunker.Systems.Cards;
using Bunker.Systems.Rounds;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Duvardaki tezgâh noktası. <b>E</b> ile menüsü açılır. SYS-02 §7e.
    ///
    /// <para><b>Neden dünyada bir yer, tur arası ekranında bir panel değil</b>
    /// (geliştirici, 2026-09-04): <i>"Tezgâh bence kartlardan bağımsız, mermi doldurma
    /// yeri gibi duvarda olmalı."</i> Doğru ayrım — kart seçimi turun <b>ödülü</b> ve
    /// zorunlu bir andır; tezgâh bir <b>harcama</b> ve oyuncunun oraya gitmeyi seçtiği
    /// bir yerdir. Gitmek bir bedel: mola süresinden yiyor ve seni haritanın belirli bir
    /// noktasına bağlıyor.</para>
    ///
    /// <para><b>Duvar silahıyla aynı dili konuşur:</b> aynı tuş (E), aynı ipucu yeri,
    /// aynı "bak ve bas" alışkanlığı. Farklı bir tuş öğretmek, mermi almayı öğrenmiş
    /// oyuncuya ikinci bir kural dayatmak olurdu.</para>
    ///
    /// <para><b>Satın almayı kendisi yapmaz.</b> Menü <c>ShopHud</c>'da,
    /// ödeme <see cref="ShopController"/>'da. Bu bileşen yalnızca "burada bir tezgâh
    /// var" der.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Shop Station")]
    public sealed class ShopStation : MonoBehaviour
    {
        [Tooltip("Oyuncuya gosterilen metin. M-03'te yerellestirme anahtari olur.")]
        [SerializeField] private string displayName = "TEZGAH";

        /// <summary>
        /// Tezgâh <b>yalnızca molada</b> açılır (2026-09-05, oyun testi).
        ///
        /// <para><b>Bulgu:</b> menü açıkken oyuncunun girdisi kesiliyor ama dünya
        /// dönmeye devam ediyordu — tur ortasında tezgâhı açmak, sürünün ortasında
        /// heykel olmak demekti. 14. turda "hiç hasar yemeden game over oldum" diye
        /// okunan ölüm buydu: dört vuruş, menünün arkasında, iki saniyede.</para>
        ///
        /// <para><b>Neden menüyü kapatmak yerine kuralı koymak:</b> tezgâh zaten tur
        /// arasının harcaması. Tur ortasında açılabiliyor olması bir özellik değil,
        /// kimsenin karar vermediği bir yan etkiydi.</para>
        /// </summary>
        public bool CanOpen => RoundSignals.IsBreather;

        /// <summary>HUD'un gösterdiği ipucu. Kapalıyken SEBEBİNİ söyler.</summary>
        public string Prompt => CanOpen
            ? $"{displayName}  -  yukseltmeler"
            : $"{displayName}  -  yalnizca tur arasinda";

        /// <summary>Menüyü açar. <c>PlayerInteract</c> çağırır.</summary>
        public void Open()
        {
            if (!CanOpen) return;

            CardSignals.SetShopOpen(true);
        }
    }
}
