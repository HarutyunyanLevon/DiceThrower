// Beholder die (liquid core): a clear glassy shell with fine bloodshot veins on its surface, and a real eyeball floating inside it.
// The eyeball is a sphere that the fragment stage ray-traces through the glass (refracted view ray, sphere hit, iris + slit pupil + eye white),
// so it has true depth: it slides against the veins as the die turns and as the camera moves. DieFxDriver sets where the eye is (_EyeCenter, with a
// spring so it sloshes), where it looks (_EyeDir) and how it is rolled (_EyeUp); when the die settles the pupil turns up toward the camera.
// A few tiny bubbles drift around the eye. With _EyeRadius = 0 (no driver) the die is just clear resin with veins.
// Glow in the dark: the global float _DiceFxDark (1 in a theme that is a dark place, set by ThemeSceneHost from ThemeScene.darkGlow, 0 elsewhere)
// makes the iris blaze and the whole resin glow cyan, the veins stay dark against it.
// Cost: 2 noise samples + 1 numeral SDF sample, ~110 ALU.
Shader "DiceThrower/DiceFX/Beholder"
{
    Properties
    {
        [Header(Numerals (signed distance field))]
        [NoScaleOffset] _NumeralMap ("Numeral SDF (R)", 2D) = "black" {}
        _NumeralColor ("Numeral colour", Color) = (1, 0.8, 0.3, 1)
        _NumeralGlow ("Numeral glow", Range(0, 4)) = 0.3
        _NumeralThick ("Numeral thickness (SDF units)", Range(-0.1, 0.2)) = 0.06
        _NumeralBevel ("Numeral engraved bevel width", Range(0.02, 0.5)) = 0.22
        _NumeralMetal ("Numeral metallic", Range(0, 1)) = 0.9
        _NumeralSmoothness ("Numeral smoothness", Range(0, 1)) = 0.6
        _NumeralHalo ("Keep effects this far from numerals (SDF units)", Range(0.1, 0.5)) = 0.3
        [Header(Surface)]
        [NoScaleOffset] _NoiseMap ("Noise pack (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale (per object unit)", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.97
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        _EyeCenter ("Eye centre (world)", Vector) = (0, 0, 0, 0)
        _EyeDir ("Where the pupil looks (world)", Vector) = (0, 1, 0, 0)
        _EyeUp ("Eye roll: the slit runs along this (world)", Vector) = (0, 0, 1, 0)
        _EyeRadius ("Eye radius (world, 0 = no eye)", Float) = 0
        [Header(Shell)]
        _ShellColor ("Clear resin", Color) = (0.62, 0.74, 0.8, 1)
        _VeinColor ("Vein colour", Color) = (0.55, 0.07, 0.07, 1)
        _VeinAmount ("Vein amount", Range(0, 1)) = 0.9
        _VeinWidth ("Vein width", Range(0.01, 0.2)) = 0.06
        [Header(Eye)]
        _EyeVein ("Bloodshot veins on the eye white", Range(0, 1)) = 0.3
        _PupilShape ("Pupil: 0 vertical slit, 1 horizontal (goat), 2 round", Float) = 0
        _ScarAmount ("Scar across the eye", Range(0, 1)) = 0
        _FibreContrast ("Iris fibre contrast", Range(0, 1)) = 0.5
        _ScleraColor ("Eye white", Color) = (0.9, 0.86, 0.86, 1)
        _IrisRadius ("Iris radius (0..1 across the eyeball)", Range(0.3, 1)) = 0.55
        _IrisInner ("Iris colour near the pupil", Color) = (0.25, 0.85, 1, 1)
        _IrisOuter ("Iris colour at the rim", Color) = (0.02, 0.2, 0.95, 1)
        _FibreScale ("Iris fibre count (whole number)", Float) = 14
        _PupilWidth ("Slit width (of the iris)", Range(0.02, 0.6)) = 0.2
        _PupilHeight ("Slit height (of the iris)", Range(0.2, 1)) = 0.85
        _EyeGlow ("Eye brightness through the glass", Range(0, 4)) = 1.5
        [HideInInspector] _IrisDepth ("(old) iris depth", Range(0, 0.8)) = 0
        [Header(Photoreal)]
        [NoScaleOffset] _EnvMap ("Studio reflection cubemap", Cube) = "" {}
        _EnvStrength ("Reflection strength (0 = off)", Range(0, 3)) = 0
        _BevelWidth ("Rounded edge width (fraction of the face, 0 = sharp)", Range(0, 0.6)) = 0
        _BevelTilt ("Rounded edge roundness", Range(0, 1.5)) = 0.7
        _EngraveDepth ("Numeral engraving depth (0 = flat paint)", Range(0, 4)) = 0
        [Header(Glow in the dark)]
        _SelfLit ("Daylight self-lighting (resin lets light through)", Range(0, 1)) = 0.25
        _GlowColor ("Glow colour", Color) = (0.1, 0.95, 0.85, 1)
        _GlowStrength ("Glow strength", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        float _DiceFxDark;   // global, set from code (not a material property)
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            float4 _EyeCenter;
            float4 _EyeDir;
            float4 _EyeUp;
            float  _EyeRadius;
            half4 _ShellColor;
            half4 _VeinColor;
            half  _VeinAmount;
            half  _VeinWidth;
            half4 _ScleraColor;
            half  _EyeVein;
            half  _PupilShape;
            half  _ScarAmount;
            half  _FibreContrast;
            half  _IrisRadius;
            half4 _IrisInner;
            half4 _IrisOuter;
            float _FibreScale;
            half  _PupilWidth;
            half  _PupilHeight;
            half  _EyeGlow;
            half  _SelfLit;
            half  _IrisDepth;
            half  _EnvStrength;
            half  _BevelWidth;
            half  _BevelTilt;
            half  _EngraveDepth;
            half4 _GlowColor;
            half  _GlowStrength;
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

            // ray (origin ro, direction rd) against a sphere: x = 1 when hit in front, y = distance
            float2 RaySphere(float3 ro, float3 rd, float3 c, float r)
            {
                float3 oc = ro - c;
                float b = dot(oc, rd);
                float disc = b * b - (dot(oc, oc) - r * r);
                float t = -b - sqrt(max(disc, 0.0));
                return float2(step(0.0, disc) * step(0.0, t), t);
            }

            half4 Frag(DiceVaryings i) : SV_Target
            {
                float3 nWS = normalize(i.normalWS);
                float3 tWS = normalize(i.tangentWS);
                float3 bWS = cross(nWS, tWS);
                float3 vWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fres = pow(1.0h - saturate((half)dot(nWS, vWS)), 3.0h);
                float valid;
                float2 q0 = DiceFaceLocal(i, valid);
                float r0 = length(q0);

                half4 nA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.faceUV * _NoiseScale);
                Light mainLight = GetMainLight();

                half vein = 0.0h;     // the shell has no veins any more: the bloodshot veins live on the eyeball

                // ---- the eyeball inside, seen through the glass
                half eyeHit = 0.0h;
                half3 eyeEm = 0.0h;
                half3 bubbleEm = 0.0h;
                half irisAmount = 0.0h;
                if (_EyeRadius > 1e-5)
                {
                    float3 rd = refract(-vWS, nWS, 0.69);    // air into resin
                    float2 hit = RaySphere(i.positionWS, rd, _EyeCenter.xyz, _EyeRadius);
                    float3 p = i.positionWS + rd * hit.y;
                    float3 n = (p - _EyeCenter.xyz) / _EyeRadius;
                    float3 D = normalize(_EyeDir.xyz);
                    float3 U = normalize(_EyeUp.xyz);
                    float3 Rr = cross(D, U);
                    float2 e = float2(dot(n, Rr), dot(n, U));      // position on the eyeball seen from the pupil's axis
                    float cosA = dot(n, D);
                    float er = length(e);
                    float eaa = max(fwidth(e.x) + fwidth(e.y), 2e-3);
                    half front = step(0.0, cosA);

                    float ang = atan2(e.y, e.x) / TWO_PI + 0.5;
                    half fibre = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, float2(ang * _FibreScale, er * 0.9 + _Seed * 0.1), 1.0).g;
                    half irisT = saturate((half)(er / _IrisRadius));
                    half3 iris = lerp(_IrisInner.rgb, _IrisOuter.rgb, smoothstep(0.1h, 1.0h, irisT));
                    iris *= (1.0h - _FibreContrast) + _FibreContrast * (0.15h + 1.7h * fibre);
                    iris += _IrisInner.rgb * (smoothstep(0.55h, 0.15h, irisT) * 0.5h);
                    iris = lerp(iris, _IrisOuter.rgb * 0.2h, smoothstep(0.8h, 1.0h, irisT));   // dark ring at the rim of the iris
                    irisAmount = (half)(1.0 - smoothstep(_IrisRadius - eaa, _IrisRadius + eaa, er)) * front;

                    // pupil: vertical slit (pointed ends), horizontal goat bar, or round
                    float pw = _PupilWidth * _IrisRadius;
                    float ph = _PupilHeight * _IrisRadius;
                    float slitQx = e.x / pw, slitQy = e.y / ph;
                    float slitD = (abs(slitQx) + slitQy * slitQy - 1.0) * pw;
                    float2 gq = float2(e.x / ph, e.y / pw);
                    float goatD = (pow(pow(abs(gq.x), 5.0) + pow(abs(gq.y), 5.0), 0.2) - 1.0) * pw;
                    float roundD = er - pw * 1.6;
                    float pupilD = _PupilShape < 0.5 ? slitD : (_PupilShape < 1.5 ? goatD : roundD);
                    half pupil = (half)(1.0 - smoothstep(-eaa, eaa, pupilD)) * front;

                    half3 sclera = _ScleraColor.rgb * (0.5h + 0.5h * saturate((half)dot(n, mainLight.direction) * 0.5h + 0.5h));
                    // bloodshot veins creeping in from the rim of the eyeball (a little, on the white only)
                    half veinLine = 1.0h - smoothstep(0.03h, 0.1h, SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, e * 1.7 + _Seed * 0.05, 0.0).r);
                    half veinReach = smoothstep(_IrisRadius * 1.05, 1.0, (float)er) * (0.5h + 0.5h * veinLine);
                    sclera = lerp(sclera, _VeinColor.rgb, saturate(veinLine * veinReach * 1.5h) * _EyeVein * front);
                    half3 eyeCol = lerp(sclera, iris, irisAmount);
                    eyeCol = lerp(eyeCol, half3(0.005h, 0.01h, 0.03h), pupil);

                    // scar: a pale ridge with dark flanks, slanting across the iris and the white
                    if (_ScarAmount > 0.001h)
                    {
                        float2 sd = normalize(float2(0.55, 0.83));
                        float along = dot(e, sd);
                        float across = dot(e, float2(-sd.y, sd.x)) - 0.18 + sin(along * 26.0) * 0.012;
                        half ridge = (half)((1.0 - smoothstep(0.012, 0.03, abs(across))) * (1.0 - smoothstep(0.55, 0.78, abs(along))));
                        half flank = (half)((1.0 - smoothstep(0.03, 0.06, abs(abs(across) - 0.035))) * (1.0 - smoothstep(0.55, 0.78, abs(along))));
                        eyeCol = lerp(eyeCol, eyeCol * 0.35h, flank * _ScarAmount * front);
                        eyeCol = lerp(eyeCol, half3(0.95h, 0.72h, 0.66h), ridge * _ScarAmount * front);
                    }

                    float3 rf = reflect(rd, n);
                    half wet = (half)pow(saturate(dot(rf, mainLight.direction)), 90.0);     // wet highlight on the eyeball
                    half3 em = eyeCol * (_EyeGlow * (0.75h + 0.7h * irisAmount * (1.0h - pupil))) + wet * 0.9h;

                    // sphere edge falls off into shadow so it reads round
                    em *= 0.55h + 0.45h * saturate((half)dot(n, -rd));
                    eyeHit = (half)hit.x;
                    eyeEm = em * eyeHit;

                    // tiny bubbles drifting around the eye
                    [unroll] for (int k = 0; k < 3; k++)
                    {
                        float fk = (float)k;
                        float3 off = float3(sin(fk * 2.4 + _Seed + _Time.y * 0.3), cos(fk * 3.1 + _Seed * 0.7 + _Time.y * 0.25), sin(fk * 1.3 + _Seed * 1.9 + _Time.y * 0.2));
                        float3 bc = _EyeCenter.xyz + off * (_EyeRadius * (1.25 + fk * 0.1));
                        float br = _EyeRadius * (0.06 + fk * 0.03);
                        float2 bh = RaySphere(i.positionWS, rd, bc, br);
                        float3 bn = normalize(i.positionWS + rd * bh.y - bc);
                        half rim = (half)pow(1.0 - saturate(dot(bn, -rd)), 2.0);
                        half glint = (half)pow(saturate(dot(reflect(rd, bn), mainLight.direction)), 40.0);
                        bubbleEm += (half)bh.x * (rim * 0.35h + glint * 1.2h);
                    }
                }

                half darkness = (half)saturate(_DiceFxDark);
                half3 shell = _ShellColor.rgb;
                half3 shellAlbedo = lerp(shell, _VeinColor.rgb, vein);
                // behind the glass the eye replaces the resin colour, the veins stay on top of it
                half seeEye = eyeHit * (1.0h - vein);
                shellAlbedo = lerp(shellAlbedo, shellAlbedo * 0.15h, seeEye);

                // glow in the dark: the resin glows cyan, the iris blazes, the veins stay dark
                half glowMask = (1.0h - vein * 0.9h) * (0.85h + 0.3h * nA.g);
                half3 glow = _GlowColor.rgb * (_GlowStrength * 0.45h * glowMask * (1.0h - seeEye * 0.5h));
                half3 eyeFinal = eyeEm * (1.0h + darkness * (0.5h + 1.4h * irisAmount)) * (1.0h - vein) * (1.0h - fres * 0.5h);

                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);

                DiceSurface s;
                s.albedo = lerp(shellAlbedo, shellAlbedo * 0.35h, darkness);
                s.emission = (glow * darkness + shell * (_SelfLit * (1.0h - darkness)) * (1.0h - seeEye) * (1.0h - vein) + eyeFinal + bubbleEm) * clear;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                DiceApplyNumerals(s, sdf);
                // photoreal: rounded edges, engraved numerals, studio reflections
                float3 nShade = DiceBevelNormal(i, nWS, tWS, bWS, _BevelWidth * i.faceCtr.z, _BevelTilt);
                if (_EngraveDepth > 0.001h)
                {
                    float hEdge = 0.5 - _NumeralThick;
                    float height = -smoothstep(hEdge - 0.08, hEdge + 0.02, (float)sdf) * (_EngraveDepth * 0.01);
                    nShade = DiceBump(nShade, i.positionWS, height, 1.0);
                }
                s.emission += DiceEnvSpecular(s, nShade, vWS, _EnvStrength);
                half4 col = DiceShade(i, s, nShade);
                // bloodshot veins are painted over the lit result, so the pale eyeball behind the glass cannot wash them out
                half veinCover = saturate(vein * 1.6h) * clear * (1.0h - darkness * 0.6h) * 1.0h;
                half3 veinCol = _VeinColor.rgb * (0.5h + 0.5h * saturate((half)dot(nShade, mainLight.direction) * 0.5h + 0.5h));
                col.rgb = lerp(col.rgb, veinCol, veinCover);
                return col;
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
