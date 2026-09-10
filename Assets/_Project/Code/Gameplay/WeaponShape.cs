using Bunker.Config;
using UnityEngine;

namespace Bunker.Gameplay
{
    /// <summary>
    /// Silahların gri kutu görselini üretir. 2026-09-06.
    ///
    /// <para><b>Neden gerekiyordu</b> (geliştirici): <i>"silahları sınıfına göre görünüm
    /// ayarla, taramalıda tabanca gibi gözükmesin."</i> Dört silah aynı modeli
    /// taşıyorsa oyuncu elindekinin ne olduğunu yalnızca yazıdan bilir — ve savaşın
    /// ortasında kimse yazı okumaz. <b>Siluet, arayüzün en hızlı okunan parçasıdır</b>
    /// (PILLAR-04).</para>
    ///
    /// <para><b>Tek üretici, iki tüketici:</b> aynı geometri hem elde (viewmodel) hem
    /// duvarda (satın alma noktası) kullanılıyor. İki ayrı yerde çizilseydi, duvardaki
    /// pompalı ile eldeki pompalı zamanla birbirine benzemez olurdu ve oyuncu duvarda
    /// gördüğü şeyi eline aldığında tanımazdı.</para>
    ///
    /// <para><b>Ölçek parametrik:</b> el modeli küçük ve kameraya yakın, duvardaki
    /// teşhir büyük. Aynı oranlar, farklı boy.</para>
    /// </summary>
    public static class WeaponShape
    {
        /// <summary>
        /// Bir silahın gövdesini kurar — <b>varsa gerçek model, yoksa gri kutu</b>.
        /// 2026-09-07.
        ///
        /// <para><b>Neden geri düşüş yolu duruyor:</b> katalog eksik ya da bir model
        /// silinmiş olabilir. O durumda elin boş kalması, oyuncunun ne taşıdığını
        /// göremediği <i>sessiz</i> bir hata olurdu — gri kutu çirkin ama okunur, ve
        /// bir sorunun olduğunu söyler.</para>
        /// </summary>
        /// <param name="art">Model kataloğu. <c>null</c> ise gri kutu kurulur.</param>
        /// <returns>Namlu ucunun yerel konumu.</returns>
        public static Vector3 Build(Transform parent, string weaponId, float scale,
                                    Material body, Material accent, ArtCatalogAsset art)
        {
            ArtCatalogAsset.WeaponModel model = art != null ? art.Weapon(weaponId) : null;
            if (model == null) return Build(parent, weaponId, scale, body, accent);

            GameObject instance = Object.Instantiate(model.prefab, parent);
            instance.name = "Model";

            instance.transform.localPosition = model.localPosition * scale;
            instance.transform.localRotation = Quaternion.Euler(model.localEulerAngles);
            instance.transform.localScale = Vector3.one * (model.localScale * scale);

            Strip(instance);
            Tint(instance, model.tint);

            return model.muzzleLocal * scale;
        }

        /// <summary>
        /// Bir bıçağın gövdesini kurar — <b>varsa gerçek model</b>. 2026-09-08.
        ///
        /// <para><b>Neden ayrı bir giriş:</b> bıçağın kataloğu ayrı
        /// (<see cref="ArtCatalogAsset.Melee"/>) ve geri düşüş yolu da farklı —
        /// bulunamayan bir bıçak modeli, gri kutu <i>bıçak</i> üretmeli, gri kutu
        /// tabanca değil.</para>
        /// </summary>
        /// <returns>Gerçek model kurulduysa <c>true</c>; çağıran taraf o zaman kendi
        /// ilkel şekillerini kurmaz.</returns>
        public static bool BuildMelee(Transform parent, string meleeId, float scale,
                                      ArtCatalogAsset art, out Vector3 tipLocal)
        {
            tipLocal = Vector3.zero;

            ArtCatalogAsset.WeaponModel model = art != null ? art.Melee(meleeId) : null;
            if (model == null) return false;

            GameObject instance = Object.Instantiate(model.prefab, parent);
            instance.name = "Model";

            instance.transform.localPosition = model.localPosition * scale;
            instance.transform.localRotation = Quaternion.Euler(model.localEulerAngles);
            instance.transform.localScale = Vector3.one * (model.localScale * scale);

            Strip(instance);
            Tint(instance, model.tint);

            tipLocal = model.muzzleLocal * scale;
            return true;
        }

