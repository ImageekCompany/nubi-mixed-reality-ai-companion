using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using Unity.Collections;
using UnityEngine;

namespace Jolly.Testing.Conversation
{
    /// <summary>Camera source handing out one persistent buffer, mid-grey unless <see cref="Fill"/> says otherwise;
    /// counts frames handed out and released.</summary>
    public sealed class FakeCameraFrameSource : ICameraFrameSource
    {
        public const int Size = 64;
        NativeArray<byte> _buffer = new(Size * Size * 4, Allocator.Persistent);

        public FakeCameraFrameSource() => Fill(128);

        public CameraAvailability Availability { get; set; } = CameraAvailability.Ready;
        public int Captures { get; private set; }
        public int Released { get; private set; }
        public bool FailNext { get; set; }

        public static CameraIntrinsics Intrinsics => new(new Vector2(Size, Size), new Vector2(Size / 2f, Size / 2f),
            new Vector2Int(Size, Size));

        public UniTask<CameraFrame> Capture(int maxSide, CancellationToken ct)
        {
            if (FailNext)
                throw new ProviderException("camera", ProviderErrorKind.Protocol, "no frame within 1 s");
            Captures++;
            return UniTask.FromResult(new CameraFrame(_buffer, Size, Size, Intrinsics,
                new Pose(Vector3.zero, Quaternion.identity), 0, _ => Released++));
        }

        /// <summary>Every pixel of later frames becomes this grey, i.e. this mean luma.</summary>
        public void Fill(byte grey)
        {
            for (var i = 0; i < _buffer.Length; i++)
                _buffer[i] = grey;
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();
        }
    }

    public sealed class FakeImageEncoder : IImageEncoder
    {
        public UniTask<EncodedImage> EncodeJpeg(CameraFrame frame, int quality, CancellationToken ct) =>
            UniTask.FromResult(new EncodedImage(new byte[] { 0xFF, 0xD8 }, "image/jpeg", frame.Width, frame.Height,
                frame.Intrinsics, frame.CameraPoseWorld));
    }

    /// <summary>ISegmenter returning queued results in order; <see cref="Manual"/> holds each call until <see cref="Release"/>.</summary>
    public sealed class ScriptedSegmenter : ISegmenter
    {
        readonly Queue<IReadOnlyList<SegmentedObject>> _results = new();
        readonly Queue<UniTaskCompletionSource> _held = new();

        public List<SegmentPrompt> Prompts { get; } = new();
        public bool Manual { get; set; }
        public ProviderException Failure { get; set; }

        public ScriptedSegmenter Returns(params SegmentedObject[] objects)
        {
            _results.Enqueue(objects);
            return this;
        }

        public async UniTask<IReadOnlyList<SegmentedObject>> Segment(EncodedImage image, SegmentPrompt prompt, CancellationToken ct)
        {
            Prompts.Add(prompt);
            if (Manual)
            {
                var hold = new UniTaskCompletionSource();
                _held.Enqueue(hold);
                await hold.Task.AttachExternalCancellation(ct);
            }
            if (Failure != null)
                throw Failure;
            return _results.Count > 0 ? _results.Dequeue() : Array.Empty<SegmentedObject>();
        }

        public void Release() => _held.Dequeue().TrySetResult();

        /// <summary>A filled square mask of side <paramref name="side"/> at (x, y).</summary>
        public static SegmentedObject Square(int x, int y, int side, float confidence = 0.9f, int id = 0)
        {
            var bits = new ulong[(side * side + 63) / 64];
            for (var i = 0; i < side * side; i++)
                bits[i >> 6] |= 1UL << (i & 63);
            return new SegmentedObject(id, new RectInt(x, y, side, side), confidence, new BitMask(side, side, bits));
        }
    }

    /// <summary>IObjectIdentifier that streams a scripted header and answer; <see cref="Gate"/> holds before the answer.</summary>
    public sealed class ScriptedObjectIdentifier : IObjectIdentifier
    {
        readonly UniTaskCompletionSource _gate = new();

        public string Label { get; set; } = "TV remote";
        public string NounPhrase { get; set; } = "remote";
        public string Place { get; set; }
        public string[] Answer { get; set; } = { "It is a TV remote. ", "Press the CC button." };
        public bool Gate { get; set; }
        public ProviderException Failure { get; set; }
        public IdentifyRequest LastRequest { get; private set; }

        public void Release() => _gate.TrySetResult();

        public IUniTaskAsyncEnumerable<IdentificationEvent> Identify(EncodedImage image, IdentifyRequest request, CancellationToken ct)
        {
            LastRequest = request;
            return UniTaskAsyncEnumerable.Create<IdentificationEvent>(async (writer, _) =>
            {
                await writer.YieldAsync(new IdentificationEvent(IdentificationEventKind.Header, Label, NounPhrase, place: Place));
                if (Gate)
                    await _gate.Task.AttachExternalCancellation(ct);
                if (Failure != null)
                    throw Failure;
                foreach (var delta in Answer)
                    await writer.YieldAsync(new IdentificationEvent(IdentificationEventKind.AnswerDelta, text: delta));
                await writer.YieldAsync(new IdentificationEvent(IdentificationEventKind.Completed, text: string.Concat(Answer)));
            });
        }
    }

    public sealed class RecordingMaskPresenter : IMaskPresenter
    {
        public List<(IReadOnlyList<SegmentedObject> objects, float distance)> Shown { get; } = new();
        public int Clears { get; private set; }

        public void Show(IReadOnlyList<SegmentedObject> objects, EncodedImage source, float distanceMeters) =>
            Shown.Add((objects, distanceMeters));

        public void Clear() => Clears++;
    }

    public sealed class RecordingSpeechSink : ISpokenTextSink
    {
        public System.Text.StringBuilder Text { get; } = new();
        public void Append(string delta) => Text.Append(delta);
    }
}
