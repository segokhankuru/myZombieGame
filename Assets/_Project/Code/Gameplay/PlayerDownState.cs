using Bunker.Systems.Rounds;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>Bir oyuncunun hayatta olma durumu (co-op).</summary>
    public enum LifeState
    {
        /// <summary>Ayakta, oynuyor.</summary>
        Alive,

        /// <summary>Yere düştü. Hareket edemez, ateş edemez — <b>ama kurtarılabilir</b>.</summary>
        Downed,

        /// <summary>Tur bitene kadar öldü. Bir sonraki turda dirilir; o zamana kadar izler.</summary>
        Dead
    }

    /// <summary>
    /// Co-op'ta <b>yere düşme, kaldırılma ve bir sonraki turda dirilme</b>.
    /// 2026-09-07.
    ///
    /// <para><b>Neden var</b> (geliştirici): <i>"co-op için, eğer o tur yere düştün ve
    /// kaldırılmazsan diğer tura kadar ölü sayılıp öyle dirileceksin; tur boyunca da
    /// arkadaşlarını spectate edebilmeli."</i></para>
    ///
    /// <para><b>Neden ölüm doğrudan run'ı bitirmiyor artık:</b> dört kişilik bir
    /// oturumda bir kişinin ölmesiyle herkesin oyununun bitmesi, oyunun <i>en zayıf
    /// oyuncunun</i> hızında oynanması demek — ve o kişi de her ölümde diğerlerinin
    /// turunu bitirdiği için oynamaktan çekinir. Yere düşmek bir <b>maliyet</b>
    /// olmalı, bir son değil: arkadaşın seni kaldırmak için ateş etmeyi bırakıp
    /// gelmek zorunda, yani ölümün bedelini takım ödüyor.</para>
    ///
    /// <para><b>Neden diriliş tur başında, anında değil:</b> anında dirilme yere
    /// düşmeyi bedelsiz yapardı. Turu izlemek gerçek bir ceza ama <i>oyundan atılmak
    /// değil</i> — bir sonraki tur kesin geliyor ve ne kadar kaldığı ekranda yazıyor.
    /// Belirsiz bir ceza, oyuncunun oyunu bırakmasına yol açar.</para>
    ///
    /// <para><b>Solo'da davranış değişmiyor:</b> tek oyuncu yere düşerse kaldıracak
    /// kimse yok, yani düşmek ölümdür ve run biter. Bunu ayrı bir kural olarak
    /// yazmıyoruz — "ayakta kimse kalmadıysa run biter" kuralı solo'yu zaten
    /// kapsıyor. İki ayrı kural, iki ayrı hata demek olurdu.</para>
    ///
    /// <para><b>Otorite sunucuda</b> (ADR-0004): durumu sunucu yazar, istemci
    /// <c>SyncVar</c> ile okur. Kaldırma isteği bir komut ve sunucu <b>doğrular</b> —
    /// mesafeyi istemcinin söylediğine güvenmek, uzaktan diriltme demek olurdu
    /// (netcode.md: her RPC bir güven sınırıdır).</para>
    /// </summary>
    [AddComponentMenu("Bunker/Player Down State")]
    public sealed class PlayerDownState : NetworkBehaviour
    {
        /// <summary>Kaldırma için gereken en fazla mesafe (metre).</summary>
        public const float ReviveRangeMeters = 2.2f;

        /// <summary>Kaldırmanın kaç saniye sürdüğü.</summary>
        public const float ReviveSeconds = 3.5f;

        /// <summary>Kaldırılan oyuncunun döndüğü can oranı.</summary>
        private const float ReviveHealthFraction01 = 0.5f;

        private static readonly List<PlayerDownState> All = new List<PlayerDownState>(4);

        /// <summary>Oturumdaki bütün oyuncular. HUD ve kurtarma taraması bunu okur.</summary>
        public static IReadOnlyList<PlayerDownState> Players => All;

        [SyncVar(hook = nameof(OnStateChanged))]
        private LifeState _state = LifeState.Alive;

        /// <summary>Kaldırma ilerlemesi (0..1). Hem kaldıran hem düşen bunu görür.</summary>
        [SyncVar] private float _reviveProgress01;

        private PlayerHealth _health;
        private PlayerController _controller;
        private PlayerWeapon _weapon;
        private PlayerMelee _melee;

        /// <summary>Sunucu tarafında: bu karede birileri beni kaldırıyor mu.</summary>
        private bool _beingRevivedThisTick;

        public LifeState State => _state;
        public bool IsAlive => _state == LifeState.Alive;
        public bool IsDowned => _state == LifeState.Downed;
        public float ReviveProgress01 => _reviveProgress01;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _controller = GetComponent<PlayerController>();
            _weapon = GetComponent<PlayerWeapon>();
            _melee = GetComponent<PlayerMelee>();
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>
        /// Tur geçişlerine <b>her oyuncu kendi adına</b> abone olur.
        ///
        /// <para><b>Neden merkezî bir yönetici yok:</b> tek bir koordinatör bileşen,
        /// sahneye eklenmeyi unutulduğu gün bütün diriliş sistemini sessizce kapatırdı
        /// — bu projedeki hataların en sık türü. Oyuncunun kendi bileşeni oyuncuyla
        /// birlikte doğar ve ölür.</para>
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            RoundSignals.RoundCleared += OnRoundCleared;
            RoundSignals.RoundStarted += OnRoundStarted;
            RunSignals.RunRestarted += OnRunRestarted;
        }

        public override void OnStopServer()
        {
            // OnStartServer'in kurdugunu OnStopServer bozar (csharp-code.md).
            RoundSignals.RoundCleared -= OnRoundCleared;
            RoundSignals.RoundStarted -= OnRoundStarted;
            RunSignals.RunRestarted -= OnRunRestarted;

            base.OnStopServer();
        }

        /// <summary>
        /// Yeniden başlatma: <b>tam canla ve ayakta</b>.
        ///
        /// <para><b>Bu satır olmadan solo oyun kilitleniyordu:</b> ölen oyuncu artık
        /// önce <i>yere düşüyor</i> ve düşmek kontrolü kapatıyor. Yeniden başlatınca
        /// canı doluyordu ama durum hâlâ <c>Downed</c> olduğu için kontrol kapalı
        /// kalıyor, oyuncu ekrana bakıp hareket edemiyordu. Yeni bir durum eklemek,
        /// o durumdan çıkan <b>bütün</b> yolları da eklemek demek.</para>
        ///
        /// <para>Tur başındaki dirilişten farklı olarak burada can TAM: yeniden
        /// başlatma bir ceza değil, yeni bir run.</para>
        /// </summary>
        [ServerCallback]
        private void OnRunRestarted() => ServerRevive(1f);

        /// <summary>
        /// Tur temizlendi. <b>Kaldırılmayan artık ölü</b> — ve bu bir <i>ilerleme</i>,
        /// bir ceza değil: ölü olmak bir sonraki turda dirileceğin anlamına geliyor,
        /// yerde yatmaya devam etmek ise takımın seni hâlâ kaldırabileceği anlamına.
        /// </summary>
        [ServerCallback]
        private void OnRoundCleared(int round) => ServerConfirmDead();

        /// <summary>
        /// Yeni tur: ölüler döner. <b>Yarı canla</b> — tam canla dönmek turu izlemenin
        /// bedelini sıfırlar ve "nasılsa dirilirim" oynatır.
        /// </summary>
        [ServerCallback]
        private void OnRoundStarted(int round)
        {
            if (_state == LifeState.Alive) return;

            ServerRevive(ReviveHealthFraction01);
        }

        // ------------------------------------------------------------- sunucu

        /// <summary>
        /// Ölümcül hasar geldi. <b>Run'ı bitirmez</b> — yere düşürür ve
        /// <see cref="EveryoneOut"/> sorusunu sorar.
        /// </summary>
        [Server]
        public void ServerGoDown()
        {
            if (_state != LifeState.Alive) return;

            _state = LifeState.Downed;
            _reviveProgress01 = 0f;
        }

        /// <summary>Tur bitti, kaldırılmadı: bir sonraki tura kadar ölü.</summary>
        [Server]
        public void ServerConfirmDead()
        {
            if (_state != LifeState.Downed) return;

            _state = LifeState.Dead;
            _reviveProgress01 = 0f;
        }

        /// <summary>Yeni tur: ölüler dirilir, düşenler ayağa kalkar.</summary>
        [Server]
        public void ServerRevive(float healthFraction01)
        {
            _state = LifeState.Alive;
            _reviveProgress01 = 0f;

            if (_health != null) _health.ServerReviveTo(healthFraction01);
        }

        /// <summary>
        /// Bir takım arkadaşı beni kaldırmaya çalışıyor. <b>Sunucu doğrular:</b>
        /// kaldıran ayakta mı, yeterince yakın mı, ben gerçekten düşmüş müyüm.
        /// </summary>
        [Server]
        public void ServerTickRevive(PlayerDownState rescuer, float deltaTime)
        {
            if (_state != LifeState.Downed || rescuer == null || !rescuer.IsAlive) return;

            float distanceSqr = (rescuer.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > ReviveRangeMeters * ReviveRangeMeters) return;

            _beingRevivedThisTick = true;
            _reviveProgress01 += deltaTime / ReviveSeconds;

            if (_reviveProgress01 < 1f) return;

            ServerRevive(ReviveHealthFraction01);
        }

        /// <summary>
        /// Kaldırma <b>bırakılınca geri sayar</b>, sıfırlanmaz.
        ///
        /// <para>Anında sıfırlamak, sürünün ortasında kaldırmayı imkânsız yapardı:
        /// bir kez ateş etmek için çekilen kurtarıcı baştan başlardı. Geri sayma,
        /// "biraz ilerledim" bilgisini korur ama beklemeyi de ödüllendirir.</para>
        /// </summary>
        [ServerCallback]
        private void LateUpdate()
        {
            if (_state != LifeState.Downed) return;

            if (!_beingRevivedThisTick && _reviveProgress01 > 0f)
            {
                _reviveProgress01 = Mathf.Max(0f, _reviveProgress01 - Time.deltaTime * 0.5f);
            }

            _beingRevivedThisTick = false;
        }

        /// <summary>
        /// Ayakta kimse kaldı mı. <b>Hayırsa run biter</b> — solo'da bu, düşer düşmez
        /// olur ve eski davranış korunur.
        /// </summary>
        public static bool EveryoneOut()
        {
            if (All.Count == 0) return false;

            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] != null && All[i].IsAlive) return false;
            }

            return true;
        }

        /// <summary>Düşmüş oyuncular arasında bana en yakın olanı bulur (kurtarma nişanı).</summary>
        public static PlayerDownState NearestDowned(Vector3 position, float maxMeters)
        {
            PlayerDownState best = null;
            float bestSqr = maxMeters * maxMeters;

            for (int i = 0; i < All.Count; i++)
            {
                PlayerDownState candidate = All[i];
                if (candidate == null || !candidate.IsDowned) continue;

                float distanceSqr = (candidate.transform.position - position).sqrMagnitude;
                if (distanceSqr > bestSqr) continue;

                bestSqr = distanceSqr;
                best = candidate;
            }

            return best;
        }

        /// <summary>İzlenecek bir takım arkadaşı (spectate). Kendisi hariç, ayakta olan.</summary>
        public static PlayerDownState AnyAliveOther(PlayerDownState self)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] != null && All[i] != self && All[i].IsAlive) return All[i];
            }

            return null;
        }

        // ------------------------------------------------------------- istemci

        /// <summary>
        /// Durum değişti: <b>yerel oyuncunun kontrolü açılır ya da kapanır</b>.
        ///
        /// <para>Bileşenleri kapatmak, "düşmüşken ateş edemezsin" kuralını tek yerde
        /// tutar. Her bileşene ayrı bir <c>if (downed) return</c> yazmak, biri
        /// unutulduğunda yerde yatarken ateş eden bir oyuncu demek olurdu.</para>
        /// </summary>
        private void OnStateChanged(LifeState previous, LifeState current)
        {
            bool playable = current == LifeState.Alive;

            if (_controller != null) _controller.enabled = playable;
            if (_weapon != null) _weapon.enabled = playable;
            if (_melee != null) _melee.enabled = playable;

            // Dusmus oyuncu YERE YATAR: ayakta duran bir "olu", takim arkadasinin
            // onu bulmasini imkansiz kilar - kalabalikta ayakta duran bir siluet
            // yasayan bir oyuncudan ayirt edilemez.
            ApplyPose(current);
        }

        /// <summary>
        /// Düşen oyuncu <b>yere yatar</b>.
        ///
        /// <para><b>Neden görünür olması şart:</b> ayakta duran bir "ölü", kalabalıkta
        /// yaşayan bir oyuncudan ayırt edilemez — takım arkadaşı seni bulamazsa
        /// kaldırma diye bir mekanik yok demektir.</para>
        ///
        /// <para>Yatan şey yalnızca <b>görsel gövde</b> (<c>Capsule</c>); kök ve kamera
        /// dik kalır. Kökü yatırmak <c>CharacterController</c>'ı yan çevirir ve
        /// kaldırıldığında oyuncu yerin altında uyanır.</para>
        ///
        /// <para><b>Bulunamazsa söyler:</b> prefab'ın görsel çocuğu yeniden
        /// adlandırılırsa düşme sessizce görünmez olurdu — ve bu, oyunun çalıştığı ama
        /// mekaniğin olmadığı türden bir hata.</para>
        /// </summary>
        private void ApplyPose(LifeState state)
        {
            Transform visual = transform.Find("Capsule");

            if (visual == null)
            {
                Debug.LogWarning("[Oyuncu/Dusme] Gorsel govde ('Capsule') bulunamadi - " +
                                 "dusen oyuncu AYAKTA gorunecek ve arkadasi onu " +
                                 "kalabalikta ayirt edemez.", this);
                return;
            }

            visual.localRotation = state == LifeState.Alive
                ? Quaternion.identity
                : Quaternion.Euler(80f, 0f, 0f);
        }
    }
}
