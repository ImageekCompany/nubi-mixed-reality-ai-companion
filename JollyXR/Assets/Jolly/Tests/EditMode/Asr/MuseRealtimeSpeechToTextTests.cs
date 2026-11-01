using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Asr;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class MuseRealtimeSpeechToTextTests
    {
        const string Ack = "{\"sessionId\":\"jolly-rt-1\"}";
        const int ChunkBytes = 1920 * 2;

        FakeClock _clock;
        FakeWebSocket _socket;
        FakeUsageMeter _meter;
        RecordingLog _log;
        InMemorySecrets _secrets;
        Channel<PcmChunk> _audio;
        MuseRealtimeSpeechToText _stt;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _socket = new FakeWebSocket();
            _meter = new FakeUsageMeter();
            _log = new RecordingLog();
            _secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "KEY");
            _audio = Channel.CreateSingleConsumerUnbounded<PcmChunk>();
            _stt = new MuseRealtimeSpeechToText(() => _socket, _secrets, _meter, _clock, _log,
                new MuseAsrOptions("muse-voice-transcribe-1.0"));
        }

        [Test]
        public void Start_ConnectsAndSendsTheHandshakeAsTheFirstFrameBeforeAnyAudio()
        {
            WriteChunks(3);

            Transcribe();

            Assert.That(_socket.ConnectedUri.AbsoluteUri, Does.StartWith("wss://api.meta.ai/v1/asr/realtime?sessionId=jolly-"));
            Assert.That(_socket.Sent.Count, Is.EqualTo(1));
            Assert.That(_socket.Sent[0].Text, Does.Contain("\"accessToken\":\"Bearer KEY\""));
        }

        [Test]
        public void Ack_SendsTheBufferedBacklogAtOnceWithoutWaiting()
        {
            Transcribe();
            WriteChunks(25);

            _socket.Push(Ack);

            Assert.That(_socket.BinaryFrameCount(), Is.EqualTo(25));
            Assert.That(_socket.BinaryBytes(), Is.EqualTo(25 * ChunkBytes));
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void LiveChunks_AfterTheBacklog_AreForwardedAsTheyArrive()
        {
            Transcribe();
            _socket.Push(Ack);

            WriteChunks(1);
            var afterOne = _socket.BinaryFrameCount();
            WriteChunks(1);

            Assert.That(afterOne, Is.EqualTo(1));
            Assert.That(_socket.BinaryFrameCount(), Is.EqualTo(2));
        }

        [Test]
        public void Audio_NeverRunsMoreThan5SecondsAheadOfTheWallClock()
        {
            Transcribe();
            _socket.Push(Ack);

            WriteChunks(70);

            Assert.That(_socket.BinaryFrameCount(), Is.EqualTo(62));
            _clock.Advance(TimeSpan.FromSeconds(0.08));
            Assert.That(_socket.BinaryFrameCount(), Is.EqualTo(63));
            _clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(_socket.BinaryFrameCount(), Is.EqualTo(70));
        }

        [Test]
        public void SourceCompletion_SendsEndStreamImmediately()
        {
            Transcribe();
            _socket.Push(Ack);
            WriteChunks(2);

            _audio.Writer.TryComplete();

            Assert.That(_socket.Sent.Last().Text, Is.EqualTo(MuseAsrHandshake.EndStream));
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void SourceCompletion_BeforeTheAck_SendsBacklogThenEndStreamOnAck()
        {
            Transcribe();
            WriteChunks(3);
            _audio.Writer.TryComplete();

            _socket.Push(Ack);

            Assert.That(_socket.BinaryFrameCount(), Is.EqualTo(3));
            Assert.That(_socket.Sent.Last().Text, Is.EqualTo(MuseAsrHandshake.EndStream));
        }

        [Test]
        public void RecordedSession_YieldsChangedPartialsThenExactlyOneNormalisedFinal()
        {
            var events = Transcribe();
            var frames = AsrFixtures.Lines("realtime_ptt_24k.txt");

            foreach (var frame in frames)
                _socket.Push(frame);

            Assert.That(events.Succeeded, Is.True);
            Assert.That(events.Items.Select(e => e.Kind), Is.EqualTo(new[]
            {
                TranscriptKind.Partial, TranscriptKind.Partial, TranscriptKind.Partial, TranscriptKind.Partial,
                TranscriptKind.Final,
            }));
            Assert.That(events.Items[0].Text, Is.EqualTo("hey"));
            Assert.That(events.Items.Last().Text, Is.EqualTo("hey Jolly walk to the kitchen and tell me what this pen is."));
            Assert.That(events.Items.Last().AudioMs, Is.EqualTo(3760));
        }

        [Test]
        public void RepeatedPartial_IsNotYieldedTwice()
        {
            var events = Transcribe();
            _socket.Push(Ack);

            _socket.Push(Transcript("hey", false));
            _socket.Push(Transcript("hey", false));

            Assert.That(events.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void Final_EndsTheStreamAndReleasesTheSocket()
        {
            var events = Transcribe();
            _socket.Push(Ack);

            _socket.Push(Transcript("", true));

            Assert.That(events.Succeeded, Is.True);
            Assert.That(events.Items.Single().Kind, Is.EqualTo(TranscriptKind.Final));
            Assert.That(events.Items.Single().Text, Is.Empty);
            Assert.That(_socket.IsDisposed, Is.True);
        }

        [Test]
        public void ErrorFrame_FailsTheStreamWithTheMappedKind()
        {
            var events = Transcribe();

            _socket.Push(AsrFixtures.Lines("realtime_errors.txt")[0]);

            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.Unauthorized));
            Assert.That(_socket.IsDisposed, Is.True);
        }

        [Test]
        public void CloseBeforeFinal_FailsWithTheCloseCodeMapping()
        {
            var events = Transcribe();
            _socket.Push(Ack);

            _socket.PushClose(1013, "Rate limited");

            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.RateLimited));
        }

        [Test]
        public void MissingAck_TimesOutAfter5Seconds()
        {
            var events = Transcribe();

            _clock.Advance(TimeSpan.FromSeconds(4.9));
            var pendingBefore = !events.IsCompleted;
            _clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.That(pendingBefore, Is.True);
            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.Timeout));
        }

        [Test]
        public void ConnectThatNeverCompletes_TimesOutAfter5Seconds()
        {
            _socket.HoldConnect = true;
            var events = Transcribe();

            _clock.Advance(TimeSpan.FromSeconds(4.9));
            var pendingBefore = !events.IsCompleted;
            _clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.That(pendingBefore, Is.True);
            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_socket.IsDisposed, Is.True);
        }

        [Test]
        public void Transcribe_ConnectsBeforeTheStreamIsEnumerated()
        {
            var stream = _stt.Transcribe(_audio.Reader.ReadAllAsync(), new SttOptions(), CancellationToken.None);

            Assert.That(_socket.ConnectedUri, Is.Not.Null);
            Assert.That(_socket.Sent.Single().Text, Does.Contain("\"accessToken\""));
            _socket.Push(Ack);
            _socket.Push(Transcript("hello", false));
            _socket.Push(Transcript("hello there", true));

            var events = new StreamCollector<TranscriptEvent>(stream);

            Assert.That(events.Succeeded, Is.True);
            Assert.That(events.Items.Select(e => e.Text), Is.EqualTo(new[] { "hello", "hello there" }));
        }

        [Test]
        public void EnumerationCancelledEarly_AbortsTheSession()
        {
            using var reader = new CancellationTokenSource();
            var events = new StreamCollector<TranscriptEvent>(
                _stt.Transcribe(_audio.Reader.ReadAllAsync(), new SttOptions(), CancellationToken.None), reader.Token);
            _socket.Push(Ack);

            reader.Cancel();

            Assert.That(events.Error, Is.InstanceOf<OperationCanceledException>());
            Assert.That(_socket.IsDisposed, Is.True);
            Assert.That(_meter.OpenReservations, Is.Zero);
        }

        [Test]
        public void SessionError_BeforeAnyoneReads_ReportsNoUnobservedExceptionOnGarbageCollection()
        {
            FailSessionWithoutReadingIt();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Ack_StopsTheAckTimer()
        {
            var events = Transcribe();
            _socket.Push(Ack);

            _clock.Advance(TimeSpan.FromSeconds(30));

            Assert.That(events.IsCompleted, Is.False);
        }

        [Test]
        public void MissingKey_FailsUnauthorizedWithoutConnecting()
        {
            _secrets = new InMemorySecrets();
            _stt = new MuseRealtimeSpeechToText(() => _socket, _secrets, _meter, _clock, _log, new MuseAsrOptions("m"));

            var events = Transcribe();

            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.Unauthorized));
            Assert.That(_socket.ConnectedUri, Is.Null);
            Assert.That(_meter.OpenReservations, Is.Zero);
        }

        [Test]
        public void ContributorModel_FailsBadRequestWithoutConnecting()
        {
            _stt = new MuseRealtimeSpeechToText(() => _socket, _secrets, _meter, _clock, _log, new MuseAsrOptions("asr-contributor"));

            var events = Transcribe();

            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.BadRequest));
            Assert.That(_socket.ConnectedUri, Is.Null);
            Assert.That(_meter.OpenReservations, Is.Zero);
        }

        [Test]
        public void BudgetRefused_FailsBudgetExceededWithoutConnecting()
        {
            _meter.RefuseReservations = true;

            var events = Transcribe();

            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.BudgetExceeded));
            Assert.That(_socket.ConnectedUri, Is.Null);
        }

        [Test]
        public void Usage_IsSettledWithTheAudioSecondsActuallySent()
        {
            var events = Transcribe();
            _socket.Push(Ack);
            WriteChunks(10);
            _audio.Writer.TryComplete();

            _socket.Push(Transcript("hi", true));

            Assert.That(events.Succeeded, Is.True);
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_meter.Records.Single().AudioSeconds, Is.EqualTo(0.8).Within(1e-9));
            Assert.That(_meter.Records.Single().Model, Is.EqualTo("muse-voice-transcribe-1.0"));
        }

        [Test]
        public void Cancellation_AbortsTheSessionAndSettlesUsage()
        {
            using var cts = new CancellationTokenSource();
            var events = Transcribe(cts.Token);
            _socket.Push(Ack);
            WriteChunks(5);

            cts.Cancel();

            Assert.That(events.Error, Is.InstanceOf<OperationCanceledException>());
            Assert.That(_socket.IsDisposed, Is.True);
            Assert.That(_meter.Records.Single().AudioSeconds, Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void SendFailure_FailsTheStreamWithTheSocketError()
        {
            var events = Transcribe();
            _socket.SendFailure = new ProviderException("meta.asr", ProviderErrorKind.Network, "reset");
            _socket.Push(Ack);

            WriteChunks(1);

            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.Network));
        }

        [Test]
        public void SentChunks_AreReturnedToThePool()
        {
            Transcribe();
            _socket.Push(Ack);
            var chunk = PcmChunk.Rent(1920, 24000);

            _audio.Writer.TryWrite(chunk);

            Assert.That(chunk.Buffer, Is.Null);
        }

        [Test]
        public void ChunkAtTheWrongRate_FailsBadRequest()
        {
            var events = Transcribe();
            _socket.Push(Ack);

            _audio.Writer.TryWrite(PcmChunk.Rent(1280, 16000));

            Assert.That(Kind(events), Is.EqualTo(ProviderErrorKind.BadRequest));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void FailSessionWithoutReadingIt()
        {
            _stt.Transcribe(_audio.Reader.ReadAllAsync(), new SttOptions(), CancellationToken.None);
            _socket.Push(AsrFixtures.Lines("realtime_errors.txt")[0]);
        }

        StreamCollector<TranscriptEvent> Transcribe(CancellationToken ct = default) =>
            new(_stt.Transcribe(_audio.Reader.ReadAllAsync(), new SttOptions(), ct));

        void WriteChunks(int count)
        {
            for (var i = 0; i < count; i++)
                _audio.Writer.TryWrite(PcmChunk.Rent(1920, 24000));
        }

        static string Transcript(string text, bool final) =>
            $"{{\"type\":\"transcript\",\"transcript\":\"{text}\",\"final\":{(final ? "true" : "false")},\"audioProcessedMs\":0}}";

        static ProviderErrorKind Kind(StreamCollector<TranscriptEvent> events)
        {
            Assert.That(events.Error, Is.InstanceOf<ProviderException>(), events.Error?.ToString());
            return ((ProviderException)events.Error).Kind;
        }
    }
}
