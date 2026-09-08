using System.Collections.Generic;
using Bunker.Audio;
using Bunker.Systems.Pickups;
using UnityEngine;

namespace Bunker.AI
{
    /// <summary>
    /// Ölen zombinin yere bıraktığı eşya (2026-09-07).
    ///
    /// <para><b>Neden var:</b> geliştirici, <i>"zombiler içerde ölünce hiç drop
    /// atmıyor"</i>. Eksik olan yalnızca bir ödül değildi — oyuncuyu <b>güvenli
    /// köşeden çıkaran</b> mekanizma yoktu. Eşya, sürünün öldüğü yere düşer; almak
    /// için oraya gitmek gerekir. Kartlar oyuncuyu güçlendirir, eşya onu
    /// <i>hareket ettirir</i>.</para>
    ///
    /// <para><b>Neden koddan üretiliyor, prefab'dan değil:</b> proje greybox; eşya bir
    /// küp, bir renk ve bir yazı. Prefab olsaydı beş eşya için beş varlık, beş
    /// materyal ve elle bağlanmış beş referans olurdu — ve hepsi
    /// <c>unity-conventions.md</c>'nin okumayı yasakladığı YAML'a yazılırdı.</para>
    ///
    /// <para><b>Havuzlu ve sınırlı</b> (systems-code.md): sahada en fazla
    /// <see cref="MaxAlive"/> eşya durur. Sınırsız bir düşürücü, ileriye ertelenmiş
    /// bir kare hızı hatasıdır.</para>
    ///
    /// <para><b>Otorite sunucuda</b> (ADR-0004): eşyayı sunucu düşürür, toplama
    /// kararını sunucu verir. M-01 solo host olduğu için ikisi aynı makinede.</para>
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PowerupPickup : MonoBehaviour
    {
        /// <summary>Sahada aynı anda durabilecek en fazla eşya.</summary>
        public const int MaxAlive = 12;

        private static readonly List<PowerupPickup> Alive = new List<PowerupPickup>(MaxAlive);

        /// <summary>Sahadaki eşyalar. <see cref="PowerupLabels"/> adları buradan yazar.</summary>
        internal static IReadOnlyList<PowerupPickup> AliveList => Alive;

        /// <summary>
        /// Eşyanın üstünde yazan şey. <b>Son üç saniyede saniye de yazılır:</b> eşya
        /// zaten yanıp sönerek gideceğini söylüyor, ama "kaç saniyem var" sorusunu
        /// yanıp sönme hızından okumak tahmin demek — ve o tahmin oyuncuyu güvenli
        /// köşeden çıkarıp çıkarmayacağına karar veriyor.
        /// </summary>
        internal string Label => _lifetimeRemaining < 3f
            ? $"{NameFor(_kind)}  {Mathf.CeilToInt(_lifetimeRemaining)}"
            : NameFor(_kind);

        /// <summary>Yazının rengi — eşyanın kendi rengiyle aynı, uzaktan eşleşsin diye.</summary>
        internal Color LabelColor => ColorFor(_kind);

        private static Material _sharedMaterial;

        private PowerupKind _kind;
        private float _lifetimeRemaining;
        private float _pickupRadiusSqr;
        private float _amount;
        private float _seconds;

        private Transform _transform;
        private Renderer _renderer;
        private float _spin;
        private float _baseY;

        /// <summary>
        /// Bir eşya düşürür. <b>Yalnızca sunucu çağırır.</b>
        ///
        /// <para>Sayıların hiçbiri burada yazmaz; hepsi <c>zombie.json → drops</c>'tan
        /// gelip parametre olarak iner (config-data.md).</para>
        /// </summary>
        public static void Spawn(PowerupKind kind, Vector3 position, float lifetimeSeconds,
                                 float pickupRadiusMeters, float amount, float seconds)
        {
            // Tavan: en ESKI esya kaldirilir. Yenisini engellemek yerine eskisini
            // almak dogru - oyuncu az once oldurdugu zombinin birakigini gormeli,
            // bes dakika once dusen ve alinmamis olani degil.
            if (Alive.Count >= MaxAlive && Alive.Count > 0)
            {
                PowerupPickup oldest = Alive[0];
                if (oldest != null) Destroy(oldest.gameObject);
                Alive.RemoveAt(0);
            }

            // SEKIL ESYAYA GORE (2026-09-07, gelistirici: "yere dusen droplari
            // ikonla ne anlama geldigini belirtirsen daha anlamli olur").
            //
            // <b>Neden sekil, neden yalnizca renk degil:</b> renk tek basina bilgi
            // tasimaz (ui-code.md) - renk korlugu bir yana, karanlik bir kosede
            // kirmizi ile turuncu ayni. Siluet uzaktan okunur ve ogrenilir:
            // KURE = can, KUTU = mermi, KAPSUL = zaman esyasi (yavaslatma/dondurma),
            // SILINDIR = nuke. Yaninda ayrica adi yaziyor (PowerupLabels).
            GameObject go = GameObject.CreatePrimitive(ShapeFor(kind));
            go.name = $"Powerup_{kind}";
            go.transform.position = position + Vector3.up * 0.6f;
            go.transform.localScale = ScaleFor(kind);

            // Carpisan KAPALI: esya oyuncuyu da zombiyi de itmemeli, ve isin
            // sorgularina (patlama sarapneli, zombi gorus kontrolu) girmemeli -
            // yerdeki bir kutunun vurusu engellemesi okunamayan bir kural olurdu.
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var pickup = go.AddComponent<PowerupPickup>();
            pickup.Init(kind, lifetimeSeconds, pickupRadiusMeters, amount, seconds);

            Alive.Add(pickup);
            PowerupLabels.EnsureInstalled();
        }

        /// <summary>Yeni run ya da saha temizliği: yerdeki her şey kalkar.</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < Alive.Count; i++)
            {
                if (Alive[i] != null) Destroy(Alive[i].gameObject);
            }

            Alive.Clear();
        }

