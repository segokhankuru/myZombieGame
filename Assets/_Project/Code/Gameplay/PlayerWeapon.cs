using System;
using Bunker.Config;
using Bunker.Systems.Cards;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
using Bunker.Systems.Rounds;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Başlangıç silahı: hitscan, şarjör, dolum, geri tepme, isabet geri bildirimi.
    /// M1-06 — bu milestone'un en kritik işi.
    ///
    /// <para><b>Girdi aynı karede karşılanır.</b> Tetiğe basıldığı an geri tepme,
    /// iz ve mermi sayacı yereldeki oyuncuda hemen olur; host'un onayı beklenmez.
    /// Beklemek, tek kişilik oyunda bile hissedilen bir gecikme üretir ve oyunun ölü
    /// hissetmesinin en yaygın sebebidir (gameplay-code.md, 60 ms hedefi).</para>
    ///
    /// <para><b>Hasar host'ta uygulanır</b> (ADR-0004). İstemci "şuraya nişan aldım"
    /// der; kimin öldüğüne host karar verir. İstemcinin gönderdiği yön ve konum
    /// doğrulanır: atış hızı sunucu tarafında ayrıca sayılır, çünkü <b>her RPC bir güven
    /// sınırıdır</b> (netcode.md) ve döngüde çağrılabilen her şey hız sınırlı olmalıdır.</para>
    ///
    /// <para><b>Sayıların hiçbiri burada değil.</b> Hepsi
    /// <c>config/balance/weapon.json</c>'dan geliyor; bu dosyada bir denge sabiti
    /// bulursan o bir defadır (config-data.md).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Weapon")]
    public sealed class PlayerWeapon : NetworkBehaviour
    {
        [Header("Ayar")]
        [Tooltip("config/balance/weapon.json'dan uretilen varlik.")]
        [SerializeField] private WeaponConfigAsset weaponConfig;

        [Header("Referanslar")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private Camera playerCamera;

        [Tooltip("Merminin ciktigi nokta. Bos birakilirsa kamera kullanilir - gri " +
                 "kutuda dogru olan da budur, cunku silah modeli yok.")]
        [SerializeField] private Transform muzzle;

        [Header("Gri kutu geri bildirimi")]
        [Tooltip("Mermi izi cizgisi. Sanat gelene kadar atisin nereye gittigini " +
                 "gosteren tek sey bu.")]
        [SerializeField] private LineRenderer tracer;

        private WeaponConfig _config;
        private WeaponState _state;

        // Sunucunun hile denetimi. Istemcinin simulasyonunu TEKRARLAMAZ; makul olup
        // olmadigina bakar. Istemci otoritesi burada biter (BUG-002).
        private ServerFireGuard _guard;

        private float _tracerRemaining;
        private float _hitMarkerRemaining;
        private bool _lastShotWasHeadshot;
        private float _lastRejectWarnTime = -99f;

        /// <summary>Bir isabet onaylandı (kafa mı, öldürdü mü). HUD buna bağlanır.</summary>
        public event Action<bool, bool> HitConfirmed;

        /// <summary>Bir zombi öldürüldü — ekonomi buna bağlanır.</summary>
        public event Action<DamageKind, bool> KillConfirmed;

        public int RoundsInMagazine => _state?.RoundsInMagazine ?? 0;
        public int Reserve => _state?.Reserve ?? 0;
        public int MagazineCapacity => _state?.MagazineCapacity ?? 0;
        public bool IsReloading => _state?.IsReloading ?? false;
        public float ReloadProgress01 => _state?.ReloadProgress01 ?? 0f;

        /// <summary>İsabet işaretinin kalan süresi (0 = gösterme).</summary>
        public float HitMarkerRemaining => _hitMarkerRemaining;

        public bool LastHitWasHeadshot => _lastShotWasHeadshot;

        // ---------------------------------------------------------------- kurulum

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);

            if (weaponConfig == null)
            {
                // Eksik ayar sessiz varsayilanla gecistirilmez (config-protocol.md).
                Debug.LogError("[Silah] weapon.asset atanmamis. 'Bunker/Config/Ice Aktar' " +
                               "ile uret, 'Bunker/Zombi/Test Alanini Kur' ile bagla.", this);
                enabled = false;
                return;
            }

            _config = weaponConfig.ToRuntime();
            _state = new WeaponState(_config);
            _guard = new ServerFireGuard(_config);

            // Geri tepmenin toparlanmasi kameranin sahibinde yasar ama sayisi silahin
            // ayarindan gelir - tek kaynak.
            if (controller != null)
            {
                controller.ConfigureRecoilRecovery(
                    _config.RecoilRecoverySpeedDegreesPerSecond, _config.RecoilMaxPitchDegrees);
            }

            if (tracer != null) tracer.enabled = false;
        }

        private void OnEnable()
        {
            RunSignals.RunRestarted += OnRunRestarted;
            CardSignals.LoadoutChanged += OnLoadoutChanged;
        }

        private void OnDisable()
        {
            RunSignals.RunRestarted -= OnRunRestarted;
            CardSignals.LoadoutChanged -= OnLoadoutChanged;
        }

        /// <summary>
        /// Kart yığını değişti: silahın türetilmiş sayıları yeniden kurulur.
        ///
        /// <para><b>İstemci ve sunucu AYNI anda güncellenir.</b> Biri güncellenip
        /// diğeri kalsaydı doğrulayıcı meşru atışları reddederdi — BUG-002'nin
        /// birebir tekrarı.</para>
        ///
        /// <para><b>Neden olayla, kare başına okumayla değil:</b> şarjör kapasitesi ve
        /// dolum süresi silahın <i>durumuna</i> giriyor; her karede yeniden hesaplamak
        /// hem gereksiz hem de dolum ortasında süreyi değiştirir.</para>
        /// </summary>
        private void OnLoadoutChanged(CardLoadout loadout)
        {
            WeaponModifiers mods = BuildModifiers();

            _state?.ApplyModifiers(mods);
            _guard?.ApplyModifiers(mods);
        }

        /// <summary>Kart VE tezgah etkileri tek noktadan okunur (RunModifiers).</summary>
        private static WeaponModifiers BuildModifiers() =>
            new WeaponModifiers(
                fireRate: RunModifiers.Total(CardStat.FireRate),
                reloadSpeed: RunModifiers.Total(CardStat.ReloadSpeed),
                damage: RunModifiers.Total(CardStat.WeaponDamage),
                magazine: Mathf.RoundToInt(RunModifiers.Total(CardStat.MagazineCapacity)),
                reserve: Mathf.RoundToInt(RunModifiers.Total(CardStat.ReserveCapacity)),
                headshotMultiplier: RunModifiers.Total(CardStat.HeadshotMultiplier));

        /// <summary>
        /// Yeni run: mermi başlangıç değerine döner.
        ///
        /// <para><b>Tur başında DEĞİL, RUN başında.</b> Önceki sürüm her tur başında
        /// mermiyi tazeliyordu ve kendi yorumu bunu şöyle gerekçelendiriyordu:
        /// <i>"M-01'de mermi kaynağı (duvar silahı, dağıtıcı) henüz yok... Bu bir denge
        /// kararı değil, eksik sistemin geçici yerine geçen şey — <b>M1-10 duvar silahı
        /// gelince kaldırılacak</b>."</i></para>
        ///
        /// <para><b>M1-10 geldi ve bu iskele kalmıştı.</b> Yani bedava mermi bir tasarım
        /// tercihi değil, silinmesi unutulmuş bir geçici çözümdü — ve oyunu tam olarak
        /// geliştiricinin 2026-09-04'te işaret ettiği kadar kolaylaştırıyordu.</para>
        ///
        /// <para><b>Sonucu küçük değil:</b> denge simülasyonu mermiyi satın alınan bir
        /// kaynak varsayarak koştu ve gelirin %77–98'inin mermiye gittiğini buldu
        /// (<c>design/economy/curves.md</c>). Yani oyun bugüne kadar simülasyonun
        /// anlattığından <b>belirgin şekilde kolaydı</b>; bu satırla ikisi hizalanıyor.</para>
        ///
        /// <para><b>Run sıfırlaması ŞART:</b> tur sıfırlaması kalkınca <c>R</c> ile
        /// başlayan ikinci run, birincinin kalan mermisiyle başlardı (M1-11 AC-6).</para>
        /// </summary>
        private void OnRunRestarted()
        {
            _state.Reset();
            _guard.Reset();
        }

        // ---------------------------------------------------------------- kare dongusu

        private void Update()
        {
            if (_state == null) return;

            float dt = Time.deltaTime;

            _state.Tick(dt);
            TickFeedback(dt);

            // Yalnizca yerel oyuncu kendi silahini surer.
            if (!isLocalPlayer) return;

            // Run bitti: girdi kesilir (M1-11, AC-3). Skor ekraninin arkasindan ates
            // etmek, olumu bir sonuc olmaktan cikarir. Silahin kendi zamani (dolum,
            // geri bildirim) yukarida akmaya devam eder - durdurulan sey KOMUT.
            // Run bitti YA DA tur arasi ekrani acik: girdi kesilir. Ekran acikken
            // ates etmek, bakis cevirmek ya da satin almak, fareyle kart secmeyi
            // imkansiz kilardi.
            if (RunSignals.IsRunOver || CardSignals.IsAnyMenuOpen) return;

            ReadInput();
        }

        private void ReadInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                StartReload();
            }

            if (mouse == null) return;

            // Yari otomatik his: basili tutmak degil, her basis bir atis. Otomatik
            // ates silah cesitliligiyle (SYS-03) gelecek.
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            if (!pressed && _state.Phase != WeaponPhase.Ready) return;

            FireResult result = _state.TryFire(pressed);

            switch (result)
            {
                case FireResult.Fired:
                    FireLocally();
                    break;

                case FireResult.Empty:
                    // Bos sarjorde tetige basmak dolum baslatir. Oyuncunun ayrica
                    // R'ye basmasini beklemek, sürünün icinde ceza gibi hissettirir.
                    StartReload();
                    break;
            }
        }

        /// <summary>
        /// Dolumu başlatır <b>ve sunucuya bildirir</b>.
        ///
        /// <para><b>Bu bildirimin yokluğu BUG-001'di:</b> istemci kendi şarjörünü
        /// dolduruyor, sunucunun saydığı mermi ise ilk şarjörden sonra hiç geri
        /// gelmiyordu. Sunucuda bir kaynağı sayan sistem, o kaynağı geri veren yolu
        /// <b>aynı anda</b> yazmak zorundadır.</para>
        /// </summary>
        private void StartReload()
        {
            if (!_state.TryStartReload()) return;

            CmdReload();
        }

        /// <summary>
        /// Yedege mermi ekler. <b>Yalnizca sunucu cagirir</b> (duvar silahi, dagitici).
        /// Istemcinin gorunen sayaci bir sonraki senkronda degil, hemen guncellensin
        /// diye yerel durum da tazelenir - mermi almanin karsiligi aninda gorulmeli.
        /// </summary>
        [Server]
        public void ServerAddReserve(int amount)
        {
            if (amount <= 0) return;

            _guard.AddReserve(amount);
            TargetAddReserve(connectionToClient, amount);
        }

        [TargetRpc]
        private void TargetAddReserve(NetworkConnection target, int amount)
        {
            _state.AddReserve(amount);
        }

        /// <summary>
        /// Sunucuya dolumu bildirir. İstemci <i>ne zaman</i> doldurduğunu söyler;
        /// <b>ne kadar süreceğine sunucu kendi ayarından karar verir</b>, yani dolum
        /// süresini kısaltarak avantaj alınamaz.
        /// </summary>
        [Command]
        private void CmdReload()
        {
            _guard.NoteReload(Time.time);
        }

        /// <summary>
        /// Yerel geri bildirim: geri tepme, iz, sayaç. <b>Host'un onayı beklenmez.</b>
        /// </summary>
        private void FireLocally()
        {
            Transform origin = muzzle != null ? muzzle : playerCamera.transform;
            Vector3 direction = ApplySpread(playerCamera.transform.forward);

            // Geri tepme atisin ayni karesinde. Bir kare sonrasi bile "gecikmis"
            // hissettirir.
            if (controller != null)
            {
                float yaw = UnityEngine.Random.Range(-1f, 1f) * _config.RecoilYawDegreesPerShot;
                controller.AddRecoil(_config.RecoilPitchDegreesPerShot, yaw);
            }

            Vector3 endPoint = origin.position + direction * _config.FireRangeMeters;

            // Yerel isin YALNIZCA gorsel icindir - hasari host uygular. Iki taraf
            // farkli sonuc bulursa gecerli olan host'unkidir.
            if (Physics.Raycast(origin.position, direction, out RaycastHit hit, _config.FireRangeMeters,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;
            }

            ShowTracer(origin.position, endPoint);

            CmdFire(origin.position, direction);
        }

        private Vector3 ApplySpread(Vector3 forward)
        {
            if (_config.FireSpreadDegrees <= 0f) return forward;

            // Koni icinde rastgele sapma. Determinizm gerekmiyor: atis tekrar
            // oynatilmiyor ve kaydedilmiyor (csharp-code.md'nin seed kurali
            // tekrarlanabilir seyler icindir).
            float radius = Mathf.Tan(_config.FireSpreadDegrees * Mathf.Deg2Rad);
            Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;

            Transform cam = playerCamera.transform;
            return (forward + cam.right * offset.x + cam.up * offset.y).normalized;
        }

        // ---------------------------------------------------------------- otorite

        /// <summary>
        /// Hasarın uygulandığı tek yer. <b>Her RPC bir güven sınırıdır</b> (netcode.md):
        /// atış hızı sunucuda ayrıca sayılır, böylece istemci döngüde çağırarak
        /// sınırsız hasar üretemez.
        /// </summary>
        [Command]
        private void CmdFire(Vector3 origin, Vector3 direction, NetworkConnectionToClient sender = null)
        {
            // Hile denetimi: sunucu simulasyonu tekrarlamaz, MAKUL olup olmadigina
            // bakar. Kare kare aynilik beklemek, komut agdan bir kare sonra geldigi
            // icin oyuncunun tikini yiyordu (BUG-002).
            FireRejection rejection = _guard.TryAcceptShot(Time.time);

            if (rejection != FireRejection.None)
            {
                // Reddedilen atis SESSIZ olmaz. Bu satirin yoklugu, sunucunun sarjoru
                // bittikten sonra butun atislarin sessizce dusmesine ve oyunun
                // "zombiler hasar yemiyor" gibi gorunmesine sebep oldu; teshis bir
                // oyun testi surdu (BUG-001).
                WarnRejectedShot(rejection);
                return;
            }

            if (direction.sqrMagnitude < 0.001f) return;
            direction.Normalize();

            // Konum dogrulamasi: istemci kendi konumu uzerinde otorite sahibidir
            // (ADR-0004) ama silahin oyuncudan kopmasina izin verilmez.
            const float maxOriginDriftMeters = 3f;
            if ((origin - transform.position).sqrMagnitude >
                maxOriginDriftMeters * maxOriginDriftMeters)
            {
                return;
            }

            // Tek isin, EN YAKIN isabet. Onceki surum RaycastNonAlloc + elle en yakini
            // bulma kullaniyordu; o cagri sekiz slotluk tamponu SIRASIZ doldurur ve
            // isin uzerinde sekizden fazla carpisan varsa gercek en yakini atabilir -
            // kalabalik bir surunun icinde tam olarak "mermi gitmedi" hatasi uretir.
            // Physics.Raycast en yakini garanti eder ve tahsis yapmaz.
            if (!Physics.Raycast(origin, direction, out RaycastHit serverHit,
                                 _config.FireRangeMeters, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            // Isabet eden sey ne oldugunu KENDISI soyler: silah kafa kutusunu bilmez,
            // katman testi yapmaz. Bilmediginde yeni bir hedef tipi eklemek silahi
            // degistirmeyi gerektirmez.
            var target = serverHit.collider.GetComponent<IDamageable>();
            if (target == null || !target.IsAlive) return;

            // Vurulan sey kafa kutusu olup olmadigini KENDISI soyler - hasar
            // uygulanmadan once. Once taban hasari gonderip sonra carpani ikinci bir
            // uygulamayla eklemek, tek atistan iki hasar olayi uretirdi; can havuzu
            // olumu bir kez bildirdigi icin de ikincisi sessizce yutulurdu.
            bool headshot = target.CountsAsHeadshot;

            DamageResult result = target.ApplyDamage(
                new DamageInfo(_guard.DamageFor(headshot), DamageKind.Bullet, headshot));

            TargetReportHit(sender, headshot, result.Killed);

            if (result.Killed) KillConfirmed?.Invoke(DamageKind.Bullet, headshot);
        }

        /// <summary>
        /// Sonucu <b>yalnızca atan oyuncuya</b> bildirir. İsabet geri bildirimi kişisel
        /// bir bilgidir; herkese yayınlamak hem bant genişliği hem gürültüdür.
        /// </summary>
        [TargetRpc]
        private void TargetReportHit(NetworkConnection target, bool headshot, bool killed)
        {
            _hitMarkerRemaining = _config.FeelHitMarkerSeconds;
            _lastShotWasHeadshot = headshot;
            HitConfirmed?.Invoke(headshot, killed);
        }

        // ---------------------------------------------------------------- geri bildirim

        /// <summary>
        /// Sunucunun reddettiği atışı görünür kılar — <b>saniyede en fazla bir kez</b>,
        /// çünkü döngüde çağrılan bir istemci Console'u da doldurabilmemeli.
        /// </summary>
        private void WarnRejectedShot(FireRejection reason)
        {
            if (Time.unscaledTime - _lastRejectWarnTime < 1f) return;
            _lastRejectWarnTime = Time.unscaledTime;

            Debug.LogWarning($"[Silah] Sunucu atisi reddetti: {reason}. " +
                             "Istemci ile sunucunun silah durumu ayrismis olabilir " +
                             "(dolum bildirilmedi, ya da atis hizi asildi).", this);
        }

        private void ShowTracer(Vector3 from, Vector3 to)
        {
            if (tracer == null) return;

            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.enabled = true;
            _tracerRemaining = _config.FeelTracerSeconds;
        }

        private void TickFeedback(float dt)
        {
            if (_tracerRemaining > 0f)
            {
                _tracerRemaining -= dt;
                if (_tracerRemaining <= 0f && tracer != null) tracer.enabled = false;
            }

            if (_hitMarkerRemaining > 0f) _hitMarkerRemaining -= dt;
        }
    }
}
