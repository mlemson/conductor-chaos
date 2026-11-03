// Conductor Chaos — gestileerde lucht: zachte verloop van diepblauw boven naar licht bij de horizon, met een warme
// gloed rond de zon (issue #33). Gebruikt als skybox door Conductor Chaos > Art > Lighting > Apply Stylized Lighting.
Shader "ConductorChaos/GradientSky"
{
    Properties
    {
        _Top ("Top", Color) = (0.43, 0.51, 0.73, 1)
        _Horizon ("Horizon", Color) = (0.6, 0.77, 0.99, 1)
        _Bottom ("Bottom", Color) = (0.76, 0.79, 0.92, 1)
        _Exponent ("Verloop (hoger = blauw zakt lager)", Range(0.3, 4)) = 1.6
        _SunColor ("Zongloed", Color) = (1, 0.93, 0.8, 1)
        _SunGlow ("Zongloed sterkte", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon, _Bottom, _SunColor;
                half _Exponent, _SunGlow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 col = d.y >= 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(d.y), 1.0 / _Exponent))
                                     : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-d.y * 4.0));
                float sun = saturate(dot(d, _MainLightPosition.xyz));
                col += _SunColor.rgb * (pow(sun, 8.0) * 0.5 + pow(sun, 64.0)) * _SunGlow;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