        private void Init(PowerupKind kind, float lifetimeSeconds, float pickupRadiusMeters,
                          float amount, float seconds)
        {
            _kind = kind;
            _lifetimeRemaining = lifetimeSeconds;
            _pickupRadiusSqr = pickupRadiusMeters * pickupRadiusMeters;
            _amount = amount;
            _seconds = seconds;

            _transform = transform;
            _baseY = _transform.position.y;
            _renderer = GetComponent<Renderer>();

            if (_renderer != null)
            {
                // Materyal PAYLASILIR, kopyalanmaz: renderer.material her esyada bir
                // materyal ornegi sizdirir (shader-graphics.md). Renk ornege ozel
                // oldugu icin MaterialPropertyBlock ile veriliyor.
                if (_sharedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                                    ?? Shader.Find("Unlit/Color");
                    if (shader != null) _sharedMaterial = new Material(shader);
                }

                if (_sharedMaterial != null) _renderer.sharedMaterial = _sharedMaterial;

                var block = new MaterialPropertyBlock();
                Color color = ColorFor(kind);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                _renderer.SetPropertyBlock(block);
            }
        }

        private void OnDestroy() => Alive.Remove(this);

        private void Update()
        {
            float dt = Time.deltaTime;

            _lifetimeRemaining -= dt;

            if (_lifetimeRemaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Donme ve sekme: yerdeki gri bir kutu, greybox sahnede geometriden
            // ayirt edilemez. Hareket, "bu alinabilir bir sey" diyen tek isaret.
            _spin += dt * 140f;
            float bob = Mathf.Sin(Time.time * 3f) * 0.12f;
            _transform.SetPositionAndRotation(
                new Vector3(_transform.position.x, _baseY + bob, _transform.position.z),
                Quaternion.Euler(25f, _spin, 0f));

            // SON UC SANIYE YANIP SONER: sure dolmadan once oyuncunun kosmaya karar
            // verebilmesi icin. Uyarisiz kaybolan bir esya, oyuncunun ogrenemedigi
            // bir kural olurdu.
            if (_renderer != null && _lifetimeRemaining < 3f)
            {
                _renderer.enabled = Mathf.Repeat(_lifetimeRemaining, 0.32f) > 0.16f;
            }

            TryCollect();
        }

        /// <summary>
        /// Toplama kontrolü. <b>En yakın hedefe</b> bakar; M-01'de tek oyuncu var.
        ///
        /// <para>TODO(netcode-programmer, M-02): co-op'ta eşya ağ üzerinden var olmalı
        /// (şu an yalnızca host'ta yaratılıyor, uzak istemci hiç görmüyor) ve toplama
        /// kararı sunucuda verilmeli — iki oyuncunun aynı karede aynı eşyayı alması
        /// klasik "aynı tick çakışması" (netcode.md).</para>
        /// </summary>
        private void TryCollect()
        {
            ZombieTargetBeacon nearest = ZombieTargets.Nearest(_transform.position);
            if (nearest == null || !nearest.IsTargetable) return;

            Vector3 delta = nearest.GroundPosition - _transform.position;
            if (delta.sqrMagnitude > _pickupRadiusSqr) return;

            // Etki, esyanin kendisinde DEGIL: can Gameplay'de, yavaslatma sahada.
            // Burasi yalnizca "toplandi" der ve yuku tasir (PowerupSignals).
            PowerupSignals.RaisePicked(_kind, _amount, _seconds);

            GameAudio.PlayAt(_kind == PowerupKind.Nuke ? SfxId.RoundCleared : SfxId.Purchase,
                             _transform.position);

            Destroy(gameObject);
        }

        /// <summary>
        /// Eşyanın <b>silueti</b>. Renkten önce okunan şey budur (2026-09-07).
        ///
        /// <para><b>Neden şekil:</b> beş eşya beş renkli küptü ve yerdeki bir küpün ne
        /// olduğunu ancak üstüne basınca öğreniyordun — yani eşya, oyuncuyu güvenli
        /// köşeden çıkaran bir <i>karar</i> olmaktan çıkıp bir piyangoya dönüyordu.
        /// "Şu köşedeki can mı, mermi mi" sorusunun cevabı, koşmaya değip değmeyeceğini
        /// belirler.</para>
        ///
        /// <para><b>Neden ilkel şekiller, ikon dokusu değil:</b> bir ikon dokusu ya
        /// dünyada bir quad (her açıdan okunmaz, kenardan bakınca kaybolur) ya da bir
        /// Canvas (eşya başına bir yeniden düzenleme) demek. Siluet her açıdan aynı
        /// şeyi söyler ve bedavaya yakındır.</para>
        /// </summary>
        private static PrimitiveType ShapeFor(PowerupKind kind) => kind switch
        {
            // Can: KURE - yumusak, organik, "iyi sey".
            PowerupKind.Health => PrimitiveType.Sphere,

            // Mermi: KUTU - sandik. Kesin hatli, endustriyel.
            PowerupKind.Ammo => PrimitiveType.Cube,

            // Zaman esyalari: KAPSUL - dikey duran, "sise" silueti.
            PowerupKind.Slow => PrimitiveType.Capsule,
            PowerupKind.Freeze => PrimitiveType.Capsule,

            // Nuke: SILINDIR - varil. En nadir esya, en farkli siluet.
            _ => PrimitiveType.Cylinder
        };

        /// <summary>
        /// Eşyanın boyu. Nuke ve can biraz daha büyük: <b>en değerli eşyalar en uzaktan
        /// okunmalı</b>, çünkü onlar için koşmaya değer.
        /// </summary>
        private static Vector3 ScaleFor(PowerupKind kind) => kind switch
        {
            PowerupKind.Health => new Vector3(0.44f, 0.44f, 0.44f),
            PowerupKind.Ammo => new Vector3(0.42f, 0.30f, 0.42f),
            PowerupKind.Slow => new Vector3(0.26f, 0.24f, 0.26f),
            PowerupKind.Freeze => new Vector3(0.26f, 0.24f, 0.26f),
            _ => new Vector3(0.36f, 0.28f, 0.36f)
        };

        /// <summary>Eşyanın adı — HUD bunu eşyanın üstüne yazar.</summary>
        internal static string NameFor(PowerupKind kind) => kind switch
        {
            PowerupKind.Health => "CAN",
            PowerupKind.Ammo => "MERMI",
            PowerupKind.Slow => "YAVASLATMA",
            PowerupKind.Freeze => "DONDURMA",
            _ => "NUKE"
        };

        /// <summary>
        /// Eşyanın rengi. <b>Renk tek başına bilgi taşımaz</b> (ui-code.md) — siluet ve
        /// yazı asıl bilgiyi taşır; renk yalnızca uzaktan ayırt etmeyi hızlandırır.
        /// </summary>
        private static Color ColorFor(PowerupKind kind) => kind switch
        {
            PowerupKind.Health => new Color(0.90f, 0.20f, 0.25f),
            PowerupKind.Ammo => new Color(0.95f, 0.75f, 0.15f),
            PowerupKind.Slow => new Color(0.35f, 0.70f, 0.95f),
            PowerupKind.Freeze => new Color(0.65f, 0.95f, 1.00f),
            _ => new Color(0.55f, 1.00f, 0.35f)
        };
    }
}
