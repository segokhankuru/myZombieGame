using Bunker.Systems.Cards;
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

        /// <summary>HUD'un gösterdiği ipucu.</summary>
        public string Prompt => $"{displayName}  -  yukseltmeler";

        /// <summary>Menüyü açar. <c>PlayerInteract</c> çağırır.</summary>
        public void Open() => CardSignals.SetShopOpen(true);
    }
}
