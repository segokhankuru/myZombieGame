using System;
using Bunker.Systems.Combat;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Dövüşün <b>görünür</b> tarafının yayın noktası: kaç hasar verdin, nereye,
    /// ve kaç hasar yedin, nereden. 2026-09-05.
    ///
    /// <para><b>Neden gerekiyordu</b> (geliştirici): <i>"turlar ilerleyince tek yiyorum
    /// sanırım, ondan birden ölüyorum diyor olabilirim."</i> Bu cümlenin kendisi
    /// hatanın tarifi: oyuncu <b>ne kadar hasar yediğini bilmiyor</b>. Sayı görünmeden
    /// "tek mi yedim, dört mü" sorusu tahmine kalır ve ölüm haksızlık gibi okunur.
    /// Aynı şey verdiğin hasar için de geçerli: 15. turda zombinin 3000 canı varken 55
    /// hasar vurduğunu görmezsen silahın işe yaramadığını <i>hissedersin</i> ama
    /// bilemezsin.</para>
    ///
    /// <para><b>Neden <c>Bunker.Systems</c>'te değil:</b> konum taşıyor
    /// (<c>Vector3</c>), <c>Systems</c> ise motor referansı taşımıyor.</para>
    ///
    /// <para><b>Statik olmanın iki kuralı</b> (RunSignals ile aynı): her abone
    /// <c>OnDisable</c>'da bırakır, <see cref="Clear"/> yalnızca açılışta.</para>
    /// </summary>
    public static class CombatFeedback
    {
        /// <summary>
        /// Bir hedefe hasar verildi: dünya konumu, miktar, kafa vuruşu mu, öldürdü mü.
        ///
        /// <para><b>Yalnızca vuran oyuncuda tetiklenir</b> — isabet geri bildirimi
        /// kişisel bir bilgidir (silahtaki <c>TargetRpc</c> ile aynı gerekçe).</para>
        /// </summary>
        public static event Action<Vector3, float, bool, bool> DamageDealt;

        /// <summary>
        /// Oyuncu hasar aldı: miktar ve <b>geldiği yön</b> (kaynağın dünya konumu).
        ///
        /// <para>Yön olmadan "arkadan mı yedim, önden mi" sorusu cevapsız kalır ve
        /// oyuncu doğru tepkiyi veremez — sürünün içinde bu, ölümle hayatta kalma
        /// arasındaki fark.</para>
        /// </summary>
        public static event Action<float, Vector3> DamageTaken;

        public static void RaiseDamageDealt(Vector3 worldPosition, float amount,
                                            bool headshot, bool killed)
        {
            if (amount <= 0f) return;

            DamageDealt?.Invoke(worldPosition, amount, headshot, killed);
        }

        public static void RaiseDamageTaken(float amount, Vector3 sourcePosition)
        {
            if (amount <= 0f) return;

            DamageTaken?.Invoke(amount, sourcePosition);
        }

        /// <summary>
        /// Oyuncuyu <b>öldüren</b> vuruşun miktarı. Skor ekranı bunu yazar.
        ///
        /// <para><b>Neden kaydediliyor</b> (2026-09-05): "birden ölüyorum" ve "tek
        /// yiyorum" cümleleri iki oturumdur tahmine dayanıyor. Ölüm ekranında
        /// <i>"son vuruş: 366 hasar (çevre)"</i> yazdığı an tahmin biter — hangi
        /// sistemin öldürdüğü okunur.</para>
        /// </summary>
        public static float LastLethalAmount { get; private set; }

        /// <summary>Öldüren hasarın türü: mermi, bıçak ya da çevre (patlama).</summary>
        public static DamageKind LastLethalKind { get; private set; }

        public static void NoteLethalHit(float amount, DamageKind kind)
        {
            LastLethalAmount = amount;
            LastLethalKind = kind;
        }

        /// <summary>Yalnızca açılışta, hiçbir sahne nesnesi uyanmadan önce.</summary>
        public static void Clear()
        {
            DamageDealt = null;
            DamageTaken = null;
            LastLethalAmount = 0f;
            LastLethalKind = DamageKind.Bullet;
        }
    }
}
