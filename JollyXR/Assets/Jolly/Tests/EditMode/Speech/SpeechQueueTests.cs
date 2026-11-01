using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Speech;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class SpeechQueueTests
    {
        FakeTextToSpeech _tts;
        ScriptedSpeechOutput _output;
        FakeUserPreferences _preferences;
        SpeechQueue _queue;

        [SetUp]
        public void SetUp()
        {
            _tts = new FakeTextToSpeech();
            _output = new ScriptedSpeechOutput();
            _preferences = new FakeUserPreferences();
            _queue = new SpeechQueue(_tts, _output, _preferences);
        }

        [TearDown]
        public void TearDown() => _queue.Dispose();

        static VoiceSpec Voice(int speed = 100) => new() { VoiceId = "Charlie", Speed = speed };

        [Test]
        public void Enqueue_ThreeSentences_SynthesizesCurrentPlusOnePrefetch()
        {
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);
            _queue.Enqueue("Three.", Voice(), 3);

            Assert.That(_tts.Requests, Has.Count.EqualTo(2));
            Assert.That(_output.Playbacks, Has.Count.EqualTo(2));
            Assert.That(_output.Playbacks[0].UtteranceId, Is.EqualTo(1));
            Assert.That(_output.Playbacks[1].UtteranceId, Is.EqualTo(2));
            Assert.That(_output.Playbacks[1].Pcm, Is.SameAs(_tts.Requests[1].Stream));
        }

        [Test]
        public void Enqueue_WhenFirstFinishes_StartsTheNextSentence()
        {
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);
            _queue.Enqueue("Three.", Voice(), 3);

            _output.Finish(1);

            Assert.That(_tts.Requests, Has.Count.EqualTo(3));
            Assert.That(_tts.Requests[2].Text, Is.EqualTo("Three."));
        }

        [Test]
        public void Enqueue_BlankSentence_IsIgnored()
        {
            _queue.Enqueue("  ", Voice(), 1);

            Assert.That(_tts.Requests, Is.Empty);
            Assert.That(_queue.WhenDrained(CancellationToken.None).Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [TestCase(1.5f, 100, 150)]
        [TestCase(2f, 120, 200)]
        [TestCase(0.5f, 80, 50)]
        public void Enqueue_AppliesUserSpeechRateClampedToWitRange(float rate, int personaSpeed, int expected)
        {
            _preferences.Current.SpeechRate = rate;

            _queue.Enqueue("Hello.", Voice(personaSpeed), 1);

            Assert.That(_tts.Requests[0].Voice.Speed, Is.EqualTo(expected));
            Assert.That(_tts.Requests[0].Voice.VoiceId, Is.EqualTo("Charlie"));
        }

        [Test]
        public void WhenDrained_CompletesAfterTheLastSentencePlayed()
        {
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);
            var drained = _queue.WhenDrained(CancellationToken.None);

            _output.Finish(1);
            var afterFirst = drained.Status;
            _output.Finish(2);

            Assert.That(afterFirst, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(drained.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void WhenDrained_AfterOutputFlush_CompletesInsteadOfHanging()
        {
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);
            _queue.Enqueue("Three.", Voice(), 3);
            var drained = _queue.WhenDrained(CancellationToken.None);

            _output.Flush();

            Assert.That(drained.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_tts.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public void Clear_CancelsInFlightTokensAndDropsPending()
        {
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);
            _queue.Enqueue("Three.", Voice(), 3);

            _queue.Clear();

            Assert.That(_tts.Requests[0].Token.IsCancellationRequested, Is.True);
            Assert.That(_tts.Requests[1].Token.IsCancellationRequested, Is.True);
            Assert.That(_output.Playbacks[0].Token.IsCancellationRequested, Is.True);
            Assert.That(_tts.Requests, Has.Count.EqualTo(2));
            Assert.That(_queue.WhenDrained(CancellationToken.None).Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Enqueue_AfterClear_PlaysWithAFreshToken()
        {
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Clear();

            _queue.Enqueue("Again.", Voice(), 2);

            Assert.That(_tts.Requests, Has.Count.EqualTo(2));
            Assert.That(_tts.Requests[1].Token.IsCancellationRequested, Is.False);
            Assert.That(_queue.WhenDrained(CancellationToken.None).Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void FirstSamplePlayed_RaisesSentenceStartedAndExtendsSpokenSoFar()
        {
            var started = new List<(int, string)>();
            _queue.SentenceStarted += (id, text) => started.Add((id, text));
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);

            _output.StartPlaying(1);
            var afterFirst = _queue.SpokenSoFar;
            _output.Finish(1);
            _output.StartPlaying(2);

            Assert.That(afterFirst, Is.EqualTo("One."));
            Assert.That(_queue.SpokenSoFar, Is.EqualTo("One. Two."));
            Assert.That(started, Is.EqualTo(new[] { (1, "One."), (2, "Two.") }));
        }

        [Test]
        public void SpokenSoFar_SurvivesClearUntilTheNextEnqueue()
        {
            _queue.Enqueue("One.", Voice(), 1);
            _output.StartPlaying(1);

            _queue.Clear();
            var afterClear = _queue.SpokenSoFar;
            _queue.Enqueue("Next turn.", Voice(), 2);

            Assert.That(afterClear, Is.EqualTo("One."));
            Assert.That(_queue.SpokenSoFar, Is.Empty);
        }

        [Test]
        public void FirstSamplePlayed_SentencesSharingAnUtteranceId_CaptionInPlayOrder()
        {
            var started = new List<(int, string)>();
            _queue.SentenceStarted += (id, text) => started.Add((id, text));
            _queue.Enqueue("One.", Voice(), 5);
            _queue.Enqueue("Two.", Voice(), 5);

            _output.StartPlaying(5);
            _output.Finish(5);
            _output.StartPlaying(5);

            Assert.That(started, Is.EqualTo(new[] { (5, "One."), (5, "Two.") }));
            Assert.That(_queue.SpokenSoFar, Is.EqualTo("One. Two."));
        }

        [Test]
        public void FirstSamplePlayed_AfterOutputFlush_DoesNotCaptionDroppedSentences()
        {
            var started = new List<(int, string)>();
            _queue.SentenceStarted += (id, text) => started.Add((id, text));
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);
            _queue.Enqueue("Three.", Voice(), 3);
            _output.Flush();

            _output.StartPlaying(3);

            Assert.That(started, Is.Empty);
            Assert.That(_queue.SpokenSoFar, Is.Empty);
        }

        [Test]
        public void Enqueue_LongerThanTheTtsLimit_ThrowsWithoutSynthesizing()
        {
            var sentence = new string('a', _tts.MaxCharacters + 1);

            Assert.Throws<ArgumentException>(() => _queue.Enqueue(sentence, Voice(), 1));
            Assert.That(_tts.Requests, Is.Empty);
        }

        [Test]
        public void FirstSamplePlayed_ForForeignUtterance_IsIgnored()
        {
            var started = 0;
            _queue.SentenceStarted += (_, _) => started++;

            _output.StartPlaying(99);

            Assert.That(started, Is.Zero);
            Assert.That(_queue.SpokenSoFar, Is.Empty);
        }

        [Test]
        public void ProviderFailure_SkipsTheSentenceAndContinues()
        {
            var failures = new List<int>();
            _queue.SentenceFailed += (id, _) => failures.Add(id);
            _queue.Enqueue("One.", Voice(), 1);
            _queue.Enqueue("Two.", Voice(), 2);
            _queue.Enqueue("Three.", Voice(), 3);

            _output.Fail(1, new ProviderException("wit", ProviderErrorKind.Timeout, "idle"));

            Assert.That(failures, Is.EqualTo(new[] { 1 }));
            Assert.That(_tts.Requests, Has.Count.EqualTo(3));
        }

        [Test]
        public void WhenDrained_CallerCancellation_Throws()
        {
            using var cts = new CancellationTokenSource();
            _queue.Enqueue("One.", Voice(), 1);
            var drained = _queue.WhenDrained(cts.Token);

            cts.Cancel();

            Assert.That(drained.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_output.Playbacks[0].Token.IsCancellationRequested, Is.False);
        }

        [Test]
        public void Dispose_UnsubscribesFromOutput()
        {
            var started = 0;
            _queue.SentenceStarted += (_, _) => started++;
            _queue.Enqueue("One.", Voice(), 1);

            _queue.Dispose();
            _output.StartPlaying(1);

            Assert.That(started, Is.Zero);
        }
    }
}
