// Camera-facing quad with procedural light rays and a soft core, drawn additively. Used for the Prismatic nat 20 flare.
// The quad's scale is the flare's size; DieFxDriver grows it from nothing, so it also fades in and out.
// Cost: no texture, ~30 ALU, one quad.
Shader "DiceThrower/DiceFX/Rays"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 6)) = 1.6
        _RayCount ("Ray count", Float) = 14
        _RaySharp ("Ray sharpness", Range(1, 24)) = 7
        _Spin ("Spin speed", Float) = 0.5
        _Wobble ("Ray wobble", Range(0, 3)) = 1.2
        _CoreGlow ("Core glow", Range(0, 3)) = 0.7
        _Rainbow ("Rainbow amount", Range(0, 1)) = 0.9
        _RainbowSpeed ("Rainbow speed", Float) = 0.25
        _Tint ("Tint (white = pure rainbow)", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Rays"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                float _RayCount;
                float _RaySharp;
                float _Spin;
                float _Wobble;
                half _CoreGlow;
                half _Rainbow;
                float _RainbowSpeed;
                half4 _Tint;
            CBUFFER_END

            float _DiceFxUiPreview; // global: 1 while DicePreviewRig draws a UI picture (straight alpha), 0 in the scene

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float sx = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float sy = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 world = center + right * (i.positionOS.x * sx) + up * (i.positionOS.y * sy);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = i.uv * 2.0 - 1.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv);
                float a = atan2(i.uv.y, i.uv.x);
                float t = _Time.y;

                float wob = sin(a * 3.0 + t * 1.3) * _Wobble;
                float ray = pow(saturate(0.5 + 0.5 * sin(a * _RayCount + t * _Spin * 6.2832 + wob)), _RaySharp);
                float fall = saturate(1.0 - r);
                fall *= fall;
                float core = exp(-r * 7.0) * _CoreGlow;

                float hue = frac(a / 6.2832 + r * 0.6 + t * _RainbowSpeed);
                half3 rainbow = 0.5h + 0.5h * cos(6.2832 * (hue + half3(0.0h, 0.33h, 0.67h)));
                half3 color = lerp(half3(1, 1, 1), rainbow, _Rainbow) * _Tint.rgb;

                half amount = (half)(ray * fall + core * fall) * _Intensity;
                half alpha = saturate(amount);
                // a UI picture is blended as straight alpha: it needs the full colour with the amount in alpha; the scene wants colour * amount
                half3 rgb = _DiceFxUiPreview > 0.5 ? color : color * amount;
                if (_DiceFxUiPreview <= 0.5) alpha = saturate(max(rgb.r, max(rgb.g, rgb.b)));
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