        /// <summary>
        /// Silahın rengini uygular. 2026-09-07.
        ///
        /// <para><b><c>MaterialPropertyBlock</c> ile, materyali kopyalayarak değil</b>
        /// (shader-graphics.md): <c>renderer.material</c> okumak materyalin bir
        /// kopyasını yaratır — silah başına bir materyal, yani silah başına bir çizim
        /// çağrısı, üstelik sızdıran bir kopya. Özellik bloğu paylaşılan materyale
        /// dokunmaz ve bedavaya yakındır.</para>
        ///
        /// <para><b>Beyazsa hiç dokunulmaz:</b> beyaz "paketten geldiği gibi" demek.
        /// Her silaha blok yazmak, hiçbir şey değiştirmeyen bir iş olurdu.</para>
        /// </summary>
        private static void Tint(GameObject instance, Color tint)
        {
            if (tint == Color.white) return;

            var block = new MaterialPropertyBlock();
            var renderers = instance.GetComponentsInChildren<Renderer>(true);

            // Parca sayaci RENDERER'LAR BOYUNCA ilerler, her renderer'da sifirlanmaz:
            // ikisi ayri nesne olan namlu ile kabza, ikisi de "0. yuva" olsaydi ayni
            // rengi alir ve model yine duz gorunurdu.
            int part = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;

                int slots = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 1;
                if (slots < 1) slots = 1;

                for (int slot = 0; slot < slots; slot++)
                {
                    Surface surface = Surfaces[part % Surfaces.Length];
                    part++;

                    Color color = surface.Apply(tint);

                    block.Clear();
                    block.SetColor(BaseColorId, color);
                    block.SetColor(ColorId, color);
                    block.SetFloat(MetallicId, surface.Metallic);
                    block.SetFloat(SmoothnessId, surface.Smoothness);

                    renderer.SetPropertyBlock(block, slot);
                }
            }
        }

        /// <summary>
        /// Bir yüzey ailesi: rengin ne kadar koyulacağı ve ne kadar parlayacağı.
        /// 2026-09-08.
        /// </summary>
        private readonly struct Surface
        {
            /// <summary>Renk çarpanı. 1 = verilen renk, 0.5 = yarı koyu.</summary>
            public readonly float Value;

            /// <summary>Doygunluk çarpanı. 0 = griye düşer (çelik), 1 = renk kalır.</summary>
            public readonly float Saturation;

            public readonly float Metallic;
            public readonly float Smoothness;

            public Surface(float value, float saturation, float metallic, float smoothness)
            {
                Value = value;
                Saturation = saturation;
                Metallic = metallic;
                Smoothness = smoothness;
            }

            /// <summary>Verilen rengin bu yüzeydeki hâli. HSV üzerinden: koyultmak
            /// RGB'yi çarpmakla aynı şey değil — doygunluk ayrı tutulmalı, yoksa
            /// koyu parçalar renklerini de kaybeder.</summary>
            public Color Apply(Color source)
            {
                Color.RGBToHSV(source, out float h, out float s, out float v);

                return Color.HSVToRGB(h, Mathf.Clamp01(s * Saturation), Mathf.Clamp01(v * Value));
            }
        }

