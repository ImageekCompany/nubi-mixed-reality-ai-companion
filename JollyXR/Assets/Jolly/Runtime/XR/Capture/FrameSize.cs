using UnityEngine;

namespace Jolly.XR.Capture
{
    public static class FrameSize
    {
        /// <summary>The largest size with the source's aspect whose longer side is at most maxSide. Never upscales.</summary>
        public static Vector2Int Fit(Vector2Int source, int maxSide)
        {
            var scale = Mathf.Min(1f, (float)maxSide / Mathf.Max(source.x, source.y));
            return new Vector2Int(Mathf.RoundToInt(source.x * scale), Mathf.RoundToInt(source.y * scale));
        }
    }
}
