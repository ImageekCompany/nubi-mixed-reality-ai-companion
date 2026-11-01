using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Unity.Collections;
using UnityEngine;

namespace Jolly.XR.Capture
{
    /// <summary>
    /// ICameraFrameSource for the Editor and Quest Link, where meta-openxr delivers no CPU images
    /// (xr-stack.md §2.2): serves one still image (a fixture JPEG/PNG) as if the head camera had captured it.
    /// Intrinsics use the Quest 3 passthrough camera's field of view so CameraMath behaves as on device.
    /// </summary>
    public sealed class EditorStillFrameSource : ICameraFrameSource
    {
        // 2·atan(864 / 1172.536): the Quest 3 left camera at 1728² (device-spike.md §1).
        const float QuestHorizontalFovDegrees = 72.77f;

        readonly IUserPoseProvider _pose;
        readonly IClock _clock;
        readonly CameraFrameSlot _slot = new();
        readonly Vector2Int _size;
        readonly CameraIntrinsics _intrinsics;
        NativeArray<byte> _pixels;

        /// <param name="encodedImage">Any format Texture2D.LoadImage accepts.</param>
        public EditorStillFrameSource(byte[] encodedImage, IUserPoseProvider pose, IClock clock)
        {
            _pose = pose;
            _clock = clock;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(encodedImage, false))
                    return;
                _size = new Vector2Int(texture.width, texture.height);
                _pixels = TopRowFirst(texture.GetPixels32(), _size);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            var focal = _size.x * 0.5f / Mathf.Tan(QuestHorizontalFovDegrees * 0.5f * Mathf.Deg2Rad);
            _intrinsics = new CameraIntrinsics(new Vector2(focal, focal), new Vector2(_size.x * 0.5f, _size.y * 0.5f), _size);
        }

        public CameraAvailability Availability => _pixels.IsCreated ? CameraAvailability.Ready : CameraAvailability.NotSupported;

        public async UniTask<CameraFrame> Capture(int maxSide, CancellationToken ct)
        {
            if (!_pixels.IsCreated)
                throw new ProviderException("editor-camera", ProviderErrorKind.Protocol, "no still image loaded");

            var size = FrameSize.Fit(_size, maxSide);
            var buffer = await _slot.Acquire(size.x * size.y * 4, ct);
            RgbaResampler.Nearest(_pixels, _size, buffer, size);
            var k = _intrinsics.Scaled((float)size.x / _size.x);
            return _slot.Publish(size.x, size.y, k, _pose.Head, _clock.Now);
        }

        public void Dispose()
        {
            _slot.Dispose();
            if (_pixels.IsCreated)
                _pixels.Dispose();
        }

        static NativeArray<byte> TopRowFirst(Color32[] bottomRowFirst, Vector2Int size)
        {
            var pixels = new NativeArray<byte>(size.x * size.y * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var dst = 0;
            for (var y = size.y - 1; y >= 0; y--)
            {
                for (var x = 0; x < size.x; x++)
                {
                    var c = bottomRowFirst[y * size.x + x];
                    pixels[dst++] = c.r;
                    pixels[dst++] = c.g;
                    pixels[dst++] = c.b;
                    pixels[dst++] = c.a;
                }
            }
            return pixels;
        }
    }
}
