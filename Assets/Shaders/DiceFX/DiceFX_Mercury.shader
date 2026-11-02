// Mercury dice: liquid chrome. A fake studio environment (sky / horizon / ground gradient + two soft boxes) is reflected
// through a slowly rippling normal; shaking the die (Energy) makes the ripples stronger. No cubemap, no probe needed.
// Cost: 1 numeral SDF sample, ~60 ALU, 0 noise samples.
Shader "DiceThrower/DiceFX/Mercury"
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
        _Smoothness ("Smoothness", Range(0, 1)) = 0.7
        _Metallic ("Metallic", Range(0, 1)) = 0.9
        [Header(Driven by DieFxDriver)]
        _Energy ("Energy 0..1 (die speed)", Range(0, 1)) = 0
        _Flash ("Flash 0..1 (impact)", Range(0, 1)) = 0
        _Highlight ("Nat 20 effect 0..1", Range(0, 1)) = 0
        _Gloom ("Nat 1 effect 0..1", Range(0, 1)) = 0
        _Seed ("Per-die seed", Float) = 0
        [Header(Chrome)]
        _Tint ("Metal tint", Color) = (0.9, 0.92, 0.96, 1)
        _SkyColor ("Env sky", Color) = (0.75, 0.85, 1, 1)
        _HorizonColor ("Env horizon", Color) = (0.95, 0.95, 0.95, 1)
        _GroundColor ("Env ground", Color) = (0.08, 0.08, 0.1, 1)
        _EnvStrength ("Env strength", Range(0, 2)) = 1
        _SoftboxStrength ("Soft box strength", Range(0, 4)) = 1.6
        [Header(Ripple)]
        _RippleScale ("Ripple scale", Float) = 2.2
        _RippleSpeed ("Ripple speed", Float) = 0.9
        _RippleStrength ("Ripple strength", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Assets/Shaders/DiceFX/DiceFX.hlsl"
        CBUFFER_START(UnityPerMaterial)
            DICEFX_COMMON_PROPERTIES
            half4 _Tint;
            half4 _SkyColor;
            half4 _HorizonColor;
            half4 _GroundColor;
            half  _EnvStrength;
            half  _SoftboxStrength;
            float _RippleScale;
            float _RippleSpeed;
            half  _RippleStrength;
        CBUFFER_END
        #include "Assets/Shaders/DiceFX/DiceFXLib.hlsl"

        half3 Studio(float3 r)
        {
            half h = (half)r.y;
            half3 col = lerp(_GroundColor.rgb, _HorizonColor.rgb, smoothstep(-0.35h, 0.05h, h));
            col = lerp(col, _SkyColor.rgb, smoothstep(0.05h, 0.7h, h));
            half box1 = pow(saturate((half)dot(r, normalize(float3(0.45, 0.8, 0.35)))), 24.0h);
            half box2 = pow(saturate((half)dot(r, normalize(float3(-0.7, 0.35, -0.5)))), 40.0h);
            return col + (box1 + box2 * 0.6h) * _SoftboxStrength;
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
                float3 vWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // two crossing sine fields, one per face-plane axis; the normal leans along them
                float2 uv = i.faceUV * _RippleScale;
                float t = _Time.y * _RippleSpeed;
                float a = sin(uv.x * 3.1 + t) + sin(uv.y * 2.3 - t * 1.3 + uv.x * 0.7);
                float b = sin(uv.y * 3.7 + t * 0.8) + sin(uv.x * 2.9 - t + uv.y * 0.5);
                float3 bitangentWS = cross(nWS, i.tangentWS);
                float amp = _RippleStrength * (1.0 + _Energy * 2.0 + _Flash);
                float3 nRip = normalize(nWS + (i.tangentWS * a + bitangentWS * b) * amp);

                half3 env = Studio(reflect(-vWS, nRip));
                half sdf = DiceNumeralSDF(i.uv);
                half clear = DiceNumeralClear(sdf);

                DiceSurface s;
                s.albedo = _Tint.rgb * 0.15h;
                s.emission = env * _Tint.rgb * (_EnvStrength * clear) + _Tint.rgb * (_Highlight * 0.5h);
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
