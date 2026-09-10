using System.Collections.Generic;
using System.IO;
using Bunker.Audio;
using Bunker.Config;
using Bunker.Editor.ConfigTools;
using Bunker.Gameplay;
using Bunker.Systems.Settings;
using UnityEditor;
using UnityEngine;

namespace Bunker.Editor
{
    /// <summary>
    /// <b>Silah Atölyesi</b>: ateşli silahları ekler, kaldırır ve düzenler — model, yön,
    /// eldeki boy ve konum, aksesuarlar, denge sayıları ve sesler. 2026-09-10.
    ///
    /// <para><b>Neden var</b> (geliştirici: <i>"import ettiği tüm silahları ve
    /// attachmentları ekleyip düzenleyebileceğim yapı kur, hatta sesleri de. Böylece
    /// senin düzenlemene ihtiyacım kalmaz"</i>): yeni bir silah dört dosyaya dokunmak
    /// demekti ve ikisi C# tablosuydu. Dürbün Ayarlayıcı'nın yerini aldı.</para>
    ///
    /// <para><b>Önizleme kaynak paketten kurulur</b>, üretilmiş kopyadan değil: model ya
    /// da aksesuar değiştiğinde kaydetmeden görünür. Yerleşim ve ölçüm
    /// <see cref="ArtIntegration"/>'ın <b>aynı</b> fonksiyonlarından geçer; pencerede
    /// görülen, Kaydet'in ürettiğidir. Kaydet'ten sonra üretilen prefab'lar diskten
    /// okunup doğrulanır.</para>
    ///
    /// <para><b>Sayılar şemadan kısılır</b> ve her alanın üstüne gelince şemadaki
    /// "aralık dışında oyuncu ne yaşar" cümlesi görünür — tasarım niyeti ayar yapılırken
    /// ekranda durmalı.</para>
    /// </summary>
    public sealed class WeaponWorkshopWindow : EditorWindow
    {
        [MenuItem("Bunker/Silah Atolyesi", false, 1)]
        public static void Open()
        {
            var window = GetWindow<WeaponWorkshopWindow>("Silah Atölyesi");
            window.minSize = new Vector2(920f, 720f);
        }

        private enum ViewKind { Eye, Side, Top }

        private enum Tab { Look, Attachments, Balance, Sound }

        private static readonly string[] TabNames = { "Görünüm", "Aksesuarlar", "Denge", "Ses" };

        private sealed class View
        {
            public ViewKind Kind;
            public PreviewRenderUtility Utility;
            public GameObject Host;
            public GameObject Weapon;

            /// <summary>Aksesuar düğümleri, <c>WeaponArtData.Attachments</c> ile aynı sırada (bulunamayan = null).</summary>
            public readonly List<Transform> Parts = new List<Transform>();
        }

        private static readonly Color Background = new Color(0.18f, 0.20f, 0.24f);

        private const float FineFactor = 0.2f;
        private const float ListWidth = 210f;

        private readonly View[] _views = new View[3];
        private readonly List<Material> _previewMaterials = new List<Material>();
        private readonly Dictionary<string, string> _tuneNotes = new Dictionary<string, string>();
        private readonly Dictionary<string, Vector2> _statRanges = new Dictionary<string, Vector2>();

        private WeaponWorkshopData _saved;
        private WeaponWorkshopData _data;
        private SchemaInfo _statsSchema;
        private SchemaInfo _artSchema;
        private SchemaInfo _audioSchema;

        private Vector2 _lengthRange, _offsetRange, _alongRange, _gapRange, _sideRange, _scaleRange,
                        _eulerRange, _pitchRange, _scopeRange;

        private int _selected;
        private Tab _tab;
        private int _attachment = -1;

        private ArtIntegration.WeaponFrame _frame;
        private bool _frameValid;
        private Bounds _framing;

        private string _error;
        private string _status;
        private Vector2 _listScroll;
        private Vector2 _panelScroll;

        private bool _creating;
        private string _newName = string.Empty;
        private string _newModel = string.Empty;
        private int _newTemplate;

        private GameObject _audioHost;
        private AudioSource _audio;

        private WeaponStatsData Stats => _data.Stats[_selected];
        private WeaponArtData Art => _data.FindArt(Stats.Id);
        private WeaponSoundData Sound => _data.FindSound(Stats.Id);

        // ============================================================ yasam dongusu

        private void OnEnable()
        {
            saveChangesMessage = "Silah Atölyesi'nde kaydedilmemiş değişiklik var. Kaydedilsin mi?";
            Reload();
        }

        private void OnDisable()
        {
            DisposeViews();

            if (_audioHost != null) DestroyImmediate(_audioHost);
        }

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        /// <summary>Diskteki hali okur; pencerede oynananları atar.</summary>
        private void Reload(string selectId = null)
        {
            _error = null;

            try
            {
                _saved = WeaponWorkshopData.Load();

                _statsSchema = new SchemaInfo(WeaponWorkshopData.StatsSchemaPath);
                _artSchema = new SchemaInfo(WeaponWorkshopData.ArtSchemaPath);
                _audioSchema = new SchemaInfo(WeaponWorkshopData.AudioSchemaPath);

                _statRanges.Clear();
                foreach (WeaponStatFields.Field field in WeaponStatFields.All)
                {
                    _statRanges[field.Key] = _statsSchema.Range("weapons", field.Key);
                }

                _scopeRange = _statsSchema.Range("weapons", "scopeMagnification");
                _lengthRange = _artSchema.Range("weapons", "lengthMeters");
                _offsetRange = _artSchema.Range("weapons", "handOffsetMeters");
                _alongRange = _artSchema.Range("weapons", "attachments", "alongBarrel01");
                _gapRange = _artSchema.Range("weapons", "attachments", "gapOfWeaponHeight");
                _sideRange = _artSchema.Range("weapons", "attachments", "sideOfWeaponWidth");
                _scaleRange = _artSchema.Range("weapons", "attachments", "scaleMultiplier");
                _eulerRange = _artSchema.Range("weapons", "attachments", "eulerDegrees");
                _pitchRange = _audioSchema.Range("weapons", "basePitch");
            }
            catch (System.Exception e)
            {
                _saved = null;
                _data = null;
                _error = e.Message;
                DisposeViews();
                return;
            }

            _data = _saved.Clone();
            _creating = false;

            int index = selectId != null ? _data.Stats.FindIndex(s => s.Id == selectId) : -1;
            _selected = index >= 0 ? index : Mathf.Clamp(_selected, 0, Mathf.Max(0, _data.Stats.Count - 1));

            SelectDefaultAttachment();
            Rebuild();
        }

        private void SelectDefaultAttachment()
        {
            WeaponArtData art = _data != null && _data.Stats.Count > 0 ? Art : null;
            _attachment = art != null && art.Attachments.Count > 0 ? 0 : -1;
        }

        // ================================================================ onizleme

