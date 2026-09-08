using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Zombinin yürüyüşü. <b>Klip yok — kemikler koddan sürülüyor.</b> 2026-09-07.
    ///
    /// <para><b>Neden prosedürel</b> (geliştirici: <i>"zombilere yürüme animasyonu
    /// yap"</i>): paketin Lite sürümü <b>tek bir animasyon klibi taşımıyor</b>, model
    /// T duruşunda geliyor. Klip satın almak ya da elle yapmak bu işin dışında; ama
    /// yürümeyen bir zombi kaymış gibi görünür ve <i>ne yaptığı okunmaz</i> —
    /// telegraf, bu türün temel kuralı (ai-code.md).</para>
    ///
    /// <para><b>Faz hızdan gelir, saatten değil.</b> Adım döngüsü zombinin gerçek
    /// hızıyla ilerliyor: yavaşlayınca adım yavaşlıyor, durunca duruyor. Sabit hızlı
    /// bir döngü, yerinde sayan ama ayakları koşan bir zombi üretirdi — oyuncunun en
    /// çok fark ettiği yanlışlık türü.</para>
    ///
    /// <para><b>Bütçe</b> (ai-code.md): kare başına altı kemiğe quaternion yazmak,
    /// <c>Animator</c>'ın yaptığı işin çok altında. Yine de uzaktaki zombi hiç
    /// güncellenmiyor — kırk ajanda görünmeyen bir yürüyüş bedavaya yakın olmalı,
    /// bedava değil.</para>
    ///
    /// <para><b>Sürünen zombiye dokunmaz:</b> bacağı koparılmış zombinin gövdesi
    /// <c>visualRig</c> ile öne yatırılıyor ve bacakları kapanıyor; oraya bir yürüyüş
    /// yazmak "sürünürken tekme atan" bir şey üretirdi.</para>
    /// </summary>
    [AddComponentMenu("Bunker/Zombie Walk Animator")]
    public sealed class ZombieWalkAnimator : MonoBehaviour
    {
        /// <summary>Bir metre yolda kaç adım döngüsü. Büyütmek adımları sıklaştırır.</summary>
        private const float CyclesPerMeter = 0.55f;

        /// <summary>Bu mesafeden uzakta hiç güncellenmez.</summary>
        private const float UpdateDistanceMeters = 28f;

        [Header("Genlik (derece)")]
        [SerializeField] private float thighSwingDegrees = 26f;
        [SerializeField] private float shinBendDegrees = 22f;
        [SerializeField] private float armSwingDegrees = 10f;

        [Header("Kollar (T pozunu kirar)")]
        [Tooltip("Kollari yanlardan asagi indirme acisi. 0 = T pozu (manken).")]
        [SerializeField] private float armReachDownDegrees = 62f;

        [Tooltip("Kollarin one uzanma acisi. Zombi silueti bundan geliyor.")]
        [SerializeField] private float armReachForwardDegrees = 48f;

        [Tooltip("Dirsek bukumu. Dumduz uzanan bir kol mankene benziyor.")]
        [SerializeField] private float elbowBendDegrees = 26f;

        [Header("Vurus")]
        [Tooltip("Hazirlikta kollarin GERI cekilme acisi - telegraf budur.")]
        [SerializeField] private float armWindupDegrees = 34f;

        [Tooltip("Vurus aninda kollarin ILERI savrulma acisi.")]
        [SerializeField] private float armStrikeDegrees = 96f;

        [Tooltip("Hazirlikta kollarin YANLARA acilma acisi. Silueti genisletir - " +
                 "uzaktan okunan tek sey bu. Vurusta ayni miktar ice kapanir.")]
        [SerializeField] private float armSwingOpenDegrees = 30f;

        [Tooltip("Vurus aninda govdenin one ATILMA acisi. Kol tek basina bir hamle " +
                 "degil; agirligini govde tasir.")]
        [SerializeField] private float spineLungeDegrees = 14f;

        [Tooltip("Govdenin one egimi. Zombi dik degil, one dusmus yurur - siluetin " +
                 "'zombi' okunmasi buradan geliyor.")]
        [SerializeField] private float spineLeanDegrees = 12f;

        private Transform _thighLeft, _thighRight;
        private Transform _shinLeft, _shinRight;
        private Transform _armLeft, _armRight;
        private Transform _forearmLeft, _forearmRight;
        private Transform _spine;

        private Quaternion _thighLeftRest, _thighRightRest;
        private Quaternion _shinLeftRest, _shinRightRest;
        private Quaternion _armLeftRest, _armRightRest;
        private Quaternion _forearmLeftRest, _forearmRightRest;
        private Quaternion _spineRest;

        private ZombieAgent _agent;
        private Transform _transform;
        private Camera _camera;
        private float _lastCameraSearch = -99f;

        private float _phase;
        private Vector3 _lastPosition;
        private bool _bound;

        private void Awake()
        {
            _agent = GetComponent<ZombieAgent>();
            _transform = transform;

            Bind();

            if (!_bound) enabled = false;
        }

        private void OnEnable()
        {
            // Havuzdan cikan zombi yeni bir adimla baslar; eski sahibinin fazinda
            // kalmasi, dogar dogmaz yarim adim atmasi demek olurdu.
            _phase = Random.value;
            _lastPosition = transform.position;
        }

        /// <summary>
        /// Kemikleri <b>adlarıyla</b> bulur (Rigify düzeni: <c>thigh.L</c>,
        /// <c>shin.L</c>, <c>upper_arm.L</c>, <c>spine</c>).
        ///
        /// <para><b>Eksik kemik sessiz geçilmez:</b> model değişirse yürüyüş çalışmaz
        /// ve sebebi söylenmezse "zombiler kayıyor" diye aylarca yaşar. Bu projedeki
        /// hataların en sık türü tam olarak bu.</para>
        /// </summary>
        private void Bind()
        {
            Transform root = transform;

            _thighLeft = Find(root, "thigh.L");
            _thighRight = Find(root, "thigh.R");
            _shinLeft = Find(root, "shin.L");
            _shinRight = Find(root, "shin.R");
            _armLeft = Find(root, "upper_arm.L");
            _armRight = Find(root, "upper_arm.R");
            _forearmLeft = Find(root, "forearm.L");
            _forearmRight = Find(root, "forearm.R");
            _spine = Find(root, "spine");

            _bound = _thighLeft != null && _thighRight != null && _spine != null;

            if (!_bound)
            {
                Debug.LogWarning(
                    "[Zombi/Yurume] Kemikler bulunamadi (thigh.L / thigh.R / spine) - " +
                    "zombi KAYARAK hareket edecek. Model degistiyse kemik adlari da " +
                    "degismis olabilir; Bunker > Gorunum > Model Onizlemesi Cek ile bak.",
                    this);
                return;
            }

            // KOL KEMIKLERI DE SESSIZ GECILMEZ (2026-09-08).
            //
            // <b>Neden eklendi:</b> `_bound` yalnizca bacaklara ve omurgaya bakiyordu.
            // Kollari olmayan bir modelde yuruyus calisiyor, VURUS ANIMASYONU ise
            // hicbir sey yapmiyordu - ve hicbir yerde bir uyari yoktu. "Zombiler
            // vururken kollarini oynatmiyor" cumlesinin sebebi tam olarak bu olabilir
            // ve teshis etmenin baska yolu yok (ai-code.md: her ajan ne yaptigini
            // soyleyebilmeli).
            if (_armLeft == null || _armRight == null)
            {
                Debug.LogWarning(
                    "[Zombi/Yurume] Kol kemikleri bulunamadi (upper_arm.L / upper_arm.R) - " +
                    "zombiler VURURKEN kollarini oynatmayacak. Model degistiyse kemik " +
                    "adlari da degismis olabilir; Bunker > Gorunum > Model Onizlemesi " +
                    "Cek ile bak.", this);
            }

            _thighLeftRest = _thighLeft.localRotation;
            _thighRightRest = _thighRight.localRotation;
            _armLeftRest = _armLeft != null ? _armLeft.localRotation : Quaternion.identity;
            _armRightRest = _armRight != null ? _armRight.localRotation : Quaternion.identity;
            _shinLeftRest = _shinLeft != null ? _shinLeft.localRotation : Quaternion.identity;
            _shinRightRest = _shinRight != null ? _shinRight.localRotation : Quaternion.identity;
            _forearmLeftRest = _forearmLeft != null ? _forearmLeft.localRotation : Quaternion.identity;
            _forearmRightRest = _forearmRight != null ? _forearmRight.localRotation : Quaternion.identity;
            _spineRest = _spine.localRotation;
        }

        private static Transform Find(Transform root, string boneName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == boneName) return all[i];
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editör önizlemesi: verilen fazın pozunu <b>oyunu çalıştırmadan</b> uygular.
        ///
        /// <para><b>Neden var:</b> yürüyüşün doğru görünüp görünmediğini bir oyun
        /// testiyle öğrenmek, her denemede bir tur oynamak demek — ve ilk sürüm tam da
        /// bu yüzden bozuk çıktı. Silah yönünde işe yarayan döngünün aynısı:
        /// <i>çek, bak, düzelt.</i></para>
        /// </summary>
        public void EditorPreviewPose(float phase01)
        {
            if (!_bound)
            {
                _transform = transform;
                Bind();
                if (!_bound) return;
            }

            ApplyPose(phase01 * Mathf.PI * 2f);
        }
#endif

        private void LateUpdate()
        {
            // Kemikler LateUpdate'te yazilir: NavMeshAgent konumu Update'te tasir ve
            // once yazsaydik poz bir kare geride kalirdi.
            Vector3 position = _transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;

            if (!ShouldAnimate(position))
            {
                return;
            }

            delta.y = 0f;
            float distance = delta.magnitude;

            // Faz YOL ile ilerler: hiz dustugunde adim da yavaslar, durunca durur.
            _phase += distance * CyclesPerMeter;

            ApplyPose(_phase * Mathf.PI * 2f);
        }

        /// <summary>Verilen faz acisinin pozunu kemiklere yazar.</summary>
        private void ApplyPose(float angle)
        {
            float swing = Mathf.Sin(angle);

            // Bacaklar zit fazda; diz yalnizca ADIM ILERI GIDERKEN bukulur (yarim
            // dalga), cunku gerceginde de oyle - geri giderken bacak duz kalir.
            SetSwing(_thighLeft, _thighLeftRest, swing * thighSwingDegrees);
            SetSwing(_thighRight, _thighRightRest, -swing * thighSwingDegrees);

            SetSwing(_shinLeft, _shinLeftRest, -Mathf.Max(0f, -swing) * shinBendDegrees);
            SetSwing(_shinRight, _shinRightRest, -Mathf.Max(0f, swing) * shinBendDegrees);

            ApplyArms(swing);

            // Govde: sabit one egim + VURUS ANINDA ek atilma (2026-09-08).
            //
            // Egim eksenden bagimsiz (ayni gerekce): omurga kemiginin yerel ekseni
            // yukari dogru uzanir, ona Euler(x) yazmak govdeyi one degil YANA yatirirdi.
            //
            // <b>Neden govde de hareket ediyor:</b> yalnizca kollari savuran bir figur
            // "el sallıyor" gibi okunur. Bir vurusun agirligi govdeden gelir - once
            // geriye toplanir, sonra one atilir. Kolun acisi iki kat buyutulse bile
            // bunu tek basina veremez.
            float spineWindup = _agent != null ? _agent.WindupProgress01 : 0f;
            float spineStrike = _agent != null ? _agent.StrikeProgress01 : 1f;

            float lean = spineLeanDegrees
                         - spineWindup * spineWindup * spineLungeDegrees * 0.6f
                         + (1f - spineStrike) * (1f - spineStrike) * spineLungeDegrees;

            SetSwing(_spine, _spineRest, lean);

            // KEMIGE KONUM YAZILMIYOR (2026-09-07, olculdu).
            //
            // Burada bir "adim basina inip cikma" (bob) vardi: <c>spine.localPosition</c>
            // uzerine 3.5 cm ekleniyordu. Ama kemik konumu ARMATURE'UN yerel biriminde
            // olculur ve bu rigin ebeveyn olcegi bire bir degil - 3.5 cm, dunyada
            // METRELERE donusuyordu. Onizleme bunu acikca gosterdi: modelin transform'u
            // (0.03, -1.59, 3.20)'de dururken cizilen mesh x=+-3.02'de zipliyordu.
            // Oyunda gorunen sey buydu: "ileri geri haritada uculuyorlar".
            //
            // <b>Ders:</b> donusler olcekten bagimsizdir, konumlar degildir. Iskelete
            // yalnizca donus yaziyoruz; govdenin inip cikmasi istenirse dogru yeri
            // <c>visualRig</c> (olcegi bilinen bir nesne), kemik degil.
        }

        /// <summary>
        /// Kollar: <b>T pozundan çıkar, öne uzanır, vururken hamle yapar</b>.
        /// 2026-09-08.
        ///
        /// <para><b>Neden gerekti</b> (geliştirici): <i>"zombilerin kolları T şeklinde
        /// kalmasın, vururken kollarıyla hamle yapsın, vuruş hissi oluşsun — yani
        /// dümdüz ölüyorum."</i> Son cümle asıl teşhis: vuruşun <b>görünmemesi</b>,
        /// ölümü haksızlık gibi okutuyor. Oyuncu hasarı yiyor ama neyin vurduğunu
        /// görmüyor.</para>
        ///
        /// <para><b>Üç katman, sırayla:</b></para>
        /// <list type="number">
        /// <item>T pozunu kır: kolları yanlardan indirip öne uzat. Model T duruşunda
        /// geliyor ve bu duruş <i>bir insanın hiç durmadığı</i> duruş — zombiyi
        /// mankene çeviren şey buydu.</item>
        /// <item>Yürürken hafif sallan.</item>
        /// <item>Vururken: hazırlıkta kollar <b>geriye</b> çekilir (telegraf, oyuncu
        /// bunu görüp çekilebilmeli), sonra <b>ileri savrulur</b>. Geri çekilme
        /// olmadan ileri hareket okunmaz — bir hamlenin okunmasını sağlayan şey
        /// gerilimidir.</item>
        /// </list>
        ///
        /// <para><b>Telegraf süresi config'ten</b> (<c>zombie.json →
        /// attack.windupSeconds</c>): burada bir süre sabiti yok, yalnızca 0..1
        /// ilerleme okunuyor. Süreyi değiştiren bir denge ayarı animasyonu da
        /// değiştirmeli, yoksa ikisi ayrışır.</para>
        /// </summary>
        private void ApplyArms(float swing)
        {
            // 1) T pozunu kir: omuzdan asagi ve one. Zombi silueti bu.
            float reachDown = armReachDownDegrees;
            float reachForward = armReachForwardDegrees;

            // 3) Vurus: once GERI VE YUKARI (telegraf), sonra ONE VE ASAGI (hamle).
            float windup = _agent != null ? _agent.WindupProgress01 : 0f;
            float strike = _agent != null ? _agent.StrikeProgress01 : 1f;

            // Hazirlik: kollar geri gider. Egri sonda hizlanir - gerilim boyle okunur.
            float pullBack = windup * windup * armWindupDegrees;

            // Hamle: vurus indigi anda tepe, sonra aciklik boyunca soner.
            float lunge = (1f - strike) * (1f - strike) * armStrikeDegrees;

            float forward = reachForward + lunge - pullBack;

            // 2) Yuruyus salinimi: hamle yokken belirgin, hamle sirasinda bastirilir -
            // iki hareket ust uste binerse ikisi de okunmaz.
            float walkBlend = Mathf.Clamp01(1f - lunge / Mathf.Max(1f, armStrikeDegrees));
            float walk = swing * armSwingDegrees * walkBlend;

            // KOLLAR HAZIRLIKTA ACILIR, VURUSTA KAPANIR (2026-09-08).
            //
            // <b>Neden gerekti</b> (gelistirici, ikinci kez: "zombiler saldirirken
            // kollar vurma animasyonu yapmali"): onceki surumde vurusun tamami
            // <i>tek bir eksende</i> yasiyordu - kollar one gidip geri geliyordu. Bir
            // izleyici ekranin ortasindaki zombiye bakarken bu hareketi PERSPEKTIFTE
            // gorur, yani neredeyse hic gormez: one uzanan bir kol, kisalmis bir kol
            // demek.
            //
            // <b>Cozum ikinci bir eksen:</b> hazirlikta kollar yanlara/yukari ACILIR
            // (siluet genisler - bu, uzaktan bile okunan tek sey), vurusta ise ice
            // KAPANIR. Siluetin genisleyip daralmasi, kolun ne kadar one geldiginden
            // bagimsiz olarak gorunur.
            float openUp = windup * windup * armSwingOpenDegrees;
            float closeIn = (1f - strike) * (1f - strike) * armSwingOpenDegrees * 1.15f;

            float roll = reachDown - openUp + closeIn;

            // Kollari YANLARDAN INDIR: bu, karakterin ILERI ekseni etrafinda bir
            // donus - sag kol saat yonunde, sol kol tersine.
            //
            // <b>SetSwing ayrica CAGRILMIYOR</b> (2026-09-08): oyle bir cagri vardi ve
            // OLU KODDU - SetRoll ayni kemigin localRotation'ini ayni 'rest'ten
            // yeniden kuruyor, yani bir onceki yazmayi silmis oluyordu. Salinim zaten
            // SetRoll'un ilk carpaninda.
            SetRoll(_armLeft, _armLeftRest, forward - walk, -roll);
            SetRoll(_armRight, _armRightRest, forward + walk, roll);

            // Dirsekler: yururken hafif bukuk, VURUSTA ACILIR. Bukuk kalan bir kolla
            // yapilan hamle, "itmek" gibi okunur; acilan kol "savurmak" gibi.
            SetSwing(_forearmLeft, _forearmLeftRest,
                     elbowBendDegrees + pullBack * 0.8f - lunge * 0.45f);
            SetSwing(_forearmRight, _forearmRightRest,
                     elbowBendDegrees + pullBack * 0.8f - lunge * 0.45f);
        }

        /// <summary>
        /// Uzvu önce sallar, sonra <b>karakterin ileri ekseni etrafında</b> yatırır —
        /// kolu yandan aşağı indiren hareket bu.
        ///
        /// <para>Tek bir <c>SetSwing</c> ile yapılamaz: iki farklı eksen etrafında iki
        /// dönüş gerekiyor ve sırası önemli. Önce omuz açısı (öne/arkaya), sonra
        /// yatırma (yandan aşağı).</para>
        /// </summary>
        private void SetRoll(Transform bone, Quaternion rest, float swingDegrees,
                             float rollDegrees)
        {
            if (bone == null || bone.parent == null) return;

            Vector3 right = bone.parent.InverseTransformDirection(_transform.right);
            Vector3 forwardAxis = bone.parent.InverseTransformDirection(_transform.forward);

            if (right.sqrMagnitude < 1e-6f || forwardAxis.sqrMagnitude < 1e-6f) return;

            Quaternion swing = Quaternion.AngleAxis(swingDegrees, right.normalized);
            Quaternion roll = Quaternion.AngleAxis(rollDegrees, forwardAxis.normalized);

            bone.localRotation = roll * swing * rest;
        }

        /// <summary>
        /// Bir uzvu <b>karakterin sağ eksenine göre</b> sallar.
        ///
        /// <para><b>Düzeltilen hata</b> (2026-09-07, geliştirici: <i>"bu nasıl yürüyüş
        /// animasyonu"</i>): önceki sürüm <c>rest * Euler(degrees, 0, 0)</c> yazıyordu,
        /// yani kemiği <b>kendi yerel X'i</b> etrafında döndürüyordu. Bu bir
        /// <i>tahmindi</i> — Blender'dan gelen bir rigde kemiğin yerel ekseni kemik
        /// boyunca uzanır ve hangi eksenin "yana" baktığı modele göre değişir. Bacak
        /// öne değil yana savruluyordu.</para>
        ///
        /// <para><b>Doğrusu ekseni rigden sormak:</b> adım her zaman karakterin sağ-sol
        /// ekseni etrafında atılır. O yönü kemiğin <i>ebeveyn</i> uzayına çevirip
        /// oradan döndürünce, kemiğin kendi eksen düzeni ne olursa olsun bacak öne
        /// gider. Aynı ders silahlarda da yaşandı: <b>geometriden sezgi çıkarma,
        /// ölç.</b></para>
        /// </summary>
        private void SetSwing(Transform bone, Quaternion rest, float degrees)
        {
            if (bone == null || bone.parent == null) return;

            // Karakterin sag ekseni, kemigin ebeveyn uzayinda.
            //
            // <b>NORMALIZE SART</b> (2026-09-07, olculdu): FBX armature'unde olcek var,
            // yani <c>InverseTransformDirection</c> BIRIM OLMAYAN bir vektor doner.
            // <c>Quaternion.AngleAxis</c> birim olmayan bir eksenle birim olmayan bir
            // quaternion uretir ve Unity onu matrise cevirirken kemigi DONDURMEZ,
            // GERER. Sonuc: deri kopuyor, mesh metrelerce oteye firliyor ve isareti
            // adim faziyla degistigi icin zombi "ileri geri uculuyor" gorunuyor.
            //
            // Bunu tahminle degil olcerek bulduk: onizlemede modelin transform'u
            // yerinde duruyordu ama renderer sinirlarinin merkezi x=+-3.02'de
            // zipliyordu. Gozle "animasyon bozuk" demek, yanlis yeri duzelttirirdi.
            Vector3 axis = bone.parent.InverseTransformDirection(_transform.right);

            if (axis.sqrMagnitude < 1e-6f) return;

            bone.localRotation = Quaternion.AngleAxis(degrees, axis.normalized) * rest;
        }

        /// <summary>
        /// Bu zombi bu karede animasyon hak ediyor mu.
        ///
        /// <para>Ölüyse hayır (yıkılma ayrı bir şey), sürünüyorsa hayır (gövde zaten
        /// yatırılmış), uzaktaysa hayır (görünmeyen bir yürüyüşün bedeli olmamalı).</para>
        /// </summary>
        private bool ShouldAnimate(Vector3 position)
        {
            if (_agent == null) return false;
            if (!_agent.IsAlive) return false;
            if (_agent.IsCrawling) return false;

            if (_camera == null) AcquireCamera();
            if (_camera == null) return false;

            float distanceSqr = (_camera.transform.position - position).sqrMagnitude;
            return distanceSqr <= UpdateDistanceMeters * UpdateDistanceMeters;
        }

        private void AcquireCamera()
        {
            if (Time.unscaledTime - _lastCameraSearch < 1f) return;
            _lastCameraSearch = Time.unscaledTime;

            _camera = Camera.main;
            if (_camera == null) _camera = FindFirstObjectByType<Camera>();
        }
    }
}
