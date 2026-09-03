using System;
using Bunker.Config;
using Bunker.Systems.Combat;
using Bunker.Systems.Config;
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

        // Sunucunun kendi kopyasi: istemcinin soyledigi degil, sunucunun saydigi
        // gecerlidir. Istemci otoritesi burada BITER.
        private WeaponState _serverState;

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
            _serverState = new WeaponState(_config);

            // Geri tepmenin toparlanmasi kameranin sahibinde yasar ama sayisi silahin
            // ayarindan gelir - tek kaynak.
            if (controller != null)
            {
                controller.ConfigureRecoilRecovery(
                    _config.RecoilRecoverySpeedDegreesPerSecond, _config.RecoilMaxPitchDegrees);
            }

            if (tracer != null) tracer.enabled = false;
        }

        // ---------------------------------------------------------------- kare dongusu

        private void Update()
        {
            if (_state == null) return;

            float dt = Time.deltaTime;

            _state.Tick(dt);
            if (isServer) _serverState.Tick(dt);

            TickFeedback(dt);

            // Yalnizca yerel oyuncu kendi silahini surer.
            if (!isLocalPlayer) return;

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
        /// <para><b>Bu satırın yokluğu M1-06'nın ilk oyun testinde bulunan hataydı:</b>
        /// istemci kendi şarjörünü dolduruyor, sunucunun gölge şarjörü ise ilk 12
        /// mermiden sonra sonsuza kadar boş kalıyordu. Sonuç, oyuncunun gördüğü şekliyle
        /// "üç zombiden sonra zombiler hasar yemiyor" — hiçbir hata mesajı olmadan.</para>
        ///
        /// <para>Ders şu: sunucuda mermi sayan bir sistem, <b>mermiyi geri veren yolu da
        /// aynı anda</b> yazmak zorundadır. Yarısı yazılmış bir otorite, otorite değil
        /// sessiz bir duvardır.</para>
        /// </summary>
        private void StartReload()
        {
            if (!_state.TryStartReload()) return;

            CmdReload();
        }

        /// <summary>
        /// Sunucunun gölge şarjörünü de doldurur. İstemci ne zaman doldurduğunu söyler;
        /// <b>ne kadar süreceğine sunucu kendi ayarından karar verir</b>, yani dolum
        /// süresini kısaltarak avantaj alınamaz.
        /// </summary>
        [Command]
        private void CmdReload()
        {
            _serverState.TryStartReload();
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
            if (Physics.Raycast(origin.position, direction, out RaycastHit hit, _config.FireRangeMeters))
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
            // Hiz siniri: istemcinin ne dedigi degil, sunucunun saydigi gecerli.
            FireResult serverResult = _serverState.TryFire(true);

            if (serverResult != FireResult.Fired)
            {
                // Reddedilen atis SESSIZ olmaz. Bu satirin yoklugu, sunucunun sarjoru
                // bittikten sonra butun atislarin sessizce dusmesine ve oyunun
                // "zombiler hasar yemiyor" gibi gorunmesine sebep oldu; teshis bir
                // oyun testi surdu. Bir istemcinin atisi reddedilebilir (hile,
                // gecikme), ama gorunmez olmamali (netcode.md).
                WarnRejectedShot(serverResult);
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
                                 _config.FireRangeMeters))
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
                new DamageInfo(_serverState.DamageFor(headshot), DamageKind.Bullet, headshot));

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
        private void WarnRejectedShot(FireResult reason)
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
