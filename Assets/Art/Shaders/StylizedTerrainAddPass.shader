// Conductor Chaos — add-pass voor ConductorChaos/StylizedTerrain (lagen 5-8 en verder), issue #118.
// Telt de gewogen kleur van de volgende vier lagen op bij de eerste pass; mist naar zwart, zodat de mistkleur
// maar één keer meetelt.
Shader "Hidden/ConductorChaos/StylizedTerrainAddPass"
{
    Properties
    {
        // zelfde eigenschappen als de hoofdshader: Terrain kopieert ze naar het add-pass-materiaal
        [HideInInspector] _Ramp0 ("Gras ramp 0 (schaduw)", Color) = (0.337255, 0.427451, 0.227451, 1)
        [HideInInspector] _Ramp1 ("Gras ramp 1", Color) = (0.419608, 0.498039, 0.219608, 1)
        [HideInInspector] _Ramp2 ("Gras ramp 2 (midden)", Color) = (0.545098, 0.623529, 0.231373, 1)
        [HideInInspector] _Ramp3 ("Gras ramp 3", Color) = (0.631373, 0.690196, 0.266667, 1)
        [HideInInspector] _Ramp4 ("Gras ramp 4 (licht)", Color) = (0.678431, 0.737255, 0.290196, 1)
        [HideInInspector] _RampShift ("Ramp Shift", Float) = 0.02
        [HideInInspector] _Cells ("Color Cells", Float) = 0.8
        [HideInInspector] _CellSize ("Cell Size (m)", Float) = 0.9
        [HideInInspector] _Rim ("Rim Strength", Float) = 0.32
        [HideInInspector] _RimColor ("Rim Color", Color) = (0.698039, 0.772549, 0.952941, 1)
        [HideInInspector] _Spec ("Spec", Float) = 0.55
        [HideInInspector] _GrassDetect ("Gras herkennen (0 = alles textuur)", Range(0, 1)) = 1
        [HideInInspector] _ShadowTint ("Schaduwtint niet-gras", Color) = (0.78, 0.82, 1.0, 1)
        [HideInInspector] _ReceiveShadowsF ("Receive Shadows", Float) = 1
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "black" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "grey" {}
    }

    SubShader
    {
        Tags { "Queue" = "Geometry-99" "IgnoreProjector" = "True" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "TerrainCompatible" = "True" }

        Pass
        {
            Name "ForwardStylizedTerrainAdd"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual

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
                return half4(MixFogColor(col, half3(0, 0, 0), i.fogFactor), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
