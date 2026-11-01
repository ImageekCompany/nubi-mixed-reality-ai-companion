// Contact shadow under Nubi: a radial falloff computed from the quad's UVs (no texture, no realtime shadows).
// In passthrough the eye buffer's alpha decides how much of the camera image shows through, so alpha is written
// as plain coverage (Blend One OneMinusSrcAlpha on alpha): the floor in passthrough darkens by exactly _Opacity.
// Real objects in front of the shadow hide it like they hide Nubi (ADR-0023); ContactShadow drives _Opacity and the
// quad's scale from how high Nubi's feet are.
Shader "Jolly/Character/BlobShadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0.08, 0.04, 0.03, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.45
        _Softness ("Softness", Range(0.05, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ XR_HARD_OCCLUSION XR_SOFT_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Jolly/Content/Rendering/Shaders/EnvironmentOcclusion.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                half _Softness;
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
                float2 centered : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.centered = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half r = length(input.centered);
                half falloff = 1.0h - smoothstep(1.0h - _Softness, 1.0h, r);
                half alpha = _Opacity * falloff * falloff * EnvironmentVisibility(input.positionWS);
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
