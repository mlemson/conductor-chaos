Shader "ConductorChaos/ForestDistance"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                half fog : TEXCOORD0;
            };

            Varyings Vert(float3 position : POSITION, half4 color : COLOR)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(position);
                output.color = color.rgb;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(MixFog(input.color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
