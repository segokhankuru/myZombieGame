using Bunker.Audio;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Oyuncunun ayak sesleri (2026-09-09, geliştirici: <i>"yürüme sesi için Footsteps -
    /// Essentials ekledim, kullandığımız karakterde bu sesi kullanalım"</i>).
    ///
    /// <para><b>Neden mesafeye göre, animasyona göre değil:</b> adım sesi
    /// <i>kat edilen yola</i> aittir. Bir zamanlayıcıya bağlansaydı, koşarken de
    /// yürürken de aynı sıklıkta çalar ve hız duyulmazdı; animasyon olayına
    /// bağlansaydı, animasyon yeniden zamanlandığında ses sessizce bozulurdu ve
    /// bunu kimse bir oyun testine kadar fark etmezdi (audio-code.md: geri bildirim
    /// <b>oyun durumundan</b> tetiklenir, animasyondan değil).</para>
    ///
    /// <para><b>Yalnızca yerel oyuncuda ve yalnızca yerdeyken.</b> Havadayken adım
    /// sesi çıkarmak, oyuncunun zıplarken yürüdüğünü duyması demek — mekaniğin
    /// yalanlandığı yer.</para>
    ///
    /// <para><b>Katalog yoksa sessizdir, hata vermez.</b> Ayak sesinin sentezlenmiş bir
    /// karşılığı yok ve olmamalı: kötü bir ayak sesi, hiç ayak sesi olmamasından daha
    /// çok rahatsız eder.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Footsteps")]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        /// <summary>
        /// İki adım arası mesafe, metre. <b>Denge değeri değil</b>, insan yürüyüşünün
        /// ölçüsü — bu yüzden config'de değil burada (config-data.md config'i denge
        /// için ayırır).
        /// </summary>
        private const float WalkStrideMeters = 2.1f;

        /// <summary>
        /// Koşarken adım aralığı. Kısa: koşu daha sık ve daha yüksek adım demek, ve
        /// oyuncunun kendi hızını <i>duyabilmesi</i> koşunun sınırlı bir kaynak
        /// olduğunu hatırlatan şeylerden biri.
        /// </summary>
        private const float SprintStrideMeters = 2.8f;

        /// <summary>Bu hızın altında adım sesi yok — yerinde dönmek yürümek değildir.</summary>
        private const float MinSpeedMetersPerSecond = 0.6f;

        private CharacterController _body;
        private PlayerController _player;

        private float _distance;
        private bool _wasGrounded = true;

        // Ayni varyantin arka arkaya calmamasi icin (audio-code.md: her havuzda bir
        // "N icinde tekrar etme" kurali). Tek bir onceki yeterli - on varyantli bir
        // havuzda daha uzun bir hafiza, rastgeleligi gozle gorulur bicimde bozar.
        private int _lastWalk = -1;
        private int _lastRun = -1;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            // Uzak oyuncunun ayak sesi M-02'nin isi: bugun onun konumu ag uzerinden
            // gelmiyor ve sesi yanlis yerden duyulurdu.
            if (_body == null || _player == null) return;
            if (!_player.isLocalPlayer) return;

            bool grounded = _body.isGrounded;

            // YERE INIS: mesafeden bagimsiz, tek seferlik. Zipladiktan sonra yere
            // degdigin an duyulmazsa, ziplama havada biten bir hareket gibi okunur.
            if (grounded && !_wasGrounded)
            {
                PlayFrom(GameAudio.Catalog?.FootstepsLand, ref _lastWalk, 0.55f);
                _distance = 0f;
            }

            _wasGrounded = grounded;

            if (!grounded) return;

            // Dikey bileseni AT: asansorde ya da egimde inerken adim atmis sayilmaz.
            Vector3 velocity = _body.velocity;
            velocity.y = 0f;

            float speed = velocity.magnitude;
            if (speed < MinSpeedMetersPerSecond) return;

            _distance += speed * Time.deltaTime;

            bool sprinting = _player.IsSprinting;
            float stride = sprinting ? SprintStrideMeters : WalkStrideMeters;

            if (_distance < stride) return;
            _distance = 0f;

            if (sprinting) PlayFrom(GameAudio.Catalog?.FootstepsRun, ref _lastRun, 0.5f);
            else PlayFrom(GameAudio.Catalog?.FootstepsWalk, ref _lastWalk, 0.38f);
        }

        /// <summary>
        /// Bir varyant seçip çalar. <b>Aynı varyant art arda çalmaz</b> — on varyantlı
        /// bir havuzun tek varyantlı gibi okunmasının en hızlı yolu, aynısını iki kez
        /// seçmektir (audio-code.md).
        /// </summary>
        private static void PlayFrom(AudioClip[] clips, ref int last, float volume)
        {
            if (clips == null || clips.Length == 0) return;

            int index;

            if (clips.Length == 1)
            {
                index = 0;
            }
            else
            {
                // Onceki HARIC bir tane sec: modulo ile kaydirmak, ayri bir dongu
                // ve yeniden zar atmaktan hem ucuz hem sapmasiz.
                index = (last + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            }

            last = index;

            // 2B calinir: kendi ayak sesin kafanin icindedir, uzayda bir yerde degil
            // (kendi silah sesiyle ayni gerekce). Oncelik DUSUK - bir patlamanin
            // kanalini calmamali.
            GameAudio.PlayClip(clips[index], Vector3.zero, spatial: false,
                               volumeScale: volume, pitchJitter: 0.10f, priority: 2);
        }
    }
}
