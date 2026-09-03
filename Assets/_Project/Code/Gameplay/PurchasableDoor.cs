using Bunker.Config;
using Bunker.Systems.Config;
using Bunker.Systems.Economy;
using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>Kapının hangi fiyat bandını kullandığı.</summary>
    public enum DoorPriceTier
    {
        Cheap,
        Mid,
        Expensive
    }

    /// <summary>
    /// Puanla açılan kapı. M1-09 — haritanın büyüme mekanizması.
    ///
    /// <para><b>Kapı bir kapı değil, bir karardır.</b> Oyuncu biriken puanını ya haritayı
    /// büyütmeye ya silaha harcar; bu seçim tur ekonomisinin belkemiği. Açılan alan geri
    /// kapanmaz — <b>tek yönlü ve kalıcı</b>, çünkü geri alınabilir bir karar karar
    /// değildir.</para>
    ///
    /// <para><b>Otorite host'ta</b> (ADR-0004): puanı düşüren ve kapıyı açan sunucudur.
    /// <c>SyncVar</c> sayesinde geç katılan bir istemci de kapıyı <b>açık</b> bulur —
    /// netcode.md'nin dört durumundan biri, ve M-01'de yazılmazsa M-02'de aranacak
    /// olan tam da budur.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Purchasable Door")]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class PurchasableDoor : NetworkBehaviour, IPurchasable
    {
        [Header("Ayar")]
        [Tooltip("config/balance/economy.json'dan uretilen varlik.")]
        [SerializeField] private EconomyConfigAsset economyConfig;

        [Tooltip("Hangi fiyat bandi. Sayilar economy.json'da; buradaki secim yalnizca " +
                 "hangi bandin kullanilacagini soyler.")]
        [SerializeField] private DoorPriceTier priceTier = DoorPriceTier.Cheap;

        [Header("Gorsel")]
        [Tooltip("Acilinca kapatilacak nesneler - kapi kanadi ve engelleyen collider.")]
        [SerializeField] private GameObject[] blockers;

        [Tooltip("Kapinin adi (oyuncuya gosterilir). M1-11'de yerellestirme anahtari olur.")]
        [SerializeField] private string displayName = "KAPI";

        /// <summary>Açık mı. <b>Geç katılan istemci de doğru değeri alır.</b></summary>
        [SyncVar(hook = nameof(OnOpenedChanged))]
        private bool _opened;

        private int _cost;

        public bool IsAvailable => !_opened;
        public int Cost => _cost;
        public string Prompt => $"{displayName}  -  {_cost} puan";

        private void Awake()
        {
            if (economyConfig == null)
            {
                Debug.LogError("[Kapi] economy.asset atanmamis. " +
                               "'Bunker/Config/Ice Aktar' ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            EconomyConfig config = economyConfig.ToRuntime();

            _cost = priceTier switch
            {
                DoorPriceTier.Mid => config.PricesDoorMid,
                DoorPriceTier.Expensive => config.PricesDoorExpensive,
                _ => config.PricesDoorCheap
            };
        }

        private void Start()
        {
            // Sunucu ve istemci ayni gorsel durumdan baslar.
            ApplyOpenState();
        }

        /// <summary>
        /// Satın alındı — <b>ödeme zaten alındı</b>, burada yalnızca kapı açılır.
        /// Ödemeyi burada almak, iki ayrı yerin cüzdana dokunması demek olurdu.
        /// </summary>
        public void OnPurchased()
        {
            if (_opened) return;

            _opened = true;   // SyncVar: istemcilere kendiliginden gider
            ApplyOpenState();
        }

        private void OnOpenedChanged(bool oldValue, bool newValue) => ApplyOpenState();

        private void ApplyOpenState()
        {
            if (blockers == null) return;

            for (int i = 0; i < blockers.Length; i++)
            {
                if (blockers[i] != null) blockers[i].SetActive(!_opened);
            }
        }
    }
}
