namespace Jolly.XR.Occlusion
{
    /// <summary>What JollyConfig asks for (ADR-0023).</summary>
    public enum OcclusionPreference
    {
        Off,
        /// <summary>Depth-only boxes from Space Setup bounding boxes; no per-frame GPU depth.</summary>
        SceneMeshes,
        /// <summary>Quest environment depth through AR Foundation shader occlusion, scene meshes while it is absent.</summary>
        EnvironmentDepth,
    }

    /// <summary>What hides Nubi behind the real room this frame. The two sources are exclusive: a bounding box is
    /// larger than the furniture in it and would cut Nubi where the depth map shows air.</summary>
    public enum OcclusionMode
    {
        None,
        SceneMeshes,
        EnvironmentDepth,
    }

    public static class OcclusionPolicy
    {
        /// <param name="depthLive">A depth frame with poses arrived within the stale window. False covers every way
        /// depth can be missing: USE_SCENE denied, an unsupported runtime, the Editor, a stalled provider.</param>
        public static OcclusionMode Select(OcclusionPreference preference, bool depthLive, bool sceneMeshFallback) =>
            preference switch
            {
                OcclusionPreference.EnvironmentDepth when depthLive => OcclusionMode.EnvironmentDepth,
                OcclusionPreference.EnvironmentDepth when sceneMeshFallback => OcclusionMode.SceneMeshes,
                OcclusionPreference.SceneMeshes => OcclusionMode.SceneMeshes,
                _ => OcclusionMode.None,
            };

        /// <param name="lastFrameAt">Clock time of the last depth frame; negative infinity before the first.</param>
        public static bool IsDepthLive(double now, double lastFrameAt, double staleSeconds) =>
            now - lastFrameAt <= staleSeconds;
    }
}
