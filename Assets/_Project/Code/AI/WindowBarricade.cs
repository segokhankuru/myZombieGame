using System;
using Bunker.Audio;
using Bunker.Config;
using Bunker.Systems.Combat;
using Bunker.Systems.Rounds;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Bir pencerenin barikatı: tahtalar, zombinin sökmesi, oyuncunun tamiri. M1-08.
    ///
    /// <para><b>Kural saf C#'ta</b> (<see cref="Barricade"/>); burası tahtaları görünür
    /// kılar ve iki tarafı bağlar. Tahtalar çalışma anında üretilir — gri kutuda bir
    /// tahta ince bir kutudur ve prefab'a gömmeye değmez.</para>
    ///
    /// <para><b>Pencerenin açık olması artık barikata bağlı</b>: <c>WindowEntry.IsOpen</c>
    /// bu bileşen varsa ondan gelir. Zombi barikatı yeterince söktüğünde geçer;
    /// tamamen sökmesi gerekmez — mutlak bir duvar, tek pencereyi tutmayı yeterli
    /// kılar ve haritayı anlamsızlaştırırdı.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Window Barricade")]
    [RequireComponent(typeof(WindowEntry))]
    public sealed class WindowBarricade : MonoBehaviour, IRepairable
    {
        [Header("Ayar")]
        [Tooltip("config/balance/barricade.json'dan uretilen varlik.")]
        [SerializeField] private BarricadeConfigAsset barricadeConfig;

        [Header("Gorsel (gri kutu)")]
        [Tooltip("Pencere acikliginin genisligi. Tahtalar buna gore yerlestirilir.")]
        [SerializeField] private float openingWidthMeters = 1.5f;
        [Tooltip("Pencere acikliginin yuksekligi.")]
        [SerializeField] private float openingHeightMeters = 1.3f;

        private WindowEntry _window;
        private Barricade _barricade;
        private Transform[] _boardVisuals;
        private static Material _boardMaterial;

        /// <summary>Bir tahta söküldü (pencere).</summary>
        public event Action<WindowBarricade> BoardTorn;

        /// <summary>Bir tahta takıldı — <b>puan burada yazılır</b>.</summary>
        public event Action<WindowBarricade> BoardRepaired;

        public int Boards => _barricade?.Boards ?? 0;
        public int Capacity => _barricade?.Capacity ?? 0;
        public bool AllowsEntry => _barricade == null || _barricade.AllowsEntry;

        public bool NeedsRepair => _barricade != null && !_barricade.IsFull;

        private void Awake()
        {
            _window = GetComponent<WindowEntry>();

            if (barricadeConfig == null)
            {
                // Eksik ayar sessiz varsayilanla gecistirilmez (config-protocol.md).
                Debug.LogError("[Barikat] barricade.asset atanmamis; pencere barikatsiz " +
                               "kalir. 'Bunker/Zombi/Test Alanini Kur' baglar.", this);
                enabled = false;
                return;
            }

            _barricade = new Barricade(barricadeConfig.ToRuntime());
            BuildBoardVisuals();
            BuildRepairTrigger();
            RefreshVisuals();
        }

        private void OnEnable()
        {
            RunSignals.RunRestarted += OnRunRestarted;
            RoundSignals.RoundEndRestock += OnRoundEndRestock;
        }

        private void OnDisable()
        {
            RunSignals.RunRestarted -= OnRunRestarted;
            RoundSignals.RoundEndRestock -= OnRoundEndRestock;
        }

        /// <summary>
        /// Tur bitti: tahtaların bir kısmı kendiliğinden geri gelir (2026-09-05,
        /// geliştirici kararı).
        ///
        /// <para><b>Kısmi, tam değil.</b> Tam yenilenme oyunu kolaylaştırıyordu ve o
        /// yüzden kaldırılmıştı; hiç yenilenmemesi ise geç turlarda molanın tamamını
        /// tamire bağlıyor ve tezgâha gitmeyi imkânsız kılıyordu. Oran
        /// <c>rounds.json → roundEnd.barricadeBoardsFraction01</c>'de; buraya bir sayı
        /// yazılmaz.</para>
        /// </summary>
        private void OnRoundEndRestock(float reserveAmmoFraction01, float boardsFraction01)
        {
            if (_barricade == null) return;

            if (_barricade.RestoreFraction(boardsFraction01) <= 0) return;

            RefreshVisuals();
        }

        /// <summary>
        /// Yeni run: barikat tam hâline döner.
        ///
        /// <para><b>Tur başında DEĞİL, RUN başında</b> (geliştirici kararı, 2026-09-04).
        /// Önceki sürüm her tur başında tam hâline dönüyordu ve gerekçesi şuydu: <i>"mola
        /// oyuncunun toparlandığı andır; sökük pencereleri tek tek tamir etmek molayı ev
        /// ödevi yapar (PILLAR-03)."</i> Geliştirici tersini seçti: <b>"tur başladığında
        /// tamamen yenilenen barikat oyunu çok kolay kılıyor."</b></para>
        ///
        /// <para><b>Bu bir farklılaştırıcı değil, bir sadakat düzeltmesi.</b> Klon taban
        /// barikatı otomatik onarmaz; oyuncu puan karşılığı tamir eder ve etmezse barikat
        /// sökük kalır. Otomatik dönüş bizim kazara eklediğimiz bir sapmaydı — bu yüzden
        /// M-02'ye ertelenmiyor, M-01'in içinde düzeltiliyor.</para>
        ///
        /// <para><b>Run sıfırlaması ŞART:</b> tur sıfırlaması kalkınca barikatı düzelten
        /// başka hiçbir yol kalmıyordu. O hâliyle <c>R</c> ile başlayan ikinci run,
        /// birincinin sökük pencereleriyle başlardı — M1-11'in AC-6'sının (sayılar
        /// sızmaz) barikat karşılığı.</para>
        ///
        /// <para><b>Oyun testinde izlenecek:</b> eski yorumun uyarısı hâlâ geçerli
        /// olabilir. 10 saniyelik mola dört pencereyi tamir etmeye yetmiyorsa mola bir
        /// dinlenme değil ev ödevi olur. Bu gerilim çözülmedi, ölçülecek.</para>
        /// </summary>
        private void OnRunRestarted()
        {
            ResetBarricade();
        }

        /// <summary>
        /// Işının çarpabileceği bir yüzey. <b>Tetikleyicidir</b> (trigger): mermi ve
        /// zombi ondan etkilenmez, yalnızca tamir sorgusu onu görür.
        ///
        /// <para>Bu olmadan tamir hiç çalışmıyordu: tahtaların collider'ı yok (mermiler
        /// pencereden geçebilsin diye) ve işaret nesnesinin de yoktu, dolayısıyla
        /// oyuncunun ışını hep arkadaki duvara çarpıyordu.</para>
        /// </summary>
        private void BuildRepairTrigger()
        {
            var trigger = gameObject.GetComponent<BoxCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<BoxCollider>();

            trigger.isTrigger = true;
            trigger.center = Vector3.zero;
            trigger.size = new Vector3(openingWidthMeters, openingHeightMeters, 0.3f);
        }

        // ---------------------------------------------------------------- zombi ve oyuncu

        /// <summary>
        /// Zombi söküyor. Bir tahta düştüyse <c>true</c> döner.
        /// <b>Yalnızca host çağırmalı</b> — barikat kalıcı sonucu olan bir durumdur.
        /// </summary>
        public bool Tear(float deltaTime)
        {
            if (_barricade == null) return false;

            bool fell = _barricade.Tear(deltaTime);
            if (!fell) return false;

            RefreshVisuals();

            // Tahtanin dusmesi 3B duyulur: oyuncu hangi pencerenin acildigini
            // gormeden bilmeli, yoksa savunma yalnizca BAKTIGIN pencerede olur.
            GameAudio.PlayAt(SfxId.BarricadeTear, transform.position);

            // SAVAS GUNLUGU (2026-09-08): barikat da bir hedef ve "kimden kime"
            // sorusunun bir parcasi. Tek tek SOKME adimlari degil, TAHTA DUSMESI
            // yaziliyor - sokme kare basina ilerleyen surekli bir is ve her adimi
            // loglamak dosyayi kullanilmaz yapardi.
            Systems.Telemetry.CombatLog.Event(
                "BARIKAT", $"{name}: tahta dustu, kalan {_barricade.Boards}");

            BoardTorn?.Invoke(this);
            return true;
        }

        public bool Repair(float deltaTime)
        {
            if (_barricade == null) return false;

            bool added = _barricade.Repair(deltaTime);
            if (!added) return false;

            RefreshVisuals();
            GameAudio.PlayAt(SfxId.BarricadeRepair, transform.position);
            BoardRepaired?.Invoke(this);
            return true;
        }

        /// <summary>Yeni run.</summary>
        public void ResetBarricade()
        {
            _barricade?.Reset();
            RefreshVisuals();
        }

        // ---------------------------------------------------------------- gorsel

        /// <summary>
        /// Tahtaları bir kez üretir ve sonra yalnızca <b>açıp kapatır</b>. Her sökümde
        /// yok edip yaratmak, tur boyunca yüzlerce tahsis demek olurdu.
        /// </summary>
        private void BuildBoardVisuals()
        {
            int capacity = _barricade.Capacity;
            _boardVisuals = new Transform[capacity];

            float spacing = openingHeightMeters / (capacity + 1);
            Transform root = transform;

            for (int i = 0; i < capacity; i++)
            {
                GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = $"Board_{i:D2}";
                UnityEngine.Object.DestroyImmediate(board.GetComponent<Collider>());

                board.transform.SetParent(root, false);

                // Pencere isaretinin forward'i disari bakar; tahtalar acikligin
                // duzlemine, esitce dagitilmis yatay cubuklar olarak oturur.
                board.transform.localPosition = new Vector3(
                    0f, -openingHeightMeters * 0.5f + spacing * (i + 1), 0f);
                board.transform.localRotation = Quaternion.identity;
                board.transform.localScale = new Vector3(openingWidthMeters, 0.09f, 0.06f);

                board.GetComponent<Renderer>().sharedMaterial = BoardMaterial();

                _boardVisuals[i] = board.transform;
            }
        }

        private void RefreshVisuals()
        {
            if (_boardVisuals == null) return;

            int boards = _barricade.Boards;

            for (int i = 0; i < _boardVisuals.Length; i++)
            {
                if (_boardVisuals[i] == null) continue;
                _boardVisuals[i].gameObject.SetActive(i < boards);
            }

            // BURADA WindowEntry.SetOpen CAGRILMAZ. Bir kez denendi ve butun oyunu
            // durdurdu (BUG-003): dogum noktasi secimi "acik pencere" ariyordu, tam
            // barikatli pencere kapali sayilinca hicbir zombi dogamadi.
            //
            // Iki kavram ayri:
            //   WindowEntry.IsOpen     -> bu pencere bir giris noktasi MI (yapisal)
            //   WindowBarricade.AllowsEntry -> su an gecilebilir mi (anlik)
            // Zombi barikatli pencerede DOGAR ve onu soker; gecilemiyor olmasi oranin
            // giris noktasi olmadigi anlamina gelmez.
        }

        /// <summary>
        /// Tahtalar tek bir <b>paylaşılan</b> materyal kullanır: her tahtaya ayrı
        /// materyal vermek tahta başına bir çizim çağrısı demektir (shader-graphics.md).
        /// </summary>
        private static Material BoardMaterial()
        {
            if (_boardMaterial != null) return _boardMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _boardMaterial = new Material(shader) { name = "mat_board_greybox_runtime" };
            _boardMaterial.SetColor("_BaseColor", new Color(0.45f, 0.34f, 0.20f));
            return _boardMaterial;
        }
    }
}
