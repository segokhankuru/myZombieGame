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

        /// <summary>
        /// Yerde <b>kaç saniye</b> dayanılır (2026-09-09, geliştirici: <i>"kişiler
        /// ölümcül hasar alınca önce bayılmalı, PUBG gibi; revive edilmezse 10 sn
        /// içinde ölmeli"</i>).
        ///
        /// <para><b>Neden bir sayaç, "tur bitene kadar" değil:</b> önceki kural
        /// düşmeyi turun uzunluğuna bağlıyordu — turun başında düşen oyuncu iki dakika
        /// kurtarılabilir kalıyordu, sonunda düşen üç saniye. Aynı hata, iki bambaşka
        /// ceza. Sabit bir süre kurtarmayı bir <b>karar</b> yapar: arkadaşın ateşi
        /// bırakıp gelecek mi, on saniyesi var.</para>
        ///
        /// <para><b>Denge değeri değil de neden burada:</b> bu bir <i>his</i> sayısı —
        /// kurtarma penceresi, kurtarma süresiyle (<see cref="ReviveSeconds"/>) birlikte
        /// okunur ve ikisi aynı yerde durmalı. İkisi ayrılırsa 3,5 saniyelik bir
        /// kurtarmanın 2 saniyelik bir pencereye sığmadığı fark edilmez.</para>
        /// </summary>
        public const float BleedOutSeconds = 10f;

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
        /// <summary>
        /// Yeni tur: ölüler <b>kendi puanlarını ödeyerek</b> dirilir (2026-09-09).
        ///
        /// <para>Geliştirici: <i>"ölen kişi öder; eğer yeterli puan yoksa puanı cana
        /// oranlayıp kaç puanı varsa ona göre canla dirilsin, hiç yoksa 1 HP ile
        /// dirilir."</i></para>
        ///
        /// <para><b>Neden ödeyememek ölüm değil:</b> puanı yetmeyen oyuncuyu bir tur
        /// daha beklettirmek, en kötü durumdaki oyuncuyu daha da geriye atardı — ve
        /// puanı olmayan biri bir sonraki turda da puan kazanamaz, yani ceza kendi
        /// kendini besleyen bir çukur olurdu. Oransal can bunun yerine <i>ölçekli</i>
        /// bir ceza veriyor: parası yoksa dirilir ama bir vuruşluk canla dirilir ve
        /// o turu son derece dikkatli oynamak zorunda kalır.</para>
        ///
        /// <para><b>1 can tabanı</b>: sıfır canla dirilmek, dirilir dirilmez tekrar
        /// düşmek demek olurdu — oyuncunun hiç oynamadığı bir tur.</para>
        ///
        /// <para><b>Kaldırılan (Downed) oyuncu bedel ödemez</b>: onu arkadaşı zaten
        /// ateşi bırakıp kurtardı, bedeli takım ödedi.</para>
        /// </summary>
        [ServerCallback]
        private void OnRoundStarted(int round)
        {
            if (_state == LifeState.Alive) return;

            if (_state == LifeState.Downed)
            {
                // Yerde tur basina girmek: kanama sayaci zaten oldururdu, ama
                // sirali bir kare kaymasi ihtimaline karsi burada da kaldiriliyor.
                ServerRevive(ReviveHealthFraction01);
                return;
            }

            ServerRevive(ServerPayForRevive(round));
        }

        /// <summary>
        /// Diriliş bedelini oyuncunun kendi puanından tahsil eder ve <b>alabildiği can
        /// oranını</b> döner.
        /// </summary>
        /// <returns>0'dan büyük bir oran; puan hiç yoksa bir vuruşluk taban.</returns>
        [Server]
        private float ServerPayForRevive(int round)
        {
            var score = GetComponent<PlayerScore>();
            if (score == null) return ReviveHealthFraction01;

            int cost = score.ReviveCostForRound(round);

            // Fiyat sifirsa (config kapatmis) diriliş bedava ve TAM.
            if (cost <= 0) return 1f;

            int available = score.Spendable;

            if (available >= cost)
            {
                score.ServerSpend(cost);
                return 1f;
            }

            // YETMIYOR: eldeki her puan harcanir ve can ORANLANIR.
            if (available > 0) score.ServerSpend(available);

            float fraction = (float)available / cost;

            // Bir vurusluk taban: sifir canla dirilmek, oyuncunun hic oynamadigi bir
            // tur demek olurdu.
            return Mathf.Max(MinimumReviveFraction01, fraction);
        }

        /// <summary>
        /// Puanı hiç olmayan oyuncunun döndüğü can oranı — pratikte "1 can".
        ///
        /// <para>Oran olarak yazılıyor çünkü maksimum can karta göre değişiyor;
        /// mutlak 1 yazmak, 300 canlı bir oyuncuda 250 canlı bir oyuncudan farklı bir
        /// ceza olurdu.</para>
        /// </summary>
        private const float MinimumReviveFraction01 = 0.01f;

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

            // KANAMA SAYACI baslar (2026-09-09). Bu sayac dolarsa oyuncu olur;
            // kaldirilirsa sifirlanir.
            _bleedOutRemaining = BleedOutSeconds;
        }

        /// <summary>Yerdeyken ölüme kalan süre. <b>HUD bunu gösterir</b> — görünmeyen
        /// bir geri sayım, kurtarma kararını tahmine bırakır.</summary>
        [SyncVar] private float _bleedOutRemaining;

        /// <summary>Yerdeyken ölüme kalan saniye (0 = yerde değil).</summary>
        public float BleedOutRemainingSeconds => _state == LifeState.Downed
            ? _bleedOutRemaining
            : 0f;

        /// <summary>
        /// Kaldırılmadı: <b>öldü</b>. Kanama sayacı dolduğunda ya da tur bittiğinde.
        ///
        /// <para><b>Ölümün iki bedeli var</b> (2026-09-09, geliştirici: <i>"ölen kişinin
        /// dropları kaybolacak, mevcut mermi kapasitesi kaça kadar birikmişse
        /// yarılanacak"</i>). İkisi de bilinçli olarak <i>kalıcı</i>: yere düşmek
        /// zaten bir turluk bekleme cezası veriyordu ama o ceza <b>zaman</b>
        /// cezasıydı ve zaman geri geliyor. Kaynak kaybı geri gelmiyor — ölmek artık
        /// run'ın geri kalanında hissedilen bir şey.</para>
        ///
        /// <para><b>Neden eşyalar tamamen, mermi yarısı:</b> eşya bir <i>fırsat</i>
        /// (zaten cepte bekliyordu, kullanmadın), mermi ise oyunun temel kaynağı.
        /// Mermiyi de tamamen silmek, dirilen oyuncuyu silahsız bırakır ve bir sonraki
        /// turda tekrar ölmesini garantiler — cezanın kendini beslemesi.</para>
        /// </summary>
        [Server]
        public void ServerConfirmDead()
        {
            if (_state != LifeState.Downed) return;

            _state = LifeState.Dead;
            _reviveProgress01 = 0f;
            _bleedOutRemaining = 0f;

            // CEP BOSALIR: biriktirilen esyalar olumle gider.
            var powerups = GetComponent<PlayerPowerups>();
            if (powerups != null) powerups.ServerClearOnDeath();

            // YEDEK MERMI YARILANIR - butun silahlarda, cantadakiler dahil.
            if (_weapon != null) _weapon.ServerHalveReserves();

            // RUN SONU BURADA DA SORULUR (2026-09-09). Onceki hâlde bu soru yalnizca
            // olumcul vurusun geldigi anda soruluyordu (PlayerHealth); kanama sayaci
            // gelince olum ARTIK BASKA BIR ANDA da olabiliyor ve o an sorulmazsa
            // son ayakta kalan oyuncu yerde kanayip olur, run ise hic bitmezdi.
            if (EveryoneOut()) RunSignals.RaisePlayerDied();
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

            // KANAMA (2026-09-09). Kaldirilma SIRASINDA da isliyor: durdurmak,
            // kurtarmayi basladigi anda garantiye alirdi ve "yetisebilecek miyim"
            // sorusu ortadan kalkardi. Tam da o soru, kurtarmayi bir karar yapan sey.
            if (!RunSignals.IsRunOver)
            {
                _bleedOutRemaining -= Time.deltaTime;

                if (_bleedOutRemaining <= 0f)
                {
                    _bleedOutRemaining = 0f;
                    ServerConfirmDead();
                }
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
