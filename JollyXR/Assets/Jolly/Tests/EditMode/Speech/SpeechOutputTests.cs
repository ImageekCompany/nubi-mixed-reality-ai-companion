using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Audio.Output;
using Jolly.Contracts;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class SpeechOutputTests
    {
        const int Rate = 24000;
        const short Half = 16384;

        SpeechRingPlayer _player;
        SpeechOutput _output;
        List<int> _firstSamples;

        [SetUp]
        public void SetUp() => Build(Rate, Rate * 10);

        [TearDown]
        public void TearDown() => _output.Dispose();

        void Build(int outputRate, int capacity)
        {
            _player = new SpeechRingPlayer(Rate, outputRate, capacity);
            _output = new SpeechOutput(_player);
            _firstSamples = new List<int>();
            _output.FirstSamplePlayed += _firstSamples.Add;
        }

        float[] Render(int frames, int channels = 1)
        {
            var data = new float[frames * channels];
            _player.Render(data, channels);
            _output.Tick();
            return data;
        }

        static Exception ErrorOf(UniTask task)
        {
            try
            {
                task.GetAwaiter().GetResult();
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        [Test]
        public void Play_FirstSamplePlayed_WaitsForTheAudioThreadToConsumeIt()
        {
            var stream = new ControlledPcmStream().EmitConstant(1000, Half);
            var play = _output.Play(stream, 7, CancellationToken.None);

            _output.Tick();
            var beforeRender = _firstSamples.Count;
            Render(256);

            Assert.That(beforeRender, Is.Zero);
            Assert.That(_firstSamples, Is.EqualTo(new[] { 7 }));
            Assert.That(play.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(_output.IsPlaying, Is.True);
        }

        [Test]
        public void Play_CompletesOnlyAfterTheLastSampleWasRendered()
        {
            var stream = new ControlledPcmStream().EmitConstant(100, Half);
            var play = _output.Play(stream, 1, CancellationToken.None);
            stream.Complete();

            Render(64);
            var midway = play.Status;
            Render(64);

            Assert.That(midway, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(play.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_output.IsPlaying, Is.False);
        }

        [Test]
        public void Render_ConvertsS16ToFloatOnEveryChannel()
        {
            var stream = new ControlledPcmStream().Emit(Half, -Half);
            _output.Play(stream, 1, CancellationToken.None).Forget();
            stream.Complete();

            var data = Render(4, channels: 2);

            Assert.That(data, Is.EqualTo(new[] { 0f, 0f, 0.5f, 0.5f, -0.5f, -0.5f, 0f, 0f }));
        }

        [Test]
        public void Render_At48k_ConsumesHalfAsManySourceSamples()
        {
            Build(48000, Rate);
            var stream = new ControlledPcmStream().EmitConstant(100, Half);
            _output.Play(stream, 1, CancellationToken.None).Forget();

            var data = Render(200);

            Assert.That(_player.Consumed, Is.EqualTo(100));
            for (var i = 4; i < 198; i++)
                Assert.That(data[i], Is.EqualTo(0.5f).Within(1e-6f), $"frame {i}");
        }

        [Test]
        public void Play_ConcurrentCalls_PlayInCallOrderEvenWhenTheSecondArrivesFirst()
        {
            var first = new ControlledPcmStream();
            var second = new ControlledPcmStream();
            var playFirst = _output.Play(first, 1, CancellationToken.None);
            var playSecond = _output.Play(second, 2, CancellationToken.None);

            second.EmitConstant(10, Half / 2).Complete();
            first.EmitConstant(10, Half).Complete();
            var data = Render(30);

            Assert.That(data[1], Is.EqualTo(0.5f));
            Assert.That(data[11], Is.EqualTo(0.25f));
            Assert.That(_firstSamples, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(playFirst.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(playSecond.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Flush_CancelsPendingPlaysAndFadesToSilenceWithin20Ms()
        {
            var playing = new ControlledPcmStream().EmitConstant(Rate, Half);
            var queued = new ControlledPcmStream().EmitConstant(Rate, Half);
            var playA = _output.Play(playing, 1, CancellationToken.None);
            var playB = _output.Play(queued, 2, CancellationToken.None);
            Render(480);

            _output.Flush();
            var fade = Render(960);
            var after = Render(480);

            Assert.That(playA.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(playB.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_output.IsPlaying, Is.False);
            Assert.That(fade[0], Is.GreaterThan(0.4f));
            for (var i = 1; i < 480; i++)
                Assert.That(fade[i], Is.LessThanOrEqualTo(fade[i - 1]), $"frame {i}");
            for (var i = 479; i < 960; i++)
                Assert.That(fade[i], Is.Zero, $"frame {i}");
            Assert.That(after, Is.All.Zero);
        }

        [Test]
        public void Flush_ThenPlayBeforeTheAudioThreadRuns_KeepsTheNewAudio()
        {
            var old = new ControlledPcmStream().EmitConstant(Rate, Half);
            _output.Play(old, 1, CancellationToken.None).Forget();
            _output.Flush();

            var fresh = new ControlledPcmStream().EmitConstant(2000, Half / 2);
            _output.Play(fresh, 2, CancellationToken.None).Forget();
            Render(960);
            var data = Render(480);

            Assert.That(data[100], Is.EqualTo(0.25f));
            Assert.That(_firstSamples, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Flush_WhenACancelledPlayStartsTheNextOneSynchronously_KeepsTheNewAudio()
        {
            var old = new ControlledPcmStream().EmitConstant(Rate, Half);
            var fresh = new ControlledPcmStream().EmitConstant(2000, Half / 2);
            PlayAfterCancellation(_output.Play(old, 1, CancellationToken.None), fresh).Forget();

            _output.Flush();
            Render(960);
            var data = Render(480);

            Assert.That(data[100], Is.EqualTo(0.25f));
            Assert.That(_firstSamples, Is.EqualTo(new[] { 2 }));
        }

        async UniTaskVoid PlayAfterCancellation(UniTask play, IUniTaskAsyncEnumerable<PcmChunk> next)
        {
            try
            {
                await play;
            }
            catch (OperationCanceledException)
            {
                _output.Play(next, 2, CancellationToken.None).Forget();
            }
        }

        [Test]
        public void Play_CallerCancellation_CancelsAndStopsWaiting()
        {
            using var cts = new CancellationTokenSource();
            var stream = new ControlledPcmStream().EmitConstant(100, Half);
            var play = _output.Play(stream, 1, cts.Token);

            cts.Cancel();

            Assert.That(play.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(stream.Token.IsCancellationRequested, Is.True);
            Assert.That(_output.IsPlaying, Is.False);
        }

        [Test]
        public void Play_StreamError_FaultsWithTheProviderError()
        {
            var stream = new ControlledPcmStream();
            var play = _output.Play(stream, 1, CancellationToken.None);
            var error = new ProviderException("wit", ProviderErrorKind.Timeout, "idle");

            stream.Fail(error);

            Assert.That(ErrorOf(play), Is.SameAs(error));
            Assert.That(_output.IsPlaying, Is.False);
        }

        [Test]
        public void Play_EmptyStream_RaisesFirstSampleAndCompletesOnTick()
        {
            var stream = new ControlledPcmStream();
            var play = _output.Play(stream, 3, CancellationToken.None);
            stream.Complete();

            _output.Tick();

            Assert.That(_firstSamples, Is.EqualTo(new[] { 3 }));
            Assert.That(play.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Play_WrongSampleRate_Throws()
        {
            var stream = new ControlledPcmStream(16000).EmitConstant(10, Half);

            var play = _output.Play(stream, 1, CancellationToken.None);

            Assert.That(ErrorOf(play), Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Play_MoreAudioThanTheRingHolds_IsWrittenAsTheRingDrains()
        {
            Build(Rate, 100);
            var stream = new ControlledPcmStream().EmitConstant(250, Half);
            var play = _output.Play(stream, 1, CancellationToken.None);
            stream.Complete();

            for (var i = 0; i < 4; i++)
                Render(100);

            Assert.That(_player.Consumed, Is.EqualTo(250));
            Assert.That(play.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Flush_AfterDispose_IsANoOp()
        {
            _output.Play(new ControlledPcmStream().EmitConstant(10, Half), 1, CancellationToken.None).Forget();

            _output.Dispose();

            Assert.DoesNotThrow(_output.Flush);
            Assert.That(_output.IsPlaying, Is.False);
        }

        [Test]
        public void Level_FollowsTheRenderedLoudness()
        {
            var stream = new ControlledPcmStream().EmitConstant(1000, Half);
            _output.Play(stream, 1, CancellationToken.None).Forget();
            stream.Complete();

            Render(512);
            var loud = _player.Level;
            Render(1024);
            Render(256);

            Assert.That(loud, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(_player.Level, Is.Zero);
        }
    }
}