        /// <summary>Seçili silahın önizlemesini kaynak prefab'lardan baştan kurar.</summary>
        private void Rebuild()
        {
            DisposeViews();
            _error = null;
            _frameValid = false;

            if (_data == null || _data.Stats.Count == 0 || _creating) return;

            WeaponArtData art = Art;
            if (art == null)
            {
                _error = "Bu silahın görünüm satırı yok. Görünüm sekmesinden bir model seç.";
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(art.ModelPath);
            if (model == null)
            {
                _error = $"Model bulunamadı: {art.ModelPath}. Görünüm sekmesinden yeni bir model seç.";
                return;
            }

            for (int i = 0; i < _views.Length; i++) _views[i] = CreateView((ViewKind)i, model, art);

            Apply();
        }

        private View CreateView(ViewKind kind, GameObject model, WeaponArtData art)
        {
            var utility = new PreviewRenderUtility();

            utility.camera.clearFlags = CameraClearFlags.SolidColor;
            utility.camera.backgroundColor = Background;

            // Isik ArtPreview ile ayni: iki taraftan, yoksa bir yuz simsiyah kalir.
            utility.lights[0].intensity = 1.4f;
            utility.lights[0].transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            utility.lights[1].intensity = 0.6f;
            utility.lights[1].transform.rotation = Quaternion.Euler(15f, -140f, 0f);
            utility.ambientColor = new Color(0.35f, 0.35f, 0.38f);

            var host = new GameObject("Workshop_Hand");
            utility.AddSingleGO(host);
            host.transform.position = ArtPreview.GunHome;

            GameObject weapon = Spawn(utility, model);
            weapon.transform.SetParent(host.transform, false);

            var view = new View { Kind = kind, Utility = utility, Host = host, Weapon = weapon };

            foreach (AttachmentData attachment in art.Attachments)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(attachment.PrefabPath);

                if (prefab == null)
                {
                    view.Parts.Add(null);
                    _error = $"Aksesuar bulunamadı: {attachment.PrefabPath}";
                    continue;
                }

                GameObject part = Spawn(utility, prefab);
                part.name = ArtIntegration.AttachmentPrefix + attachment.Name;
                part.transform.SetParent(weapon.transform, false);
                view.Parts.Add(part.transform);
            }

            return view;
        }

        private GameObject Spawn(PreviewRenderUtility utility, GameObject prefab)
        {
            GameObject instance = utility.InstantiatePrefabInScene(prefab);

            // Bag koparilir: onizleme ne gosterecegini yalnizca bu pencere soyler.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                                               InteractionMode.AutomatedAction);

            // Paketin Built-in materyali URP'de PEMBE cizilir; Kaydet'in yapacagi
            // donusumun diske yazmayan bir kopyasi.
            ArtIntegration.PreviewMaterials(instance, _previewMaterials);

            return instance;
        }

        private void DisposeViews()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null) continue;

