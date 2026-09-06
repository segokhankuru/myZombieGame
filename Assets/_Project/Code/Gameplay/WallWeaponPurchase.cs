using Bunker.Config;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Bunker.Systems.Economy;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Duvardaki silah alımı. M1-10, silah kataloğuyla genişletildi (2026-09-06).
    ///
    /// <para><b>İki aşamalı, klasik ve doğru sıra:</b> ilk alışta <b>silahı</b>
    /// verir, sonraki alışlarda aynı silahın <b>mermisini</b>. Oyuncu duvara ilk
    /// geldiğinde bir karar verir (bu silahı istiyor muyum), sonra oraya bir musluk
    /// olarak döner.</para>
    ///
    /// <para><b>Neden mermi musluğu şart:</b> musluk olmadan tur ekonomisi tek yönlü —
    /// puan birikir, harcanacak yer yoktur, mermi biter ve oyun bıçakla oynanan başka
    /// bir oyuna döner.</para>
    ///
    /// <para><b>Silah id'si buradan, fiyat katalogdan.</b> Fiyatı bu bileşene yazmak,
    /// aynı silahın iki duvarda iki farklı fiyata satılmasına açık kapı bırakırdı
    /// (config-data.md: bir sayı tek yerde durur).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Wall Weapon Purchase")]
    public sealed class WallWeaponPurchase : MonoBehaviour, IPurchasable
    {
        [Header("Ayar")]
        [Tooltip("config/balance/economy.json'dan uretilen varlik.")]
        [SerializeField] private EconomyConfigAsset economyConfig;

        [Tooltip("config/content/weapons.json'dan uretilen katalog. Duvarin ne sattigini " +
                 "OYUNCUDAN BAGIMSIZ bilmesi icin: fiyat ve model, kimse bakmasa da bellidir.")]
        [SerializeField] private WeaponCatalogAsset catalog;

        [Tooltip("Bu duvarin sattigi silahin id'si (config/content/weapons.json). " +
                 "Bos birakilirsa yalnizca eldeki silaha mermi satar.")]
        [SerializeField] private string weaponId = string.Empty;

        [SerializeField] private string displayName = "MERMI";

        [Tooltip("Duvardaki silah modelinin buyuklugu. Denge degeri degil - okunabilirlik.")]
        [SerializeField] private float displayScale = 2.2f;

        /// <summary>Duvarin sattigi silahin tanimi. Katalogdan bir kez cozulur.</summary>
        private WeaponDefinition _sold;

        private int _ammoCost;
        private int _magazinesPerPurchase;

        /// <summary>Son satın alan oyuncu; sunucu silahı/mermiyi ona yazar.</summary>
        private PlayerWeapon _lastBuyer;

        // Fiyat ve ipucu ALICIYA GORE degisir: silahi olmayan icin "SATIN AL",
        // olan icin "MERMI". Ikisini tek metinde birlestirmek, oyuncunun ne
        // odeyecegini bastan bilmemesi demek olurdu.
        private PlayerWeapon _prospect;

        public bool IsAvailable => true;   // kaynak, karar degil: tekrar alinabilir

        public int Cost
        {
            get
            {
                WeaponDefinition definition = Definition(_prospect);

                if (!definition.IsValid) return _ammoCost;

                bool owned = _prospect != null && _prospect.Owns(definition.Id);

                return owned
                    ? (definition.AmmoPrice > 0 ? definition.AmmoPrice : _ammoCost)
                    : definition.Price;
            }
        }

        public string Prompt
        {
            get
            {
                WeaponDefinition definition = Definition(_prospect);

                if (!definition.IsValid) return $"{displayName}  -  {Cost} puan";

                bool owned = _prospect != null && _prospect.Owns(definition.Id);

                return owned
                    ? $"{definition.DisplayName} MERMISI  -  {Cost} puan"
                    : $"{definition.DisplayName}  -  {Cost} puan";
            }
        }

        private void Awake()
        {
            if (economyConfig == null)
            {
                Debug.LogError("[Duvar silahi] economy.asset atanmamis. " +
                               "'Bunker/Config/Ice Aktar' ile uret, kurulum araci baglar.", this);
                enabled = false;
                return;
            }

            EconomyConfig config = economyConfig.ToRuntime();

            // Mermi fiyati SILAH bandindan gelmiyor (2026-09-05): silahin kendi mermi
            // fiyati katalogda; buradaki sayi yalnizca katalogsuz duruma yedek.
            _ammoCost = config.AmmoRefillCost;
            _magazinesPerPurchase = config.AmmoMagazinesPerPurchase;

            ResolveSoldWeapon();
            BuildDisplayModel();
        }

        /// <summary>
        /// Duvarın sattığı silahı katalogdan çözer.
        ///
        /// <para><b>Oyuncudan bağımsız</b>: duvarın ne sattığı, kimse ona bakmasa da
        /// bellidir. Önceki sürüm tanımı <i>alıcının</i> kataloğundan okuyordu ve bu,
        /// duvarın kimse yaklaşmadan önce ne sattığını bilmemesi demekti — yani modelini
        /// de çizemezdi.</para>
        /// </summary>
        private void ResolveSoldWeapon()
        {
            if (string.IsNullOrEmpty(weaponId) || catalog == null) return;

            // His ayarlari burada onemsiz (duvar ates etmiyor); sifir gecmek yeterli.
            System.Collections.Generic.List<WeaponDefinition> all = catalog.ToRuntime(0f, 0f, 0f);

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id != weaponId) continue;

                _sold = all[i];
                return;
            }

            Debug.LogWarning($"[Duvar silahi] '{weaponId}' katalogda yok; bu duvar yalnizca " +
                             "mermi satacak. config/content/weapons.json'a bak.", this);
        }

        /// <summary>
        /// Duvara silahın <b>modelini</b> asar (2026-09-06, geliştirici: <i>"silahları
        /// duvara ekle ve görseliyle görünsün"</i>).
        ///
        /// <para><b>Neden model, yazı değil:</b> oyuncu duvarın önünden koşarak geçiyor.
        /// "POMPALI 1200 puan" yazısını okumak için durmak gerekir; silüeti tanımak için
        /// bakmak yeter. Yazı da duruyor — ama ikinci sırada.</para>
        ///
        /// <para>Model <see cref="WeaponShape"/>'ten geliyor, yani <b>elde göreceğin
        /// şeyin aynısı</b>. Ayrı çizilseydi duvardaki pompalı ile eldeki pompalı zamanla
        /// birbirine benzemez olurdu.</para>
        /// </summary>
        private void BuildDisplayModel()
        {
            if (!_sold.IsValid) return;

            var host = new GameObject("WeaponDisplay");
            host.transform.SetParent(transform, false);

            // Levhanin biraz onunde ve hafif egik: duz duran bir silah levhaya yapisik
            // gorunur, egik olan "asili" okunur.
            host.transform.localPosition = new Vector3(0f, 0.15f, -0.18f);
            host.transform.localRotation = Quaternion.Euler(0f, 90f, 12f);

            Material body = MakeMaterial(new Color(0.18f, 0.19f, 0.22f));
            Material accent = MakeMaterial(new Color(0.38f, 0.34f, 0.29f));

            WeaponShape.Build(host.transform, _sold.Id, displayScale, body, accent);
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.3f);
            return material;
        }

        /// <summary>
        /// Alıcıyı işaretler. Etkileşim tarafı çağırır; <b>kim aldıysa silah onun</b>.
        ///
        /// <para>Fiyat ve ipucu da bu oyuncuya göre hesaplanır — duvara bakan iki
        /// oyuncudan biri silahı almışsa ikisi farklı fiyat görmeli.</para>
        /// </summary>
        public void SetBuyer(PlayerWeapon buyer)
        {
            _lastBuyer = buyer;
            _prospect = buyer;
        }

        public void OnPurchased()
        {
            if (_lastBuyer == null) return;

            WeaponDefinition definition = Definition(_lastBuyer);

            if (definition.IsValid && !_lastBuyer.Owns(definition.Id))
            {
                // Silahi ver ve ELINE AL: satin alip envanterde beklemesi, oyuncunun
                // "aldim mi almadim mi" diye tusa basmasi demek olurdu.
                _lastBuyer.ServerGrantWeapon(definition, equip: true);
                _lastBuyer = null;
                return;
            }

            // Mermi: silahin KENDI sarjor kapasitesi kadar. Sabit bir sayi, pompaliya
            // 60, tabancaya 60 vermek olurdu - biri comert, digeri cimri.
            _lastBuyer.ServerAddReserve(_magazinesPerPurchase * _lastBuyer.MagazineCapacity);
            _lastBuyer = null;
        }

        /// <summary>
        /// Bu duvarin sattigi silah. <b>Katalogdan cozuldu</b>, aliciya bagli degil -
        /// yalnizca "bu oyuncu almis mi" sorusu aliciya bakar.
        /// </summary>
        private WeaponDefinition Definition(PlayerWeapon buyer) => _sold;
    }
}
