// Shell fur for skinned characters on Quest (ADR-0045). One material is the skin: the opaque base layer. ShellFurRenderer
// appends N copies of it to the renderer, each with the _FUR_SHELL keyword and its own _ShellHeight, and Unity draws
// every material past the last submesh on that submesh again, from the vertices it skinned once.
// A shell pushes the skin out along the normal by height × length, sags it with gravity, combs it along the flow map
// and swings it with the root's inertia, then keeps only the strands of a tiling noise taller than that height.
// Shells are alpha-tested, never blended: a surviving strand writes alpha = environment visibility (1 unless a real
// object is in front), so the fur cannot thin the eye buffer's alpha and let passthrough show through the body.
// Everything a shell fragment needs except the strand noise is read per vertex (albedo, length, density, AO, lighting):
// a shell fragment is one texture read, one clip and the occlusion test.
// A strand survives shell h where its noise height > h^_StrandTaper: 1 is a cone (spiky), above 1 the strand keeps its
// width higher up (plush). _SkinStrands prints the same noise onto the base layer, so short or skin-stretched patches
// (armpits under a raised arm) read as pile, not bare plastic.
// Maps (all UV0): _FurMap R length 0..1, G density, B tip lightening, A ambient occlusion (linear);
// _FurFlowMap RG comb direction in UV space (rg * 2 - 1), B comb strength (linear); _FurNoiseMap R strand height,
// B per-strand random (linear, Repeat). Tangents must be imported (Mikktspace) for the comb.
Shader "Jolly/Character/ShellFur"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _FurMap ("Fur (R length, G density, B tip, A AO)", 2D) = "white" {}
        [NoScaleOffset] _FurFlowMap ("Flow (RG direction, B strength)", 2D) = "gray" {}
        [NoScaleOffset] _FurNoiseMap ("Strand Noise (R height, B random)", 2D) = "white" {}
        _NoiseTiling ("Strand Tiling", Float) = 18.3

        [Header(Pile)]
        _FurLength ("Length (m)", Range(0, 0.08)) = 0.024
        _FurDroop ("Gravity Droop", Range(0, 1)) = 0.35
        _FurComb ("Comb", Range(0, 1.5)) = 0.5
        _DensityThin ("Thinning At Low Density", Range(0, 1)) = 0.6
        _MinLength ("Bald Below Length", Range(0, 0.2)) = 0.03
        _StrandTaper ("Strand Fullness", Range(0.3, 3)) = 1

        [Header(Colour)]
        _RootShade ("Root Shade", Range(0, 1)) = 0.7
        _TipTint ("Tip Brightness", Range(0.5, 2)) = 1.1
        _TipLighten ("Tip Lightening", Range(0, 1)) = 0.1
        _StrandJitter ("Strand Colour Jitter", Range(0, 0.5)) = 0.14
        _SkinStrands ("Strand Shading On Skin", Range(0, 1)) = 0

        [Header(Light)]
        _Wrap ("Diffuse Wrap", Range(0, 1)) = 0.5
        _RimColor ("Rim Color", Color) = (1, 0.94, 0.88, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35

        [Header(Shells read by ShellFur)]
        [IntRange] _ShellCount ("Shells Near", Range(0, 32)) = 16
        [IntRange] _ShellCountMid ("Shells Mid", Range(0, 32)) = 10
        [IntRange] _ShellCountFar ("Shells Far", Range(0, 32)) = 6
        _ShellDistanceMid ("Mid From (m)", Float) = 1.6
        _ShellDistanceFar ("Far From (m)", Float) = 3.2
        _InertiaScale ("Inertia Swing", Range(0, 4)) = 0.6
        _FurSway ("Idle Sway (m)", Range(0, 0.01)) = 0.0015

        [HideInInspector] _ShellHeight ("Shell Height", Float) = 0
        [HideInInspector] _FurInertia ("Inertia (world, m)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _NoiseTiling;
            float _FurLength;
            half _FurDroop;
            half _FurComb;
            half _DensityThin;
            half _MinLength;
            half _StrandTaper;
            half _RootShade;
            half _TipTint;
            half _TipLighten;
            half _StrandJitter;
            half _SkinStrands;
            half _Wrap;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            float _ShellCount;
            float _ShellCountMid;
            float _ShellCountFar;
            float _ShellDistanceMid;
            float _ShellDistanceFar;
            float _InertiaScale;
            float _FurSway;
            float _ShellHeight;
            float4 _FurInertia;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_FurMap);
        SAMPLER(sampler_FurMap);
        TEXTURE2D(_FurFlowMap);
        SAMPLER(sampler_FurFlowMap);
        TEXTURE2D(_FurNoiseMap);
        SAMPLER(sampler_FurNoiseMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // World position of this layer's vertex; fur.r is the length 0..1 at the vertex.
        float3 ShellPositionWS(float3 positionWS, float3 normalWS, float4 tangentWS, half4 fur, float2 uv)
        {
        #if defined(_FUR_SHELL)
            half4 flow = SAMPLE_TEXTURE2D_LOD(_FurFlowMap, sampler_FurFlowMap, uv, 0);
            float h = _ShellHeight;
            float reach = h * _FurLength * fur.r;
            float3 bitangentWS = cross(normalWS, tangentWS.xyz) * tangentWS.w;
            float2 comb = flow.rg * 2.0 - 1.0;
            float3 combWS = tangentWS.xyz * comb.x + bitangentWS * comb.y;
            combWS *= rsqrt(max(dot(combWS, combWS), 1e-6));
            // Bend grows with height (h * reach = h² × length): roots stay put, tips sag, comb over and trail.
            float bend = h * reach;
            // A slow breeze through the pile, out of phase across the body, so standing still is not frozen.
            float sway = sin(_Time.y * 1.7 + dot(positionWS, float3(9.0, 7.0, 5.0))) * _FurSway * h * h * fur.r;
            positionWS += normalWS * reach
                        + float3(0.0, -1.0, 0.0) * bend * _FurDroop
                        + combWS * (bend * flow.b * _FurComb + sway)
                        + _FurInertia.xyz * (h * h * fur.r * _InertiaScale);
        #endif
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ XR_HARD_OCCLUSION XR_SOFT_OCCLUSION
            #pragma multi_compile_local _ _FUR_SHELL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Jolly/Content/Rendering/Shaders/EnvironmentOcclusion.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            #if defined(_FUR_SHELL)
                half3 color : TEXCOORD2;     // lit strand colour at this height
                half2 strand : TEXCOORD3;    // x: survival threshold, y: length (bald below _MinLength)
            #else
                float3 normalWS : TEXCOORD2;
                half4 shade : TEXCOORD3;     // rgb ambient SH, a root shade × AO
                half skinStrands : TEXCOORD4;
            #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 Lit(half3 albedo, half3 n, half3 v, half3 ambient, half rimWeight)
            {
                Light light = GetMainLight();
                half wrapped = saturate((dot(n, light.direction) + _Wrap) / (1.0h + _Wrap));
                half3 color = albedo * (light.color * (light.distanceAttenuation * wrapped) + ambient);
                half rim = pow(1.0h - saturate(dot(n, v)), _RimPower) * _RimStrength * rimWeight;
                return color + _RimColor.rgb * rim;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half4 fur = SAMPLE_TEXTURE2D_LOD(_FurMap, sampler_FurMap, input.uv, 0);
                float3 positionWS = ShellPositionWS(position.positionWS, normal.normalWS,
                                                    float4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale()),
                                                    fur, input.uv);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                half3 ambient = SampleSH(normal.normalWS);
            #if defined(_FUR_SHELL)
                half h = (half)_ShellHeight;
                half3 albedo = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, output.uv, 0).rgb * _BaseColor.rgb;
                half shade = lerp(_RootShade, _TipTint, h) * (1.0h + fur.b * _TipLighten * h) * lerp(fur.a, 1.0h, h);
                half3 v = GetWorldSpaceNormalizeViewDir(positionWS);
                output.color = Lit(albedo * shade, normalize(normal.normalWS), v, ambient, h);
                output.strand = half2(pow(h, _StrandTaper) + (1.0h - fur.g) * _DensityThin, fur.r);
            #else
                output.normalWS = normal.normalWS;
                output.shade = half4(ambient, lerp(1.0h, _RootShade, fur.r) * fur.a);
                output.skinStrands = _SkinStrands * saturate(fur.r * 8.0h);
            #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_FUR_SHELL)
                half3 noise = SAMPLE_TEXTURE2D(_FurNoiseMap, sampler_FurNoiseMap, input.uv * _NoiseTiling).rgb;
                clip(min(noise.r - input.strand.x, input.strand.y - _MinLength));
                half3 color = input.color * (1.0h - 0.5h * _StrandJitter + _StrandJitter * noise.b);
            #else
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half noise = SAMPLE_TEXTURE2D(_FurNoiseMap, sampler_FurNoiseMap, input.uv * _NoiseTiling).r;
                half3 color = Lit(albedo * input.shade.a, normalize(input.normalWS), v, input.shade.rgb, 0.0h);
                color *= 1.0h + input.skinStrands * (noise - 0.5h);
            #endif
                half visible = EnvironmentVisibility(input.positionWS);
                return half4(color * visible, visible);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _FUR_SHELL

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            #if defined(_FUR_SHELL)
                float2 uv : TEXCOORD0;
                half2 strand : TEXCOORD1;
            #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half4 fur = SAMPLE_TEXTURE2D_LOD(_FurMap, sampler_FurMap, input.uv, 0);
                float3 positionWS = ShellPositionWS(position.positionWS, normal.normalWS,
                                                    float4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale()),
                                                    fur, input.uv);
                output.positionCS = TransformWorldToHClip(positionWS);
            #if defined(_FUR_SHELL)
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.strand = half2(pow((half)_ShellHeight, _StrandTaper) + (1.0h - fur.g) * _DensityThin, fur.r);
            #endif
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_FUR_SHELL)
                half noise = SAMPLE_TEXTURE2D(_FurNoiseMap, sampler_FurNoiseMap, input.uv * _NoiseTiling).r;
                clip(min(noise - input.strand.x, input.strand.y - _MinLength));
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
