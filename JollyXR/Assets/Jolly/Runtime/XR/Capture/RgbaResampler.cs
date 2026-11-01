using Unity.Collections;
using UnityEngine;

namespace Jolly.XR.Capture
{
    static class RgbaResampler
    {
        /// <summary>Nearest-neighbour RGBA32 resize, pixel centres aligned. Same aliasing trade-off as the device
        /// path's XRCpuImage downscale (device-spike.md §2.3).</summary>
        public static void Nearest(NativeArray<byte> src, Vector2Int srcSize, NativeArray<byte> dst, Vector2Int dstSize)
        {
            for (var y = 0; y < dstSize.y; y++)
            {
                var sy = Mathf.Min(srcSize.y - 1, (int)((y + 0.5f) * srcSize.y / dstSize.y));
                for (var x = 0; x < dstSize.x; x++)
                {
                    var sx = Mathf.Min(srcSize.x - 1, (int)((x + 0.5f) * srcSize.x / dstSize.x));
                    NativeArray<byte>.Copy(src, (sy * srcSize.x + sx) * 4, dst, (y * dstSize.x + x) * 4, 4);
                }
            }
        }
    }
}
