// Depth-only stand-in for a real object (SceneOccluders, ADR-0023). It writes depth and no colour, one queue step
// before Nubi, so Nubi's fragments behind it fail the depth test and the eye buffer keeps the clear alpha of 0 there:
// the passthrough underlay, i.e. the real furniture, shows.
Shader "Jolly/XR/DepthOccluder"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-1" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DepthOccluder"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
