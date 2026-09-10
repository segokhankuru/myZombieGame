using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bunker.Audio
{
    /// <summary>
    /// Mağazadan gelen <b>gerçek ses dosyaları</b>: menü müziği, ayak sesleri ve silah
    /// sesleri (2026-09-09).
    ///
    /// <para><b>ADR-0006'yı kısmen supersede eder.</b> O karar "varlıksız ses" diyordu:
    /// <see cref="SfxBank"/> sesleri çalışma anında sentezliyor ve proje tek bir
    /// <c>.wav</c> taşımıyordu. Gerekçesi geçerliydi — sanat yönü kilitlenmeden alınan
    /// ses, atılacak iştir. Geliştirici üç paket ekledi (Footsteps Essentials, Free
    /// Weapon Sound Effects, Music Loops Mini Set) ve karar değişti: <b>artık elimizde
    /// ses var, sentez ise yalnızca karşılığı olmayan olaylar için kalıyor.</b>
    /// Sentezlenmiş yol silinmedi; bu katalogda karşılığı olmayan her <see cref="SfxId"/>
    /// hâlâ oradan çalıyor.</para>
    ///
    /// <para><b>Neden bir katalog varlığı, <c>Resources.Load</c> değil:</b>
    /// <c>Resources.Load</c> yeni kodda yasak (csharp-code.md) ve iyi bir sebebi var —
    /// yolu yanlış yazılmış bir dosya adı <b>çalışma anında sessizce</b> null döner,
    /// yani ses hiç çıkmaz ve kimse sebebini bilmez. Katalog, referansı <i>derleme
    /// öncesinde</i> tutar: dosya taşınırsa Unity referansı korur, silinirse
    /// <c>AudioIntegration</c> içe aktarmada yüksek sesle söyler.</para>
    ///
    /// <para><b>Yön tek:</b> paketler kaynak, bu varlık çıktı. <c>AudioIntegration</c>
    /// üretir (<c>Bunker/Gorunum/Ses Dosyalarini Bagla</c>); <b>elle düzenlenmez.</b>
    /// Üçüncü parti klasörleri hiç değiştirilmez (asset-art.md).</para>
    ///
    /// <para><b>Varyantlar dizi, tek klip değil</b> (audio-code.md): dakikada birkaç
    /// kez tetiklenen her ses en az dört varyant ister. Tek varyantlı bir ayak sesi,
    /// oyuncunun oyunu sessize almasının en kısa yoludur.</para>
    /// </summary>
    public sealed class AudioCatalogAsset : ScriptableObject
    {
        /// <summary>Bir silahın ses ailesi. Id, <c>weapons.json</c>'daki id ile aynı.</summary>
        [Serializable]
        public sealed class WeaponSounds
        {
            [Tooltip("config/content/weapons.json'daki id. KALICIDIR.")]
            public string weaponId;

            [Tooltip("Ates sesi varyantlari. En az iki tane olmali - tek varyantli bir " +
                     "silah sesi, saniyede on kez tekrar ettiginde makine sesine doner.")]
            public AudioClip[] fire = Array.Empty<AudioClip>();

            [Tooltip("Dolum BASLANGICI (sarjor cikar).")]
            public AudioClip reloadOut;

            [Tooltip("Dolum SONU (sarjor oturur). Zamanlama silahin durumundan gelir, " +
                     "animasyondan degil (audio-code.md).")]
            public AudioClip reloadIn;

            [Tooltip("Bos tetik.")]
            public AudioClip dryFire;

            [Tooltip("Silahi ele alma.")]
            public AudioClip equip;

            /// <summary>
            /// Bu silahın <b>taban perdesi</b>. 1 = kaynak dosyanın kendi perdesi.
            ///
            /// <para><b>Neden gerekli:</b> pakette dört ses ailesi var (tabanca,
            /// tüfek, pompalı, bombaatar) ama oyunda <b>altı</b> silah. Uzi ile AK-74
            /// aynı tüfek kaydını paylaşıyor ve aynı perdeden çalarlarsa oyuncu
            /// kulağıyla ikisini ayırt edemez — oysa silahın <i>kimliği</i> yarı yarıya
            /// sesidir. Uzi yukarı (ince, hızlı), M107 aşağı (ağır, .50 kalibre).</para>
            ///
            /// <para><b>Perde kaydırmak yeni bir ses kaydetmenin yerini tutmaz</b> ve
            /// bunu iddia etmiyor: bu, sanat yönü kilitlenene kadar süren bir köprü.
            /// Gerçek çözüm silah başına kayıt; o gelene kadar altı silahın altı ayrı
            /// duyulması, altısının aynı duyulmasından iyi.</para>
            /// </summary>
            [Range(0.5f, 2f)]
            public float basePitch = 1f;
        }

        [Header("Muzik")]
        [Tooltip("Ana menu dongusu. YALNIZCA menude calar - oyuna girince susar.")]
        [SerializeField] private AudioClip menuMusic;

        [Header("Ayak sesleri")]
        [SerializeField] private AudioClip[] footstepsWalk = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] footstepsRun = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] footstepsLand = Array.Empty<AudioClip>();

        [Header("Silahlar")]
        [SerializeField] private List<WeaponSounds> weapons = new List<WeaponSounds>();

        public AudioClip MenuMusic => menuMusic;
        public AudioClip[] FootstepsWalk => footstepsWalk;
        public AudioClip[] FootstepsRun => footstepsRun;
        public AudioClip[] FootstepsLand => footstepsLand;

        /// <summary>
        /// Bir silahın ses ailesi. Bulunamazsa <c>null</c> — çağıran taraf sentezlenmiş
        /// yola düşer, <b>sessiz kalmaz</b>.
        /// </summary>
        public WeaponSounds FindWeapon(string weaponId)
        {
            if (weapons == null || string.IsNullOrEmpty(weaponId)) return null;

            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null &&
                    string.Equals(weapons[i].weaponId, weaponId, StringComparison.Ordinal))
                {
                    return weapons[i];
                }
            }

            return null;
        }

        /// <summary>İçe aktarıcının yazdığı yer. <b>Yalnızca editörden çağrılır.</b></summary>
        public void Fill(AudioClip music, AudioClip[] walk, AudioClip[] run,
                         AudioClip[] land, List<WeaponSounds> weaponList)
        {
            menuMusic = music;
            footstepsWalk = walk ?? Array.Empty<AudioClip>();
            footstepsRun = run ?? Array.Empty<AudioClip>();
            footstepsLand = land ?? Array.Empty<AudioClip>();
            weapons = weaponList ?? new List<WeaponSounds>();
        }
    }
}
