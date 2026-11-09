// Conductor Chaos — gestileerd terrein (issue #118). Gras exact zoals het gras op de klifmodellen
// (zelfde berekening en ramp als StylizedRamp + M_Env_Grass); overige lagen houden hun textuur.
// Code: StylizedTerrainCommon.hlsl. Instellen: Conductor Chaos > Art > Terrain > Use Stylized Terrain Shader.
// Lagen 5-8 tekent Unity met de add-pass (Hidden/ConductorChaos/StylizedTerrainAddPass).
Shader "ConductorChaos/StylizedTerrain"
{
    Properties
    {
        [Header(Gras (zelfde waarden als M_Env_Grass))]
        _Ramp0 ("Gras ramp 0 (schaduw)", Color) = (0.337255, 0.427451, 0.227451, 1)
        _Ramp1 ("Gras ramp 1", Color) = (0.419608, 0.498039, 0.219608, 1)
        _Ramp2 ("Gras ramp 2 (midden)", Color) = (0.545098, 0.623529, 0.231373, 1)
        _Ramp3 ("Gras ramp 3", Color) = (0.631373, 0.690196, 0.266667, 1)
        _Ramp4 ("Gras ramp 4 (licht)", Color) = (0.678431, 0.737255, 0.290196, 1)
        _RampShift ("Ramp Shift", Float) = 0.02
        _Cells ("Color Cells", Float) = 0.8
        _CellSize ("Cell Size (m)", Float) = 0.9
        _Rim ("Rim Strength", Float) = 0.32
        _RimColor ("Rim Color", Color) = (0.698039, 0.772549, 0.952941, 1)
        _Spec ("Spec", Float) = 0.55
        [Header(Overig)]
        _GrassDetect ("Gras herkennen (0 = alles textuur)", Range(0, 1)) = 1
        _ShadowTint ("Schaduwtint niet-gras", Color) = (0.78, 0.82, 1.0, 1)
        _ReceiveShadowsF ("Receive Shadows", Float) = 1
        [Header(Rust op afstand (aarde en steen))]
        _DetailNear ("Textuur dichtbij (0 = egaal zoals gras)", Range(0, 1)) = 0.35
        _DetailFadeStart ("Textuur vervaagt vanaf (m)", Float) = 10
        _DetailFadeEnd ("Textuur egaal vanaf (m)", Float) = 45

        // door Terrain gezet
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "grey" {}
        [HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _BaseColor ("Main Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "TerrainCompatible" = "True" }

        Pass
        {
            Name "ForwardStylizedTerrain"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TerrainVert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "StylizedTerrainCommon.hlsl"

            half4 frag(TerrainVaryings i) : SV_Target
            {
                half3 col = CC_TerrainShade(i);
                return half4(MixFog(col, i.fogFactor), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesS { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 vertShadow(AttributesS v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 vertDepth(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half fragDepth(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDN
            #pragma fragment fragDN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AttributesDN { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct VaryingsDN { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            VaryingsDN vertDN(AttributesDN v)
            {
                VaryingsDN o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 fragDN(VaryingsDN i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }

    Dependency "AddPassShader" = "Hidden/ConductorChaos/StylizedTerrainAddPass"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
