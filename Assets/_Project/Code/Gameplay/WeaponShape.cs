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
        /// Bir silahın gövdesini <paramref name="parent"/> altına kurar.
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
                case "weapon.rifle": return BuildRifle(parent, scale, body, accent);
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
