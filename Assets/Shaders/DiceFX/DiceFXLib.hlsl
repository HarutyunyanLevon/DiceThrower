// DiceFX shared include, part 2 (after the material CBUFFER): vertex stage, numeral helpers, lighting, shadow caster.

#ifndef DICEFX_LIB_INCLUDED
#define DICEFX_LIB_INCLUDED

// A direction that is parallel to no face normal of the dice (d4..d20); used to build each face's plane axes.
static const float3 DiceFaceRef = float3(0.3714, 0.5571, 0.7428);

DiceVaryings DiceVert(DiceAttributes v)
{
    DiceVaryings o;
    VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
    float3 nOS = normalize(v.normalOS);
    float3 tOS = normalize(cross(nOS, DiceFaceRef) + float3(1e-6, 0.0, 0.0));
    float3 bOS = cross(nOS, tOS);

    o.positionCS = p.positionCS;
    o.positionWS = p.positionWS;
    o.normalWS   = TransformObjectToWorldNormal(nOS);
    o.tangentWS  = TransformObjectToWorldDir(tOS);
    o.uv         = v.uv;
    o.faceUV     = float2(dot(v.positionOS.xyz, tOS), dot(v.positionOS.xyz, bOS)) + _Seed * DiceSeedShift;
    o.fogFactor  = ComputeFogFactor(p.positionCS.z);
    o.facet      = frac(sin(dot(nOS, float3(12.9898, 78.233, 37.719))) * 43758.5453);
    o.faceCtr    = v.uv1.xyz;
    o.poly0      = v.uv2;
    o.poly1      = v.uv3;
    o.poly2      = v.uv4;
    return o;
}

// ---------------------------------------------------------------- photoreal helpers (opt-in per shader)

// Rounds the edges of a flat face: near the face's border the normal tilts outward, so the edge catches light like a polished bevel.
// width = bevel width in object units, tilt = how far the normal leans at the very edge (about 0.5..1). 0 width = off.
float3 DiceBevelNormal(DiceVaryings i, float3 nWS, float3 tWS, float3 bWS, float width, float tilt)
{
    if (width <= 0.0 || i.faceCtr.z <= 1e-4) return nWS;
    float2 p = i.faceUV - _Seed * DiceSeedShift;
    float2 poly[5];
    poly[0] = i.poly0.xy; poly[1] = i.poly0.zw; poly[2] = i.poly1.xy; poly[3] = i.poly1.zw; poly[4] = i.poly2.xy;

    float best = 1e9;
    float2 outward = float2(0.0, 0.0);
    [unroll]
    for (int k = 0; k < 5; k++)
    {
        float2 a = poly[k];
        float2 b = poly[(k + 1) % 5];
        float2 e = b - a;
        float len = length(e);
        if (len < 1e-5) continue;
        float2 inward = float2(-e.y, e.x) / len;
        if (dot(inward, i.faceCtr.xy - a) < 0.0) inward = -inward;
        float d = dot(inward, p - a);
        if (d < best) { best = d; outward = -inward; }
    }
    float s = 1.0 - smoothstep(0.0, width, best);
    float3 outWS = tWS * outward.x + bWS * outward.y;
    return normalize(nWS + outWS * (tilt * s * s));
}

// Bump from a height value using screen-space derivatives (no tangent space needed). h is in world units; scale 1 = true slope.
float3 DiceBump(float3 n, float3 positionWS, float h, float scale)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float dhx = ddx(h);
    float dhy = ddy(h);
    float3 r1 = cross(dpdy, n);
    float3 r2 = cross(n, dpdx);
    float det = dot(dpdx, r1);
    float3 grad = sign(det) * (dhx * r1 + dhy * r2);
    return normalize(abs(det) * n - scale * grad);
}

