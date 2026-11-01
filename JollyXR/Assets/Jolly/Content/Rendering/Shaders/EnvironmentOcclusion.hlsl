// Quest environment-depth occlusion (ADR-0024) for the materials that must hide behind real objects and hands.
// Reads the globals AR Foundation's ARShaderOcclusion publishes each depth frame (ARShaderOcclusion.cs:189-231):
//   _EnvironmentDepthTexture              2-slice array, one per eye; depth in [0,1] of the projection below
//   _EnvironmentDepthTexturePreprocessed  soft mode only: (1 - near, 1 - far, avg - near, far - near) per texel
//                                         (SoftOcclusionPreprocessing.shader:36-128)
//   _EnvironmentDepthProjectionMatrices   world -> depth-camera clip space, OpenGL z, w = metres in front
//   _NdcLinearConversionParameters        symmetric NDC z -> metres (Utils.hlsl:20-23)
//   _IsOcclusionOn                        0 once ARShaderOcclusion is disabled; the keywords stay set
// The caller includes URP Core.hlsl first, declares `#pragma multi_compile _ XR_HARD_OCCLUSION XR_SOFT_OCCLUSION`,
// and calls UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX before EnvironmentVisibility.
#ifndef JOLLY_ENVIRONMENT_OCCLUSION_INCLUDED
#define JOLLY_ENVIRONMENT_OCCLUSION_INCLUDED

#if defined(XR_HARD_OCCLUSION) || defined(XR_SOFT_OCCLUSION)
TEXTURE2D_ARRAY(_EnvironmentDepthTexture);
TEXTURE2D_ARRAY(_EnvironmentDepthTexturePreprocessed);
float4x4 _EnvironmentDepthProjectionMatrices[2];
float4 _NdcLinearConversionParameters;
int _IsOcclusionOn;

// Metres a fragment may sit behind the sensed surface and still show: Nubi's soles touch the floor the sensor sees,
// and the depth map is coarse (Meta's runtime depth is far below eye-buffer resolution).
#define JOLLY_OCCLUSION_BIAS 0.04

float JollyEnvironmentMetres(float depth01)
{
    return _NdcLinearConversionParameters.x / (depth01 * 2.0 - 1.0 + _NdcLinearConversionParameters.y);
}
#endif

// 1 where the fragment is in front of the real world, 0 where a real surface (furniture, a hand) is in front of it.
// One exit only: early returns behind UNITY_BRANCH draw "potentially uninitialized" warnings from the Vulkan compiler.
half EnvironmentVisibility(float3 positionWS)
{
    half visible = 1.0h;
#if defined(XR_HARD_OCCLUSION) || defined(XR_SOFT_OCCLUSION)
    uint eye = unity_StereoEyeIndex;
    float4 depthCS = mul(_EnvironmentDepthProjectionMatrices[eye], float4(positionWS, 1.0));
    float2 uv = depthCS.xy / depthCS.w * 0.5 + 0.5;
    // Outside the depth camera's frustum there is no data: show rather than cut.
    bool inFrustum = depthCS.w > 0.0 && all(uv >= 0.0) && all(uv <= 1.0);
    UNITY_BRANCH
    if (_IsOcclusionOn != 0 && inFrustum)
    {
        float fragment = depthCS.w - JOLLY_OCCLUSION_BIAS;
  #if defined(XR_SOFT_OCCLUSION)
        float4 texel = SAMPLE_TEXTURE2D_ARRAY_LOD(_EnvironmentDepthTexturePreprocessed, sampler_LinearClamp, uv, eye, 0);
        float nearest = JollyEnvironmentMetres(1.0 - texel.x);
        float farthest = JollyEnvironmentMetres(1.0 - texel.y);
        // Between the near and far surfaces of the neighbourhood (an edge): visible in proportion to the share of it
        // that lies on the far side.
        float edge = saturate(texel.z / max(texel.w, 1e-5));
        visible = (half)(fragment <= nearest ? 1.0 : (fragment >= farthest ? 0.0 : edge));
  #else
        float sensed = JollyEnvironmentMetres(
            SAMPLE_TEXTURE2D_ARRAY_LOD(_EnvironmentDepthTexture, sampler_PointClamp, uv, eye, 0).r);
        visible = fragment <= sensed ? 1.0h : 0.0h;
  #endif
    }
#endif
    return visible;
}

#endif