        /// <summary>
        /// Silahın yüzey ailesi. <b>Tek düz renk yerine dört yüzey.</b> 2026-09-08.
        ///
        /// <para><b>Neden değişti</b> (geliştirici: <i>"silahlara dümdüz renk verme,
        /// güzelce yüzeyine parça parça renklendir"</i>): önceki hâl tek bir
        /// <c>_BaseColor</c>'ı modelin <b>bütün</b> renderer'larına yazıyordu. Sonuç,
        /// dokusu olsa bile tek renge boyanmış bir siluetti — plastik oyuncak. Bir
        /// silah gerçekte tek parça değil: gövde boyalı, namlu çıplak çelik, kabza
        /// mat ve koyu, ayrıntılar neredeyse siyah.</para>
        ///
        /// <para><b>Neden yuvaya göre, isme göre değil:</b> dört farklı paketten gelen
        /// modellerin parça adları birbirini tutmuyor ("Barrel", "barrel_01",
        /// "polySurface12"). İsimle eşleştirmek, beşinci paket geldiğinde sessizce tek
        /// renge dönerdi. Yuva sırası her modelde vardır ve sanatçılar gövdeyi
        /// neredeyse her zaman ilk yuvaya koyar.</para>
        ///
        /// <para><b>Renk hâlâ TEK bir sanat kararından türüyor</b> (katalogdaki
        /// <c>tint</c>): dört sayı ayrı ayrı verilseydi silah başına dört karar olurdu
        /// ve palet dağılırdı. Burada verilen renk gövdedir; kalanı ondan çıkar.</para>
        /// </summary>
        private static readonly Surface[] Surfaces =
        {
            new Surface(1.00f, 1.00f, 0.10f, 0.35f),   // govde: verilen renk, mat boya
            new Surface(0.62f, 0.35f, 0.85f, 0.62f),   // metal: koyu, doygunlugu dusuk, parlak
            new Surface(0.40f, 0.75f, 0.05f, 0.18f),   // kabza: koyu ve mat
            new Surface(0.22f, 0.50f, 0.30f, 0.45f)    // ayrinti: neredeyse siyah
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        /// <summary>
        /// Modeli oyuna uygun hâle getirir: <b>çarpıştırıcı yok, gölge yok</b>.
        ///
        /// <para>Çarpıştırıcı bırakmak iki ayrı hata demek olurdu: el modeli oyuncunun
        /// kendi ışınını keserdi (kendi silahına ateş etmek), duvardaki teşhir de
        /// arkasındaki zombiye ateş etmeyi engellerdi. Paketlerden gelen prefab'lar
        /// çarpıştırıcı taşıyabilir; varsayamayız.</para>
        /// </summary>
        private static void Strip(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Object.Destroy(colliders[i]);

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }
        }

        /// <summary>
        /// Bir silahın gri kutu gövdesini <paramref name="parent"/> altına kurar.
        /// </summary>
        /// <param name="weaponId">Katalog id'si (<c>weapon.pistol</c>, ...).</param>
        /// <param name="scale">Boy çarpanı. El modeli 1, duvar teşhiri daha büyük.</param>
        /// <returns>Namlu ucunun yerel konumu — namlu alevi oraya konur.</returns>
        public static Vector3 Build(Transform parent, string weaponId, float scale,
                                    Material body, Material accent)
        {
            switch (weaponId)
            {
                case "weapon.smg": return BuildSmg(parent, scale, body, accent);
                case "weapon.shotgun": return BuildShotgun(parent, scale, body, accent);

                // AK-74, M4 ve M107 gri kutuda TUFEK silüetini paylasiyor
                // (2026-09-09). Bilerek: bu yol yalnizca magaza modeli bulunamadiginda
                // calisan bir YEDEK ve uc ayri gri kutu kutlesi cizmek, hicbir zaman
                // gorulmeyecek bir ayrimi kodlamak olurdu. Model varken zaten uc silah
                // uc ayri modelden gorunuyor.
                //
                // weapon.rifle EMEKLIYE AYRILDI (weapons.json v2) ama satiri duruyor:
                // eski bir kayittan ya da sahnedeki eski bir duvar noktasindan gelen
                // id, gri kutuya duser - null'a degil.
                case "weapon.rifle":
                case "weapon.ak74":
                case "weapon.m4":
                case "weapon.sniper": return BuildRifle(parent, scale, body, accent);

                default: return BuildPistol(parent, scale, body, accent);
            }
        }

        /// <summary>
        /// Tabanca: <b>kısa ve yalın</b>. Referans silah; diğerlerinin okunması buna
        /// göre kolaylaşıyor.
        /// </summary>
        private static Vector3 BuildPistol(Transform p, float s, Material body, Material accent)
        {
            Part(p, "Slide", V(0f, 0f, 0f, s), V(0.045f, 0.055f, 0.24f, s), body);
            Part(p, "Barrel", V(0f, 0.005f, 0.16f, s), V(0.022f, 0.022f, 0.12f, s), body);
            Part(p, "Grip", V(0f, -0.075f, -0.06f, s), V(0.040f, 0.110f, 0.055f, s), accent);
            Part(p, "Sight", V(0f, 0.038f, 0.10f, s), V(0.010f, 0.014f, 0.012f, s), accent);

            return V(0f, 0.005f, 0.235f, s);
        }

