using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Offline;
using Jolly.Testing;
using Jolly.Testing.Offline;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Offline
{
    public sealed class OfflineResponderTests
    {
        AudioClip _clip;
        FakeCannedLines _lines;
        FakeSpeechOutput _speech;
        FakeClock _clock;
        RecordingLog _log;
        List<(int id, string text)> _captions;
        OfflineResponder _responder;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("line", 2400, 1, 24000, false);
            _lines = new FakeCannedLines();
            _speech = new FakeSpeechOutput();
            _clock = new FakeClock();
            _log = new RecordingLog();
            _captions = new List<(int, string)>();
            _responder = new OfflineResponder(_lines, _speech, _clock, _log);
            _responder.CaptionStarted += (id, text) => _captions.Add((id, text));
        }

        [TearDown]
        public void TearDown()
        {
            _responder.Dispose();
            UnityEngine.Object.DestroyImmediate(_clip);
        }

        [Test]
        public void Respond_PlaysTheClipAndCompletesWhenItHasPlayed()
        {
            _lines.Add(LineCategory.BudgetReached, "budget.1", _clip);

            var respond = _responder.Respond(LineCategory.BudgetReached, 7, CancellationToken.None);

            Assert.That(_speech.Played, Is.EqualTo(new[] { (_clip, 7) }));
            Assert.That(respond.Status, Is.EqualTo(UniTaskStatus.Pending));
            _speech.FinishCurrent();
            Assert.That(respond.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Respond_RaisesTheCaptionAtTheFirstPlayedSample()
        {
            _lines.Add(LineCategory.Error, "error.1", _clip);

            _responder.Respond(LineCategory.Error, 3, CancellationToken.None).Forget();
            var beforeAudio = _captions.Count;
            _speech.StartCurrent();

            Assert.That(beforeAudio, Is.Zero);
            Assert.That(_captions, Is.EqualTo(new[] { (3, "text:error.1") }));
        }

        [Test]
        public void Respond_IgnoresTheFirstSampleOfAnotherUtterance()
        {
            _lines.Add(LineCategory.Error, "error.1", _clip);

            _responder.Respond(LineCategory.Error, 3, CancellationToken.None).Forget();
            _speech.RaiseFirstSample(99);

            Assert.That(_captions, Is.Empty);
        }

        [Test]
        public void Respond_ForOffline_AlternatesOfflineLinesWithWhoAmIAndGreeting()
        {
            _lines.Add(LineCategory.Offline, "offline.1", _clip)
                  .Add(LineCategory.WhoAmI, "whoami.1", _clip)
                  .Add(LineCategory.Greeting, "greeting.1", _clip);

            for (var i = 0; i < 5; i++)
            {
                _responder.Respond(LineCategory.Offline, i, CancellationToken.None).Forget();
                _speech.FinishCurrent();
            }

            Assert.That(_lines.Requested, Is.EqualTo(new[]
            {
                LineCategory.Offline, LineCategory.WhoAmI, LineCategory.Offline, LineCategory.Greeting, LineCategory.Offline,
            }));
        }

        [Test]
        public void Respond_ForOffline_NeverRepeatsALineWithinTheLastFive()
        {
            for (var i = 0; i < 5; i++)
            {
                _lines.Add(LineCategory.Offline, "offline." + i, _clip)
                      .Add(LineCategory.WhoAmI, "whoami." + i, _clip)
                      .Add(LineCategory.Greeting, "greeting." + i, _clip);
            }
            var spoken = new List<string>();
            _responder.CaptionStarted += (_, text) => spoken.Add(text);

            for (var i = 0; i < 24; i++)
            {
                _responder.Respond(LineCategory.Offline, i, CancellationToken.None).Forget();
                _speech.StartCurrent();
                _speech.FinishCurrent();
            }

            for (var i = 1; i < spoken.Count; i++)
            {
                for (var back = 1; back <= 5 && back <= i; back++)
                    Assert.That(spoken[i], Is.Not.EqualTo(spoken[i - back]), $"pick {i} repeats pick {i - back}");
            }
        }

        [Test]
        public void Respond_WhenTheCategoryIsEmpty_FallsBackToError()
        {
            _lines.Add(LineCategory.Error, "error.1", _clip);

            _responder.Respond(LineCategory.BudgetReached, 1, CancellationToken.None).Forget();

            Assert.That(_speech.Played.Count, Is.EqualTo(1));
            Assert.That(_lines.Requested[^1], Is.EqualTo(LineCategory.Error));
        }

        [Test]
        public void Respond_WhenTheOfflineFillerIsEmpty_FallsBackToAnOfflineLine()
        {
            _lines.Add(LineCategory.Offline, "offline.1", _clip);
            _responder.Respond(LineCategory.Offline, 1, CancellationToken.None).Forget();
            _speech.FinishCurrent();

            _responder.Respond(LineCategory.Offline, 2, CancellationToken.None).Forget();

            Assert.That(_speech.Played.Count, Is.EqualTo(2));
            Assert.That(_lines.Requested[^1], Is.EqualTo(LineCategory.Offline));
        }

        [Test]
        public void Respond_WithNoLineAtAll_CompletesSilentlyAndWarns()
        {
            var respond = _responder.Respond(LineCategory.Unknown, 1, CancellationToken.None);

            Assert.That(respond.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_speech.Played, Is.Empty);
            Assert.That(_log.Contains(LogLevel.Warning, "evt=offline.no_line"), Is.True);
        }

        [Test]
        public void Respond_CaptionOnlyLine_CaptionsAtOnceAndHoldsForTheReadingTime()
        {
            _lines.Add(LineCategory.Greeting, "greeting.1");

            var respond = _responder.Respond(LineCategory.Greeting, 4, CancellationToken.None);

            Assert.That(_captions, Is.EqualTo(new[] { (4, "text:greeting.1") }));
            Assert.That(_speech.Played, Is.Empty);
            _clock.Advance(TimeSpan.FromSeconds(1.4));
            Assert.That(respond.Status, Is.EqualTo(UniTaskStatus.Pending));
            _clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.That(respond.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Respond_WhenCancelled_ThrowsAndDropsThePendingCaption()
        {
            _lines.Add(LineCategory.Error, "error.1", _clip);
            using var cts = new CancellationTokenSource();
            var respond = _responder.Respond(LineCategory.Error, 5, cts.Token);

            cts.Cancel();
            _speech.RaiseFirstSample(5);

            Assert.That(respond.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_captions, Is.Empty);
        }

        [Test]
        public void Dispose_UnsubscribesFromTheSpeechOutput()
        {
            _responder.Dispose();

            Assert.That(_speech.HasSubscribers, Is.False);
        }
    }
}
