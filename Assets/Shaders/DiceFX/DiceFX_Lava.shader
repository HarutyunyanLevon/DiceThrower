// Lava dice: pale speckled stone with big glowing orange patches that drift and morph (like a yooperlite).
// Two noise layers, warped by a slow third one, scroll in different directions; where their sum passes a threshold the stone glows.
// The threshold moves with heat: warm = patches grow, nat 20 (_Highlight) = brighter and bigger, nat 1 (_Gloom) = patches shrink to nothing
// and only the dark stone is left. The glow is sampled with a small view-dependent offset, so it looks like it lies inside the stone.
// Cost: 3 noise samples + 1 numeral SDF sample, ~70 ALU.
Shader "DiceThrower/DiceFX/Lava"
{
    Properties
    {
        [Header(Numerals (signed distance field))]
        [NoScaleOffset] _NumeralMap ("Numeral SDF (R)", 2D) = "black" {}
        _NumeralColor ("Numeral colour", Color) = (1, 0.78, 0.3, 1)
        _NumeralGlow ("Numeral glow", Range(0, 4)) = 1.2
        _NumeralThick ("Numeral thickness (SDF units)", Range(-0.1, 0.2)) = 0.07
        _NumeralBevel ("Numeral engraved bevel width", Range(0.02, 0.5)) = 0.22
        _NumeralMetal ("Numeral metallic", Range(0, 1)) = 0.9
        _NumeralSmoothness ("Numeral smoothness", Range(0, 1)) = 0.6
        _NumeralHalo ("Keep effects this far from numerals (SDF units)", Range(0.1, 0.5)) = 0.3
        [Header(Surface)]
        [NoScaleOffset] _NoiseMap ("Noise pack (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per object unit)", Float) = 1.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Stone)]
        _StoneColor ("Stone colour", Color) = (0.85, 0.85, 0.9, 1)
        _StoneLift ("Stone self-light (keeps it pale in dim scenes)", Range(0, 1)) = 0.35
        _SpeckColor ("Speck colour", Color) = (0.03, 0.03, 0.035, 1)
        _SpeckLow ("Speck threshold low", Range(0, 0.5)) = 0.17
        _SpeckHigh ("Speck threshold high", Range(0, 0.5)) = 0.3
        _CharWidth ("Dark crust around the glow", Range(0, 0.4)) = 0.12
        [Header(Glow patches)]
        _ColdColor ("Glow: edge", Color) = (0.55, 0.04, 0.0, 1)
        _HotColor ("Glow: body", Color) = (1, 0.42, 0.04, 1)
        _CoreColor ("Glow: core", Color) = (1, 0.9, 0.4, 1)
        _PatchScale ("Patch size (bigger = smaller patches)", Float) = 0.7
        _Depth ("Glow depth inside the stone", Range(0, 1.5)) = 0.35
        _ThresholdCold ("Threshold when cold (no patches)", Range(0.5, 1.3)) = 1.05
        _ThresholdHot ("Threshold when hot (large patches)", Range(0.1, 0.9)) = 0.4
        _Soft ("Patch edge softness", Range(0.01, 0.4)) = 0.12
        _CoreWidth ("Hot core spread", Range(0.05, 0.6)) = 0.3
        _Spill ("Glow bleeding outside the patches", Range(0, 1)) = 0.15
        _SpillWidth ("Bleed width", Range(0.02, 0.5)) = 0.18
        [Header(Flow and heat)]
        _FlowSpeed ("Flow speed", Float) = 0.045
        _FlowWarp ("Flow warp", Range(0, 1.5)) = 0.5
        _HeatBase ("Heat when still", Range(0, 1)) = 0.3
        _EnergyHeat ("Extra heat while moving", Range(0, 1)) = 0.3
        _PulseSpeed ("Pulse speed", Float) = 1.3
        _PulseAmount ("Pulse amount", Range(0, 0.3)) = 0.06
        _EmissionStrength ("Emission strength", Range(0, 3)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _StoneColor;
            half  _StoneLift;
            half4 _SpeckColor;
            half  _SpeckLow;
            half  _SpeckHigh;
            half  _CharWidth;
            half4 _ColdColor;
            half4 _HotColor;
            half4 _CoreColor;
            float _PatchScale;
            half  _Depth;
            half  _ThresholdCold;
            half  _ThresholdHot;
            half  _Soft;
            half  _CoreWidth;
            half  _Spill;
            half  _SpillWidth;
            float _FlowSpeed;
            half  _FlowWarp;
            half  _HeatBase;
            half  _EnergyHeat;
            float _PulseSpeed;
            half  _PulseAmount;
            half  _EmissionStrength;
        CBUFFER_END
        #include "Assets/Shaders/DiceFX/DiceFXLib.hlsl"

        half3 HeatColor(half g)
        {
            half3 c = lerp(_ColdColor.rgb, _HotColor.rgb, saturate(g * 2.0h));
            return lerp(c, _CoreColor.rgb, saturate(g * 2.0h - 1.0h));
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
            #pragma vertex DiceVert
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

                // the glow lies a little inside the stone: shift the lookup against the view direction
                float2 uv = (i.faceUV - float2(dot(vWS, tWS), dot(vWS, bWS)) * _Depth) * (_NoiseScale * _PatchScale);
                float t = _Time.y * _FlowSpeed;

                half4 w = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv * 0.5 + float2(t * 0.6, -t * 0.4));
                float2 flow = (w.bg - 0.5h) * _FlowWarp;
                half a = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv + flow + float2(t, t * 0.4)).g;
                half4 nb = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv * 1.6 + flow * 1.3 + float2(-t * 0.6, t * 0.5) + 0.5);
                half field = a * 0.6h + nb.b * 0.4h;

                // heat decides how far the patches reach: nat 1 takes it to zero, nat 20 to the maximum
                half pulse = 0.5h + 0.5h * (half)sin(_Time.y * _PulseSpeed + w.r * 6.2832);
                half level = saturate(_HeatBase + _Energy * _EnergyHeat + pulse * _PulseAmount + _Flash * 0.4h + _Highlight) * (1.0h - _Gloom);
                half thr = lerp(_ThresholdCold, _ThresholdHot, level);

                half g = smoothstep(thr, thr + _Soft, field);
                half core = smoothstep(thr + _Soft * 0.5h, thr + _CoreWidth, field);
                half rim = smoothstep(thr - _CharWidth, thr, field) * (1.0h - g);
                half spill = smoothstep(thr - _SpillWidth, thr, field) * (1.0h - g) * level;
                half specks = 1.0h - smoothstep(_SpeckLow, _SpeckHigh, nb.b);

                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);

                half3 stone = lerp(_StoneColor.rgb, _SpeckColor.rgb, specks) * (1.0h - rim * 0.85h) * lerp(1.0h, 0.75h, _Gloom);
                half3 glow = HeatColor(g * (0.35h + 0.65h * core) * (0.75h + 0.25h * level));
                half3 emit = glow * (g * _EmissionStrength) * (1.0h - specks * 0.8h) + _HotColor.rgb * (spill * _Spill * _EmissionStrength)
                           + stone * (_StoneLift * (1.0h - g));

                DiceSurface s;
                s.albedo = lerp(stone, glow * 0.2h, g);
                s.emission = emit * clear;
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
