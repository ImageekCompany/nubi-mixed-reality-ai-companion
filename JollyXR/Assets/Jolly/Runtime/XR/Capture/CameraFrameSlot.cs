using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Unity.Collections;
using UnityEngine;

namespace Jolly.XR.Capture
{
    /// <summary>
    /// The single reusable pixel buffer behind an ICameraFrameSource: one outstanding CameraFrame at a time
    /// (contracts §6). A frame may be released from a worker thread; waiters resume on the main thread.
    /// </summary>
    public sealed class CameraFrameSlot : IDisposable
    {
        readonly Action<CameraFrame> _release;
        NativeArray<byte> _buffer;
        int _leased;
        bool _disposed;

        public CameraFrameSlot() => _release = OnReleased;

        public bool IsLeased => Volatile.Read(ref _leased) != 0;

        /// <summary>Waits for the previous frame's release, then leases a buffer of at least byteCount bytes.
        /// Follow with Publish, or Abort if the capture fails.</summary>
        public UniTask<NativeArray<byte>> Acquire(int byteCount, CancellationToken ct)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CameraFrameSlot));
            return AcquireAsync(byteCount, ct);
        }

        async UniTask<NativeArray<byte>> AcquireAsync(int byteCount, CancellationToken ct)
        {
            while (Interlocked.CompareExchange(ref _leased, 1, 0) != 0)
                await UniTask.WaitWhile(this, s => s.IsLeased, cancellationToken: ct);

            if (_disposed)
            {
                Volatile.Write(ref _leased, 0);
                throw new ObjectDisposedException(nameof(CameraFrameSlot));
            }
            if (!_buffer.IsCreated || _buffer.Length < byteCount)
            {
                if (_buffer.IsCreated)
                    _buffer.Dispose();
                _buffer = new NativeArray<byte>(byteCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
            return _buffer;
        }

        public CameraFrame Publish(int width, int height, CameraIntrinsics intrinsics, Pose cameraPoseWorld, double timestamp)
        {
            if (!IsLeased)
                throw new InvalidOperationException("Acquire the slot before publishing a frame.");
            return new CameraFrame(_buffer, width, height, intrinsics, cameraPoseWorld, timestamp, _release);
        }

        public void Abort() => OnReleased(null);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!IsLeased)
                FreeBuffer();
        }

        void OnReleased(CameraFrame _)
        {
            Volatile.Write(ref _leased, 0);
            if (_disposed)
                FreeBuffer();
        }

        void FreeBuffer()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();
        }
    }
}