                if (_views[i].Host != null) DestroyImmediate(_views[i].Host);
                _views[i].Utility.Cleanup();
                _views[i] = null;
            }

            foreach (Material material in _previewMaterials)
            {
                if (material != null) DestroyImmediate(material);
            }

            _previewMaterials.Clear();
        }

        /// <summary>Düzenlenen sayıları önizlemeye uygular (yeniden kurmadan).</summary>
        private void Apply()
        {
            View eye = _views[0];

            if (eye != null && _data != null && Art != null)
            {
                WeaponArtData art = Art;

                _frameValid = ArtIntegration.TryWeaponFrame(art, eye.Weapon, out _frame);
                if (!_frameValid) _error = "Silah ölçülemedi: modelde mesh yok.";

                for (int j = 0; j < eye.Parts.Count && j < art.Attachments.Count; j++)
                {
                    if (eye.Parts[j] != null) ArtIntegration.FitAccessory(art, eye.Weapon, eye.Parts[j], art.Attachments[j]);
                }

                ArtCatalogAsset.WeaponModel model = ArtIntegration.MeasureWeapon(art, eye.Weapon);

                foreach (View view in _views)
                {
                    if (view == null) continue;

                    if (model != null)
                    {
                        view.Weapon.transform.localPosition = model.localPosition;
                        view.Weapon.transform.localRotation = Quaternion.Euler(model.localEulerAngles);
                        view.Weapon.transform.localScale = Vector3.one * model.localScale;
                    }

                    if (view == eye) continue;

                    for (int j = 0; j < view.Parts.Count && j < eye.Parts.Count; j++)
                    {
                        if (view.Parts[j] == null || eye.Parts[j] == null) continue;

                        view.Parts[j].localPosition = eye.Parts[j].localPosition;
                        view.Parts[j].localRotation = eye.Parts[j].localRotation;
                        view.Parts[j].localScale = eye.Parts[j].localScale;
                    }
                }

                // Cerceve GOVDEDEN: aksesuar suruklenirken kamera kaymasin.
                _framing = BodyBounds(eye.Weapon);
            }

            MarkDirty();
            Repaint();
        }

        private void MarkDirty()
        {
            hasUnsavedChanges = _saved != null && _data != null && _data.DiffersFrom(_saved);
        }

        private static Bounds BodyBounds(GameObject root)
        {
            bool any = false;
            var bounds = new Bounds(root.transform.position, Vector3.one * 0.3f);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (ArtIntegration.IsUnderAttachment(renderer.transform, root.transform)) continue;

                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);

                any = true;
            }

            return bounds;
        }

        // =============================================================== surukleme

        private void Drag(View view, Vector2 pixelDelta, float viewHeight, bool fine)
        {
            if (view.Kind == ViewKind.Eye)
            {
                MoveHand(view, pixelDelta, viewHeight, fine);
                return;
            }

            WeaponArtData art = Art;
            if (!_frameValid || art == null || _attachment < 0 || _attachment >= art.Attachments.Count) return;

            Camera camera = view.Utility.camera;
            float worldPerPixel = 2f * camera.orthographicSize / Mathf.Max(1f, viewHeight) * (fine ? FineFactor : 1f);

            // Ekran y'si asagi dogru artar, dunya y'si yukari.
            Vector3 world = camera.transform.right * (pixelDelta.x * worldPerPixel)
                            + camera.transform.up * (-pixelDelta.y * worldPerPixel);

            Vector3 local = view.Weapon.transform.InverseTransformVector(world);
            AttachmentData fit = art.Attachments[_attachment];

            fit.AlongBarrel01 = Clamp(
                fit.AlongBarrel01 - Vector3.Dot(local, _frame.Forward) / Mathf.Max(_frame.Length, 1e-5f), _alongRange);

            if (view.Kind == ViewKind.Side)
            {
                fit.GapOfWeaponHeight = Clamp(
                    fit.GapOfWeaponHeight + Vector3.Dot(local, _frame.Up) / Mathf.Max(_frame.Height, 1e-5f), _gapRange);
            }
            else
            {
                fit.SideOfWeaponWidth = Clamp(
                    fit.SideOfWeaponWidth + Vector3.Dot(local, _frame.Right) / Mathf.Max(_frame.Width, 1e-5f), _sideRange);
            }

            Apply();
        }

        /// <summary>Oyuncunun gözü görünümünde silahı eldeki yerinde kaydırır.</summary>
        private void MoveHand(View view, Vector2 pixelDelta, float viewHeight, bool fine)
        {
            WeaponArtData art = Art;
            if (art == null) return;

            Camera camera = view.Utility.camera;
            float depth = Mathf.Max(0.1f, ArtPreview.GunHome.z + art.HandOffsetMeters.z);
            float worldPerPixel = 2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                                  / Mathf.Max(1f, viewHeight) * (fine ? FineFactor : 1f);

            Vector3 offset = art.HandOffsetMeters;
            offset.x = Clamp(offset.x + pixelDelta.x * worldPerPixel, _offsetRange);
            offset.y = Clamp(offset.y - pixelDelta.y * worldPerPixel, _offsetRange);
            art.HandOffsetMeters = offset;

            Apply();
        }

        private void Scroll(View view, float delta, bool fine)
        {
            WeaponArtData art = Art;
            if (art == null) return;

            if (view.Kind == ViewKind.Eye)
            {
                Vector3 offset = art.HandOffsetMeters;
                offset.z = Clamp(offset.z - delta * (fine ? 0.001f : 0.005f), _offsetRange);
                art.HandOffsetMeters = offset;
            }
            else
            {
                if (_attachment < 0 || _attachment >= art.Attachments.Count) return;

                AttachmentData fit = art.Attachments[_attachment];
                fit.ScaleMultiplier = Clamp(fit.ScaleMultiplier * (1f - delta * (fine ? 0.005f : 0.025f)), _scaleRange);
            }

            Apply();
        }

        // ===================================================================== GUI

        private void OnGUI()
        {
            if (_data == null)
            {
                EditorGUILayout.HelpBox(_error ?? "Silah dosyaları okunamadı.", MessageType.Error);
                if (GUILayout.Button("Yeniden dene", GUILayout.Height(28f))) Reload();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawWeaponList();

                using (new EditorGUILayout.VerticalScope())
                {
                    if (_creating) DrawCreate();
                    else if (_data.Stats.Count > 0) DrawEditor();
                }
            }
        }

        private void DrawWeaponList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                EditorGUILayout.LabelField("SİLAHLAR", EditorStyles.boldLabel);

                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                for (int i = 0; i < _data.Stats.Count; i++)
                {
                    WeaponStatsData stats = _data.Stats[i];
                    bool selected = !_creating && i == _selected;

                    Color previous = GUI.backgroundColor;
                    if (selected) GUI.backgroundColor = new Color(0.45f, 0.7f, 1f);

                    if (GUILayout.Button($"{stats.Name}\n{stats.Id}", GUILayout.Height(38f)) && !selected)
                    {
                        _selected = i;
                        _creating = false;
                        _status = null;
                        SelectDefaultAttachment();
                        Rebuild();
                    }

                    GUI.backgroundColor = previous;
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("+ Yeni silah", GUILayout.Height(32f)))
                {
                    _creating = true;
                    _newName = string.Empty;
                    _newModel = string.Empty;
                    _newTemplate = _selected;
                    _status = null;
                    DisposeViews();
                }
            }
        }

        private void DrawEditor()
        {
            EditorGUILayout.LabelField($"{Stats.Name}   ({Stats.Id})", EditorStyles.largeLabel);

            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Warning);

            DrawViews();

            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabNames, GUILayout.Height(26f));

            _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll, GUILayout.ExpandHeight(true));

            EditorGUI.BeginChangeCheck();

            switch (_tab)
            {
                case Tab.Look: DrawLook(); break;
                case Tab.Attachments: DrawAttachments(); break;
                case Tab.Balance: DrawBalance(); break;
                case Tab.Sound: DrawSound(); break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                _status = null;
                Apply();
            }

            EditorGUILayout.EndScrollView();

            DrawSaveBar();
        }

        private void DrawViews()
        {
            if (_views[0] == null) return;

            float eyeHeight = Mathf.Max(170f, position.height * 0.28f);
            float rowHeight = Mathf.Max(130f, position.height * 0.19f);

            Rect eye = GUILayoutUtility.GetRect(10f, eyeHeight, GUILayout.ExpandWidth(true));
            DrawView(_views[0], eye,
                     _tab == Tab.Look
                         ? "OYUNCUNUN GÖZÜNDEN — sürükle: silahı elde kaydır · tekerlek: ileri/geri"
                         : $"OYUNCUNUN GÖZÜNDEN (görüş açısı {GameSettings.DefaultFieldOfView:0}°)",
                     _tab == Tab.Look);

            Rect row = GUILayoutUtility.GetRect(10f, rowHeight, GUILayout.ExpandWidth(true));
            float half = (row.width - 4f) * 0.5f;

            WeaponArtData art = Art;
            bool canDrag = art != null && _attachment >= 0 && _attachment < art.Attachments.Count;
            string selected = canDrag ? $" · seçili: {art.Attachments[_attachment].Name}" : " · önce Aksesuarlar'dan bir parça seç";

            DrawView(_views[1], new Rect(row.x, row.y, half, row.height),
                     "YANDAN — sürükle: öne/arkaya, yukarı/aşağı" + selected, canDrag);
            DrawView(_views[2], new Rect(row.x + half + 4f, row.y, half, row.height),
                     "ÜSTTEN — sürükle: öne/arkaya, sağa/sola", canDrag);

            EditorGUILayout.LabelField("Tekerlek: aksesuar boyutu · Shift: ince ayar", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawView(View view, Rect rect, string title, bool draggable)
        {
            if (view == null) return;

            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive, rect);

            if (draggable) EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);

            switch (e.GetTypeForControl(control))
            {
                case EventType.MouseDown:
                    if (draggable && e.button == 0 && rect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = control;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == control)
                    {
                        Drag(view, e.delta, rect.height, e.shift);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == control)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    if (draggable && rect.Contains(e.mousePosition))
                    {
                        Scroll(view, e.delta.y, e.shift);
                        e.Use();
                    }
                    break;

                case EventType.Repaint:
                    ConfigureCamera(view, rect);
                    view.Utility.BeginPreview(rect, GUIStyle.none);
                    view.Utility.Render(true, false);
                    view.Utility.EndAndDrawPreview(rect);
                    break;
            }

            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f), title, EditorStyles.whiteBoldLabel);

            if (view.Kind == ViewKind.Eye) return;

            // Namlu ucu iki gorunumde de SAGDA.
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 20f, 120f, 18f), "◀ dipçik", EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(rect.xMax - 90f, rect.yMax - 20f, 84f, 18f), "namlu ucu ▶", EditorStyles.whiteMiniLabel);
        }

        private void ConfigureCamera(View view, Rect rect)
        {
            Camera camera = view.Utility.camera;
            float aspect = rect.width / Mathf.Max(1f, rect.height);

            if (view.Kind == ViewKind.Eye)
            {
                // Oyundaki el kamerasi: goz orijinde, ileri +Z, ayni yakin kesme.
                camera.orthographic = false;
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                camera.fieldOfView = GameSettings.DefaultFieldOfView;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 20f;
                return;
            }

            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;

            Vector3 center = _framing.center;
            Vector3 size = _framing.size;

            if (view.Kind == ViewKind.Side)
            {
                // Sagdan: ekranin sagi +Z (namlu), yukarisi +Y.
                camera.transform.SetPositionAndRotation(center + Vector3.right * 3f,
                                                        Quaternion.LookRotation(Vector3.left, Vector3.up));
                camera.orthographicSize = Fit(size.z, size.y, aspect);
            }
            else
            {
                // Ustten: ekranin sagi +Z (namlu), yukarisi silahin solu.
                camera.transform.SetPositionAndRotation(center + Vector3.up * 3f,
                                                        Quaternion.LookRotation(Vector3.down, Vector3.left));
                camera.orthographicSize = Fit(size.z, size.x, aspect);
            }
        }

        // ---------------------------------------------------------------- gorunum

        private void DrawLook()
        {
            WeaponArtData art = Art;

            Stats.Name = EditorGUILayout.TextField(new GUIContent("Görünen ad", _statsSchema.Description("weapons", "name")),
                                                   Stats.Name);

            EditorGUILayout.Space(6f);

            if (art == null)
            {
                EditorGUILayout.HelpBox("Bu silahın modeli yok (oyunda gri kutu görünür).", MessageType.Info);

                PickerButton("Paketten model seç", "Weapons", path =>
                {
                    _data.Art.Add(new WeaponArtData { WeaponId = Stats.Id, ModelPath = path });
                    Rebuild();
                });

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Model", Path.GetFileNameWithoutExtension(art.ModelPath), EditorStyles.boldLabel);

                PickerButton("Değiştir…", "Weapons", path =>
                {
                    art.ModelPath = path;
                    Rebuild();
                });
            }

            EditorGUILayout.LabelField(" ", art.ModelPath, EditorStyles.miniLabel);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox("Üstteki görüntüde namlu ekranın içine (ileri) bakmalı ve silah dik durmalı. " +
                                    "Bakmıyorsa aşağıdaki yönleri değiştir — paketler modelleri farklı yönlerde çiziyor.",
                                    MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                int forward = System.Array.IndexOf(WeaponAxes.Names, art.NativeForward);
                int picked = EditorGUILayout.Popup(new GUIContent("Namlu yönü", _artSchema.Description("weapons", "nativeForward")),
                                                   Mathf.Max(0, forward), WeaponAxes.Names);

                if (GUILayout.Button("Ters çevir", GUILayout.Width(90f))) picked = picked ^ 1;

                string axis = WeaponAxes.Names[picked];
                if (axis != art.NativeForward)
                {
                    art.NativeForward = axis;

                    // Namlu ustle ayni eksene dustuyse ust, dik bir eksene kayar.
                    if (WeaponAxes.SameLine(art.NativeForward, art.NativeUp)) art.NativeUp = axis[1] == 'Y' ? "+Z" : "+Y";
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int up = System.Array.IndexOf(WeaponAxes.Names, art.NativeUp);
                int picked = EditorGUILayout.Popup(new GUIContent("Üst yönü", _artSchema.Description("weapons", "nativeUp")),
                                                   Mathf.Max(0, up), WeaponAxes.Names);

                if (GUILayout.Button("Ters çevir", GUILayout.Width(90f))) picked = picked ^ 1;

                string axis = WeaponAxes.Names[picked];

                if (WeaponAxes.SameLine(axis, art.NativeForward))
                {
                    if (axis != art.NativeUp) _status = "Üst yönü namlu yönüyle aynı eksen olamaz.";
                }
                else
                {
                    art.NativeUp = axis;
                }
            }

            EditorGUILayout.Space(6f);

            art.LengthMeters = EditorGUILayout.Slider(
                new GUIContent("Eldeki boy (m)", _artSchema.Description("weapons", "lengthMeters")),
                art.LengthMeters, _lengthRange.x, _lengthRange.y);

            using (new EditorGUILayout.HorizontalScope())
            {
                Vector3 offset = EditorGUILayout.Vector3Field(
                    new GUIContent("El konumu düzeltmesi (m)", _artSchema.Description("weapons", "handOffsetMeters")),
                    art.HandOffsetMeters);

                art.HandOffsetMeters = new Vector3(Clamp(offset.x, _offsetRange), Clamp(offset.y, _offsetRange),
                                                   Clamp(offset.z, _offsetRange));

                if (GUILayout.Button("Sıfırla", GUILayout.Width(70f))) art.HandOffsetMeters = Vector3.zero;
            }

            EditorGUILayout.Space(16f);
            DrawRemove();
        }

        private void DrawRemove()
        {
            bool starter = Stats.Id == PlayerWeapon.StarterWeaponId;

            using (new EditorGUI.DisabledScope(starter))
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.5f);

                var label = new GUIContent("Silahı oyundan kaldır",
                                           starter ? "Başlangıç silahı kaldırılamaz: her oyuncu run'a onunla başlar." : null);

                if (GUILayout.Button(label, GUILayout.Width(200f)))
                {
                    string id = Stats.Id;

                    bool confirmed = EditorUtility.DisplayDialog(
                        "Silahı oyundan kaldır",
                        $"{Stats.Name} ({id}) oyundan kaldırılacak: denge, model ve ses satırları silinir." +
                        (_saved.FindStats(id) != null
                            ? " Id emekliye ayrılır ve bir daha kullanılamaz."
                            : string.Empty) +
                        "\n\nKaydet'e basana kadar hiçbir dosyaya yazılmaz; Geri Al ile dönebilirsin.",
                        "Kaldır", "Vazgeç");

                    if (confirmed)
                    {
                        _data.Stats.RemoveAt(_selected);
                        _data.Art.RemoveAll(a => a.WeaponId == id);
                        _data.Sounds.RemoveAll(s => s.WeaponId == id);

                        // Hic kaydedilmemis bir silahin id'si hicbir kayda ya da telemetriye
                        // girmedi; onu emekliye ayirmak bos yere bir ismi yakmak olurdu.
                        if (_saved.FindStats(id) != null && !_data.RetiredIds.Contains(id)) _data.RetiredIds.Add(id);

                        _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _data.Stats.Count - 1));
                        _status = $"{id} kaldırıldı. Kaydet'e basınca oyundan çıkar.";
                        SelectDefaultAttachment();
                        Rebuild();
                        GUIUtility.ExitGUI();
                    }
                }

                GUI.backgroundColor = previous;
            }
        }

        // ------------------------------------------------------------- aksesuarlar

        private void DrawAttachments()
        {
            WeaponArtData art = Art;

            if (art == null)
            {
                EditorGUILayout.HelpBox("Önce Görünüm sekmesinden bir model seç.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("Listeden bir aksesuar seç, sonra üstteki YANDAN ve ÜSTTEN görüntülerde fareyle sürükle. " +
                                    "Aksesuarlar yalnızca görünüştür, oyun kuralını değiştirmez.", MessageType.None);

            for (int j = 0; j < art.Attachments.Count; j++)
            {
                AttachmentData attachment = art.Attachments[j];

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool selected = EditorGUILayout.Toggle(j == _attachment, EditorStyles.radioButton, GUILayout.Width(18f));
                    if (selected && j != _attachment) _attachment = j;

                    string name = EditorGUILayout.TextField(attachment.Name, GUILayout.Width(160f));
                    string clean = CleanName(name);
                    if (clean != attachment.Name && clean.Length > 0 && UniqueName(art, clean, j)) attachment.Name = clean;

                    EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(attachment.PrefabPath), EditorStyles.miniLabel);

                    int index = j;
                    PickerButton("Değiştir…", "Attachments", path =>
                    {
                        art.Attachments[index].PrefabPath = path;
                        Rebuild();
                    });

                    if (GUILayout.Button("Sil", GUILayout.Width(40f)))
                    {
                        art.Attachments.RemoveAt(j);
                        _attachment = Mathf.Min(_attachment, art.Attachments.Count - 1);
                        Rebuild();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            PickerButton("+ Aksesuar ekle", "Attachments", path =>
            {
                string baseName = CleanName(Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
                if (baseName.Length == 0) baseName = "parca";

                string name = baseName;
                for (int n = 2; !UniqueName(art, name, -1); n++) name = $"{baseName}_{n}";

                art.Attachments.Add(new AttachmentData { Name = name, PrefabPath = path });
                _attachment = art.Attachments.Count - 1;
                Rebuild();
            });

            if (_attachment < 0 || _attachment >= art.Attachments.Count) return;

            AttachmentData fit = art.Attachments[_attachment];

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"Seçili: {fit.Name}", EditorStyles.boldLabel);

            fit.AlongBarrel01 = Clamp(EditorGUILayout.FloatField(
                new GUIContent("Öne/arkaya (0 namlu · 1 dipçik)", _artSchema.Description("weapons", "attachments", "alongBarrel01")),
                fit.AlongBarrel01), _alongRange);

            using (new EditorGUILayout.HorizontalScope())
            {
                fit.GapOfWeaponHeight = Clamp(EditorGUILayout.FloatField(
                    new GUIContent("Boşluk (0 = tepeye oturur)", _artSchema.Description("weapons", "attachments", "gapOfWeaponHeight")),
                    fit.GapOfWeaponHeight), _gapRange);
                if (GUILayout.Button("Tepeye oturt", GUILayout.Width(100f))) fit.GapOfWeaponHeight = 0f;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                fit.SideOfWeaponWidth = Clamp(EditorGUILayout.FloatField(
                    new GUIContent("Yana (0 = tam orta)", _artSchema.Description("weapons", "attachments", "sideOfWeaponWidth")),
                    fit.SideOfWeaponWidth), _sideRange);
                if (GUILayout.Button("Ortala", GUILayout.Width(100f))) fit.SideOfWeaponWidth = 0f;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                fit.ScaleMultiplier = Clamp(EditorGUILayout.FloatField(
                    new GUIContent("Boyut (1 = paketin boyu)", _artSchema.Description("weapons", "attachments", "scaleMultiplier")),
                    fit.ScaleMultiplier), _scaleRange);
                if (GUILayout.Button("1", GUILayout.Width(100f))) fit.ScaleMultiplier = 1f;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Vector3 euler = EditorGUILayout.Vector3Field(
                    new GUIContent("Dönüş (derece)", _artSchema.Description("weapons", "attachments", "eulerDegrees")),
                    fit.EulerDegrees);
                fit.EulerDegrees = new Vector3(Clamp(euler.x, _eulerRange), Clamp(euler.y, _eulerRange), Clamp(euler.z, _eulerRange));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Hızlı çevir:", GUILayout.Width(80f));
                if (GUILayout.Button("X +90")) fit.EulerDegrees.x = Wrap(fit.EulerDegrees.x + 90f);
                if (GUILayout.Button("Y +90")) fit.EulerDegrees.y = Wrap(fit.EulerDegrees.y + 90f);
                if (GUILayout.Button("Z +90")) fit.EulerDegrees.z = Wrap(fit.EulerDegrees.z + 90f);
                if (GUILayout.Button("Y 180 (ters)")) fit.EulerDegrees.y = Wrap(fit.EulerDegrees.y + 180f);
                if (GUILayout.Button("Sıfırla")) fit.EulerDegrees = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ denge

        private void DrawBalance()
        {
            WeaponStatsData stats = Stats;

            stats.Name = EditorGUILayout.TextField(new GUIContent("Görünen ad", _statsSchema.Description("weapons", "name")), stats.Name);
            stats.Text = EditorGUILayout.TextField(new GUIContent("Tezgâh açıklaması", _statsSchema.Description("weapons", "text")), stats.Text);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(Summary(stats), MessageType.Info);
            EditorGUILayout.LabelField("Alanların üstüne gelince izinli aralığın dışında oyuncunun ne yaşayacağı yazar.",
                                       EditorStyles.miniLabel);

            foreach (WeaponStatFields.Field[] line in WeaponStatFields.Lines)
            {
                EditorGUILayout.Space(2f);

                foreach (WeaponStatFields.Field field in line)
                {
                    Vector2 range = _statRanges[field.Key];
                    var label = new GUIContent($"{field.Label}   [{WeaponWorkshopData.Format(range.x)} – {WeaponWorkshopData.Format(range.y)}]",
                                               _statsSchema.Description("weapons", field.Key));

                    stats.Numbers.TryGetValue(field.Key, out double value);

                    if (field.Integer)
                    {
                        int picked = EditorGUILayout.IntField(label, (int)System.Math.Round(value));
                        stats.Numbers[field.Key] = Mathf.Clamp(picked, Mathf.CeilToInt(range.x), Mathf.FloorToInt(range.y));
                    }
                    else
                    {
                        float picked = EditorGUILayout.FloatField(label, (float)value);
                        stats.Numbers[field.Key] = Clamp(picked, range);
                    }
                }

                if (line[0].Key == "reloadSeconds")
                {
                    stats.ReloadPerShell = EditorGUILayout.Toggle(
                        new GUIContent("Mermi mermi dolum (pompalı)", _statsSchema.Description("weapons", "reloadPerShell")),
                        stats.ReloadPerShell);

                    bool scoped = EditorGUILayout.Toggle(new GUIContent("Dürbün (sağ tık yakınlaştırır)"), stats.HasScope);

                    if (scoped && !stats.HasScope) stats.ScopeMagnification = Mathf.Max(2f, _scopeRange.x);
                    if (!scoped) stats.ScopeMagnification = 0f;

                    if (stats.HasScope)
                    {
                        EditorGUI.indentLevel++;

                        stats.ScopeMagnification = EditorGUILayout.Slider(
                            new GUIContent("Büyütme (kat)", _statsSchema.Description("weapons", "scopeMagnification")),
                            stats.ScopeMagnification, _scopeRange.x, _scopeRange.y);

                        int style = stats.ScopeStyle == "Prism" ? 1 : 0;
                        style = EditorGUILayout.Popup(
                            new GUIContent("Nişan görüntüsü", _statsSchema.Description("weapons", "scopeStyle")), style,
                            new[] { "Nişancı dürbünü (ekran kararır)", "Prizmalı optik (gövde görünür)" });
                        stats.ScopeStyle = style == 1 ? "Prism" : "LongRange";

                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Space(8f);

            _tuneNotes.TryGetValue(stats.Id, out string note);
            note = EditorGUILayout.TextField(
                new GUIContent("Neden değiştirdim (isteğe bağlı)",
                               "Kaydedince silahın '_lastTune' notuna tarihle yazılır. Üç ay sonra 'bu sayı neden böyle' sorusunun cevabı."),
                note ?? string.Empty);
            _tuneNotes[stats.Id] = note;
        }

        private static string Summary(WeaponStatsData stats)
        {
            double Get(string key) => stats.Numbers.TryGetValue(key, out double v) ? v : 0d;

            double perShot = Get("damage") * System.Math.Max(1d, Get("pelletCount"));
            double perSecond = perShot * Get("roundsPerMinute") / 60d;
            double emptySeconds = Get("roundsPerMinute") > 0d ? Get("magazineCapacity") / (Get("roundsPerMinute") / 60d) : 0d;

            return $"Atış başına {perShot:0} hasar · saniyede ≈ {perSecond:0} · kafadan {perShot * Get("headshotMultiplier"):0} · " +
                   $"şarjör {emptySeconds:0.0} sn'de biter. (Tur 1 zombisi 150 can.)";
        }

        // ------------------------------------------------------------------- ses

        private void DrawSound()
        {
            WeaponSoundData sound = Sound;

            if (sound == null)
            {
                EditorGUILayout.HelpBox("Bu silahın ses satırı yok: sentezlenmiş sesler çalar.", MessageType.Info);

                if (GUILayout.Button("Ses satırı oluştur", GUILayout.Width(180f)))
                {
                    sound = new WeaponSoundData { WeaponId = Stats.Id };
                    if (_data.Presets.Count > 0) sound.CopySoundsFrom(_data.Presets[0]);
                    _data.Sounds.Add(sound);
                }

                return;
            }

            EditorGUILayout.HelpBox("▶ ile dinle. Ses gelmiyorsa Game penceresinin üstündeki 'Mute Audio' kapalı olmalı. " +
                                    "Boş bırakılan dolum ve boş tetik sesleri sentezlenmiş sese düşer, silah sessiz kalmaz.",
                                    MessageType.None);

            if (_data.Presets.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var names = new string[_data.Presets.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = _data.Presets[i].PresetName;

                    int preset = EditorGUILayout.Popup("Hazır aile", SessionState.GetInt("WeaponWorkshop.Preset", 0), names);
                    SessionState.SetInt("WeaponWorkshop.Preset", preset);

                    if (GUILayout.Button("Bu aileyle doldur", GUILayout.Width(140f)))
                    {
                        float pitch = sound.BasePitch;
                        sound.CopySoundsFrom(_data.Presets[Mathf.Clamp(preset, 0, _data.Presets.Count - 1)]);

                        // Perde silahin kimligi; aile degisince sifirlanmasi, ayni kaydi
                        // paylasan iki silahi yeniden ayirt edilmez yapardi.
                        sound.BasePitch = pitch;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                sound.BasePitch = EditorGUILayout.Slider(
                    new GUIContent("Perde", _audioSchema.Description("weapons", "basePitch")),
                    sound.BasePitch, _pitchRange.x, _pitchRange.y);

                if (GUILayout.Button("▶ Ateş", GUILayout.Width(70f)) && sound.Fire.Count > 0)
                {
                    PlayClip(AssetDatabase.LoadAssetAtPath<AudioClip>(sound.Fire[Random.Range(0, sound.Fire.Count)]),
                             sound.BasePitch);
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Ateş sesi varyantları", EditorStyles.boldLabel);

            for (int i = 0; i < sound.Fire.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    sound.Fire[i] = ClipField($"  {i + 1}", sound.Fire[i], sound.BasePitch);

                    if (GUILayout.Button("✕", GUILayout.Width(26f)))
                    {
                        sound.Fire.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("+ Varyant ekle", GUILayout.Width(140f))) sound.Fire.Add(string.Empty);

            if (sound.Fire.Count < 3)
            {
                EditorGUILayout.HelpBox("Sık çalan bir ses en az 3-4 varyant ister; tek varyantlı silah sesi seri atışta makine sesine döner.",
                                        MessageType.Warning);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Dolum ve diğerleri", EditorStyles.boldLabel);

            sound.ReloadOut = ClipField("Dolum başı (şarjör çıkar)", sound.ReloadOut, sound.BasePitch);
            sound.ReloadIn = ClipField("Dolum sonu (şarjör oturur)", sound.ReloadIn, sound.BasePitch);
            sound.DryFire = ClipField("Boş tetik", sound.DryFire, sound.BasePitch);
            sound.Equip = ClipField("Ele alma", sound.Equip, sound.BasePitch);
        }

        private string ClipField(string label, string path, float pitch)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                AudioClip clip = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var picked = (AudioClip)EditorGUILayout.ObjectField(label, clip, typeof(AudioClip), false);

                using (new EditorGUI.DisabledScope(picked == null))
                {
                    if (GUILayout.Button("▶", GUILayout.Width(26f))) PlayClip(picked, pitch);
                }

                if (picked != clip) path = picked == null ? string.Empty : AssetDatabase.GetAssetPath(picked);
            }

            if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
            {
                EditorGUILayout.LabelField(" ", $"bulunamadı: {path}", EditorStyles.miniBoldLabel);
            }

            return path;
        }

        private void PlayClip(AudioClip clip, float pitch)
        {
            if (clip == null) return;

            if (_audio == null)
            {
                _audioHost = EditorUtility.CreateGameObjectWithHideFlags("WeaponWorkshopAudio", HideFlags.HideAndDontSave,
                                                                         typeof(AudioSource));
                _audio = _audioHost.GetComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            _audio.Stop();
            _audio.pitch = pitch;
            _audio.clip = clip;
            _audio.Play();
        }

        // ------------------------------------------------------------ yeni silah

        private void DrawCreate()
        {
            EditorGUILayout.LabelField("YENİ SİLAH", EditorStyles.largeLabel);
            EditorGUILayout.Space(6f);

            _newName = EditorGUILayout.TextField("Görünen ad", _newName);

            string id = WeaponWorkshopData.MakeId(_newName ?? string.Empty);
            EditorGUILayout.LabelField("Kalıcı id", id);
            EditorGUILayout.LabelField(" ", "Id sonradan değişmez (kayıtlar ve telemetri ona bağlanır). Adı istediğin zaman değiştirebilirsin.",
                                       EditorStyles.wordWrappedMiniLabel);

            var names = new string[_data.Stats.Count];
            for (int i = 0; i < names.Length; i++) names[i] = _data.Stats[i].Name;

            _newTemplate = EditorGUILayout.Popup(
                new GUIContent("Şuna benzesin", "Denge sayıları, sesler, eldeki boy ve model yönü bu silahtan kopyalanır. Sonra hepsini değiştirebilirsin."),
                Mathf.Clamp(_newTemplate, 0, Mathf.Max(0, names.Length - 1)), names);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Model", string.IsNullOrEmpty(_newModel) ? "(seçilmedi)" : Path.GetFileNameWithoutExtension(_newModel));
                PickerButton("Paketten seç…", "Weapons", path => _newModel = path);
            }

            string problem = string.IsNullOrWhiteSpace(_newName) ? "Bir ad yaz."
                           : _data.CheckNewId(id) ?? (string.IsNullOrEmpty(_newModel) ? "Paketten bir model seç." : null);

            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Info);

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(problem != null))
                {
                    if (GUILayout.Button("Oluştur", GUILayout.Height(30f), GUILayout.Width(160f))) CreateWeapon(id);
                }

                if (GUILayout.Button("Vazgeç", GUILayout.Height(30f), GUILayout.Width(100f)))
                {
                    _creating = false;
                    Rebuild();
                }
            }
        }

        private void CreateWeapon(string id)
        {
            WeaponStatsData template = _data.Stats[Mathf.Clamp(_newTemplate, 0, _data.Stats.Count - 1)];

            WeaponStatsData stats = template.Clone();
            stats.Id = id;
            stats.Name = _newName.Trim();
            stats.Text = string.Empty;
            stats.Notes = new List<KeyValuePair<string, string>>();
            _data.Stats.Add(stats);

            WeaponArtData templateArt = _data.FindArt(template.Id);
            _data.Art.Add(new WeaponArtData
            {
                WeaponId = id,
                ModelPath = _newModel,
                LengthMeters = templateArt?.LengthMeters ?? 0.5f,
                NativeForward = templateArt?.NativeForward ?? "-Z",
                NativeUp = templateArt?.NativeUp ?? "+Y"
            });

            var sound = new WeaponSoundData { WeaponId = id };
            WeaponSoundData templateSound = _data.FindSound(template.Id);
            if (templateSound != null) sound.CopySoundsFrom(templateSound);
            else if (_data.Presets.Count > 0) sound.CopySoundsFrom(_data.Presets[0]);
            _data.Sounds.Add(sound);

            _selected = _data.Stats.Count - 1;
            _creating = false;
            _tab = Tab.Look;
            _attachment = -1;
            _status = "Silah oluşturuldu. Üstteki görüntüde namlunun ileri baktığını kontrol et, sonra Kaydet'e bas.";

            Rebuild();
        }

        // ------------------------------------------------------------------ kayit

        private void DrawSaveBar()
        {
            EditorGUILayout.Space(4f);

            if (_status != null) EditorGUILayout.HelpBox(_status, MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
                {
                    if (GUILayout.Button("Geri Al (kaydedilmiş hale dön)", GUILayout.Height(30f)))
                    {
                        _status = null;
                        _tuneNotes.Clear();
                        Reload(Stats.Id);
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || !hasUnsavedChanges))
                {
                    if (GUILayout.Button(hasUnsavedChanges ? "Kaydet  *" : "Kaydet (değişiklik yok)", GUILayout.Height(30f)))
                    {
                        Save();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void Save()
        {
            if (_data == null) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _status = "Oyun çalışırken kaydedilemez. Önce Play'i durdur.";
                return;
            }

            string problem = Validate();
            if (problem != null)
            {
                _error = problem;
                return;
            }

            string weaponId = _data.Stats.Count > 0 ? Stats.Id : null;

            ApplyTuneNotes();

            // Dogrulama icin: pencerede gorulen aksesuar yerleri (silah kokunun uzayinda).
            var expected = new List<Vector3>();
            if (_views[0] != null)
            {
                foreach (Transform part in _views[0].Parts) expected.Add(part != null ? part.localPosition : Vector3.zero);
            }

            float length = _frameValid ? _frame.Length : 1f;
            string importError = null;
            (bool Stats, bool Art, bool Audio) changed;

            try
            {
                changed = _data.Save();

                if (changed.Stats && !WeaponCatalogImporter.Import())
                {
                    importError = "Denge dosyası yazıldı ama İÇE AKTARILAMADI - oyun hâlâ eski sayılarla çalışıyor. " +
                                  "Console'daki [Silah] satırlarına bak.";
                }

                if (changed.Art) ArtIntegration.LinkStoreArt();
                if (changed.Audio) AudioIntegration.LinkAudio();
            }
            catch (System.Exception e)
            {
                _error = $"Kaydedilemedi: {e.Message}";
                Debug.LogError($"[Silah Atolyesi] Kaydetme basarisiz: {e}");
                return;
            }

            hasUnsavedChanges = false;
            _tuneNotes.Clear();

            Reload(weaponId);

            _status = Verify(weaponId, expected, length, changed);
            if (importError != null) _error = importError;
        }

        private void ApplyTuneNotes()
        {
            string today = System.DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

            foreach (WeaponStatsData stats in _data.Stats)
            {
                if (!_tuneNotes.TryGetValue(stats.Id, out string note) || string.IsNullOrWhiteSpace(note)) continue;

                stats.SetNote("_lastTune", $"{today}: {note.Trim()}");
            }
        }

        /// <summary>Kaydetmeden önce: üreticilerin yarı yolda patlayacağı her şey.</summary>
        private string Validate()
        {
            var problems = new System.Text.StringBuilder();

            foreach (WeaponStatsData stats in _data.Stats)
            {
                WeaponArtData art = _data.FindArt(stats.Id);

                if (art != null)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(art.ModelPath) == null)
                    {
                        problems.Append($"{stats.Name}: model bulunamadı ({art.ModelPath}).\n");
                    }

                    if (WeaponAxes.SameLine(art.NativeForward, art.NativeUp))
                    {
                        problems.Append($"{stats.Name}: namlu ve üst yönü aynı eksende.\n");
                    }

                    var names = new HashSet<string>();

                    foreach (AttachmentData attachment in art.Attachments)
                    {
                        if (!names.Add(attachment.Name)) problems.Append($"{stats.Name}: '{attachment.Name}' adlı iki aksesuar var.\n");

                        if (AssetDatabase.LoadAssetAtPath<GameObject>(attachment.PrefabPath) == null)
                        {
                            problems.Append($"{stats.Name}: aksesuar bulunamadı ({attachment.PrefabPath}).\n");
                        }
                    }
                }

                WeaponSoundData sound = _data.FindSound(stats.Id);

                if (sound != null)
                {
                    sound.Fire.RemoveAll(string.IsNullOrEmpty);
                    if (sound.Fire.Count == 0) problems.Append($"{stats.Name}: en az bir ateş sesi seç.\n");
                }
            }

            return problems.Length > 0 ? "Kaydedilmedi:\n" + problems : null;
        }

        /// <summary>Kaydedileni <b>diskten</b> okuyup oyuna gerçekten işlendiğini ölçer.</summary>
        private string Verify(string weaponId, List<Vector3> expected, float length, (bool Stats, bool Art, bool Audio) changed)
        {
            if (!changed.Stats && !changed.Art && !changed.Audio) return "Değişiklik yoktu, hiçbir dosyaya dokunulmadı.";
            if (weaponId == null) return "Kaydedildi.";

            var lines = new List<string> { $"Kaydedildi ({weaponId})." };

            var weapons = AssetDatabase.LoadAssetAtPath<WeaponCatalogAsset>(WeaponCatalogImporter.AssetPath);
            bool inCatalog = weapons != null && weapons.ToRuntime(0f, 0f, 0f).Exists(d => d.Id == weaponId);
            bool removed = _data.FindStats(weaponId) == null;

            if (removed)
            {
                lines.Add(inCatalog ? "✗ Silah hâlâ katalogda - Console'a bak." : "✓ Silah oyundan kaldırıldı.");
                return string.Join("\n", lines);
            }

            lines.Add(inCatalog ? "✓ Denge oyuna işlendi, tezgâhta görünür." : "✗ Silah katalogda yok - Console'daki [Silah] satırlarına bak.");

            ArtCatalogAsset art = ArtIntegration.LoadCatalog();
            GameObject prefab = art != null ? art.Weapon(weaponId)?.prefab : null;
            WeaponArtData artData = _data.FindArt(weaponId);

            if (artData == null)
            {
                lines.Add("– Modeli yok: oyunda gri kutu görünür.");
            }
            else if (prefab == null)
            {
                lines.Add("✗ Model üretilemedi - Console'daki [Magaza sanati] satırlarına bak.");
            }
            else
            {
                float worst = 0f;
                int missing = 0;

                for (int j = 0; j < artData.Attachments.Count; j++)
                {
                    Transform child = prefab.transform.Find(ArtIntegration.AttachmentPrefix + artData.Attachments[j].Name);

                    if (child == null)
                    {
                        missing++;
                        continue;
                    }

                    if (j < expected.Count)
                    {
                        worst = Mathf.Max(worst, (child.localPosition - expected[j]).magnitude / Mathf.Max(length, 1e-5f));
                    }
                }

                if (missing > 0) lines.Add($"✗ {missing} aksesuar prefab'da yok - Console'a bak.");
                else if (worst >= 0.001f) lines.Add($"✗ Aksesuarlar pencerede gördüğünden %{worst * 100f:0.0} kaymış. Bunu bildir.");
                else lines.Add(artData.Attachments.Count > 0
                                   ? "✓ Model ve aksesuarlar tam burada gördüğün yerde."
                                   : "✓ Model üretildi.");
            }

            var audio = AssetDatabase.LoadAssetAtPath<AudioCatalogAsset>(AudioIntegration.CatalogPath);
            lines.Add(audio != null && audio.FindWeapon(weaponId) != null
                          ? "✓ Sesler bağlandı."
                          : "– Ses satırı yok: sentezlenmiş ses çalar.");

            lines.Add("Oyunda denemek için Play'e bas; tezgâhtan satın alabilirsin.");

            return string.Join("\n", lines);
        }

        // =============================================================== yardimci

        private void PickerButton(string label, string suggestedSearch, System.Action<string> onPick)
        {
            var content = new GUIContent(label);
            Rect rect = GUILayoutUtility.GetRect(content, GUI.skin.button, GUILayout.Width(140f));

            if (GUI.Button(rect, content))
            {
                PopupWindow.Show(rect, new PrefabPickerPopup(path =>
                {
                    onPick(path);
                    Apply();
                }, suggestedSearch));
            }
        }

        private static string CleanName(string name)
        {
            var clean = new System.Text.StringBuilder(name.Length);

            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                clean.Append(ok ? c : '_');
            }

            return clean.ToString();
        }

        private static bool UniqueName(WeaponArtData art, string name, int exceptIndex)
        {
            for (int i = 0; i < art.Attachments.Count; i++)
            {
                if (i != exceptIndex && art.Attachments[i].Name == name) return false;
            }

            return true;
        }

        private static float Clamp(float value, Vector2 range) => Mathf.Clamp(value, range.x, range.y);

        private static float Wrap(float degrees) => Mathf.Repeat(degrees + 180f, 360f) - 180f;

        /// <summary>Silahı çerçeveye sığdıran ortografik yarı yükseklik, kenarda pay bırakarak.</summary>
        private static float Fit(float across, float tall, float aspect) =>
            Mathf.Max(across / Mathf.Max(aspect, 0.01f), tall) * 0.5f * 1.15f;
    }

    /// <summary>
    /// Paketlerdeki prefab'ları küçük resimleriyle listeleyen arama kutusu.
    ///
    /// <para><c>_Project</c> (üretilmiş kopyalar) ve <c>Mirror</c> listelenmez: bir
    /// üretilmiş kopyayı kaynak diye seçmek, onu bir sonraki üretimde kendi üstüne
    /// yazdırırdı.</para>
    /// </summary>
    internal sealed class PrefabPickerPopup : PopupWindowContent
    {
        private const int MaxShown = 80;

        private static List<string> _all;

        private readonly System.Action<string> _onPick;
        private string _search;
        private Vector2 _scroll;
        private bool _focused;

        public PrefabPickerPopup(System.Action<string> onPick, string suggestedSearch)
        {
            _onPick = onPick;
            _search = suggestedSearch ?? string.Empty;
        }

        public override Vector2 GetWindowSize() => new Vector2(560f, 560f);

        public override void OnGUI(Rect rect)
        {
            if (_all == null) Collect();

            EditorGUILayout.LabelField("Paketlerdeki prefab'lar", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName("PrefabSearch");
                _search = EditorGUILayout.TextField("Ara", _search);

                if (!_focused)
                {
                    EditorGUI.FocusTextInControl("PrefabSearch");
                    _focused = true;
                }

                if (GUILayout.Button("Temizle", GUILayout.Width(60f))) _search = string.Empty;
                if (GUILayout.Button("Yenile", GUILayout.Width(60f))) Collect();
            }

            EditorGUILayout.LabelField("İpucu: 'Weapons', 'Optic', 'Muzzle', 'Laser', 'Grip', 'Scope' ya da paket adı yaz.",
                                       EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int shown = 0;

            foreach (string path in _all)
            {
                if (_search.Length > 0 && path.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (++shown > MaxShown)
                {
                    EditorGUILayout.LabelField($"… daha fazlası var, aramayı daralt.", EditorStyles.miniLabel);
                    break;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Texture thumbnail = asset != null
                    ? AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset)
                    : null;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(thumbnail, GUILayout.Width(52f), GUILayout.Height(52f));

                    string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
                    var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };

                    if (GUILayout.Button($"{Path.GetFileNameWithoutExtension(path)}\n{folder}", style, GUILayout.Height(52f)))
                    {
                        _onPick(path);
                        editorWindow.Close();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            if (AssetPreview.IsLoadingAssetPreviews()) editorWindow.Repaint();
        }

        private static void Collect()
        {
            _all = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.StartsWith("Assets/")) continue;
                if (path.StartsWith("Assets/_Project/") || path.StartsWith("Assets/Mirror/")) continue;

                _all.Add(path);
            }

            _all.Sort(System.StringComparer.OrdinalIgnoreCase);
        }
    }
}
