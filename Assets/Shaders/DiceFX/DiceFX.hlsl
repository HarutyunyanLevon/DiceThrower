// DiceFX shared include, part 1 (before the material CBUFFER): textures, property macro, types, pure helpers.
// Part 2 (DiceFXLib.hlsl) comes after the CBUFFER and uses the properties.
//
// Dice meshes are flat-shaded: every face owns its vertices and has ONE normal. So effects use a per-face planar
// coordinate ("faceUV": object-space position projected onto the face plane) instead of the model's atlas UVs
// (those are only good for the numeral mask). faceUV gives undistorted, tileable noise on every face of every die.

#ifndef DICEFX_INCLUDED
#define DICEFX_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_NumeralMap); SAMPLER(sampler_NumeralMap);   // R = numeral signed distance field (0.5 = glyph edge, > 0.5 inside), in the model's atlas UVs
TEXTURE2D(_NoiseMap);   SAMPLER(sampler_NoiseMap);     // R cell edge, G fbm A, B fbm B, A cell F1; tileable
TEXTURECUBE(_EnvMap);   SAMPLER(sampler_EnvMap);       // studio reflection cubemap (HDR, with mips); bound by the shaders that list it

// Every DiceFX shader puts this at the top of its UnityPerMaterial CBUFFER (keeps the SRP Batcher happy).
#define DICEFX_COMMON_PROPERTIES \
    half4 _NumeralColor; \
    half  _NumeralGlow; \
    half  _NumeralHalo; \
    half  _NumeralThick; \
    half  _NumeralBevel; \
    half  _NumeralMetal; \
    half  _NumeralSmoothness; \
    half  _Smoothness; \
    half  _Metallic; \
    float _NoiseScale; \
    float _Energy; \
    float _Flash; \
    float _Highlight; \
    float _Gloom; \
    float _Seed;

struct DiceAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    float4 uv2        : TEXCOORD2;   // baked by ModelDie: the face polygon in the face plane (xy,zw = two corners per channel; unused slots repeat corner 0)
    float4 uv3        : TEXCOORD3;
    float4 uv4        : TEXCOORD4;
    float4 uv1        : TEXCOORD1;   // baked by ModelDie: xy = face centre in the face plane (same axes as faceUV), z = face inradius, 0 when the mesh has none
};

struct DiceVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;   // model atlas UV (numerals only)
    float2 faceUV     : TEXCOORD1;   // planar per-face coordinate, object units
    float3 positionWS : TEXCOORD2;
    float3 normalWS   : TEXCOORD3;
    float3 tangentWS  : TEXCOORD4;   // face plane axis matching faceUV.x
    half   fogFactor  : TEXCOORD5;
    half   facet      : TEXCOORD6;   // 0..1, constant per face
    float3 faceCtr    : TEXCOORD7;
    float4 poly0      : TEXCOORD8;   // face polygon corners (see DiceAttributes.uv2..uv4); the shader reads them only for bevels
    float4 poly1      : TEXCOORD9;
    float4 poly2      : TEXCOORD10;   // xy = face centre in faceUV units, z = face inradius (0 = unknown)
};

struct DiceSurface
{
    half3 albedo;
    half3 emission;
    half  metallic;
    half  smoothness;
};

// The vertex stage adds _Seed * this to faceUV (so two dice of one set differ). Subtract it to get coordinates centred on the face.
static const float2 DiceSeedShift = float2(0.3719, 0.7117);

float DiceHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Unit direction of "down the picture" inside a face's plane, so drips run down the screen whichever way the die lies.
float2 DiceScreenDown(float3 tWS, float3 bWS)
{
    float3 camUp = UNITY_MATRIX_V[1].xyz;
    float2 d = -float2(dot(camUp, tWS), dot(camUp, bWS));
    float len = length(d);
    return len > 1e-3 ? d / len : float2(0.0, -1.0);
}

// Rainbow palette (cosine palette), t in turns.
half3 DicePalette(float t)
{
    return 0.5h + 0.5h * cos(6.28318h * (t + half3(0.0h, 0.33h, 0.67h)));
}

#endif
