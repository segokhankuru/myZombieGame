using System;

namespace Bunker.Systems.Pickups
{
    /// <summary>
    /// Sahaya etki eden eşyaların <b>süreli</b> kısmı: yavaşlatma ve dondurma
    /// (2026-09-07).
    ///
    /// <para><b>Neden tek nokta:</b> her zombinin kendi yavaşlatma sayacını tutması,
    /// eşyanın etkisini doğduğu ana bağlardı — eşya toplandıktan sonra doğan zombi
    /// yavaşlamaz, oyuncu da kuralı öğrenemezdi. Etki <i>sahaya</i> aittir, zombiye
    /// değil; o yüzden burada tek bir sayaç var ve her zombi ona <b>bakar</b>.</para>
    ///
    /// <para><b>Dondurma yavaşlatmayı EZER, toplamaz:</b> ikisi çarpılsaydı iki eşya
    /// üst üste geldiğinde zombiler durur ama oyuncu <i>neden</i> durduklarını
    /// göremezdi. Burada kural okunabilir: donmuşsa durur, değilse yavaşlamıştır.</para>
    ///
    /// <para><b>Saf C#, statik.</b> <see cref="Tick"/>'i sunucu tarafındaki tek bir
    /// yer çağırır (<c>ZombieDirector</c>); iki yerden ilerletilirse süreler iki kat
    /// hızlı akar — sayaç kaybının en sinsi hâli.</para>
    /// </summary>
    public static class PowerupState
    {
        private static float _slowRemaining;
        private static float _slowFraction01;
        private static float _freezeRemaining;

        /// <summary>Yavaşlatma sürüyor mu.</summary>
        public static bool IsSlowActive => _slowRemaining > 0f;

        /// <summary>Dondurma sürüyor mu.</summary>
        public static bool IsFreezeActive => _freezeRemaining > 0f;

        /// <summary>Kalan yavaşlatma süresi (arayüz sayacı için).</summary>
        public static float SlowRemainingSeconds => _slowRemaining;

        /// <summary>Kalan dondurma süresi (arayüz sayacı için).</summary>
        public static float FreezeRemainingSeconds => _freezeRemaining;

        /// <summary>
        /// Zombi hızının eşyalardan gelen çarpanı. Zombi bunu kendi hız satırında
        /// okur; <b>0</b> tamamen durmuş demektir.
        /// </summary>
        public static float ZombieSpeedMultiplier
        {
            get
            {
                if (_freezeRemaining > 0f) return 0f;
                if (_slowRemaining > 0f) return 1f - _slowFraction01;

                return 1f;
            }
        }

        /// <summary>Yavaşlatmayı başlatır. Süre <b>tazelenir</b>, üst üste binmez.</summary>
        public static void ActivateSlow(float fraction01, float seconds)
        {
            if (seconds <= 0f || fraction01 <= 0f) return;

            // Oran 0.9'da tutulur: 1.0 olsaydi yavaslatma sessizce dondurmaya
            // donusur ve iki esya arasindaki fark kaybolurdu.
            _slowFraction01 = fraction01 > 0.9f ? 0.9f : fraction01;

            // Kalan sureyi UZATMAZ, TAZELER: toplanan her esya "tam sure" vaat eder
            // ve birikmeyen bir sure, oyuncunun kafasinda tutabilecegi tek kural.
            if (seconds > _slowRemaining) _slowRemaining = seconds;
        }

        /// <summary>Dondurmayı başlatır. Süre tazelenir, üst üste binmez.</summary>
        public static void ActivateFreeze(float seconds)
        {
            if (seconds <= 0f) return;
            if (seconds > _freezeRemaining) _freezeRemaining = seconds;
        }

        /// <summary>Sayaçları ilerletir. <b>Tek bir çağıran</b> (ZombieDirector).</summary>
        public static void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (_slowRemaining > 0f)
            {
                _slowRemaining -= deltaTime;
                if (_slowRemaining < 0f) _slowRemaining = 0f;
            }

            if (_freezeRemaining > 0f)
            {
                _freezeRemaining -= deltaTime;
                if (_freezeRemaining < 0f) _freezeRemaining = 0f;
            }
        }

        /// <summary>
        /// Yeni run ve oyun açılışı: sayaçlar sıfırlanır.
        ///
        /// <para>Bu satırın eksikliği, ikinci run'ın birincinin dondurmasıyla
        /// başlaması demek — <c>CardLoadout.Reset</c> ile aynı sınıf hata.</para>
        /// </summary>
        public static void Clear()
        {
            _slowRemaining = 0f;
            _slowFraction01 = 0f;
            _freezeRemaining = 0f;
        }

        /// <summary>Arayüzün "şu an ne aktif" satırı için. Hiçbiri yoksa <c>null</c>.</summary>
        public static string ActiveLabel()
        {
            if (_freezeRemaining > 0f) return "DONDU";
            if (_slowRemaining > 0f) return "YAVAS";

            return null;
        }

        /// <summary>En uzun kalan süre — arayüz tek bir sayaç gösterir.</summary>
        public static float ActiveRemainingSeconds =>
            Math.Max(_freezeRemaining, _slowRemaining);
    }
}
