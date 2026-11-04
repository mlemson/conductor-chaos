// Conductor Chaos — gestileerd water v2 (issue #110). Look: Docs/References/water-reference.png.
//
// Helder turquoise in het ondiepe, diep azuurblauw in het midden, een wit schuimrandje langs de oever met
// een tweede dunne lijn erachter, lichte golfstreepjes en kleine zonneschitteringen. Alles procedureel
// (geen textures). Beweegt standaard rustig (issue #125): golfjes drijven en vervormen langzaam, schitteringen
// twinkelen en het schuim langs de oever ademt. _FlowSpeed = 0 zet alles stil; daarvoor liet _FlowSpeed > 0 het
// patroon langzaam drijven.
//
// Diepte = verticale afstand van het oppervlak tot de bodem eronder, gereconstrueerd uit de depth texture.
// Breking: de bodem (opaque texture) wordt licht verschoven en blauwgroen gekleurd. Beide vereisen
// "Depth Texture" en "Opaque Texture" op de URP-asset (PC_RPAsset staat al aan). Zet _SceneTextures op 0
// als ze uit staan (bv. Mobile_RPAsset): dan alleen een vaste doorzichtigheid, zonder schuimrand.
Shader "ConductorChaos/StylizedWater"
{
    Properties
    {
        [Header(Kleur)]
        _ShallowColor ("Ondiep (rgb) + dekking (a)", Color) = (0.24, 0.78, 0.74, 0.35)
        _DeepColor ("Diep (rgb) + dekking (a)", Color) = (0.01, 0.52, 0.76, 0.97)
        _HorizonColor ("Horizon / scheerlicht", Color) = (0.36, 0.79, 0.92, 1)
        _DepthDistance ("Diepte tot volle kleur (m)", Range(0.2, 8)) = 2.2
        _BedTint ("Bodemkleur onder water", Color) = (0.55, 0.92, 0.88, 1)

        [Header(Schuim langs de oever)]
        _FoamColor ("Schuim", Color) = (0.97, 1.0, 1.0, 1)
        _FoamWidth ("Schuimrand breedte (m diepte)", Range(0, 1)) = 0.16
        _FoamLine ("Tweede lijn (m diepte)", Range(0, 2)) = 0.42
        _FoamLineWidth ("Tweede lijn dikte", Range(0, 0.2)) = 0.035
        _FoamWobble ("Schuim golving", Range(0, 1)) = 0.45

        [Header(Oppervlak)]
        _StreakColor ("Golfstreepjes", Color) = (0.62, 0.90, 0.98, 1)
        _StreakAmount ("Golfstreepjes sterkte", Range(0, 1)) = 0.55
        _StreakScale ("Golfstreepjes schaal (m)", Range(0.5, 12)) = 3.2
        _GlintAmount ("Zonneschittering", Range(0, 2)) = 1.0
        _Refraction ("Breking", Range(0, 0.1)) = 0.025
        _FlowSpeed ("Drijfsnelheid (0 = stil)", Range(0, 0.5)) = 0.12
        _FlowDir ("Drijfrichting (xz)", Vector) = (1, 0.35, 0, 0)

        [Header(Techniek)]
        [Toggle] _SceneTextures ("Depth + Opaque Texture gebruiken", Float) = 1
        _ReceiveShadowsF ("Schaduw ontvangen", Range(0, 1)) = 0.35
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _HorizonColor, _BedTint, _FoamColor, _StreakColor;
                float4 _FlowDir;
                half _DepthDistance, _FoamWidth, _FoamLine, _FoamLineWidth, _FoamWobble;
                half _StreakAmount, _StreakScale, _GlintAmount, _Refraction, _FlowSpeed;
                half _SceneTextures, _ReceiveShadowsF, _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // ---------------------------------------------------------------- ruis
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return ValueNoise(p) * 0.6 + ValueNoise(p * 2.13 + 7.1) * 0.3 + ValueNoise(p * 4.37 + 3.7) * 0.1;
            }

            half4 frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 posWS = i.positionWS;
                float3 toCam = _WorldSpaceCameraPos - posWS;
                float3 V = normalize(toCam);
                float tm = _Time.y * (_FlowSpeed > 0.0 ? 1.0 : 0.0);         // 0 = alles stil
                float2 flow = normalize(_FlowDir.xy + 1e-4) * _FlowSpeed * _Time.y;
                float2 xz = posWS.xz + flow;

                // ------------------------------------------------ van onder bekeken: eenvoudig, donker oppervlak
                if (!frontFace || toCam.y < 0.0)
                {
                    half3 under = lerp(_DeepColor.rgb, _HorizonColor.rgb, 0.35);
                    return half4(MixFog(under, i.fogFactor), 0.85);
                }

                // ------------------------------------------------ diepte onder het oppervlak (verticaal, in m)
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float depth = 10.0;                                     // zonder depth texture: 'diep'
                if (_SceneTextures > 0.5)
                {
                    float rawD = SampleSceneDepth(suv);
                    float sceneEye = LinearEyeDepth(rawD, _ZBufferParams);
                    float surfEye = i.screenPos.w;
                    float3 bedWS = _WorldSpaceCameraPos + (posWS - _WorldSpaceCameraPos) * (sceneEye / max(surfEye, 1e-4));
                    depth = max(0.0, posWS.y - bedWS.y);
                }
                float depth01 = 1.0 - exp(-depth / max(_DepthDistance, 0.05) * 1.6);

                // ------------------------------------------------ oppervlakte-normaal: zachte, brede deining
                float2 q = xz / max(_StreakScale, 0.1);
                float hx = Fbm(q * 0.9 + float2(0.05, 0)) - Fbm(q * 0.9 - float2(0.05, 0));
                float hz = Fbm(q * 0.9 + float2(0, 0.05)) - Fbm(q * 0.9 - float2(0, 0.05));
                float3 N = normalize(float3(-hx * 1.6, 1.0, -hz * 1.6));

                // ------------------------------------------------ waterkleur
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depth01);
                half fres = pow(1.0 - saturate(dot(V, float3(0, 1, 0))), 4.0);
                water = lerp(water, _HorizonColor.rgb, fres * 0.45);

                // licht en schaduw (gestileerd: alleen wat helderheid, geen grijze schaduw)
                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light light = GetMainLight(shadowCoord);
                half shadow = lerp(1.0, light.shadowAttenuation, _ReceiveShadowsF);
                water *= lerp(0.82, 1.0, shadow);

                // ------------------------------------------------ golfstreepjes: korte, gebogen lichte strepen
                float2 sq = float2(q.x * 0.55 + q.y * 0.20, q.y * 1.9 - q.x * 0.25) * 2.2;   // uitgerekt: lange smalle vormen
                float wave = Fbm(sq + Fbm(sq * 0.35 + tm * 0.06) * 1.4);           // golfjes vervormen langzaam
                float line_ = 1.0 - smoothstep(0.0, 0.05, abs(wave - 0.5));                // contourlijnen = golfjes
                float dashes = smoothstep(0.52, 0.68, ValueNoise(sq * 0.8 + 11.3));          // in losse stukjes
                float streak = line_ * dashes * _StreakAmount * lerp(0.35, 1.0, depth01) * (1.0 - fres * 0.6);
                water = lerp(water, _StreakColor.rgb, streak);
                alpha = max(alpha, streak * 0.9);

                // ------------------------------------------------ zonneschittering: kleine sterretjes in de spiegeling
                float3 H = normalize(light.direction + V);
                half sunRefl = pow(saturate(dot(N, H)), 60.0);
                float sparkleN = ValueNoise(xz * 7.0 + 41.0 + tm * 0.35) * ValueNoise(xz * 12.7 - 13.0 - tm * 0.5);   // twinkelen
                half glint = smoothstep(0.47, 0.55, sparkleN) * smoothstep(0.25, 0.8, sunRefl) * _GlintAmount * shadow;
                glint += pow(saturate(dot(N, H)), 400.0) * 0.6 * _GlintAmount * shadow;
                water += light.color * glint;
                alpha = max(alpha, saturate(glint));

                // ------------------------------------------------ breking: de bodem schemert door in het ondiepe
                half3 col = water;
                half outA = alpha;
                if (_SceneTextures > 0.5)
                {
                    float2 off = (N.xz) * _Refraction * saturate(depth * 2.0);
                    float2 ruv = suv + off;
                    // niet verschuiven naar iets dat vóór het water ligt (anders 'lekt' de oever erin)
                    float rEye = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                    ruv = rEye < i.screenPos.w ? suv : ruv;
                    half3 bed = SampleSceneColor(ruv) * _BedTint.rgb;
                    col = lerp(bed, water, alpha);
                    outA = 1.0;
                }

                // ------------------------------------------------ schuim langs de oever (alleen met depth texture)
                if (_SceneTextures > 0.5)
                {
                    float n = Fbm(xz * 1.7) - 0.5;
                    float d = depth + n * _FoamWobble * 0.35 + sin(tm * 1.3 + n * 12.0) * _FoamWidth * 0.25;   // schuim ademt
                    half foam = 1.0 - smoothstep(_FoamWidth * 0.75, _FoamWidth, d);
                    half line2 = 1.0 - smoothstep(_FoamLineWidth * 0.5, _FoamLineWidth, abs(d - _FoamLine - sin(tm * 0.7) * _FoamWidth * 0.4));
                    line2 *= smoothstep(0.35, 0.6, ValueNoise(xz * 0.9 + 5.0));                // tweede lijn onderbroken
                    half f = saturate(max(foam, line2 * 0.85));
                    col = lerp(col, _FoamColor.rgb * lerp(0.85, 1.0, shadow), f);
                    // helemaal tegen de oever iets zachter in laten lopen, zodat er geen harde naad is
                    outA = lerp(outA, outA * saturate(depth / 0.03), 1.0 - f);
                    outA = max(outA, f * saturate(depth / 0.02));
                }

                col = MixFog(col, i.fogFactor);
                return half4(col, outA);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
