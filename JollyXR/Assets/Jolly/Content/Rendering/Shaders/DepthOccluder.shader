// Stand-in for a real object (SceneOccluders, ADR-0024). Drawn after Nubi and its shadow, it depth-tests against
// them and writes the clear colour (0,0,0,0) wherever it is nearer, so the passthrough underlay, i.e. the real
// furniture, shows there. It writes no depth and comes before the Transparent queue, so world-space UI (TMP and UI
// shaders: Queue Transparent, ZTest [unity_GUIZTestMode] = LEqual on a world-space canvas) still tests only against
// Nubi, as with environment depth. A depth-only box in front of Nubi's queue would hide that UI too.
Shader "Jolly/XR/DepthOccluder"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-1" "IgnoreProjector" = "True" }

        Pass
        {
            Name "DepthOccluder"
            Tags { "LightMode" = "UniversalForward" }
            Blend Off
            ZWrite Off
            ZTest LEqual

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
