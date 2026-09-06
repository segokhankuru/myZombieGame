using System;

namespace Bunker.Systems.Cards
{
    /// <summary>
    /// Kart sisteminin yayın noktası. <c>RoundSignals</c> ve <c>RunSignals</c> ile
    /// aynı desen ve aynı gerekçe: draft ekranı <c>Bunker.UI</c>'de, turu yürüten
    /// <c>ZombieDirector</c> <c>Bunker.AI</c>'da, silah ve can <c>Bunker.Gameplay</c>'de
    /// yaşıyor. Üçünün de gördüğü tek yer <c>Bunker.Systems</c>.
    ///
    /// <para><b>Statik olmanın iki kuralı aynen geçerli:</b> her abone
    /// <c>OnDisable</c>'da bırakır ve <see cref="Clear"/> yalnızca açılışta
    /// <c>RoundSignalsBootstrap</c>'ten çağrılır.</para>
    ///
    /// <para><b>Draft açıkken tur ilerlemez.</b> <see cref="IsDraftOpen"/> bunu
    /// söyler; <c>ZombieDirector</c> okur. Aksi hâlde oyuncu kart seçerken bir sonraki
    /// tur başlar ve seçim ekranının arkasından sürü gelir.</para>
    /// </summary>
    public static class CardSignals
    {
        /// <summary>Bu run'da toplanan kartlar. Yeniden başlatma bunu sıfırlar.</summary>
        public static CardLoadout Loadout { get; } = new CardLoadout();

        /// <summary>Açık draft, yoksa <c>null</c>.</summary>
        public static CardDraft Draft { get; private set; }

        public static bool IsDraftOpen => Draft != null && Draft.IsOpen;

        /// <summary>
        /// Tezgah menusu acik mi (duvardaki istasyon, E ile).
        ///
        /// <para>Draft'tan AYRI: draft turun zorunlu odulu, tezgah oyuncunun gitmeyi
        /// sectigi bir harcama noktasi. Ikisi ayni bayragi paylassaydi tezgahi acmak
        /// turu durdururdu.</para>
        /// </summary>
        public static bool IsShopOpen { get; private set; }

        /// <summary>Tezgah menusu acildi ya da kapandi.</summary>
        public static event Action<bool> ShopVisibilityChanged;

        public static void SetShopOpen(bool open)
        {
            if (IsShopOpen == open) return;

            IsShopOpen = open;
            ShopVisibilityChanged?.Invoke(open);
        }

        /// <summary>
        /// Herhangi bir menu acik mi - girdi kesme noktasi.
        ///
        /// <para>Duraklatma menusu <c>MenuSignals</c>'ta yasar (o kart sisteminin
        /// parcasi degil), ama girdiyi kesen kapi tek olmali: bes ayri yerde
        /// "su menu ya da bu menu" yazmak, alti bir sonraki menude unutulacak bir
        /// kontrol demektir.</para>
        /// </summary>
        public static bool IsAnyMenuOpen => IsDraftOpen || IsShopOpen || Ui.MenuSignals.IsPauseOpen;

        /// <summary>Bir draft açıldı — arayüz burayı dinler.</summary>
        public static event Action<CardDraft> DraftOpened;

        /// <summary>Draft kapandı (kart seçildi ya da atlandı).</summary>
        public static event Action<CardDefinition> DraftClosed;

        /// <summary>Yığın değişti — silah, can ve ekonomi burayı dinleyip kendini tazeler.</summary>
        public static event Action<CardLoadout> LoadoutChanged;

        /// <summary>
        /// Bir draft açar. <b>Yalnızca sunucuda çağrılmalı</b> (ADR-0004): hangi üç
        /// kartın çıktığı kalıcı sonucu olan bir şeydir.
        /// </summary>
        public static void OpenDraft(CardDraft draft)
        {
            Draft = draft ?? throw new ArgumentNullException(nameof(draft));
            DraftOpened?.Invoke(draft);
        }

        /// <summary>Seçim yapıldı; yığın güncellendi.</summary>
        public static void NotifyPicked(in CardDefinition card)
        {
            Draft = null;
            DraftClosed?.Invoke(card);
            LoadoutChanged?.Invoke(Loadout);
        }

        /// <summary>Seçim yapılmadan kapandı.</summary>
        public static void NotifySkipped()
        {
            Draft = null;
            DraftClosed?.Invoke(default);
        }

        /// <summary>
        /// Her şeyi siler. <b>Yalnızca oyun başlarken</b>, hiçbir sahne nesnesi
        /// uyanmadan önce.
        /// </summary>
        public static void Clear()
        {
            DraftOpened = null;
            DraftClosed = null;
            LoadoutChanged = null;
            ShopVisibilityChanged = null;
            IsShopOpen = false;
            Draft = null;
            Loadout.Reset();
        }

        /// <summary>
        /// Etkiler değişti — kart alındı ya da tezgâhtan yükseltme alındı.
        ///
        /// <para>Tezgâh da bu yayını kullanır: silah, can ve ekonomi <b>tek bir olayı</b>
        /// dinler ve kaynağın hangisi olduğunu bilmek zorunda kalmaz.</para>
        /// </summary>
        public static void NotifyModifiersChanged() => LoadoutChanged?.Invoke(Loadout);

        /// <summary>Yeni run: yığın sıfırlanır, açık draft kapanır.</summary>
        public static void ResetRun()
        {
            Draft = null;
            Loadout.Reset();
            LoadoutChanged?.Invoke(Loadout);
        }
    }
}
