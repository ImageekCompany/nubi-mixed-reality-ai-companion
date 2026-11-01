using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Jolly.Core.Imaging
{
    /// <summary>
    /// <see cref="IImageEncoder"/> over <see cref="ImageConversion.EncodeNativeArrayToJPG{T}"/>, run on the thread pool.
    /// No resize: the frame source already converted to the target size. Stateless, so concurrent calls are safe.
    /// </summary>
    public sealed class JpegImageEncoder : IImageEncoder
    {
        const string MimeType = "image/jpeg";
        const int BytesPerPixel = 4;

        public UniTask<EncodedImage> EncodeJpeg(CameraFrame frame, int quality, CancellationToken ct)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (quality < 1 || quality > 100)
                throw new ArgumentOutOfRangeException(nameof(quality), quality, "JPEG quality is 1..100.");
            return ct.IsCancellationRequested ? UniTask.FromCanceled<EncodedImage>(ct) : Encode(frame, quality, ct);
        }

        static async UniTask<EncodedImage> Encode(CameraFrame frame, int quality, CancellationToken ct)
        {
            byte[] bytes;
            await UniTask.SwitchToThreadPool();
            try
            {
                bytes = EncodeBottomUp(frame, quality);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }

            ct.ThrowIfCancellationRequested();
            return new EncodedImage(bytes, MimeType, frame.Width, frame.Height, frame.Intrinsics, frame.CameraPoseWorld);
        }

        /// <summary>
        /// ImageConversion reads its input as texture memory, bottom row first (EditMode test
        /// <c>EncodeJpeg_KeepsTheTopRowAtTheTop</c>), while <see cref="CameraFrame.Rgba32"/> is top row first, so rows are
        /// reversed into a native scratch copy. The frame is no longer read once this returns.
        /// </summary>
        static byte[] EncodeBottomUp(CameraFrame frame, int quality)
        {
            var rowBytes = frame.Width * BytesPerPixel;
            using var flipped = new NativeArray<byte>(frame.Rgba32.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (var row = 0; row < frame.Height; row++)
                NativeArray<byte>.Copy(frame.Rgba32, row * rowBytes, flipped, (frame.Height - 1 - row) * rowBytes, rowBytes);

            using var jpeg = ImageConversion.EncodeNativeArrayToJPG(flipped, GraphicsFormat.R8G8B8A8_UNorm,
                (uint)frame.Width, (uint)frame.Height, (uint)rowBytes, quality);
            return jpeg.ToArray();
        }
    }
}
