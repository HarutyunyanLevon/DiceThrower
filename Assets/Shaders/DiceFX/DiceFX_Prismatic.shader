// Prismatic dice: clear glass with dichroic rainbow panels, internal facet lines and light rays. Opaque, so it costs no transparency:
// the "inside" is faked by sampling the noise pack along the refracted view ray (two depths = parallax), which makes panels, lines
// and rays slide as the die turns. Nat 20 (_Highlight): rainbow flare + sweeping shine. Nat 1 (_Gloom): dull grey-blue with raindrop streaks.
// Cost: 3 noise samples + 1 numeral SDF sample, ~90 ALU.
Shader "DiceThrower/DiceFX/Prismatic"
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
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Glass)]
        _GlassTint ("Clear glass tint", Color) = (0.55, 0.6, 0.66, 1)
        _Ior ("Refraction (1 / index)", Range(0.4, 1)) = 0.67
        _Depth ("Interior depth", Range(0, 2)) = 0.55
        _FresnelPower ("Rim power", Range(0.5, 8)) = 2.5
        _RimGlow ("Rim rainbow glow", Range(0, 3)) = 0.9
        _Sheen ("White rim sheen", Range(0, 2)) = 0.35
        [Header(Dichroic panels)]
        _PanelLow ("Panel threshold low", Range(0, 1)) = 0.42
        _PanelHigh ("Panel threshold high", Range(0, 1)) = 0.7
        _PanelStrength ("Panel strength", Range(0, 3)) = 1.3
        _PanelHue ("Hue spread between panels", Float) = 1.2
        _HueShift ("Hue shift (turns)", Float) = 0
        _Bands ("Colour bands across the view angle", Float) = 1.6
        _FacetVariation ("Per-face hue variation", Float) = 0.35
        _Drift ("Hue drift per second", Float) = 0.03
        _EnergyShift ("Hue shift while moving", Float) = 0.4
        _Saturation ("Saturation", Range(0, 1.5)) = 1
        [Header(Internal lines and rays)]
        _LineWidth ("Facet line width", Range(0.01, 0.2)) = 0.05
        _LineStrength ("Facet line strength", Range(0, 3)) = 0.9
        _RayFreq ("Ray density", Float) = 18
        _RaySharp ("Ray sharpness", Range(1, 20)) = 6
        _RayStrength ("Ray strength", Range(0, 3)) = 0.9
        [Header(Nat 20 shine)]
        _ShineDir ("Shine direction (world)", Vector) = (0.7, 0.3, 0.6, 0)
        _ShineFreq ("Shine bands per unit", Float) = 0.9
        _ShineSpeed ("Shine speed", Float) = 1.2
        _ShineSharp ("Shine sharpness", Range(1, 20)) = 6
        _ShineStrength ("Shine strength", Range(0, 5)) = 2.2
        [Header(Nat 1 gloom)]
        _GloomTint ("Gloom tint", Color) = (0.42, 0.5, 0.62, 1)
        _RainStrength ("Raindrop streaks", Range(0, 2)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            float4 _GlassTint;
            half  _Ior;
            half  _Depth;
            half  _FresnelPower;
            half  _RimGlow;
            half  _Sheen;
            half  _PanelLow;
            half  _PanelHigh;
            half  _PanelStrength;
            half  _PanelHue;
            float _HueShift;
            float _Bands;
            float _FacetVariation;
            float _Drift;
            float _EnergyShift;
            half  _Saturation;
            half  _LineWidth;
            half  _LineStrength;
            float _RayFreq;
            half  _RaySharp;
            half  _RayStrength;
            float4 _ShineDir;
            float _ShineFreq;
            float _ShineSpeed;
            half  _ShineSharp;
            half  _ShineStrength;
            half4 _GloomTint;
            half  _RainStrength;
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
            #pragma multi_compile_fog

            half4 Frag(DiceVaryings i) : SV_Target
            {
                float3 nWS = normalize(i.normalWS);
                float3 tWS = normalize(i.tangentWS);
                float3 bWS = cross(nWS, tWS);
                float3 vWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half ndv = saturate(dot(nWS, vWS));
                half fres = pow(1.0h - ndv, _FresnelPower);

                // where the view ray goes after it bends into the glass; two sample depths give the parallax of a real interior
                float3 rIn = refract(-vWS, nWS, _Ior);
                float travel = _Depth / max(-dot(rIn, nWS), 0.25);
                float2 lateral = float2(dot(rIn, tWS), dot(rIn, bWS)) * travel;
                float2 uvA = (i.faceUV + lateral) * _NoiseScale;
                float2 uvB = (i.faceUV + lateral * 1.8) * (_NoiseScale * 1.7) + 0.37;
                half4 nA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvA);
                half4 nB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvB);

                // dichroic panels: rainbow patches that change colour with the view angle
                half panel = smoothstep(_PanelLow, _PanelHigh, nA.g);
                float hue = _HueShift + nA.b * _PanelHue + (1.0 - ndv) * _Bands + i.facet * _FacetVariation
                          + _Time.y * _Drift + _Energy * _EnergyShift + _Highlight * 0.3;
                half3 prism = DicePalette(hue);
                half lum = dot(prism, half3(0.299h, 0.587h, 0.114h));
                prism = lerp(lum.xxx, prism, _Saturation * (1.0h + _Highlight * 0.3h) * (1.0h - _Gloom));

                // bright facet edges deep inside, and streaks of dispersed light across the panels
                half lines = saturate((1.0h - smoothstep(0.0h, _LineWidth, nA.r)) + (1.0h - smoothstep(0.0h, _LineWidth * 1.5h, nB.r)) * 0.6h);
                half rays = pow(saturate((half)sin((uvA.x + uvA.y * 0.6) * _RayFreq + nB.b * 6.2832) * 0.5h + 0.5h), _RaySharp) * panel;

                half sdf = DiceNumeralSDF(i.uv);
                half clear = lerp(1.0h, DiceNumeralClear(sdf), 0.85h);

                half3 col = _GlassTint.rgb * (1.0h - panel * 0.5h);
                col += prism * panel * _PanelStrength;
                col += lerp(half3(1, 1, 1), prism, 0.45h) * lines * _LineStrength;
                col += prism * rays * _RayStrength;

                // nat 1: the colour drains to a dull grey-blue and drops run down the faces
                half colLum = dot(col, half3(0.299h, 0.587h, 0.114h));
                col = lerp(col, colLum * _GloomTint.rgb * 1.4h, _Gloom);
                half drop = smoothstep(0.82h, 0.97h, SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(i.faceUV.x * 2.3, i.positionWS.y * 0.9 + _Time.y * 0.35)).g);
                col += _GloomTint.rgb * drop * (_Gloom * _RainStrength);

                // nat 20: a rainbow band sweeps across the die, repeating
                float sweep = dot(i.positionWS, _ShineDir.xyz) * _ShineFreq - _Time.y * _ShineSpeed;
                half band = pow(saturate(1.0h - abs((half)frac(sweep) - 0.5h) * 2.0h), _ShineSharp);
                half3 shine = DicePalette(sweep * 0.5 + 0.1) * (band * _Highlight * _ShineStrength);

                DiceSurface s;
                s.albedo = col * 0.25h;
                s.emission = (col * 0.75h + prism * (fres * _RimGlow * (1.0h - _Gloom) + _Flash * 0.5h)
                              + fres * _Sheen + shine) * clear;
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
