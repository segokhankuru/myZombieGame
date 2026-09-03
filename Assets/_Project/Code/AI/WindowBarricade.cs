using System;
using Bunker.Config;
using Bunker.Systems.Combat;
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
            RefreshVisuals();
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
            BoardTorn?.Invoke(this);
            return true;
        }

        public bool Repair(float deltaTime)
        {
            if (_barricade == null) return false;

            bool added = _barricade.Repair(deltaTime);
            if (!added) return false;

            RefreshVisuals();
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

            // Pencerenin acikligi artik barikatin isi.
            if (_window != null) _window.SetOpen(_barricade.AllowsEntry);
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
