using System;
using UnityEngine;

namespace Jolly.XR.Occlusion
{
    [Serializable]
    public sealed class OcclusionSettings
    {
        public OcclusionPreference preference = OcclusionPreference.EnvironmentDepth;
        [Tooltip("Soft edges from AR Foundation's depth preprocess pass (one small full-texture pass per frame); hard is a single depth compare.")]
        public bool softEdges = true;
        [Tooltip("Use the Space Setup bounding boxes while environment depth is unavailable.")]
        public bool sceneMeshFallback = true;
        [Tooltip("Depth counts as lost after this long without a frame, s.")]
        public float depthStaleSeconds = 0.5f;
    }
}