// Studio reflection added on top of the PBR result: Schlick fresnel from the surface's own albedo / metallic / smoothness, rough surfaces read the blurrier mips.
half3 DiceEnvSpecular(DiceSurface s, float3 nWS, float3 vWS, half strength)
{
    if (strength <= 0.0h) return half3(0.0h, 0.0h, 0.0h);
    float3 r = reflect(-vWS, nWS);
    half rough = 1.0h - s.smoothness;
    half3 env = SAMPLE_TEXTURECUBE_LOD(_EnvMap, sampler_EnvMap, r, rough * 5.0h).rgb;
    half ndv = saturate((half)dot(nWS, vWS));
    half3 f0 = lerp(half3(0.04h, 0.04h, 0.04h), s.albedo, s.metallic);
    half3 f = f0 + (1.0h - f0) * pow(1.0h - ndv, 5.0h);
    return env * f * strength;
}

// Coordinates centred on the face and measured in face inradii (1 = the edge of the largest circle inside the face).
// valid = 0 when the mesh carries no face data.
float2 DiceFaceLocal(DiceVaryings i, out float valid)
{
    valid = i.faceCtr.z > 1e-4 ? 1.0 : 0.0;
    return (i.faceUV - _Seed * DiceSeedShift - i.faceCtr.xy) / max(i.faceCtr.z, 1e-4);
}

// ---------------------------------------------------------------- numerals

// The numeral texture is a signed distance field: 0.5 on a glyph's edge, above 0.5 inside, below outside.
half DiceNumeralSDF(float2 uv)
{
    return SAMPLE_TEXTURE2D(_NumeralMap, sampler_NumeralMap, uv).r;
}

// 1 away from the numbers, falling to 0 on and just around them. Multiply an effect by this to keep the numbers readable.
half DiceNumeralClear(half sdf)
{
    return 1.0h - smoothstep(0.5h - _NumeralHalo, 0.5h - _NumeralThick, sdf);
}

// Paints the numbers over the surface as engraved metal: the stroke centre is bright, its rim darker (the engraved wall),
// and the stroke is metallic and smooth so it catches the light. Glows more on a nat 20 and a little on impact.
// The edge is anti-aliased with the screen-space derivative of the field, so the glyphs stay crisp at any size.
void DiceApplyNumerals(inout DiceSurface s, half sdf)
{
    float edge = 0.5 - _NumeralThick;
    float w = max(fwidth((float)sdf) * 0.75, 0.002);
    half mask = (half)smoothstep(edge - w, edge + w, (float)sdf);
    half depth = saturate((sdf - (half)edge) / max(_NumeralBevel, 0.001h));
    half3 metal = _NumeralColor.rgb * lerp(0.75h, 1.3h, depth);
    half boost = 1.0h + _Highlight * 2.0h + _Flash * 0.5h;
    s.albedo     = lerp(s.albedo, metal, mask);
    s.emission   = lerp(s.emission, metal * (_NumeralGlow * boost), mask);
    s.metallic   = lerp(s.metallic, _NumeralMetal, mask);
    s.smoothness = lerp(s.smoothness, _NumeralSmoothness, mask);
}

// ---------------------------------------------------------------- lighting

half4 DiceShade(DiceVaryings i, DiceSurface s, float3 normalWS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS;
    inputData.positionCS = i.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
    inputData.vertexLighting = half3(0, 0, 0);
    inputData.bakedGI = SampleSH(normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    SurfaceData sd = (SurfaceData)0;
    sd.albedo = s.albedo;
    sd.metallic = s.metallic;
    sd.specular = half3(0, 0, 0);
    sd.smoothness = s.smoothness;
    sd.occlusion = 1.0h;
    sd.emission = s.emission;
    sd.alpha = 1.0h;
    sd.normalTS = half3(0, 0, 1);

    half4 color = UniversalFragmentPBR(inputData, sd);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = 1.0h;
    return color;
}

// ---------------------------------------------------------------- shadow caster pass

float3 _LightDirection;
float3 _LightPosition;

struct DiceShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
};

struct DiceShadowVaryings
{
    float4 positionCS : SV_POSITION;
};

DiceShadowVaryings DiceShadowVert(DiceShadowAttributes v)
{
    DiceShadowVaryings o;
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    o.positionCS = positionCS;
    return o;
}

half4 DiceShadowFrag(DiceShadowVaryings i) : SV_Target
{
    return 0;
}

#endif
