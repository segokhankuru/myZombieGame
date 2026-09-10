// Bunker/Weather/Rain Streak - yagmur damlasi cizgisi. 2026-09-10.
//
// NEDEN EL YAZISI HLSL (shader-graphics.md): onceki yagmur calisma aninda
// Shader.Find("Universal Render Pipeline/Particles/Unlit") ile malzeme kuruyordu.
// Iki sorunu vardi: (1) o shader'a hicbir sahne malzemesi referans vermediginde BUILD'E
// GIRMIYOR ve yagmur build'de sessizce yok oluyor; (2) '_Surface = 1' yazmak URP
// particle shader'inda karistirma durumunu degistirmiyor, yani damla saydam degil
// opak ciziliyordu. Bu dosya 20 satirlik bir cizgi; WeatherShaders onu build'e dahil eder.
//
// KALITE KADEMESI: tek kademe. Doku YOK (ornekleme yok), isik YOK.
// VARYANT: yalnizca multi_compile_fog (4). Uzaktaki damla sisle birlikte solmali.
// YEDEK: shader bulunamazsa OutdoorWeather hata basar ve yagmuru KURMAZ.

Shader "Bunker/Weather/Rain Streak"
{
    Properties
    {
        _Color ("Renk", Color) = (0.70, 0.76, 0.85, 0.45)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "RainStreak"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.color = input.color * _Color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Cizgi kenarlara ve uclara dogru soner: kure gibi okunan bir damla kar
                // tanesine benzer, yumusak uclu bir cizgi yagmura.
                half across = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                half along = sin(input.uv.y * 3.14159265);

                half4 color = input.color;
                color.a *= across * along;
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
