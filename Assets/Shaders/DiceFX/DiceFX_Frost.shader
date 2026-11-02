// Frost dice: a block of ice. Crystal facet lines sit at two depths inside the glass (parallax), white frost creeps in from the edges,
// and tiny glints flash as the die turns. Nat 20 (_Highlight): frost blooms outward from every face centre and the glints multiply.
// Nat 1 (_Gloom): the ice melts - the frost retreats, the colour goes dull and wet, and streaks run down the picture.
// Low FX (global keyword DICEFX_LOW, see DiceFxConfig): the second crystal layer is dropped.
// Cost: 3 noise samples + 1 numeral SDF sample, ~100 ALU (low FX: 2 + 1).
Shader "DiceThrower/DiceFX/Frost"
{
    Properties
    {
        [Header(Numerals (signed distance field))]
        [NoScaleOffset] _NumeralMap ("Numeral SDF (R)", 2D) = "black" {}
        _NumeralColor ("Numeral colour", Color) = (0.04, 0.16, 0.35, 1)
        _NumeralGlow ("Numeral glow", Range(0, 4)) = 0
        _NumeralThick ("Numeral thickness (SDF units)", Range(-0.1, 0.2)) = 0.07
        _NumeralBevel ("Numeral engraved bevel width", Range(0.02, 0.5)) = 0.22
        _NumeralMetal ("Numeral metallic", Range(0, 1)) = 0.9
        _NumeralSmoothness ("Numeral smoothness", Range(0, 1)) = 0.6
        _NumeralHalo ("Keep effects this far from numerals (SDF units)", Range(0.1, 0.5)) = 0.3
        [Header(Surface)]
        [NoScaleOffset] _NoiseMap ("Noise pack (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per object unit)", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Ice)]
        _IceDeep ("Ice, deep", Color) = (0.08, 0.28, 0.5, 1)
        _IceLight ("Ice, light", Color) = (0.6, 0.85, 1, 1)
        _Ior ("Refraction (1 / index)", Range(0.4, 1)) = 0.7
        _Depth ("Interior depth", Range(0, 2)) = 0.5
        _FresnelPower ("Rim power", Range(0.5, 8)) = 2.5
        _RimGlow ("Rim glow", Range(0, 3)) = 0.7
        [Header(Crystal lines)]
        _LineWidth ("Line width", Range(0.01, 0.2)) = 0.05
        _LineStrength ("Line strength", Range(0, 3)) = 0.9
        [Header(Frost)]
        _FrostColor ("Frost colour", Color) = (0.92, 0.97, 1, 1)
        _FrostAmount ("Frost amount", Range(0, 1)) = 0.45
        _FrostScale ("Frost pattern scale (x noise scale)", Float) = 2.2
        _FrostEdge ("Frost gathers at the rim", Range(0, 2)) = 0.8
        _FrostSoft ("Frost edge softness", Range(0.02, 0.5)) = 0.15
        [Header(Glints)]
        _SparkleColor ("Glint colour", Color) = (1, 1, 1, 1)
        _SparkleDensity ("Glint cells per object unit", Float) = 14
        _SparkleAmount ("Share of cells with a glint", Range(0, 1)) = 0.25
        _SparkleSize ("Glint size (cell fraction)", Range(0.02, 0.5)) = 0.14
        _SparkleStrength ("Glint brightness", Range(0, 6)) = 2
        _SparkleTurn ("Glint flashes per view-angle unit", Float) = 9
        _SparkleSharp ("Glint sharpness", Range(1, 30)) = 8
        [Header(Nat 20 frost bloom)]
        _BloomReach ("Bloom reach from the face centre (object units)", Float) = 1.4
        _BloomLines ("Extra line strength", Range(0, 4)) = 1.2
        _BloomSparkle ("Extra share of glints", Range(0, 1)) = 0.5
        _BloomGain ("Extra glint brightness", Range(0, 6)) = 2.5
        _BloomColor ("Bloom glow colour", Color) = (0.65, 0.9, 1, 1)
        _BloomGlow ("Bloom glow", Range(0, 3)) = 0.5
        [Header(Nat 1 melt)]
        _MeltColor ("Melted ice colour", Color) = (0.35, 0.5, 0.6, 1)
        _MeltAmount ("How far the colour drains", Range(0, 1)) = 0.7
        _DripFreq ("Streaks across the face", Float) = 3.5
        _DripStretch ("Streak length (smaller = longer)", Float) = 0.35
        _DripSpeed ("Streak speed", Float) = 0.45
        _DripLow ("Streak threshold low", Range(0, 1)) = 0.55
        _DripHigh ("Streak threshold high", Range(0, 1)) = 0.75
        _DripStrength ("Streak brightness", Range(0, 3)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _IceDeep;
            half4 _IceLight;
            half  _Ior;
            half  _Depth;
            half  _FresnelPower;
            half  _RimGlow;
            half  _LineWidth;
            half  _LineStrength;
            half4 _FrostColor;
            half  _FrostAmount;
            float _FrostScale;
            half  _FrostEdge;
            half  _FrostSoft;
            half4 _SparkleColor;
            float _SparkleDensity;
            half  _SparkleAmount;
            half  _SparkleSize;
            half  _SparkleStrength;
            float _SparkleTurn;
            half  _SparkleSharp;
            float _BloomReach;
            half  _BloomLines;
            half  _BloomSparkle;
            half  _BloomGain;
            half4 _BloomColor;
            half  _BloomGlow;
            half4 _MeltColor;
            half  _MeltAmount;
            float _DripFreq;
            float _DripStretch;
            float _DripSpeed;
            half  _DripLow;
            half  _DripHigh;
            half  _DripStrength;
        CBUFFER_END
        #include "Assets/Shaders/DiceFX/DiceFXLib.hlsl"

        static const float GlintJitter = 0.5;           // how far a glint may sit from its cell centre (cell fraction)
        static const float2 GlintView = float2(0.9, 0.6); // which way of turning the die makes the glints flash
        static const float BloomPulseSpeed = 3.0;         // the nat 20 glow breathes outward from the face centre
        static const float BloomPulseWaves = 6.0;

        // Glints: at most one per grid cell; each flashes at its own view angle. gain scales their size.
        half Glints(float2 uv, float2 view, half amount, half sizeScale)
        {
            float2 g = uv * _SparkleDensity;
            float2 cell = floor(g);
            float h = DiceHash(cell);
            float2 jitter = (float2(DiceHash(cell + 3.7), DiceHash(cell + 9.1)) - 0.5) * GlintJitter;
            float d = length(frac(g) - 0.5 - jitter);
            half spot = (half)saturate(1.0 - d / (_SparkleSize * sizeScale * (0.5 + h)));
            half flash = pow(0.5h + 0.5h * (half)sin(dot(view, GlintView) * _SparkleTurn + h * 40.0), _SparkleSharp);
            return spot * spot * flash * (half)step(1.0 - amount, DiceHash(cell + 7.7));
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
            #pragma multi_compile_fragment _ DICEFX_LOW
            #pragma multi_compile_fog

            half4 Frag(DiceVaryings i) : SV_Target
            {
                float3 nWS = normalize(i.normalWS);
                float3 tWS = normalize(i.tangentWS);
                float3 bWS = cross(nWS, tWS);
                float3 vWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half ndv = saturate((half)dot(nWS, vWS));
                half fres = pow(1.0h - ndv, _FresnelPower);
                float2 view = float2(dot(vWS, tWS), dot(vWS, bWS));

                float2 fc = i.faceUV - _Seed * DiceSeedShift; // centred on the face
                float r = length(fc);

                // the crystals lie inside the ice: the lookup shifts along the refracted view ray (two depths = parallax)
                float3 rIn = refract(-vWS, nWS, _Ior);
                float travel = _Depth / max(-dot(rIn, nWS), 0.25);
                float2 lateral = float2(dot(rIn, tWS), dot(rIn, bWS)) * travel;
                half4 nA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (i.faceUV + lateral) * _NoiseScale);
                half lines = 1.0h - smoothstep(0.0h, _LineWidth, nA.r);
            #if !defined(DICEFX_LOW)
                half4 nB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (i.faceUV + lateral * 1.8) * (_NoiseScale * 1.7) + 0.37);
                lines = saturate(lines + (1.0h - smoothstep(0.0h, _LineWidth * 1.5h, nB.r)) * 0.6h);
            #endif

                half3 ice = lerp(_IceDeep.rgb, _IceLight.rgb, saturate(nA.g * 0.7h + fres * 0.5h));
                half3 col = ice + _IceLight.rgb * (lines * _LineStrength * (1.0h + _Highlight * _BloomLines));

                // frost lies on the surface (no parallax); it gathers at the rim, grows outward on a nat 20 and retreats on a nat 1
                half frostNoise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.faceUV * (_NoiseScale * _FrostScale)).b;
                half bloom = _Highlight * (1.0h - smoothstep(_BloomReach * 0.6, _BloomReach, r));
                half frostField = frostNoise * 0.7h + fres * _FrostEdge + bloom;
                half threshold = 1.0h - _FrostAmount * (1.0h - _Gloom);
                half frost = smoothstep(threshold, threshold + _FrostSoft, frostField);
                col = lerp(col, _FrostColor.rgb * (0.75h + 0.25h * nA.g), frost);

                // nat 1: the colour drains to dull wet ice and streaks run down the picture
                half lum = dot(col, half3(0.299h, 0.587h, 0.114h));
                col = lerp(col, lum * _MeltColor.rgb * 1.6h, _Gloom * _MeltAmount);
                float2 down = DiceScreenDown(tWS, bWS);
                float2 dripUV = float2(dot(fc, float2(-down.y, down.x)) * _DripFreq, dot(fc, down) * _DripStretch - _Time.y * _DripSpeed);
                half drip = smoothstep(_DripLow, _DripHigh, SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, dripUV).g);
                col += _IceLight.rgb * (drip * _DripStrength * _Gloom);

                // glints, more of them and brighter on a nat 20
                half glints = Glints(i.faceUV, view, saturate(_SparkleAmount + _Highlight * _BloomSparkle), 1.0h + _Highlight)
                              * _SparkleStrength * (1.0h + _Highlight * _BloomGain + _Flash);
                half3 glow = _BloomColor.rgb * (_Highlight * _BloomGlow * (0.5h + 0.5h * (half)sin(_Time.y * BloomPulseSpeed - r * BloomPulseWaves)));

                half sdf = DiceNumeralSDF(i.uv);
                half clear = lerp(1.0h, DiceNumeralClear(sdf), 0.85h);

                DiceSurface s;
                s.albedo = col * 0.25h;
                s.emission = (col * 0.75h + _IceLight.rgb * (fres * _RimGlow) + _SparkleColor.rgb * glints + glow) * clear;
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
