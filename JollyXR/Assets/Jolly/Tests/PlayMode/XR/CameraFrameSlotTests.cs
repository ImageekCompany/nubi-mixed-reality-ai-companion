using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.XR.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class CameraFrameSlotTests
    {
        static readonly CameraIntrinsics K = new(new Vector2(400f, 400f), new Vector2(320f, 240f), new Vector2Int(640, 480));

        [TestCase(1728, 1728, 640, 640, 640)]
        [TestCase(1280, 960, 640, 640, 480)]
        [TestCase(960, 1280, 640, 480, 640)]
        [TestCase(320, 240, 640, 320, 240)]
        public void FrameSize_Fit_KeepsAspectAndNeverUpscales(int w, int h, int maxSide, int ew, int eh)
        {
            Assert.That(FrameSize.Fit(new Vector2Int(w, h), maxSide), Is.EqualTo(new Vector2Int(ew, eh)));
        }

        [UnityTest]
        public IEnumerator Publish_ExposesExactlyTheFrameBytes() => UniTask.ToCoroutine(async () =>
        {
            using var slot = new CameraFrameSlot();

            var buffer = await slot.Acquire(640 * 480 * 4, CancellationToken.None);
            buffer[0] = 7;
            using var frame = slot.Publish(640, 480, K, Pose.identity, 1.5);

            Assert.That(frame.Rgba32.Length, Is.EqualTo(640 * 480 * 4));
            Assert.That(frame.Rgba32[0], Is.EqualTo(7));
            Assert.That(frame.Timestamp, Is.EqualTo(1.5));
        });

        [UnityTest]
        public IEnumerator Acquire_WhileAFrameIsOutstanding_WaitsForItsRelease() => UniTask.ToCoroutine(async () =>
        {
            using var slot = new CameraFrameSlot();
            await slot.Acquire(16, CancellationToken.None);
            var first = slot.Publish(2, 2, K, Pose.identity, 0);

            var second = slot.Acquire(16, CancellationToken.None);
            await UniTask.NextFrame();
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));

            first.Dispose();
            first.Dispose();
            await second;
            slot.Abort();
            Assert.That(slot.IsLeased, Is.False);
        });

        [UnityTest]
        public IEnumerator Acquire_CancelledWhileWaiting_Throws() => UniTask.ToCoroutine(async () =>
        {
            using var slot = new CameraFrameSlot();
            await slot.Acquire(16, CancellationToken.None);
            using var frame = slot.Publish(2, 2, K, Pose.identity, 0);
            using var cts = new CancellationTokenSource();

            var waiting = slot.Acquire(16, cts.Token);
            cts.Cancel();

            var thrown = false;
            try { await waiting; }
            catch (OperationCanceledException) { thrown = true; }
            Assert.That(thrown, Is.True);
        });

        [UnityTest]
        public IEnumerator Dispose_WhileAFrameIsOutstanding_KeepsTheBufferUntilRelease() => UniTask.ToCoroutine(async () =>
        {
            var slot = new CameraFrameSlot();
            await slot.Acquire(16, CancellationToken.None);
            var frame = slot.Publish(2, 2, K, Pose.identity, 0);

            slot.Dispose();
            var pixels = frame.Rgba32;
            pixels[15] = 1;

            frame.Dispose();
            Assert.Throws<ObjectDisposedException>(() => slot.Acquire(16, CancellationToken.None).Forget());
        });
    }
}
