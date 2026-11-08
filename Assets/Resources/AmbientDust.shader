Shader "ConductorChaos/AmbientDust"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                half fog : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                half light = saturate(dot(TransformObjectToWorldNormal(v.normalOS), normalize(half3(.4h, .8h, .5h))));
                o.color = v.color.rgb * lerp(half3(.62h, .65h, .73h), half3(1.0h, .96h, .87h), light);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(MixFog(i.color, i.fog), 1.0h);
            }
            ENDHLSL
        }
    }
}
