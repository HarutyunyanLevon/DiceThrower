// Jelly die: frosted translucent orange with a cream stripe through it, soft light-through-the-body shading and a wobble.
// The wobble is only visual (the collider stays rigid): every vertex moves by a function of its own position, so the faces never tear.
// It rings after an impact (_Flash from DieFxDriver) and a little while the die moves (_Energy); a tiny idle sway keeps it alive.
// Cost: 2 noise samples + 1 numeral SDF sample, ~70 ALU.
Shader "DiceThrower/DiceFX/Jelly"
{
    Properties
    {
        [Header(Numerals (signed distance field))]
        [NoScaleOffset] _NumeralMap ("Numeral SDF (R)", 2D) = "black" {}
        _NumeralColor ("Numeral colour", Color) = (1, 0.88, 0.55, 1)
        _NumeralGlow ("Numeral glow", Range(0, 4)) = 0.5
        _NumeralThick ("Numeral thickness (SDF units)", Range(-0.1, 0.2)) = 0.03
        _NumeralBevel ("Numeral engraved bevel width", Range(0.02, 0.5)) = 0.22
        _NumeralMetal ("Numeral metallic", Range(0, 1)) = 0.8
        _NumeralSmoothness ("Numeral smoothness", Range(0, 1)) = 0.6
        _NumeralHalo ("Keep effects this far from numerals (SDF units)", Range(0.1, 0.5)) = 0.3
        [Header(Surface)]
        [NoScaleOffset] _NoiseMap ("Noise pack (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per object unit)", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.4
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Jelly)]
        _DeepColor ("Deep colour", Color) = (1, 0.38, 0.05, 1)
        _LightColor ("Light colour (the stripe)", Color) = (1, 0.82, 0.55, 1)
        _StripeDir ("Stripe direction (xy)", Vector) = (1, 0.3, 0, 0)
        _StripeScale ("Stripe frequency", Float) = 1.3
        _StripeWarp ("Stripe wobble", Float) = 0.7
        _StripeSoft ("Stripe softness", Range(0.05, 0.5)) = 0.25
        _Subsurface ("Light through the body", Range(0, 2)) = 0.8
        _RimColor ("Edge glow colour", Color) = (1, 0.7, 0.4, 1)
        _RimGlow ("Edge glow", Range(0, 3)) = 0.5
        _FresnelPower ("Edge glow power", Range(0.5, 8)) = 2.5
        _Grain ("Frosted grain", Range(0, 1)) = 0.25
        _ShadeColor ("Colour on the side away from the light (the red end)", Color) = (0.9, 0.12, 0.03, 1)
        _ShadeAmount ("Red end amount", Range(0, 1)) = 0
        [Header(Photoreal)]
        [NoScaleOffset] _EnvMap ("Studio reflection cubemap", Cube) = "" {}
        _EnvStrength ("Reflection strength (0 = off)", Range(0, 3)) = 0
        _BevelWidth ("Rounded edge width (fraction of the face, 0 = sharp)", Range(0, 0.6)) = 0
        _BevelTilt ("Rounded edge roundness", Range(0, 1.5)) = 0.7
        _EngraveDepth ("Numeral engraving depth (0 = flat paint)", Range(0, 4)) = 0
        [Header(Wobble)]
        _WobbleAmount ("Wobble after an impact", Range(0, 0.3)) = 0.09
        _WobbleMoving ("Wobble while moving", Range(0, 0.2)) = 0.03
        _WobbleIdle ("Idle sway", Range(0, 0.05)) = 0.008
        _WobbleSpeed ("Wobble speed", Float) = 22
        _WobbleWaves ("Wobble waves across the die", Float) = 14
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _DeepColor;
            half4 _LightColor;
            float4 _StripeDir;
            float _StripeScale;
            float _StripeWarp;
            half  _StripeSoft;
            half  _Subsurface;
            half4 _RimColor;
            half  _RimGlow;
            half  _FresnelPower;
            half  _Grain;
            half4 _ShadeColor;
            half  _ShadeAmount;
            half  _EnvStrength;
            half  _BevelWidth;
            half  _BevelTilt;
            half  _EngraveDepth;
            half  _WobbleAmount;
            half  _WobbleMoving;
            half  _WobbleIdle;
            float _WobbleSpeed;
            float _WobbleWaves;
        CBUFFER_END
        #include "Assets/Shaders/DiceFX/DiceFXLib.hlsl"

        // Squash and stretch along a wave that runs through the die. The offset depends only on the vertex position.
        DiceVaryings JellyVert(DiceAttributes v)
        {
            float amp = _Flash * _WobbleAmount + _Energy * _WobbleMoving + _WobbleIdle;
            float3 p = v.positionOS.xyz;
            float phase = _Time.y * _WobbleSpeed + dot(p, float3(0.62, 0.55, 0.56)) * _WobbleWaves + _Seed * 6.0;
            float phase2 = _Time.y * _WobbleSpeed * 0.77 + dot(p, float3(-0.4, 0.7, 0.59)) * _WobbleWaves;
            float3 offset = p * (amp * (0.6 * sin(phase) + 0.4 * sin(phase2)));
            v.positionOS.xyz = p + offset;
            return DiceVert(v);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex JellyVert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            half4 Frag(DiceVaryings i) : SV_Target
            {
                float3 nWS = normalize(i.normalWS);
                float3 tWS = normalize(i.tangentWS);
                float3 bWS = cross(nWS, tWS);
                float3 vWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fres = pow(1.0h - saturate((half)dot(nWS, vWS)), _FresnelPower);

                float2 uv = i.faceUV * _NoiseScale;
                half4 nA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv);
                half4 nB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv * 3.1 + 0.4);

                float2 dir = normalize(_StripeDir.xy + float2(1e-4, 0.0));
                float s = dot(i.faceUV, dir) * _StripeScale + ((float)nA.g - 0.5) * _StripeWarp + i.facet * 4.0;
                half stripe = (half)(1.0 - abs(frac(s) * 2.0 - 1.0));
                half cream = smoothstep(0.55h - _StripeSoft, 0.55h + _StripeSoft, stripe);
                half3 c = lerp(_DeepColor.rgb, _LightColor.rgb, cream);
                c *= 1.0h + (nB.g - 0.5h) * _Grain;

                // light passing through the body: a wrapped diffuse term lifts the shadow side
                Light mainLight = GetMainLight();
                half wrap = saturate(dot(nWS, mainLight.direction) * 0.5h + 0.5h);
                // the end of the die turned away from the light runs to deep red, as in the photos
                c = lerp(c, _ShadeColor.rgb, smoothstep(0.6h, 0.2h, wrap) * _ShadeAmount * (0.7h + 0.3h * nA.g));
                half3 emission = c * (_Subsurface * (0.25h + 0.75h * wrap)) * (0.6h + 0.4h * nA.b);
                emission += _RimColor.rgb * (fres * _RimGlow * (1.0h + _Flash));

                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);

                DiceSurface s0;
                s0.albedo = c;
                s0.emission = emission * clear;
                s0.metallic = _Metallic;
                s0.smoothness = _Smoothness + cream * 0.1h;
                DiceApplyNumerals(s0, sdf);
                // photoreal: rounded edges, engraved numerals, studio reflections
                float3 nShade = DiceBevelNormal(i, nWS, tWS, bWS, _BevelWidth * i.faceCtr.z, _BevelTilt);
                if (_EngraveDepth > 0.001h)
                {
                    float hEdge = 0.5 - _NumeralThick;
                    float height = -smoothstep(hEdge - 0.08, hEdge + 0.02, (float)sdf) * (_EngraveDepth * 0.01);
                    nShade = DiceBump(nShade, i.positionWS, height, 1.0);
                }
                s0.emission += DiceEnvSpecular(s0, nShade, vWS, _EnvStrength);
                return DiceShade(i, s0, nShade);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DiceShadowVert
            #pragma fragment DiceShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
    Fallback Off
}
