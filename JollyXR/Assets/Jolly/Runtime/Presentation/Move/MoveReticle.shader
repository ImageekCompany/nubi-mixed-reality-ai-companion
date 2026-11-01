// A thin ring on the floor where a tap sends the character (ADR-0044): a light core with a dark rim, so it reads on a
// pale floor and a dark one in passthrough without relying on colour alone. Drawn on a quad; _Radius is the ring's
// centre radius in uv units (0.5 = the quad edge). Single-pass instanced stereo safe for Quest multiview.
Shader "Jolly/MoveReticle"
{
    Properties
    {
        _Color ("Ring", Color) = (1, 1, 1, 0.9)
        _RimColor ("Rim", Color) = (0, 0, 0, 0.45)
        _Radius ("Radius (uv)", Range(0.05, 0.5)) = 0.36
        _Width ("Width (uv)", Range(0.005, 0.2)) = 0.05
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        // Separate alpha blend keeps destination alpha meaningful for passthrough composition.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "MoveReticle"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _RimColor;
                float _Radius;
                float _Width;
                half _Alpha;
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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float d = abs(length(input.uv - 0.5) - _Radius);
                float aa = fwidth(d) + 1e-4;
                half core = 1.0 - smoothstep(_Width * 0.5 - aa, _Width * 0.5 + aa, d);
                half rim = 1.0 - smoothstep(_Width - aa, _Width + aa, d);
                half4 c = lerp(half4(_RimColor.rgb, _RimColor.a * rim), _Color, core);
                return half4(c.rgb, c.a * _Alpha);
            }
            ENDHLSL
        }
    }
}
