// Void dice: near-black glass with a purple glow at the edges and thin rifts that glow inside the dark (parallax: they slide as the die turns).
// Nat 20 (_Highlight): the rifts tear open - wide, white-hot cores - and the rim flares.
// Nat 1 (_Gloom): the die fades out - it dissolves away through a glowing edge until only a ghost (and the numbers) is left.
// Low FX (global keyword DICEFX_LOW, see DiceFxConfig): one rift layer, and a plain dimming instead of the dissolve (no discard).
// Cost: 2 noise samples (+1 while fading) + 1 numeral SDF sample, ~80 ALU (low FX: 1 + 1).
Shader "DiceThrower/DiceFX/Void"
{
    Properties
    {
        [Header(Numerals (signed distance field))]
        [NoScaleOffset] _NumeralMap ("Numeral SDF (R)", 2D) = "black" {}
        _NumeralColor ("Numeral colour", Color) = (0.85, 0.6, 1, 1)
        _NumeralGlow ("Numeral glow", Range(0, 4)) = 1.2
        _NumeralThick ("Numeral thickness (SDF units)", Range(-0.1, 0.2)) = 0.07
        _NumeralBevel ("Numeral engraved bevel width", Range(0.02, 0.5)) = 0.22
        _NumeralMetal ("Numeral metallic", Range(0, 1)) = 0.9
        _NumeralSmoothness ("Numeral smoothness", Range(0, 1)) = 0.6
        _NumeralHalo ("Keep effects this far from numerals (SDF units)", Range(0.1, 0.5)) = 0.3
        [Header(Surface)]
        [NoScaleOffset] _NoiseMap ("Noise pack (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per object unit)", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _Metallic ("Metallic", Range(0, 1)) = 0.3
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Void)]
        _VoidColor ("Body colour", Color) = (0.02, 0.012, 0.04, 1)
        _MistColor ("Mist colour", Color) = (0.25, 0.08, 0.45, 1)
        _MistAmount ("Mist inside the dark", Range(0, 1)) = 0.35
        _Parallax ("Parallax depth", Range(0, 2)) = 0.7
        _RimColor ("Edge glow colour", Color) = (0.55, 0.2, 1, 1)
        _RimGlow ("Edge glow", Range(0, 4)) = 1.6
        _FresnelPower ("Edge glow power", Range(0.5, 8)) = 2.2
        _EnergyGlow ("Extra glow while moving", Range(0, 4)) = 1.2
        [Header(Rifts)]
        _RiftColor ("Rift colour", Color) = (0.7, 0.35, 1, 1)
        _LineWidth ("Rift width", Range(0.01, 0.2)) = 0.045
        _LineStrength ("Rift glow", Range(0, 3)) = 0.7
        _PulseSpeed ("Pulse speed", Float) = 1.2
        _PulseAmount ("Pulse amount", Range(0, 1)) = 0.35
        [Header(Nat 20 rift flare)]
        _FlareColor ("Flare core colour", Color) = (1, 0.88, 1, 1)
        _FlareWidth ("Extra rift width", Range(0, 0.3)) = 0.12
        _FlareStrength ("Flare core brightness", Range(0, 6)) = 2.5
        _FlareRim ("Extra edge glow", Range(0, 4)) = 1.5
        [Header(Nat 1 fade out)]
        _FadeMax ("How far it dissolves (1 = gone)", Range(0, 1)) = 0.9
        _FadeScale ("Dissolve pattern scale (x noise scale)", Float) = 1.6
        _FadeEdge ("Glowing edge width", Range(0.01, 0.3)) = 0.08
        _FadeEdgeStrength ("Glowing edge brightness", Range(0, 6)) = 2.5
        _FadeDim ("Dimming of what is left", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _VoidColor;
            half4 _MistColor;
            half  _MistAmount;
            float _Parallax;
            half4 _RimColor;
            half  _RimGlow;
            half  _FresnelPower;
            half  _EnergyGlow;
            half4 _RiftColor;
            half  _LineWidth;
            half  _LineStrength;
            float _PulseSpeed;
            half  _PulseAmount;
            half4 _FlareColor;
            half  _FlareWidth;
            half  _FlareStrength;
            half  _FlareRim;
            half  _FadeMax;
            float _FadeScale;
            half  _FadeEdge;
            half  _FadeEdgeStrength;
            half  _FadeDim;
        CBUFFER_END
        #include "Assets/Shaders/DiceFX/DiceFXLib.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DiceVert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ DICEFX_LOW
            #pragma multi_compile_fog

            half4 Frag(DiceVaryings i) : SV_Target
            {
                float3 nWS = normalize(i.normalWS);
                float3 tWS = normalize(i.tangentWS);
                float3 bWS = cross(nWS, tWS);
                float3 vWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fres = pow(1.0h - saturate((half)dot(nWS, vWS)), _FresnelPower);
                float2 view = float2(dot(vWS, tWS), dot(vWS, bWS));

                // rifts and mist lie inside the dark: the lookup shifts against the view direction (two depths = parallax)
                half4 nA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (i.faceUV - view * _Parallax) * _NoiseScale);
                half width = _LineWidth + _Highlight * _FlareWidth;
                half rifts = 1.0h - smoothstep(0.0h, width, nA.r);
            #if !defined(DICEFX_LOW)
                half4 nB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (i.faceUV - view * (_Parallax * 2.2)) * (_NoiseScale * 1.7) + 0.37);
                rifts = saturate(rifts + (1.0h - smoothstep(0.0h, width * 1.5h, nB.r)) * 0.6h);
            #endif
                half mist = smoothstep(0.4h, 0.8h, nA.g) * _MistAmount;

                half pulse = 1.0h + _PulseAmount * (half)sin(_Time.y * _PulseSpeed + i.facet * TWO_PI);
                half moving = 1.0h + _Energy * _EnergyGlow;

                half3 emission = _MistColor.rgb * mist;
                emission += _RiftColor.rgb * (rifts * _LineStrength * pulse * moving);
                emission += _FlareColor.rgb * (rifts * _Highlight * _FlareStrength);
                emission += _RimColor.rgb * (fres * _RimGlow * (moving + _Highlight * _FlareRim + _Flash));

                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);

                // nat 1: dissolve through a glowing edge (the numbers stay). Low FX only dims.
                emission *= 1.0h - _FadeDim * _Gloom;
            #if !defined(DICEFX_LOW)
                if (_Gloom > 0.001h)
                {
                    half dissolve = smoothstep(0.25h, 0.75h, SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.faceUV * (_NoiseScale * _FadeScale) + 0.61).g);
                    half level = _Gloom * _FadeMax;
                    clip(dissolve - level + (1.0h - clear));
                    emission += _RiftColor.rgb * ((1.0h - smoothstep(0.0h, _FadeEdge, dissolve - level)) * _FadeEdgeStrength * clear);
                }
            #endif

                DiceSurface s;
                s.albedo = _VoidColor.rgb;
                s.emission = emission * clear;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                DiceApplyNumerals(s, sdf);
                return DiceShade(i, s, nWS);
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