        /// <summary>
        /// Taramalı: <b>uzun gövde, aşağı sarkan şarjör, katlanır dipçik</b>. Tabancadan
        /// ilk bakışta ayrılan şey sarkan şarjör.
        /// </summary>
        private static Vector3 BuildSmg(Transform p, float s, Material body, Material accent)
        {
            Part(p, "Receiver", V(0f, 0f, 0.02f, s), V(0.050f, 0.060f, 0.34f, s), body);
            Part(p, "Barrel", V(0f, 0.008f, 0.24f, s), V(0.020f, 0.020f, 0.16f, s), body);
            Part(p, "Handguard", V(0f, -0.010f, 0.18f, s), V(0.042f, 0.040f, 0.12f, s), accent);

            // Sarkan sarjor: taramaliyi bir bakista taniyan sey bu.
            Part(p, "Magazine", V(0f, -0.105f, 0.03f, s), V(0.032f, 0.150f, 0.060f, s), accent);

            Part(p, "Grip", V(0f, -0.070f, -0.08f, s), V(0.038f, 0.100f, 0.050f, s), accent);
            Part(p, "Stock", V(0f, 0.005f, -0.20f, s), V(0.030f, 0.045f, 0.14f, s), body);
            Part(p, "Sight", V(0f, 0.042f, 0.12f, s), V(0.010f, 0.016f, 0.014f, s), accent);

            return V(0f, 0.008f, 0.325f, s);
        }

        /// <summary>
        /// Pompalı: <b>kalın namlu, altında pompa kolu, geniş dipçik</b>. Silüeti en
        /// kalın olan silah — yakın mesafenin cevabı olduğunu görünüşü söylemeli.
        /// </summary>
        private static Vector3 BuildShotgun(Transform p, float s, Material body, Material accent)
        {
            Part(p, "Receiver", V(0f, 0f, 0.02f, s), V(0.058f, 0.070f, 0.30f, s), body);

            // KALIN namlu: pompaliyi taniyan sey capi.
            Part(p, "Barrel", V(0f, 0.012f, 0.26f, s), V(0.042f, 0.042f, 0.26f, s), body);

            // Pompa kolu namlunun ALTINDA ve one dogru - silahin adi burada.
            Part(p, "Pump", V(0f, -0.030f, 0.24f, s), V(0.052f, 0.040f, 0.11f, s), accent);

            Part(p, "Grip", V(0f, -0.075f, -0.06f, s), V(0.042f, 0.105f, 0.055f, s), accent);
            Part(p, "Stock", V(0f, -0.020f, -0.22f, s), V(0.046f, 0.075f, 0.18f, s), body);

            return V(0f, 0.012f, 0.40f, s);
        }

        /// <summary>
        /// Tüfek: <b>en uzun namlu ve dürbün</b>. Uzaktan iş gördüğünü siluetinden
        /// anlaşılmalı.
        /// </summary>
        private static Vector3 BuildRifle(Transform p, float s, Material body, Material accent)
        {
            Part(p, "Receiver", V(0f, 0f, 0.02f, s), V(0.046f, 0.062f, 0.36f, s), body);
            Part(p, "Barrel", V(0f, 0.008f, 0.34f, s), V(0.020f, 0.020f, 0.34f, s), body);
            Part(p, "Magazine", V(0f, -0.080f, 0.02f, s), V(0.030f, 0.090f, 0.070f, s), accent);
            Part(p, "Grip", V(0f, -0.075f, -0.09f, s), V(0.038f, 0.100f, 0.050f, s), accent);
            Part(p, "Stock", V(0f, -0.010f, -0.24f, s), V(0.038f, 0.070f, 0.20f, s), body);

            // Durbun: tufegin imzasi.
            Part(p, "Scope", V(0f, 0.060f, 0.10f, s), V(0.030f, 0.030f, 0.16f, s), accent);
            Part(p, "ScopeMount", V(0f, 0.040f, 0.10f, s), V(0.014f, 0.026f, 0.05f, s), body);

            return V(0f, 0.008f, 0.52f, s);
        }

        // ---------------------------------------------------------------- yardimci

        private static Vector3 V(float x, float y, float z, float s) =>
            new Vector3(x * s, y * s, z * s);

        /// <summary>
        /// Tek bir parça. <b>Çarpıştırıcısı yok</b>: ne el modeli dünyaya dokunmalı, ne
        /// de duvardaki teşhir oyuncunun ışınını engellemeli — ikincisi olsaydı
        /// silahın önündeki zombiye ateş edilemezdi.
        /// </summary>
        private static void Part(Transform parent, string name, Vector3 localPosition,
                                 Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            Object.DestroyImmediate(go.GetComponent<Collider>());

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
