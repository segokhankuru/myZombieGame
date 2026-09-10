using Bunker.Config;
using Bunker.Systems.Pickups;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Oyuncunun eşya cebi ve onu boşaltan tuşlar (2026-09-09).
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"yerden topladığımız dropları
    /// stacklenebilen şekilde biriktirebilelim... atıyorum canı 6'ya koydun ve birikti,
    /// canım azaldığında 6'ya basınca canım dolsun."</i> Eşya artık toplandığı an
    /// patlamıyor; cebe giriyor ve oyuncunun seçtiği anda harcanıyor.</para>
    ///
    /// <para><b>Bu bileşen etkiyi UYGULAMAZ.</b> Yalnızca "şu eşya harcandı" der
    /// (<c>PowerupSignals.RaisePicked</c>) ve etkiyi zaten bilen taraflar dinler: can
    /// <see cref="PlayerHealth"/>'te, mermi <see cref="PlayerWeapon"/>'da, saha etkisi
    /// <c>ZombieDirector</c>'da. Etkiyi buraya taşımak, aynı iş kuralını iki yere
    /// koymak olurdu (csharp-code.md) — ve <c>Gameplay</c>, <c>AI</c>'ı zaten göremez
    /// (gameplay-code.md).</para>
    ///
    /// <para><b>Girdi yerelde okunur, harcama sunucuda olur</b> (ADR-0004). İstemci
    /// "bunu kullanmak istiyorum" der; cebin gerçekten dolu olup olmadığına sunucu
    /// karar verir — <b>her RPC bir güven sınırıdır</b> (netcode.md). Yerelde bir
    /// tahmin yapılmıyor: eşya kullanmak nadir ve tek seferlik, yani 60 ms'lik his
    /// bütçesi burada geçerli değil; yanlış tahmin edilip geri alınan bir nuke,
    /// gecikmeden çok daha kötü okunur.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Powerups")]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerPowerups : NetworkBehaviour
    {
        [Header("Ayar")]
        [Tooltip("config/balance/zombie.json'dan uretilen varlik. Slot basina yigin " +
                 "tavani (drops.stackPerSlot) buradan gelir.")]
        [SerializeField] private ZombieConfigAsset zombieConfig;

        private PowerupInventory _inventory;

        // Esyanin YUKU cebe girerken saklanir: ne kadar iyilestirdigi ve kac saniye
        // surdugu zombie.json'da yaziyor ve onu okuyan taraf esyayi DUSUREN taraf
        // (PowerupPickup). Cebin kendi sayisini tutmasi, ayni denge degerinin iki
        // yerde durmasi olurdu (config-data.md).
        private readonly float[] _amounts = new float[8];
        private readonly float[] _seconds = new float[8];

        /// <summary>Arayüzün okuduğu cep. Sunucuda gerçek, istemcide yansıtılmış.</summary>
        public PowerupInventory Inventory => _inventory;

        private void Awake()
        {
            int capacity = zombieConfig != null
                ? zombieConfig.ToRuntime().DropsStackPerSlot
                : 5;

            _inventory = new PowerupInventory(capacity);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Cep TEK sahipli (PowerupSignals.StoreRequest notu). Ikinci bir oyuncu
            // bu satiri ezerse esya yanlis cebe girer - M-02'de sunucunun toplayan
            // oyuncuyu bulmasi gerekecek.
            PowerupSignals.StoreRequest = ServerTryStore;
            RunSignals.RunRestarted += OnRunRestarted;
        }

        public override void OnStopServer()
        {
            // OnStartServer'in kurdugunu OnStopServer bozar (csharp-code.md).
            // Statik bir delegede kalan olu referans, sahne degisiminde patlar.
            if (PowerupSignals.StoreRequest == ServerTryStore)
            {
                PowerupSignals.StoreRequest = null;
            }

            RunSignals.RunRestarted -= OnRunRestarted;

            base.OnStopServer();
        }

        /// <summary>Yeni run: cep boşalır, yoksa ikinci run birincinin nuke'uyla başlar.</summary>
        private void OnRunRestarted()
        {
            _inventory.Clear();
            TargetClear(connectionToClient);
        }

        /// <summary>
        /// Oyuncu öldü: <b>biriktirdiği eşyalar kaybolur</b> (2026-09-09, geliştirici
        /// isteği).
        ///
        /// <para>Ölümün kalıcı bedellerinden biri. Eşya bir <i>fırsattı</i> — cepte
        /// bekliyordu ve kullanılmadı; ölüm o fırsatı kapatıyor. Bu, cebi bir depo
        /// olmaktan çıkarıp bir <b>zamanlama kararı</b> olarak tutan şey: "sonra
        /// kullanırım" artık bir risk.</para>
        /// </summary>
        [Server]
        public void ServerClearOnDeath()
        {
            if (_inventory.IsEmpty) return;

            _inventory.Clear();
            TargetClear(connectionToClient);
        }

        [Server]
        private bool ServerTryStore(PowerupKind kind, float amount, float seconds)
        {
            if (!_inventory.TryStore(kind)) return false;

            int slot = (int)kind;
            _amounts[slot] = amount;
            _seconds[slot] = seconds;

            TargetStored(connectionToClient, (int)kind);
            return true;
        }

        /// <summary>İstemcinin cebi sunucununkini yansıtır — arayüz onu okuyor.</summary>
        [TargetRpc]
        private void TargetStored(NetworkConnection target, int kind)
        {
            if (isServer) return;
            _inventory.TryStore((PowerupKind)kind);
        }

        [TargetRpc]
        private void TargetConsumed(NetworkConnection target, int kind)
        {
            if (isServer) return;
            _inventory.TryConsume((PowerupKind)kind);
        }

        [TargetRpc]
        private void TargetClear(NetworkConnection target)
        {
            if (isServer) return;
            _inventory.Clear();
        }

        private void Update()
        {
            if (!isLocalPlayer) return;
            if (RunSignals.IsRunOver) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 4-5-6-7-8 (2026-09-09: atesli silah slotu ikiye inince 6'dan 4'e kaydi).
            // Sira PowerupKind sirasi (PowerupInventory notu): can, mermi, yavaslatma,
            // dondurma, nuke.
            if (keyboard.digit4Key.wasPressedThisFrame) TryUse(0);
            else if (keyboard.digit5Key.wasPressedThisFrame) TryUse(1);
            else if (keyboard.digit6Key.wasPressedThisFrame) TryUse(2);
            else if (keyboard.digit7Key.wasPressedThisFrame) TryUse(3);
            else if (keyboard.digit8Key.wasPressedThisFrame) TryUse(4);
        }

        private void TryUse(int slot)
        {
            if (slot < 0 || slot >= PowerupInventory.SlotCount) return;

            // Yerelde bir on eleme: bos slota basmak sunucuya paket gondermesin.
            // Bu bir GUVENLIK kontrolu DEGIL - sunucu ayni kontrolu yeniden yapiyor
            // (netcode.md: her RPC bir guven sinriri). Yalnizca gurultu azaltir.
            if (!_inventory.Has(PowerupInventory.KindAt(slot))) return;

            CmdUse(slot);
        }

        [Command]
        private void CmdUse(int slot)
        {
            if (RunSignals.IsRunOver) return;
            if (slot < 0 || slot >= PowerupInventory.SlotCount) return;

            PowerupKind kind = PowerupInventory.KindAt(slot);

            // ONCE TUKET, SONRA UYGULA (PowerupInventory.TryConsume notu): ters
            // sirada, etkinin icinden gelen bir hata sayaci azaltmadan birakir ve
            // esya sonsuzlasir.
            if (!_inventory.TryConsume(kind)) return;

            TargetConsumed(connectionToClient, slot);

            PowerupSignals.RaisePicked(kind, _amounts[slot], _seconds[slot]);
        }
    }
}
