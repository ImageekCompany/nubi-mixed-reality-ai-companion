using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Unity.Collections;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>Solid-colour frames of a fixed size, honouring the one-outstanding-frame rule of ICameraFrameSource.</summary>
    public sealed class FakeFrameSource : ICameraFrameSource
    {
        readonly Action<CameraFrame> _release;
        NativeArray<byte> _pixels;
        bool _outstanding;

        public FakeFrameSource(int width = 640, int height = 480)
        {
            Width = width;
            Height = height;
            Intrinsics = new CameraIntrinsics(new Vector2(width * 0.68f, width * 0.68f),
                new Vector2(width / 2f, height / 2f), new Vector2Int(width, height));
            _pixels = new NativeArray<byte>(width * height * 4, Allocator.Persistent);
            _release = _ => _outstanding = false;
        }

        public int Width { get; }
        public int Height { get; }
        public CameraIntrinsics Intrinsics { get; }
        public CameraAvailability Availability { get; set; } = CameraAvailability.Ready;
        public Pose CameraPose { get; set; } = Pose.identity;
        public double Timestamp { get; set; }
        public int Captures { get; private set; }
        public bool IsFrameOutstanding => _outstanding;

        public UniTask<CameraFrame> Capture(int maxSide, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Availability != CameraAvailability.Ready)
                throw new ProviderException("fake-camera", ProviderErrorKind.Protocol, "camera not ready");
            if (_outstanding)
                throw new InvalidOperationException("previous frame not disposed");
            _outstanding = true;
            Captures++;
            return UniTask.FromResult(new CameraFrame(_pixels, Width, Height, Intrinsics, CameraPose, Timestamp, _release));
        }

        public void Dispose()
        {
            if (_pixels.IsCreated)
                _pixels.Dispose();
        }
    }
}
