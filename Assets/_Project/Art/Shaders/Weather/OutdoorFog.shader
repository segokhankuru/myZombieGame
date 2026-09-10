// Bunker/Weather/Outdoor Fog - bina DISINDA biriken sis. 2026-09-10.
//
// NEDEN EL YAZISI HLSL, SHADER GRAPH DEGIL (shader-graphics.md): bu shader bir yuzey
// cizmiyor. Ekrani kaplayan bir dortgeni dogrudan kirpma uzayina yaziyor ve her
// pikselde kamera isininin BINA KUTUSUNUN DISINDA kalan boyunu olcuyor (isin-kutu
// kesisimi). Shader Graph'ta ikisi de ancak Custom Function dugumuyle olur - yani yine
// HLSL, ustune bir grafik.
//
// NE YAPAR: sis miktari kameranin nerede oldugundan degil, ISININ nereden gectiginden
// gelir. Iceriden pencereden bakinca disaridaki 20 m sisin icinde, odanin karsi duvari
// temiz. Disaridan bakinca her sey ayni kuralla - ayri bir "ic/dis" gecisi yok.
//
// KALITE KADEMESI: tek kademe (PC). Maliyet: ekran basina bir saydam cizim ve bir
// derinlik ornegi. Keyword YOK -> tek varyant; derleme ve build maliyeti sabit.
// YEDEK: shader bulunamazsa OutdoorWeather hata basar ve dis sisi CIZMEZ; Unity'nin
// ince global sisi calismaya devam eder. Oyun sissiz ama oynanabilir kalir.
// GEREKSINIM: URP varliginda 'Depth Texture' acik (PC_RPAsset'te acik).

Shader "Bunker/Weather/Outdoor Fog"
{
    Properties
    {
        _FogColor ("Sis rengi", Color) = (0.15, 0.16, 0.19, 1)
        _Density ("Yogunluk (metre basina, us kare)", Float) = 0.055
        _MaxDistance ("Gokyuzu icin sayilan mesafe (m)", Float) = 60
        _BoxMin ("Bina kutusu alt kose", Vector) = (0, -10000, 0, 0)
        _BoxMax ("Bina kutusu ust kose", Vector) = (0, -10000, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-1"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OutdoorFog"

            // Saydam nesnelerden (yagmur, bulut) ONCE: onlar sisin ustune cizilir.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density;
                float _MaxDistance;
                float4 _BoxMin;
                float4 _BoxMax;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // Mesh koseleri -1..1: dogrudan kirpma uzayi. z = 0.5 hem ters-Z (0..1)
                // hem OpenGL (-1..1) araliginin icinde; ZTest Always oldugu icin degeri
                // baska bir sey etkilemez.
                output.positionHCS = float4(input.positionOS.xy, 0.5, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Dunya konumu derinlikten (URP belgesindeki kalip).
                float2 uv = input.positionHCS.xy / _ScaledScreenParams.xy;

            #if UNITY_REVERSED_Z
                real depth = SampleSceneDepth(uv);
            #else
                real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
            #endif

                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 origin = _WorldSpaceCameraPos;
                float3 ray = worldPos - origin;

                float sceneDistance = length(ray);
                float3 dir = ray / max(sceneDistance, 1e-4);

                // Gokyuzu uzak kirpma planinda: sonsuz degil _MaxDistance kadar sayilir.
                float distance = min(sceneDistance, _MaxDistance);

                // Isin-kutu kesisimi (slab yontemi). Sifir bilesen sonsuza bolunmesin.
                float3 safeDir = (step(0.0, dir) * 2.0 - 1.0) * max(abs(dir), 1e-5);
                float3 t0 = (_BoxMin.xyz - origin) / safeDir;
                float3 t1 = (_BoxMax.xyz - origin) / safeDir;
                float3 tNear = min(t0, t1);
                float3 tFar = max(t0, t1);

                float enter = max(max(tNear.x, tNear.y), tNear.z);
                float exit = min(min(tFar.x, tFar.y), tFar.z);

                // Isinin [0, distance] araliginda kutunun ICINDE kalan kismi. Kesismiyorsa
                // ya da kutu arkadaysa sonuc negatife duser ve sifira kirpilir.
                float inside = max(0.0, min(exit, distance) - max(enter, 0.0));
                float outside = max(0.0, distance - inside);

                // Us kare sis - Unity'nin FogMode.ExponentialSquared'i ile ayni egri.
                float f = _Density * outside;
                float fog = 1.0 - exp(-f * f);

                return half4(_FogColor.rgb, saturate(fog) * _FogColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
