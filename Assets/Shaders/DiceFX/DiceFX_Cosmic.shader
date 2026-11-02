// Cosmic dice: a window into deep space. Nebula clouds and two star layers sit at different depths behind the faces (parallax),
// so they slide as the die turns. Nat 20 (_Highlight): supernova - nebula and stars flare, a shockwave ring runs out from every face centre.
// Nat 1 (_Gloom): black hole - each face swirls into a dark disc with a glowing accretion ring and the rest dims.
// Low FX (global keyword DICEFX_LOW, see DiceFxConfig): one nebula layer and one star layer.
// Cost: 2 noise samples + 1 numeral SDF sample, ~110 ALU (low FX: 1 + 1, ~70 ALU).
Shader "DiceThrower/DiceFX/Cosmic"
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
        _NoiseScale ("Noise scale (per object unit)", Float) = 1.1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.6
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Space)]
        _SpaceColor ("Deep space", Color) = (0.015, 0.01, 0.05, 1)
        _NebulaA ("Nebula colour A", Color) = (0.5, 0.12, 0.9, 1)
        _NebulaB ("Nebula colour B", Color) = (0.1, 0.45, 1, 1)
        _NebulaStrength ("Nebula strength", Range(0, 3)) = 1.2
        _CloudLow ("Cloud threshold low", Range(0, 1)) = 0.35
        _CloudHigh ("Cloud threshold high", Range(0, 1)) = 0.75
        _FacetVariation ("Per-face colour variation", Range(0, 1)) = 0.4
        _Parallax ("Parallax depth", Range(0, 2)) = 0.6
        _Drift ("Nebula drift speed", Float) = 0.02
        _FresnelPower ("Rim power", Range(0.5, 8)) = 3
        _RimGlow ("Rim glow", Range(0, 3)) = 0.6
        [Header(Stars)]
        _StarColor ("Star colour", Color) = (1, 0.97, 0.9, 1)
        _StarDensity ("Star cells per object unit", Float) = 9
        _StarAmount ("Share of cells with a star", Range(0, 1)) = 0.35
        _StarSize ("Star size (cell fraction)", Range(0.02, 0.4)) = 0.1
        _StarStrength ("Star brightness", Range(0, 4)) = 1.8
        _Twinkle ("Twinkle speed", Float) = 2.5
        [Header(Nat 20 supernova)]
        _BurstColor ("Burst colour", Color) = (1, 0.85, 0.55, 1)
        _BurstStrength ("Burst strength", Range(0, 4)) = 1.4
        _BurstGlow ("Steady glow between rings", Range(0, 1)) = 0.3
        _NebulaBoost ("Nebula boost", Range(0, 4)) = 1.5
        _StarFlare ("Star flare (size and brightness)", Range(0, 6)) = 3
        _RingSpeed ("Shockwave rings per second", Float) = 0.8
        _RingReach ("Shockwave reach (object units)", Float) = 0.9
        _RingWidth ("Shockwave width (object units)", Float) = 0.12
        [Header(Nat 1 black hole)]
        _HoleRadius ("Black hole radius (object units)", Float) = 0.45
        _Twist ("Swirl twist", Float) = 3
        _SwirlSpeed ("Swirl speed", Float) = 1.5
        _ArmCount ("Accretion arms", Float) = 3
        _AccretionColor ("Accretion colour", Color) = (1, 0.5, 0.15, 1)
        _AccretionStrength ("Accretion strength", Range(0, 4)) = 1.6
        _AccretionOffset ("Accretion ring radius (in hole radii)", Float) = 1.25
        _AccretionWidth ("Accretion ring width (in hole radii)", Float) = 0.3
        _GloomDim ("Dimming of the rest", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _SpaceColor;
            half4 _NebulaA;
            half4 _NebulaB;
            half  _NebulaStrength;
            half  _CloudLow;
            half  _CloudHigh;
            half  _FacetVariation;
            float _Parallax;
            float _Drift;
            half  _FresnelPower;
            half  _RimGlow;
            half4 _StarColor;
            float _StarDensity;
            half  _StarAmount;
            half  _StarSize;
            half  _StarStrength;
            float _Twinkle;
            half4 _BurstColor;
            half  _BurstStrength;
            half  _BurstGlow;
            half  _NebulaBoost;
            half  _StarFlare;
            float _RingSpeed;
            float _RingReach;
            float _RingWidth;
            float _HoleRadius;
            float _Twist;
            float _SwirlSpeed;
            float _ArmCount;
            half4 _AccretionColor;
            half  _AccretionStrength;
            float _AccretionOffset;
            float _AccretionWidth;
            half  _GloomDim;
        CBUFFER_END
        #include "Assets/Shaders/DiceFX/DiceFXLib.hlsl"

        static const float StarJitter = 0.5; // how far a star may sit from its cell centre (cell fraction)

        // One layer of stars: at most one per grid cell, twinkling. sizeScale grows the stars (nat 20 flare).
        half StarLayer(float2 uv, float cellShift, half sizeScale)
        {
            float2 g = uv * _StarDensity;
            float2 cell = floor(g) + cellShift;
            float h = DiceHash(cell);
            float2 jitter = (float2(DiceHash(cell + 3.7), DiceHash(cell + 9.1)) - 0.5) * StarJitter;
            float d = length(frac(g) - 0.5 - jitter);
            float size = _StarSize * sizeScale * (0.4 + 0.6 * h);
            half core = (half)saturate(1.0 - d / size);
            half twinkle = 0.65h + 0.35h * (half)sin(_Time.y * _Twinkle * (0.5 + h) + h * 40.0);
            return core * core * twinkle * (half)step(1.0 - _StarAmount, h);
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
                half fres = pow(1.0h - saturate((half)dot(nWS, vWS)), _FresnelPower);
                float2 view = float2(dot(vWS, tWS), dot(vWS, bWS));

                // face-centred coordinates; the black hole winds them up around the centre (tighter near it)
                float2 centre = _Seed * DiceSeedShift;
                float2 fc = i.faceUV - centre;
                float r = length(fc);
                float sn, cs;
                sincos(_Gloom * _Twist / (0.25 + r), sn, cs);
                float2 warped = float2(fc.x * cs - fc.y * sn, fc.x * sn + fc.y * cs) + centre;

                // nebula: two clouds at different depths
                float2 drift = float2(_Time.y * _Drift, -_Time.y * _Drift * 0.7);
                half4 nA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (warped - view * _Parallax) * _NoiseScale + drift);
            #if defined(DICEFX_LOW)
                half field = nA.g;
                half hueMix = nA.b;
            #else
                half4 nB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (warped - view * (_Parallax * 2.0)) * (_NoiseScale * 1.9) + 0.37 - drift);
                half field = nA.g * 0.6h + nB.b * 0.4h;
                half hueMix = lerp(nA.b, nB.g, 0.5h);
            #endif
                half cloud = smoothstep(_CloudLow, _CloudHigh, field);
                half3 nebula = lerp(_NebulaA.rgb, _NebulaB.rgb, saturate(hueMix + (i.facet - 0.5h) * _FacetVariation)) * (cloud * _NebulaStrength);

                // stars: a far and a near layer; the nat 20 flare makes them bigger and brighter
                half flare = _Highlight * _StarFlare;
                half stars = StarLayer(warped - view * (_Parallax * 1.5), 0.0, 1.0h + flare);
            #if !defined(DICEFX_LOW)
                stars += StarLayer((warped - view * (_Parallax * 3.0)) * 1.7 + 5.3, 11.0, 1.0h + flare) * 0.6h;
            #endif
                half3 starLight = _StarColor.rgb * (stars * _StarStrength * (1.0h + flare + _Flash + _Energy));

                half3 col = _SpaceColor.rgb + nebula * (1.0h + _Highlight * _NebulaBoost) + starLight;
                col += lerp(_NebulaA.rgb, _NebulaB.rgb, 0.5h) * (fres * _RimGlow);

                // nat 20: a shockwave ring runs out from the centre of every face, a steady glow stays between rings
                float ringT = (r - frac(_Time.y * _RingSpeed) * _RingReach) / _RingWidth;
                half ring = (half)exp(-ringT * ringT);
                col += _BurstColor.rgb * (_Highlight * _BurstStrength * (_BurstGlow + ring));

                // nat 1: black hole - dark disc, spiral accretion ring, everything else dims
                float holeR = max(_HoleRadius * _Gloom, 1e-3);
                half horizon = 1.0h - smoothstep(holeR * 0.75, holeR, r);
                half arms = 0.5h + 0.5h * (half)sin(atan2(fc.y, fc.x) * _ArmCount + r * _Twist * TWO_PI - _Time.y * _SwirlSpeed);
                float accT = (r - holeR * _AccretionOffset) / (holeR * _AccretionWidth);
                half accretion = (half)exp(-accT * accT) * arms;
                col = col * (1.0h - _GloomDim * _Gloom) * (1.0h - horizon);
                col += _AccretionColor.rgb * (accretion * _AccretionStrength * _Gloom);

                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);

                DiceSurface s;
                s.albedo = col * 0.15h;
                s.emission = col * clear;
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
