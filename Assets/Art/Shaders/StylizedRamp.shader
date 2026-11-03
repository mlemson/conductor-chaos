// Conductor Chaos — stylized ramp shader (URP 17 / Unity 6). Issue #33.
//
// Engine-versie van de art-render (ArtSource/Render/render.html): elke kleur komt uit een ramp van 5 tinten
// uit palette.json. Licht bepaalt alleen de positie in de ramp, zodat schaduwen koel en verzadigd blijven
// in plaats van grijs. Verder:
//   - naar boven gerichte vlakken lichter, gebakken occlusie (vertexkleur R) en randlicht (vertexkleur G);
//   - grote, effen kleurvlakken (3D-Voronoi in objectruimte, harde randen);
//   - vuil onderin (per cel), koele rim, emissieve lenzen, URP-fog;
//   - optioneel transparant (glas) via _SrcBlend/_DstBlend/_ZWrite.
// De materialen worden gegenereerd door ArtSource/Tools/unity_assets.py; terugvallen op URP Lit kan via
// het menu Conductor Chaos > Art > Use URP Lit (fallback).
Shader "ConductorChaos/StylizedRamp"
{
    Properties
    {
        _Ramp0 ("Ramp 0 (schaduw)", Color) = (0.37, 0.15, 0.12, 1)
        _Ramp1 ("Ramp 1", Color) = (0.55, 0.20, 0.20, 1)
        _Ramp2 ("Ramp 2 (midden)", Color) = (0.77, 0.30, 0.24, 1)
        _Ramp3 ("Ramp 3", Color) = (0.85, 0.32, 0.25, 1)
        _Ramp4 ("Ramp 4 (licht)", Color) = (0.99, 0.49, 0.41, 1)
        _BaseColor ("Base Color (preview/fallback)", Color) = (0.77, 0.30, 0.24, 1)
        _RampShift ("Ramp Shift", Float) = 0
        _Flat ("Flat", Range(0, 1)) = 0
        _Edge ("Edge Light", Float) = 1
        _Rim ("Rim Strength", Float) = 0.32
        _RimColor ("Rim Color", Color) = (0.70, 0.77, 0.95, 1)
        _Spec ("Spec", Float) = 0.55
        _Cells ("Color Cells", Float) = 1
        _CellSize ("Cell Size (m)", Float) = 0.45
        _Grime ("Grime", Float) = 0
        _GrimeY ("Grime Y (from, to)", Vector) = (-0.35, 0.85, 0, 0)
        _DirtColor ("Dirt Color", Color) = (0.31, 0.20, 0.19, 1)
        _Emissive ("Emissive", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
        _HasShade ("Use Vertex Shade", Float) = 1
        _ReceiveShadowsF ("Receive Shadows", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Ramp0, _Ramp1, _Ramp2, _Ramp3, _Ramp4;
            half4 _BaseColor;
            half4 _RimColor;
            half4 _DirtColor;
            float4 _GrimeY;
            half _RampShift, _Flat, _Edge, _Rim, _Spec, _Cells, _CellSize, _Grime, _Emissive, _Opacity, _HasShade;
            half _ReceiveShadowsF, _SrcBlend, _DstBlend, _ZWrite, _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardStylized"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half4  shade      : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.shade = v.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 SampleRamp(half t)
            {
                t = saturate(t) * 4.0;
                half i = min(floor(t), 3.0);
                half f = t - i;
                half3 a = _Ramp0.rgb, b = _Ramp1.rgb;
                if (i >= 1.0) { a = _Ramp1.rgb; b = _Ramp2.rgb; }
                if (i >= 2.0) { a = _Ramp2.rgb; b = _Ramp3.rgb; }
                if (i >= 3.0) { a = _Ramp3.rgb; b = _Ramp4.rgb; }
                return lerp(a, b, f);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // id (0..1) van de dichtstbijzijnde Voronoi-cel + het middelpunt van die cel
            float4 Cell3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float best = 9.0;
                float3 bc = 0;
                [unroll] for (int x = -1; x <= 1; x++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int z = -1; z <= 1; z++)
                {
                    float3 g = float3(x, y, z);
                    float3 o = Hash33(i + g);
                    float3 d = g + o - f;
                    float dd = dot(d, d);
                    if (dd < best) { best = dd; bc = i + g + o; }
                }
                return float4(Hash33(bc * 1.37 + 0.5).x, bc);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half shadow = lerp(1.0, light.shadowAttenuation, _ReceiveShadowsF);
                half lit = saturate(dot(N, light.direction)) * shadow;
                half band = smoothstep(0.0, 0.5, lit);              // zachte, geschilderde terminator
                half up = N.y * 0.5 + 0.5;
                half tShadow = lerp(0.05, 0.36, up);
                half tLit = lerp(0.56, 0.93, up);                   // naar boven gerichte vlakken lichter
                half4 sh = _HasShade > 0.5 ? i.shade : half4(1, 0, 0.5, 1);
                half ao = sh.r, edgeV = sh.g;
                band *= lerp(0.45, 1.0, smoothstep(0.15, 0.9, ao));
                half t = lerp(tShadow, tLit, band);
                t -= (1.0 - ao) * 0.17;
                t += edgeV * lerp(0.05, 0.13, band) * _Edge;

                // grote kleurvlakken + vuil onderin (objectruimte: beweegt mee met de trein)
                float4 cl = Cell3(i.positionOS / max(_CellSize, 0.01));
                t += (cl.x - 0.5) * 0.10 * _Cells;
                float cy = _Cells > 0.001 ? cl.z * _CellSize : i.positionOS.y;   // hoogte van het celmiddelpunt
                half grime = (1.0 - smoothstep(_GrimeY.x, _GrimeY.y, cy + (Hash33(cl.yzw).y - 0.5) * 0.45)) * _Grime;
                t -= grime * 0.11;

                half spec = pow(saturate(dot(N, normalize(light.direction + V))), 18.0) * band;
                t += spec * 0.30 * _Spec;
                t = lerp(t + _RampShift, 0.5 + _RampShift, _Flat);

                half3 col = SampleRamp(t);
                col = lerp(col, _DirtColor.rgb, grime * 0.22);
                col *= 1.0 + (cl.x - 0.5) * 0.09 * _Cells;
                half fr = pow(1.0 - saturate(dot(N, V)), 3.0);
                col = lerp(col, _RimColor.rgb, saturate(fr * _Rim * (1.15 - band * 0.75)));
                col = lerp(col, SampleRamp(0.25 + 0.75 * pow(saturate(dot(N, V)), 0.9) + _RampShift), _Emissive);
                col = MixFog(col, i.fogFactor);
                half alpha = _Opacity >= 0.999 ? 1.0 : saturate(_Opacity + fr * 0.45);
                return half4(col, alpha);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesS
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vertShadow(AttributesS v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
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

            half4 fragShadow() : SV_Target
            {
                // glas (doorzichtig) werpt geen schaduw
                clip(_Opacity - 0.999);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct AttributesD
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vertDepth(AttributesD v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half fragDepth(float4 positionCS : SV_POSITION) : SV_Target
            {
                clip(_Opacity - 0.999);
                return positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
