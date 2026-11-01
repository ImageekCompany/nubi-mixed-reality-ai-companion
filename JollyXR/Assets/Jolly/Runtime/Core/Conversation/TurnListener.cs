using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>What the user said (after aliases) and the frame captured at PTT release, if any.</summary>
    public readonly struct HeardTurn
    {
        public HeardTurn(string text, EncodedImage preCaptured) { Text = text; PreCaptured = preCaptured; }
        public string Text { get; }
        public EncodedImage PreCaptured { get; }
    }

    /// <summary>
    /// The Listening phase (architecture §5.1): mic from the press, streaming ASR with live partial captions, and at release
    /// the mic stops (ASR then sends endStream) while one camera frame is pre-captured for a possible vision tool.
    /// </summary>
    public sealed class TurnListener
    {
        readonly IAudioCapture _capture;
        readonly ISpeechToText _stt;
        readonly FrameGrabber _frames;
        readonly TranscriptAliases _aliases;
        readonly ILocalizer _localizer;
        readonly ConversationEvents _events;
        readonly ILatencyTracker _latency;
        readonly ILog _log;
        readonly ConversationTimings _timings;

        public TurnListener(IAudioCapture capture, ISpeechToText stt, FrameGrabber frames, TranscriptAliases aliases,
                            ILocalizer localizer, ConversationEvents events, ILatencyTracker latency, ILog log,
                            ConversationTimings timings)
        {
            _capture = capture;
            _stt = stt;
            _frames = frames;
            _aliases = aliases;
            _localizer = localizer;
            _events = events;
            _latency = latency;
            _log = log;
            _timings = timings;
        }

        public bool CanListen => _capture.IsAvailable;

        /// <summary>Throws ProviderException when ASR fails; <paramref name="released"/> completes at PTT release.</summary>
        public async UniTask<HeardTurn> Listen(int turnId, UniTask released, CancellationToken ct)
        {
            using var session = _capture.Start(_timings.CaptureSampleRate);
            var options = new SttOptions
            {
                LanguageBias = _localizer.Voice?.AsrLanguageBias ?? "English", SampleRate = _timings.CaptureSampleRate,
            };
            // Cancelled on every exit, so a release after an ASR failure never touches the disposed session.
            using var listening = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var preCapture = OnRelease(turnId, released, session, listening.Token);
            try
            {
                string final = null;
                await foreach (var e in _stt.Transcribe(session.Chunks, options, ct).WithCancellation(ct))
                {
                    if (e.Kind == TranscriptKind.Partial)
                    {
                        _events.RaiseUserPartial(_aliases.Apply(e.Text));
                        continue;
                    }
                    final = e.Text;
                    break;
                }
                return new HeardTurn(_aliases.Apply(final), await preCapture);
            }
            finally
            {
                listening.Cancel();
            }
        }

        async UniTask<EncodedImage> OnRelease(int turnId, UniTask released, IAudioCaptureSession session, CancellationToken ct)
        {
            if (await released.AttachExternalCancellation(ct).SuppressCancellationThrow())
                return null;
            _latency.Mark(turnId, LatencyMark.PttReleased);
            session.Stop();
            try
            {
                return await _frames.Capture(ct);
            }
            catch (ProviderException e)
            {
                _log.Write(LogLevel.Warning, "conversation", $"evt=precapture.failed kind={e.Kind}");
                return null;
            }
        }
    }
}
