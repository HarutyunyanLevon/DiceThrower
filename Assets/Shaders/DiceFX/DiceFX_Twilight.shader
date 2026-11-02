// Twilight resin die: clear resin with swirls of pink and deep blue smoke, and iridescent glitter flakes inside. The smoke and the flakes sit at two
// depths and slide against each other as the die turns (parallax), which gives the look of something suspended in the resin.
// Cost: 5 noise samples + 1 numeral SDF sample, ~110 ALU.
Shader "DiceThrower/DiceFX/Twilight"
{
    Properties
    {
        [Header(Numerals (signed distance field))]
        [NoScaleOffset] _NumeralMap ("Numeral SDF (R)", 2D) = "black" {}
        _NumeralColor ("Numeral colour", Color) = (1, 0.84, 0.4, 1)
        _NumeralGlow ("Numeral glow", Range(0, 4)) = 0.3
        _NumeralThick ("Numeral thickness (SDF units)", Range(-0.1, 0.2)) = 0.04
        _NumeralBevel ("Numeral engraved bevel width", Range(0.02, 0.5)) = 0.22
        _NumeralMetal ("Numeral metallic", Range(0, 1)) = 0.9
        _NumeralSmoothness ("Numeral smoothness", Range(0, 1)) = 0.6
        _NumeralHalo ("Keep effects this far from numerals (SDF units)", Range(0.1, 0.5)) = 0.3
        [Header(Surface)]
        [NoScaleOffset] _NoiseMap ("Noise pack (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per object unit)", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Resin)]
        _ClearColor ("Clear resin", Color) = (0.86, 0.9, 0.98, 1)
        _PinkColor ("Pink smoke", Color) = (0.95, 0.6, 0.78, 1)
        _BlueColor ("Blue smoke", Color) = (0.08, 0.2, 0.42, 1)
        _SmokeLow ("Smoke threshold low", Range(0, 1)) = 0.38
        _SmokeHigh ("Smoke threshold high", Range(0, 1)) = 0.7
        _Warp ("Swirl warp", Range(0, 2)) = 0.8
        _Depth ("Parallax depth", Range(0, 2)) = 0.8
        _Glow ("Light through the resin", Range(0, 2)) = 0.4
        _Refract ("Refraction through the cut edges", Range(0, 3)) = 0
        [Header(Photoreal)]
        [NoScaleOffset] _EnvMap ("Studio reflection cubemap", Cube) = "" {}
        _EnvStrength ("Reflection strength (0 = off)", Range(0, 3)) = 0
        _BevelWidth ("Rounded edge width (fraction of the face, 0 = sharp)", Range(0, 0.6)) = 0
        _BevelTilt ("Rounded edge roundness", Range(0, 1.5)) = 0.7
        _EngraveDepth ("Numeral engraving depth (0 = flat paint)", Range(0, 4)) = 0
        [Header(Glitter)]
        _GlitterAmount ("Glitter amount", Range(0, 1)) = 0.5
        _GlitterScale ("Glitter cell scale", Float) = 6
        _GlitterStrength ("Glitter brightness", Range(0, 6)) = 2.5
        _RimColor ("Edge glow colour", Color) = (0.8, 0.85, 1, 1)
        _RimGlow ("Edge glow", Range(0, 3)) = 0.5
        _FresnelPower ("Edge glow power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _ClearColor;
            half4 _PinkColor;
            half4 _BlueColor;
            half  _SmokeLow;
            half  _SmokeHigh;
            half  _Warp;
            float _Depth;
            half  _Glow;
            half  _Refract;
            half  _EnvStrength;
            half  _BevelWidth;
            half  _BevelTilt;
            half  _EngraveDepth;
            half  _GlitterAmount;
            float _GlitterScale;
            half  _GlitterStrength;
            half4 _RimColor;
            half  _RimGlow;
            half  _FresnelPower;
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
                half fres = pow(1.0h - saturate((half)dot(nWS, vWS)), _FresnelPower);
                float2 view = float2(dot(vWS, tWS), dot(vWS, bWS));

                // the cut edges bend the light: the smoke seen near an edge is shifted by how far the rounded normal leans from the face
                float3 nShade = DiceBevelNormal(i, nWS, tWS, bWS, _BevelWidth * i.faceCtr.z, _BevelTilt);
                float3 lean = nShade - nWS;
                float2 refr = float2(dot(lean, tWS), dot(lean, bWS)) * _Refract;

                // smoke at two depths, domain-warped by a second noise so it swirls
                float2 uvA = (i.faceUV - view * _Depth * 0.5 + refr) * _NoiseScale;
                half4 warpN = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvA * 0.7 + 0.17);
                float2 warp = (float2(warpN.g, warpN.b) - 0.5) * _Warp;
                half4 smokeA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvA + warp);
                half4 smokeB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (i.faceUV - view * _Depth + refr * 1.6) * (_NoiseScale * 1.4) + warp * 0.8 + 0.53);

                half pink = smoothstep(_SmokeLow, _SmokeHigh, smokeA.g);
                half blue = smoothstep(_SmokeLow + 0.05h, _SmokeHigh + 0.1h, smokeB.b * 0.7h + smokeA.b * 0.3h);
                half3 c = lerp(_ClearColor.rgb, _PinkColor.rgb, pink * 0.85h);
                c = lerp(c, _BlueColor.rgb, blue);

                // glitter: flakes in cells of the noise; each flake flashes only when the view hits its angle
                float2 gUV = (i.faceUV - view * _Depth * 1.6) * _GlitterScale;
                half4 cell = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, gUV * 0.37 + 0.71);
                float flakeId = cell.a * 61.0 + floor(cell.g * 8.0);
                half flicker = (half)pow(saturate(sin(dot(view, float2(7.0, 5.0)) * 5.0 + flakeId * 12.9898) * 0.5 + 0.5), 6.0);
                half flake = smoothstep(0.55h, 0.9h, cell.b) * flicker * _GlitterAmount * (0.35h + 0.65h * blue + 0.3h * pink);
                half3 glitter = DicePalette(flakeId * 0.37 + dot(view, float2(0.6, 0.4))) * (flake * _GlitterStrength);

                half3 emission = c * (_Glow * (0.4h + 0.6h * smokeA.g)) + glitter;
                emission += _RimColor.rgb * (fres * _RimGlow * (1.0h + _Flash));

                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);

                DiceSurface s;
                s.albedo = c;
                s.emission = emission * clear;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                DiceApplyNumerals(s, sdf);

                // photoreal: engraved numerals and studio reflections (the bevel normal was computed above for the refraction)
                if (_EngraveDepth > 0.001h)
                {
                    float hEdge = 0.5 - _NumeralThick;
                    float height = -smoothstep(hEdge - 0.08, hEdge + 0.02, (float)sdf) * (_EngraveDepth * 0.01);
                    nShade = DiceBump(nShade, i.positionWS, height, 1.0);
                }
                s.emission += DiceEnvSpecular(s, nShade, vWS, _EnvStrength);
                return DiceShade(i, s, nShade);
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
