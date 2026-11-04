// Conductor Chaos — gedeelde code voor ConductorChaos/StylizedTerrain en de add-pass (issue #118).
// Gras op het terrein wordt met exact dezelfde berekening en ramp getekend als het gras bovenop de klifmodellen
// (StylizedRamp.shader + M_Env_Grass): zelfde lichtband, zelfde kleurvlakken (cellen), zelfde rim en spec.
// Grastexels worden herkend aan hun kleur (geelgroen); bloemetjes, aarde, stenen en zand houden hun textuur
// en krijgen alleen dezelfde gestileerde belichting (geen grijze schaduw, koel in de schaduw).
#ifndef CC_STYLIZED_TERRAIN_COMMON
#define CC_STYLIZED_TERRAIN_COMMON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_Control);    SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);     SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);
TEXTURE2D(_Splat2);
TEXTURE2D(_Splat3);

CBUFFER_START(UnityPerMaterial)
    float4 _Control_ST;
    float4 _Control_TexelSize;
    float4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
    half4 _Ramp0, _Ramp1, _Ramp2, _Ramp3, _Ramp4;
    half4 _RimColor;
    half4 _ShadowTint;
    half _RampShift, _Cells, _CellSize, _Rim, _Spec, _GrassDetect, _ReceiveShadowsF;
CBUFFER_END

struct TerrainAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
};

struct TerrainVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float2 uv         : TEXCOORD2;
    half   fogFactor  : TEXCOORD3;
};

TerrainVaryings TerrainVert(TerrainAttributes v)
{
    TerrainVaryings o = (TerrainVaryings)0;
    VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS = p.positionCS;
    o.positionWS = p.positionWS;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.uv = v.uv;
    o.fogFactor = ComputeFogFactor(p.positionCS.z);
    return o;
}

// ------------------------------------------------- identiek aan StylizedRamp.shader
half3 CC_SampleRamp(half t)
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

float3 CC_Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float4 CC_Cell3(float3 p)
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
        float3 o = CC_Hash33(i + g);
        float3 d = g + o - f;
        float dd = dot(d, d);
        if (dd < best) { best = dd; bc = i + g + o; }
    }
    return float4(CC_Hash33(bc * 1.37 + 0.5).x, bc);
}

// Gras precies zoals een StylizedRamp-materiaal zonder gebakken vertexkleur (ao = 1, randlicht = 0).
half3 CC_GrassColor(float3 positionWS, float3 N, float3 V, Light light, half shadow, out half band)
{
    half lit = saturate(dot(N, light.direction)) * shadow;
    band = smoothstep(0.0, 0.5, lit);
    half up = N.y * 0.5 + 0.5;
    half tShadow = lerp(0.05, 0.36, up);
    half tLit = lerp(0.56, 0.93, up);
    half t = lerp(tShadow, tLit, band);
    float4 cl = CC_Cell3(positionWS / max(_CellSize, 0.01));
    half cellT = (cl.x - 0.5) * 0.10 * _Cells;
    half spec = pow(saturate(dot(N, normalize(light.direction + V))), 18.0) * band;
    t += spec * 0.30 * _Spec;
    t = t + _RampShift;
    half cellA = 0.05 * _Cells;
    t = max(t, cellA) + cellT;
    half3 col = CC_SampleRamp(t);
    col *= 1.0 + (cl.x - 0.5) * 0.09 * _Cells;
    half fr = pow(1.0 - saturate(dot(N, V)), 3.0);
    col = lerp(col, _RimColor.rgb, saturate(fr * _Rim * (1.15 - band * 0.75)));
    return col;
}

// 1 = grastexel (geelgroen), 0 = iets anders (bloem, aarde, steen, zand)
half CC_GrassMask(half3 albedo)
{
    half rg = albedo.g / max(albedo.r, 1e-3);
    half bg = albedo.g / max(albedo.b, 1e-3);
    return saturate((rg - 1.05) * 8.0) * saturate((bg - 1.8) * 3.0) * _GrassDetect;
}

half3 CC_LayerColor(half3 albedo, half3 grass, half band, half up)
{
    // niet-gras: textuur met dezelfde gestileerde belichting (lichter naar boven, koele schaduw, nooit grijs)
    half k = lerp(0.62, 1.0, band) * lerp(0.92, 1.04, up);
    half3 other = albedo * k * lerp(_ShadowTint.rgb, 1.0, band);
    return lerp(other, grass, CC_GrassMask(albedo));
}

half3 CC_TerrainShade(TerrainVaryings i)
{
    float3 N = normalize(i.normalWS);
    float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    Light light = GetMainLight(shadowCoord);
    half shadow = lerp(1.0, light.shadowAttenuation, _ReceiveShadowsF);
    half band;
    half3 grass = CC_GrassColor(i.positionWS, N, V, light, shadow, band);
    half up = N.y * 0.5 + 0.5;

    float2 cuv = (i.uv * (_Control_TexelSize.zw - 1.0) + 0.5) * _Control_TexelSize.xy;
    half4 w = SAMPLE_TEXTURE2D(_Control, sampler_Control, cuv);
    half3 c0 = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, i.uv * _Splat0_ST.xy + _Splat0_ST.zw).rgb;
    half3 c1 = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, i.uv * _Splat1_ST.xy + _Splat1_ST.zw).rgb;
    half3 c2 = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, i.uv * _Splat2_ST.xy + _Splat2_ST.zw).rgb;
    half3 c3 = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, i.uv * _Splat3_ST.xy + _Splat3_ST.zw).rgb;
    return w.r * CC_LayerColor(c0, grass, band, up) + w.g * CC_LayerColor(c1, grass, band, up)
         + w.b * CC_LayerColor(c2, grass, band, up) + w.a * CC_LayerColor(c3, grass, band, up);
}

#endif
