using System;
using Jolly.Contracts;
using Jolly.Core.Diagnostics;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Diagnostics
{
    public sealed class LatencyTrackerTests
    {
        [Test]
        public void Between_UsesTheLastCompletedTurn()
        {
            var clock = new FakeClock();
            var tracker = new LatencyTracker(clock, new RecordingLog());
            tracker.BeginTurn(1);
            tracker.Mark(1, LatencyMark.PttReleased);
            clock.Advance(TimeSpan.FromMilliseconds(250));
            tracker.Mark(1, LatencyMark.AsrFinal);
            tracker.Mark(1, LatencyMark.TurnCompleted);

            Assert.That(tracker.Between(LatencyMark.PttReleased, LatencyMark.AsrFinal), Is.EqualTo(250).Within(1e-6));
        }

        [Test]
        public void Between_MissingMark_IsNaN()
        {
            var tracker = new LatencyTracker(new FakeClock(), new RecordingLog());
            tracker.BeginTurn(1);
            tracker.Mark(1, LatencyMark.PttPressed);
            tracker.Mark(1, LatencyMark.TurnCompleted);

            Assert.That(double.IsNaN(tracker.Between(LatencyMark.PttPressed, LatencyMark.AsrFinal)), Is.True);
        }

        [Test]
        public void Between_BeforeAnyCompletedTurn_IsNaN()
        {
            var tracker = new LatencyTracker(new FakeClock(), new RecordingLog());
            tracker.BeginTurn(1);
            tracker.Mark(1, LatencyMark.PttPressed);

            Assert.That(double.IsNaN(tracker.Between(LatencyMark.PttPressed, LatencyMark.PttPressed)), Is.True);
        }

        [Test]
        public void Mark_KeepsFirstOccurrenceAndIgnoresStaleTurns()
        {
            var clock = new FakeClock();
            var tracker = new LatencyTracker(clock, new RecordingLog());
            tracker.BeginTurn(2);
            tracker.Mark(2, LatencyMark.PttPressed);
            clock.Advance(TimeSpan.FromSeconds(1));
            tracker.Mark(2, LatencyMark.FirstAudioPlayed);
            clock.Advance(TimeSpan.FromSeconds(1));
            tracker.Mark(2, LatencyMark.FirstAudioPlayed);
            tracker.Mark(1, LatencyMark.FirstAudioPlayed);
            tracker.Mark(2, LatencyMark.TurnCompleted);

            Assert.That(tracker.Between(LatencyMark.PttPressed, LatencyMark.FirstAudioPlayed), Is.EqualTo(1000).Within(1e-6));
        }

        [Test]
        public void TurnCompleted_LogsOneLatencyLine()
        {
            var clock = new FakeClock();
            var log = new RecordingLog();
            var tracker = new LatencyTracker(clock, log);
            tracker.BeginTurn(7);
            tracker.Mark(7, LatencyMark.PttPressed);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            tracker.Mark(7, LatencyMark.FirstAudioPlayed);
            tracker.Mark(7, LatencyMark.TurnCompleted);

            Assert.That(log.Entries.Count, Is.EqualTo(1));
            Assert.That(log.Entries[0].Message, Does.StartWith("evt=turn.latency turn=7 "));
            Assert.That(log.Entries[0].Message, Does.Contain("PttPressed=0 "));
            Assert.That(log.Entries[0].Message, Does.Contain("FirstAudioPlayed=1500"));
        }

        [Test]
        public void BeginTurn_FinalisesAnInterruptedTurn()
        {
            var clock = new FakeClock();
            var log = new RecordingLog();
            var tracker = new LatencyTracker(clock, log);
            tracker.BeginTurn(1);
            tracker.Mark(1, LatencyMark.PttPressed);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            tracker.Mark(1, LatencyMark.BargeIn);

            tracker.BeginTurn(2);

            Assert.That(log.Entries.Count, Is.EqualTo(1));
            Assert.That(tracker.Between(LatencyMark.PttPressed, LatencyMark.BargeIn), Is.EqualTo(400).Within(1e-6));
        }
    }
}
