using System.Collections.Generic;
using Bunker.AI;
using Bunker.Systems.Net;
using Mirror;
using UnityEngine;

namespace Bunker.Net
{
    /// <summary>
    /// Zombi konumlarının <b>tek</b> ağ geçidi. M1-05, ADR-0004'ün kilitlenen seam'i.
    ///
    /// <para><b>Zombide <c>NetworkTransform</c> yoktur.</b> Bu kural buradan doğar: 40
    /// nesnenin her biri kendi başına konum yayınlarsa 40 ayrı mesaj, 40 ayrı başlık ve
    /// nesne başına ayar olur; bant genişliği kimsenin bakmadığı bir yerde büyür. Burada
    /// tek paket var, maliyeti tek yerde ölçülüyor: 40 zombi = 482 bayt, 10 Hz'de
    /// istemci başına ~4.8 KB/sn.</para>
    ///
    /// <para><b>Bağımlılık yönü:</b> <c>Bunker.AI</c> ağı bilmez. Otoriteyi bu sınıf
    /// söyler (<c>SetAuthoritative</c>), zombi yönetmeni yalnızca dinler. Sayesinde
    /// zombi mantığı tek başına, ağsız test edilebilir kalır.</para>
    ///
    /// <para><b>M-01'de doğrulanmadı.</b> Bu kod ağ-farkındadır ama iki istemciyle
    /// sınanmamıştır; doğrulama M-02'nin işi (netcode.md: tek makinede çalışan şey
    /// çalışmış sayılmaz). Sınanacak dört durum: geç katılan, host kopması, eylem
    /// ortasında kopan istemci, aynı tick'te çakışma.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Network Relay")]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class ZombieNetworkRelay : NetworkBehaviour
    {
        [Header("Referans")]
        [SerializeField] private ZombieDirector director;

        [Header("Bant genisligi")]
        [Tooltip("Saniyede kac konum paketi. Tick hizi degil - bilincli olarak dusuk. " +
                 "Artirmak bant genisligini dogrusal buyutur; azaltmak zombileri " +
                 "istemcide kesik gosterir. 10 Hz + istemci yumusatmasi bu tur icin yeterli.")]
        [SerializeField] private float snapshotsPerSecond = 10f;

        private readonly List<ushort> _seen = new List<ushort>(64);
        private readonly List<ushort> _toRemove = new List<ushort>(16);

        // Tamponlar bir kez ayrilir: kare basina tahsis yasak (csharp-code.md), ve
        // ag kodu her saniye onlarca kez calisir.
        private ZombieState[] _states;
        private byte[] _buffer;

        private float _timer;
        private bool _warnedNotSpawned;

        private void Awake()
        {
            if (director == null) director = FindFirstObjectByType<ZombieDirector>();

            _states = new ZombieState[ZombieSnapshot.MaxZombies];
            _buffer = new byte[ZombieSnapshot.SizeFor(ZombieSnapshot.MaxZombies)];

            if (director == null)
            {
                Debug.LogError("[Zombi/Ag] ZombieDirector bulunamadi. Zombi konumlari " +
                               "yayinlanamaz.", this);
                enabled = false;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Host otoritelidir: zombiler yalnizca burada dusunur (M-01 kisiti).
            director.SetAuthoritative(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Host ayni zamanda istemcidir; onun simulasyonu kapatilmaz.
            if (isServer) return;

            director.SetAuthoritative(false);
        }

        private void Update()
        {
            if (!isServer) return;

            float interval = snapshotsPerSecond <= 0f ? 0.1f : 1f / snapshotsPerSecond;

            _timer += Time.deltaTime;
            if (_timer < interval) return;
            _timer = 0f;

            // Uzak istemci yoksa paket uretmenin anlami yok. Solo oturum bir host
            // oturumudur ve bu satir onu bedavaya cevirir.
            if (NetworkServer.connections.Count <= 1) return;

            // Sahne nesnesi Mirror tarafindan spawn edilmemisse RPC sessizce dusherdi
            // ve zombiler istemcilerde HIC gorunmezdi - solo oynarken fark edilmeyen,
            // M-02'de bir gun kaybettiren cinsten bir hata. Bir kez, yuksek sesle.
            if (netId == 0 && !_warnedNotSpawned)
            {
                _warnedNotSpawned = true;
                Debug.LogError("[Zombi/Ag] Bu nesne ag uzerinde spawn edilmemis " +
                               "(sahne nesnesinin sceneId'si yok). Sahneyi Unity'de bir kez " +
                               "kaydet ya da 'Bunker/Zombi/Test Alanini Kur' calistir; " +
                               "aksi halde zombi konumlari istemcilere gitmez.", this);
                return;
            }

            BroadcastSnapshot();
        }

        private void BroadcastSnapshot()
        {
            IReadOnlyList<ZombieAgent> active = director.Active;
            int count = 0;

            for (int i = 0; i < active.Count && count < ZombieSnapshot.MaxZombies; i++)
            {
                ZombieAgent zombie = active[i];
                if (zombie == null || zombie.NetId == 0) continue;

                Vector3 p = zombie.transform.position;

                _states[count++] = new ZombieState
                {
                    Id = zombie.NetId,
                    X = p.x,
                    Y = p.y,
                    Z = p.z,
                    Yaw = zombie.transform.eulerAngles.y
                };
            }

            int length = ZombieSnapshot.Write(_buffer, _states, count);

            // ArraySegment: her paket icin yeni bir byte[] ayirmak saniyede 10 kez cop
            // uretirdi. Tampon bir kez ayrildi, yalnizca dolu kismi gonderiliyor.
            RpcApplySnapshot(new System.ArraySegment<byte>(_buffer, 0, length));
        }

        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcApplySnapshot(System.ArraySegment<byte> payload)
        {
            // Host kendi paketini uygulamaz: zaten gercegin kaynagi o.
            if (isServer) return;
            if (payload.Array == null) return;

            if (payload.Count > _buffer.Length)
            {
                Debug.LogWarning("[Zombi/Ag] Paket tampondan buyuk, atildi.");
                return;
            }

            // Okuyucunun tamponu bu cagri bitince gecersiz olur; kendi tamponumuza
            // kopyalanir. Kopya tahsis degildir - hedef zaten ayrilmis durumda.
            System.Array.Copy(payload.Array, payload.Offset, _buffer, 0, payload.Count);

            int count = ZombieSnapshot.Read(_buffer, payload.Count, _states);

            if (count < 0)
            {
                // Bozuk paket sessizce yutulmaz. Guvenilmeyen veri bir hata degil,
                // beklenen bir durumdur - ama gorunmez olmamali (netcode.md).
                Debug.LogWarning("[Zombi/Ag] Bozuk konum paketi atildi.");
                return;
            }

            _seen.Clear();

            for (int i = 0; i < count; i++)
            {
                ZombieState s = _states[i];
                ZombieAgent zombie = director.EnsureProxy(s.Id);

                if (zombie == null) continue;   // havuz sinirinda - tanimli durum

                zombie.ApplyNetworkState(new Vector3(s.X, s.Y, s.Z), s.Yaw);
                _seen.Add(s.Id);
            }

            RemoveMissingProxies();
        }

        /// <summary>
        /// Sunucunun artık göndermediği vekilleri kaldırır — ölen zombiler ayrı bir
        /// mesajla değil, <b>pakette görünmeyerek</b> yok olur. Ayrı bir ölüm mesajı
        /// kaybolabilir; paket zaten her tick geliyor, kaybolan bir paketi bir
        /// sonraki düzeltir.
        /// </summary>
        private void RemoveMissingProxies()
        {
            _toRemove.Clear();
            IReadOnlyList<ZombieAgent> active = director.Active;

            for (int i = 0; i < active.Count; i++)
            {
                ZombieAgent zombie = active[i];
                if (zombie == null) continue;
                if (_seen.Contains(zombie.NetId)) continue;

                _toRemove.Add(zombie.NetId);
            }

            for (int i = 0; i < _toRemove.Count; i++)
            {
                director.RemoveProxy(_toRemove[i]);
            }
        }
    }
}
