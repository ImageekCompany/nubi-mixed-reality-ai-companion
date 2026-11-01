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
        // Lease and dispose bits in one int, changed only by compare-exchange: Dispose (main thread) and a release
        // (any thread) each see the other's bit, so exactly the later of the two frees the buffer.
        const int Leased = 1;
        const int Disposed = 2;

        readonly Action<CameraFrame> _release;
        NativeArray<byte> _buffer;
        int _state;

        public CameraFrameSlot() => _release = OnReleased;

        public bool IsLeased => (Volatile.Read(ref _state) & Leased) != 0;

        /// <summary>Waits for the previous frame's release, then leases a buffer of at least byteCount bytes.
        /// Follow with Publish, or Abort if the capture fails.</summary>
        public UniTask<NativeArray<byte>> Acquire(int byteCount, CancellationToken ct)
        {
            ThrowIfDisposed(Volatile.Read(ref _state));
            return AcquireAsync(byteCount, ct);
        }

        async UniTask<NativeArray<byte>> AcquireAsync(int byteCount, CancellationToken ct)
        {
            int state;
            while ((state = Interlocked.CompareExchange(ref _state, Leased, 0)) != 0)
            {
                ThrowIfDisposed(state);
                await UniTask.WaitWhile(this, s => s.IsLeased, cancellationToken: ct);
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
            int state;
            do
            {
                state = Volatile.Read(ref _state);
                if ((state & Disposed) != 0)
                    return;
            } while (Interlocked.CompareExchange(ref _state, state | Disposed, state) != state);
            if ((state & Leased) == 0)
                FreeBuffer();
        }

        void OnReleased(CameraFrame _)
        {
            int state;
            do
            {
                state = Volatile.Read(ref _state);
            } while (Interlocked.CompareExchange(ref _state, state & ~Leased, state) != state);
            if ((state & Disposed) != 0)
                FreeBuffer();
        }

        static void ThrowIfDisposed(int state)
        {
            if ((state & Disposed) != 0)
                throw new ObjectDisposedException(nameof(CameraFrameSlot));
        }

        void FreeBuffer()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();
        }
    }
}
