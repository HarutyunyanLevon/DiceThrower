// Soft particle for the rain streaks: alpha-blended, no texture, fades toward the quad's edges, tinted by the particle colour.
// Cost: ~10 ALU per pixel.
Shader "DiceThrower/DiceFX/Particle"
{
    Properties
    {
        _Softness ("Edge softness", Range(0.5, 4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Particle"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv * 2.0 - 1.0;
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half soft = saturate(1.0h - abs(i.uv.x));
                soft *= saturate(1.0h - abs(i.uv.y));
                soft = pow(soft, _Softness);
                return half4(i.color.rgb, i.color.a * soft);
            }
            ENDHLSL
        }
    }
}
