using UnityEngine;

namespace Bunker.Config
{
    /// <summary>
    /// Oyunun kullandığı fizik katmanları. 2026-09-07.
    ///
    /// <para><b>Neden gerekti</b> (geliştirici): <i>"barikat penceresinden dışarı
    /// çıkabiliyorum, bunu engelle."</i> Pencere duvarda gerçek bir delik; zombinin
    /// girdiği yerden oyuncu da çıkabiliyor. Deliği kapatmanın tek yolu oraya bir
    /// çarpıştırıcı koymak — ama o çarpıştırıcı <b>mermiyi de durdururdu</b>, yani
    /// pencereden ateş etmek imkânsız hâle gelirdi. Bu, çözdüğünden büyük bir hata
    /// olurdu.</para>
    ///
    /// <para><b>Çözüm bir katman:</b> <c>PlayerBlocker</c> yalnızca oyuncunun
    /// <c>CharacterController</c>'ını durdurur; her ışın sorgusu bu katmanı
    /// <see cref="WorldMask"/> ile dışarıda bırakır. Zombiler ışın kullanmadan,
    /// <c>NavMeshAgent</c> ve elle sürülen tırmanışla geçtiği için onları hiç
    /// etkilemez.</para>
    ///
    /// <para><b>Neden tetikleyici (trigger) değil:</b> tetikleyici zaten bütün
    /// sorgular tarafından yok sayılıyor — ama <c>CharacterController</c>'ı da
    /// durdurmaz. Yani tam olarak istemediğimiz şeyi yapardı.</para>
    ///
    /// <para><b>Katman yoksa hiçbir şey bozulmaz:</b> <see cref="WorldMask"/> o zaman
    /// eski davranışa (<c>~0</c>) döner. Bir teşhis satırıyla, sessizce değil.</para>
    /// </summary>
    public static class GameLayers
    {
        /// <summary>Yalnızca oyuncuyu durduran, ışınların görmediği katmanın adı.</summary>
        public const string PlayerBlockerName = "PlayerBlocker";

        private static int _playerBlocker = -1;   // -1: bulunamadi / henuz bakilmadi
        private static bool _warned;

        /// <summary>
        /// <c>PlayerBlocker</c> katmanının indeksi; proje ayarlarında tanımlı değilse
        /// <c>-1</c>.
        /// </summary>
        public static int PlayerBlocker
        {
            get
            {
                if (_playerBlocker >= 0) return _playerBlocker;

                // BULUNAMAYAN sonuc onbelleklenmez: katmani editor araci sonradan
                // aciyor ve onbellek, aracin urettigi duzeltmeyi bir alan yeniden
                // yuklemesine kadar gormezden gelirdi.
                _playerBlocker = LayerMask.NameToLayer(PlayerBlockerName);

                if (_playerBlocker < 0 && !_warned)
                {
                    _warned = true;
                    Debug.LogWarning(
                        $"[GameLayers] '{PlayerBlockerName}' katmani proje ayarlarinda yok. " +
                        "Pencere tikaclari mermiyi de durdurur. Duzeltmek icin: " +
                        "Bunker > Level > LVL-01 Gri Kutu Uret (katmani kendisi acar).");
                }

                return _playerBlocker;
            }
        }

        /// <summary>
        /// <b>Her nişan alma sorgusunun kullanması gereken maske</b>: dünyanın tamamı,
        /// eksi oyuncu tıkaçları. Mermi, bıçak, etkileşim ışını, tamir ışını ve
        /// zombinin görüş kontrolü hepsi bunu kullanır — biri kullanmazsa, o sorgu
        /// pencerenin önünde durur ve sebebi aylarca bulunamaz.
        /// </summary>
        public static int WorldMask
        {
            get
            {
                int layer = PlayerBlocker;
                return layer < 0 ? ~0 : ~(1 << layer);
            }
        }
    }
}
