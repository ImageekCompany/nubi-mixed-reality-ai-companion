// Translucent fill plus a near-opaque inner outline of a box-local R8 mask, so the highlight does not rely on colour
// alone (architecture §11 Accessibility). Single-pass instanced stereo safe for Quest multiview.
Shader "Jolly/MaskOutline"
{
    Properties
    {
        [NoScaleOffset] _MaskTex ("Mask (R8)", 2D) = "black" {}
        _FillColor ("Fill", Color) = (0.25, 0.85, 1, 0.28)
        _OutlineColor ("Outline", Color) = (1, 1, 1, 0.95)
        _OutlineTexels ("Outline width (mask texels)", Range(1, 8)) = 3
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        // Separate alpha blend keeps destination alpha meaningful for passthrough composition.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "MaskOutline"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_TexelSize;
                half4 _FillColor;
                half4 _OutlineColor;
                float _OutlineTexels;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // Outside the box counts as background, so objects touching the box edge still get an outline there.
            half Mask(float2 uv)
            {
                half inBox = all(uv >= 0.0) && all(uv <= 1.0) ? 1.0 : 0.0;
                return SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).r * inBox;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 d = _MaskTex_TexelSize.xy * _OutlineTexels;
                half inside = Mask(input.uv);
                half eroded = min(min(min(Mask(input.uv + float2(d.x, 0)), Mask(input.uv - float2(d.x, 0))),
                                      min(Mask(input.uv + float2(0, d.y)), Mask(input.uv - float2(0, d.y)))),
                                  min(min(Mask(input.uv + d), Mask(input.uv - d)),
                                      min(Mask(input.uv + float2(d.x, -d.y)), Mask(input.uv + float2(-d.x, d.y)))));
                half outline = saturate(inside - eroded);
                half3 rgb = lerp(_FillColor.rgb, _OutlineColor.rgb, outline);
                half alpha = max(_FillColor.a * inside, _OutlineColor.a * outline) * _Fade;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
