// Gem dice: one procedural stone shader for the gemstone sets (Amethyst, Moss Agate, Super Seven, Tiger's Eye, Rutilated Quartz, Indian Agate).
// A stone = banded or cloudy colour ramp (3 colours) + optional veins, moss patches, rutile needles, tiger's-eye chatoyance, a faked inner glow
// and an engraved gold decoration drawn per face (tarot sun, compass rose) around the numerals.
// Indian Agate uses _PerDie: every die of the set picks one of four tints from its seed, as the photo shows.
// Cost: 5 noise samples + 1 numeral SDF sample, ~110 ALU. No nat 20 / nat 1 effects (material only).
Shader "DiceThrower/DiceFX/Gem"
{
    Properties
    {
        [Header(Numerals (signed distance field))]
        [NoScaleOffset] _NumeralMap ("Numeral SDF (R)", 2D) = "black" {}
        _NumeralColor ("Numeral colour", Color) = (1, 0.84, 0.4, 1)
        _NumeralGlow ("Numeral glow", Range(0, 4)) = 0.4
        _NumeralThick ("Numeral thickness (SDF units)", Range(-0.1, 0.2)) = 0.05
        _NumeralBevel ("Numeral engraved bevel width", Range(0.02, 0.5)) = 0.22
        _NumeralMetal ("Numeral metallic", Range(0, 1)) = 0.9
        _NumeralSmoothness ("Numeral smoothness", Range(0, 1)) = 0.6
        _NumeralHalo ("Keep effects this far from numerals (SDF units)", Range(0.1, 0.5)) = 0.3
        [Header(Surface)]
        [NoScaleOffset] _NoiseMap ("Noise pack (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per object unit)", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Stone colours)]
        _ColorA ("Colour A (low)", Color) = (0.2, 0.06, 0.38, 1)
        _ColorB ("Colour B (mid)", Color) = (0.45, 0.2, 0.75, 1)
        _ColorC ("Colour C (high)", Color) = (0.8, 0.65, 0.95, 1)
        _BandDir ("Band direction (xy, in the face plane)", Vector) = (1, 0.4, 0, 0)
        _BandScale ("Band frequency", Float) = 3
        _BandWarp ("Band wobble", Float) = 1.2
        _BandSoft ("Band softness", Range(0.01, 0.5)) = 0.15
        _Cloudiness ("0 = bands, 1 = cloudy", Range(0, 1)) = 0.5
        [Header(Per die tint (Indian Agate))]
        _PerDie ("Per-die tint amount", Range(0, 1)) = 0
        _TintA ("Tint A", Color) = (0.15, 0.4, 0.25, 1)
        _TintB ("Tint B", Color) = (0.6, 0.2, 0.12, 1)
        _TintC ("Tint C", Color) = (0.85, 0.6, 0.2, 1)
        _TintD ("Tint D", Color) = (0.55, 0.3, 0.3, 1)
        _TintE ("Tint E", Color) = (0.15, 0.5, 0.4, 1)
        _TintMix ("Per-die gradient: how far the second colour shows (0 = one colour)", Range(0, 1)) = 0
        [Header(Veins)]
        _VeinColor ("Vein colour", Color) = (0.9, 0.85, 0.95, 1)
        _VeinAmount ("Vein amount", Range(0, 1)) = 0
        _VeinWidth ("Vein width", Range(0.01, 0.3)) = 0.06
        [Header(Moss)]
        _MossColor ("Moss colour", Color) = (0.08, 0.3, 0.14, 1)
        _MossAmount ("Moss amount", Range(0, 1)) = 0
        _MossLow ("Moss threshold low", Range(0, 1)) = 0.4
        _MossHigh ("Moss threshold high", Range(0, 1)) = 0.7
        [Header(Rutile needles)]
        _NeedleColor ("Needle colour", Color) = (0.02, 0.02, 0.03, 1)
        _NeedleAmount ("Needle amount", Range(0, 1)) = 0
        _NeedleWidth ("Needle width", Range(0.005, 0.1)) = 0.03
        [Header(Chatoyance (tiger eye))]
        _SheenColor ("Chatoyance colour", Color) = (1, 0.8, 0.35, 1)
        _Chatoyance ("Chatoyance strength", Range(0, 3)) = 0
        _ChatoScale ("Band frequency", Float) = 3
        _ChatoWarp ("Band wobble", Float) = 1
        _ChatoPower ("Band sharpness", Range(1, 16)) = 4
        [Header(Depth and glow)]
        _InnerColor ("Inner glow colour", Color) = (0.5, 0.2, 0.9, 1)
        _InnerGlow ("Inner glow", Range(0, 2)) = 0.3
        _RimColor ("Edge glow colour", Color) = (0.8, 0.6, 1, 1)
        _RimGlow ("Edge glow", Range(0, 3)) = 0.3
        _FresnelPower ("Edge glow power", Range(0.5, 8)) = 3
        _Depth ("Parallax depth", Range(0, 2)) = 0.5
        [Header(Photoreal)]
        [NoScaleOffset] _EnvMap ("Studio reflection cubemap", Cube) = "" {}
        _EnvStrength ("Reflection strength (0 = off)", Range(0, 3)) = 0
        _BevelWidth ("Rounded edge width (fraction of the face, 0 = sharp)", Range(0, 0.6)) = 0
        _BevelTilt ("Rounded edge roundness", Range(0, 1.5)) = 0.7
        _EngraveDepth ("Numeral engraving depth (0 = flat paint)", Range(0, 4)) = 0
        [Header(Fractures and fire)]
        _FractureColor ("Fracture colour", Color) = (0.8, 0.65, 1, 1)
        _FractureAmount ("Internal fractures (seen through the stone, they shift as it turns)", Range(0, 2)) = 0
        _FractureWidth ("Fracture width", Range(0.01, 0.2)) = 0.05
        _Fire ("Facet fire (a glint per facet as the die turns)", Range(0, 3)) = 0
        [Header(Moss filaments)]
        _MossFilament ("Branching moss filaments", Range(0, 1)) = 0
        [Header(Engraved decoration)]
        _DecorMode ("Decoration (0 none, 1 tarot sun, 2 compass rose, 3 night tarot with wreath)", Range(0, 3)) = 0
        _DecorColor ("Decoration colour", Color) = (1, 0.84, 0.4, 1)
        _DecorStrength ("Decoration strength", Range(0, 1)) = 1
        _DecorSize ("Decoration size (1 = fills the face)", Range(0.3, 1.2)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _ColorA;
            half4 _ColorB;
            half4 _ColorC;
            float4 _BandDir;
            float _BandScale;
            float _BandWarp;
            half  _BandSoft;
            half  _Cloudiness;
            half  _PerDie;
            half4 _TintA;
            half4 _TintB;
            half4 _TintC;
            half4 _TintD;
            half4 _TintE;
            half  _TintMix;
            half4 _VeinColor;
            half  _VeinAmount;
            half  _VeinWidth;
            half4 _MossColor;
            half  _MossAmount;
            half  _MossLow;
            half  _MossHigh;
            half4 _NeedleColor;
            half  _NeedleAmount;
            half  _NeedleWidth;
            half4 _SheenColor;
            half  _Chatoyance;
            float _ChatoScale;
            float _ChatoWarp;
            half  _ChatoPower;
            half4 _InnerColor;
            half  _InnerGlow;
            half4 _RimColor;
            half  _RimGlow;
            half  _FresnelPower;
            float _Depth;
            half  _EnvStrength;
            half  _BevelWidth;
            half  _BevelTilt;
            half  _EngraveDepth;
            half4 _FractureColor;
            half  _FractureAmount;
            half  _FractureWidth;
            half  _Fire;
            half  _MossFilament;
            half  _DecorMode;
            half4 _DecorColor;
            half  _DecorStrength;
            half  _DecorSize;
        CBUFFER_END
        #include "Assets/Shaders/DiceFX/DiceFXLib.hlsl"

        // ---------------------------------------------------------------- engraved decoration (q = face coordinates in inradii)

        half DecorAA(float d, float w)
        {
            return (half)(1.0 - smoothstep(-w, w, d));
        }

        // A sun with twelve rays, a thin ring and eight small dots: the tarot look.
        half DecorTarot(float2 q, float w)
        {
            float r = length(q);
            float a = atan2(q.y, q.x);
            float tri = 1.0 - abs(2.0 * frac(a * 12.0 / TWO_PI) - 1.0);
            half sun = DecorAA(r - 0.2, w);
            half rays = DecorAA(max(0.26 - r, r - (0.26 + 0.3 * tri * tri)), w);
            half ring = DecorAA(abs(r - 0.8) - 0.018, w);
            float ca = (floor(a * 8.0 / TWO_PI) + 0.5) * TWO_PI / 8.0;
            float2 dot0 = 0.66 * float2(cos(ca), sin(ca));
            half dots = DecorAA(length(q - dot0) - 0.04, w);
            return saturate(sun + rays + ring + dots);
        }

        // A compass rose: two rings, 24 ticks, a long four-point star over a short diagonal one.
        half DecorCompass(float2 q, float w)
        {
            float r = length(q);
            float a = atan2(q.y, q.x);
            half rings = saturate(DecorAA(abs(r - 0.84) - 0.016, w) + DecorAA(abs(r - 0.64) - 0.01, w));
            float c = frac(a * 24.0 / TWO_PI) - 0.5;
            float tickD = max(abs(c) * r * TWO_PI / 24.0 - 0.012, max(0.66 - r, r - 0.8));
            half ticks = DecorAA(tickD, w);
            float d1 = (abs(q.x) / 0.78 + abs(q.y) / 0.1 - 1.0) * 0.1;
            float d2 = (abs(q.y) / 0.78 + abs(q.x) / 0.1 - 1.0) * 0.1;
            float2 p = float2(q.x - q.y, q.x + q.y) * 0.7071;
            float d3 = (abs(p.x) / 0.45 + abs(p.y) / 0.08 - 1.0) * 0.08;
            float d4 = (abs(p.y) / 0.45 + abs(p.x) / 0.08 - 1.0) * 0.08;
            half star = DecorAA(min(min(d1, d2), min(d3, d4)), w);
            half hub = DecorAA(abs(r - 0.07) - 0.012, w);
            return saturate(rings + ticks + star + hub);
        }

        // Night tarot: a laurel wreath under the numeral, a small sun above it, two crescent moons, two stars and a ring of dots.
        half DecorNight(float2 q, float w)
        {
            float r = length(q);
            float a = atan2(q.y, q.x);
            const float lo = -2.9;
            const float hi = -0.24;
            half inArc = (half)(step(lo, a) * step(a, hi));
            half d = DecorAA(abs(r - 0.7) - 0.012, w) * inArc;                     // stem

            float u = (a - lo) / (hi - lo) * 9.0;                                   // nine leaf cells along the arc
            float cell = floor(u);
            float side = (fmod(cell, 2.0) < 1.0) ? 1.0 : -1.0;                      // leaves alternate inside / outside the stem
            float2 lp = float2((frac(u) - 0.5) * ((hi - lo) / 9.0) * 0.7, r - (0.7 + side * 0.07));
            lp.y += lp.x * side * 0.45;                                             // lean outward along the stem
            float leafD = (length(lp / float2(0.09, 0.03)) - 1.0) * 0.03;
            d = max(d, DecorAA(leafD, w) * inArc * (half)step(0.0, cell) * (half)step(cell, 8.5));

            float2 sp = q - float2(0.0, 0.7);                                       // the small sun
            float sr = length(sp);
            float sa = atan2(sp.y, sp.x);
            float tri = 1.0 - abs(2.0 * frac(sa * 8.0 / TWO_PI) - 1.0);
            d = max(d, DecorAA(sr - 0.065, w));
            d = max(d, DecorAA(max(0.085 - sr, sr - (0.085 + 0.1 * tri * tri)), w));

            for (int k = 0; k < 2; k++)                                             // crescent moons, mirrored
            {
                float sgn = k == 0 ? -1.0 : 1.0;
                float2 mp = q - float2(0.58 * sgn, 0.42);
                float moonD = max(length(mp) - 0.1, 0.085 - length(mp - float2(0.05 * sgn, 0.02)));
                d = max(d, DecorAA(moonD, w));
                float2 st = q - float2(0.84 * sgn, -0.02);                          // four-point stars
                float starD = (sqrt(abs(st.x)) + sqrt(abs(st.y)) - 0.34) * 0.08;
                d = max(d, DecorAA(starD, w));
            }

            float ca = (floor(a * 26.0 / TWO_PI) + 0.5) * TWO_PI / 26.0;           // dotted ring
            float2 dp = 0.93 * float2(cos(ca), sin(ca));
            d = max(d, DecorAA(length(q - dp) - 0.016, w) * (half)step(0.88, r));
            return saturate(d);
        }

        // one of the five photo colours (mustard, brick red, moss green, mauve, teal) for h in 0..1
        half3 DiceTintOf(float h)
        {
            half k = floor(saturate((half)h) * 4.999h);
            return k < 1.0h ? _TintA.rgb : (k < 2.0h ? _TintB.rgb : (k < 3.0h ? _TintC.rgb : (k < 4.0h ? _TintD.rgb : _TintE.rgb)));
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
                half fres = pow(1.0h - saturate((half)dot(nWS, vWS)), _FresnelPower);
                float2 view = float2(dot(vWS, tWS), dot(vWS, bWS));

                float2 uv = i.faceUV * _NoiseScale;
                half4 nA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv);
                half4 nB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, (i.faceUV - view * _Depth) * (_NoiseScale * 2.3) + 0.37);

                // colour ramp: bands (a wobbling line pattern) blended toward cloud
                float2 dir = normalize(_BandDir.xy + float2(1e-4, 0.0));
                float s = dot(i.faceUV, dir) * _BandScale + ((float)nA.g - 0.5) * _BandWarp + ((float)nB.b - 0.5) * _BandWarp * 0.5 + i.facet * 5.0;
                half t = (half)abs(frac(s) * 2.0 - 1.0);
                t = lerp(t, nA.g, _Cloudiness);
                half w = _BandSoft;
                half3 c = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.25h - w, 0.25h + w, t));
                c = lerp(c, _ColorC.rgb, smoothstep(0.65h - w, 0.65h + w, t));

                // per-die tint (Indian Agate): the seed picks two of five deep colours and each die blends from one to the other across its
                // faces (every facet starts at a different point of the gradient), so no two dice look alike; the ramp's brightness is kept
                half3 tintA = DiceTintOf(frac(_Seed * 0.7311 + 0.13));
                half3 tintB = DiceTintOf(frac(_Seed * 0.3917 + 0.61));
                half facetHash = (half)frac(sin(i.facet * 12.9898 + _Seed * 3.1) * 43758.5453);
                half g = saturate(0.5h + ((half)dot(i.faceUV, float2(0.55, 0.83)) * 0.35h) + (facetHash - 0.5h) * 0.7h + ((half)nA.g - 0.5h) * 0.5h);
                half3 tint = lerp(tintA, tintB, smoothstep(0.15h, 0.85h, g) * _TintMix);
                half lum = dot(c, half3(0.3h, 0.59h, 0.11h));
                c = lerp(c, tint * (0.6h + lum * 1.3h), _PerDie);

                // veins along the noise cell edges
                half vein = (1.0h - smoothstep(0.0h, _VeinWidth, nA.r)) * _VeinAmount;
                c = lerp(c, _VeinColor.rgb, vein);

                // moss: soft green cloud with a darker mottling
                half filament = (1.0h - smoothstep(0.0h, 0.09h, (half)nA.r + ((half)nB.g - 0.5h) * 0.2h)) * _MossFilament;
                half moss = max(smoothstep(_MossLow, _MossHigh, nB.g * 0.6h + nA.b * 0.4h) * _MossAmount, filament);
                c = lerp(c, _MossColor.rgb * (0.7h + 0.6h * nA.a), moss);

                // rutile needles: two stretched cell-edge patterns crossing at an angle
                float2 uvN1 = float2(uv.x * 0.3 + uv.y * 0.1, uv.y * 3.2 - uv.x * 0.2) + 0.21;
                float2 uvN2 = float2(uv.x * 0.2 - uv.y * 0.35, uv.y * 0.9 + uv.x * 2.8) + 0.63;
                half needle1 = 1.0h - smoothstep(0.0h, _NeedleWidth, SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvN1).r);
                half needle2 = 1.0h - smoothstep(0.0h, _NeedleWidth, SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvN2).r);
                half needles = saturate(needle1 + needle2 * 0.8h) * _NeedleAmount;
                c = lerp(c, _NeedleColor.rgb, needles);

                // chatoyance: a bright band that slides across the fibres as the view angle changes
                float fibre = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(uv.x * 0.2 + 0.4, uv.y * 7.0)).g;
                float s2 = dot(view, dir) * _ChatoScale + ((float)nA.g - 0.5) * _ChatoWarp + i.facet * 3.0;
                half cat = pow(saturate(1.0h - (half)abs(frac(s2) * 2.0 - 1.0)), _ChatoPower);
                c *= 1.0h + cat * _Chatoyance * (0.4h + 0.8h * (half)fibre);

                // internal fractures: a second, deeper layer that slides against the surface as the die turns
                float2 uvF = (i.faceUV - view * (_Depth * 2.4)) * (_NoiseScale * 1.7) + 0.71;
                half4 nF = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uvF);
                half fracture = (1.0h - smoothstep(0.0h, _FractureWidth, (half)nF.r + ((half)nF.g - 0.5h) * 0.07h)) * _FractureAmount;

                half3 emission = _SheenColor.rgb * (cat * _Chatoyance * 0.3h);
                // per-die stone: the deep colour also glows a little from within, so the dice read as lit stone, never as black
                emission += tint * (0.6h + lum * 1.3h) * (_PerDie * 0.55h);
                emission += _InnerColor.rgb * (_InnerGlow * (0.35h + 0.65h * nB.g) * (1.0h - fres * 0.5h)) * (1.0h - needles);
                emission += _RimColor.rgb * (fres * _RimGlow * (1.0h + _Flash));
                emission += _FractureColor.rgb * (fracture * (0.35h + 0.65h * (1.0h - fres)));

                // engraved decoration
                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);
                half decor = 0.0h;
                float valid;
                float2 q = DiceFaceLocal(i, valid) / _DecorSize;
                float aa = max(fwidth(q.x) + fwidth(q.y), 1e-4);
                if (_DecorMode > 0.5h && valid > 0.5)
                    decor = (_DecorMode < 1.5h ? DecorTarot(q, aa) : (_DecorMode < 2.5h ? DecorCompass(q, aa) : DecorNight(q, aa))) * clear * _DecorStrength;

                DiceSurface surf;
                surf.albedo = lerp(c, _DecorColor.rgb, decor);
                surf.emission = lerp(emission, _DecorColor.rgb * 0.25h, decor);
                surf.metallic = lerp(_Metallic, 0.9h, decor);
                surf.smoothness = lerp(_Smoothness, 0.6h, decor);
                DiceApplyNumerals(surf, sdf);

                // photoreal: rounded edges, engraved numerals (a recess you can see in the light), studio reflections
                float3 nShade = DiceBevelNormal(i, nWS, tWS, bWS, _BevelWidth * i.faceCtr.z, _BevelTilt);
                if (_EngraveDepth > 0.001h)
                {
                    float hEdge = 0.5 - _NumeralThick;
                    float height = -smoothstep(hEdge - 0.08, hEdge + 0.02, (float)sdf) * (_EngraveDepth * 0.01) - (float)decor * (_EngraveDepth * 0.006);
                    nShade = DiceBump(nShade, i.positionWS, height, 1.0);
                }
                surf.emission += DiceEnvSpecular(surf, nShade, vWS, _EnvStrength);

                // facet fire: every facet has its own glint direction, so the facets flash one after another as the die rolls
                if (_Fire > 0.001h)
                {
                    float fh1 = frac(sin(i.facet * 91.7) * 43758.5453);
                    float fh2 = frac(sin(i.facet * 37.3 + 1.7) * 24634.6345);
                    float3 lh = normalize(float3(fh1 - 0.5, 0.25 + fh2, frac(fh1 * 7.13 + fh2 * 3.7) - 0.5));
                    float3 rv = reflect(-vWS, nShade);
                    half fire = (half)pow(saturate(dot(rv, lh)), 18.0) * _Fire;
                    surf.emission += lerp(_RimColor.rgb, half3(1.0h, 1.0h, 1.0h), 0.5h) * fire * (1.0h - decor);
                }
                return DiceShade(i, surf, nShade);
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
