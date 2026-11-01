using Jolly.Contracts;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    /// <summary>Architecture §6 watchdogs: each resets to Idle, cancels the turn and flushes audio.</summary>
    public sealed class WatchdogTests
    {
        ConversationFixture _f;

        [SetUp]
        public void SetUp() => _f = new ConversationFixture().Start();

        [TearDown]
        public void TearDown() => _f.Dispose();

        [Test]
        public void Listening_ExpiresFiveSecondsAfterReleaseWithoutAFinal()
        {
            _f.Stt.Hang = true;
            _f.Talk();

            _f.Advance(4.9);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            _f.Advance(0.2);

            AssertReset(ConversationState.Listening);
            Assert.That(_f.Mic.Last.Disposed, Is.True);
        }

        [Test]
        public void Listening_NeverExpiresWhileThePinchIsHeld()
        {
            _f.Voice.Press();

            _f.Advance(120);

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void Thinking_ExpiresAfterTwentySeconds()
        {
            _f.Agent.Hold();
            _f.Talk();

            _f.Advance(19.9);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Thinking));
            _f.Advance(0.2);

            AssertReset(ConversationState.Thinking);
            Assert.That(_f.Agent.InterruptedSpoken, Is.Not.Null, "the agent saw the cancellation");
        }

        [Test]
        public void Acting_ExpiresAfterThirtySeconds()
        {
            _f.Agent.ToolStarted("capture_and_identify").Hold();
            _f.Talk();

            _f.Advance(29.9);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Acting));
            _f.Advance(0.2);

            AssertReset(ConversationState.Acting);
        }

        [Test]
        public void Speaking_ExpiresAfterSixtySeconds()
        {
            _f.Agent.Say("Hello.").Hold();
            _f.Talk();
            _f.Queue.PlayNext();

            _f.Advance(59.9);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));
            _f.Advance(0.2);

            AssertReset(ConversationState.Speaking);
        }

        [Test]
        public void EnteringANewState_RestartsTheTimer()
        {
            _f.Agent.ToolStarted("walk_to").Hold().ToolCompleted("walk_to").Hold();
            _f.Talk();
            _f.Advance(25);

            _f.Agent.Release();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Thinking));
            _f.Advance(19.9);

            Assert.That(_f.State, Is.EqualTo(ConversationState.Thinking));
        }

        [Test]
        public void CompletedTurn_LeavesNoTimerRunning()
        {
            _f.Agent.Say("Hi.");
            _f.Talk();
            _f.Queue.Drain();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void AfterAWatchdog_TheNextTurnWorks()
        {
            _f.Agent.Hold();
            _f.Talk("first");
            _f.Advance(21);

            _f.Agent.Say("Fine.");
            _f.Talk("second");
            _f.Queue.Drain();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Captions, Is.EqualTo(new[] { "Fine." }));
        }

        void AssertReset(ConversationState from)
        {
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Queue.Clears, Is.EqualTo(1));
            Assert.That(_f.Log.Contains(LogLevel.Warning, $"evt=watchdog state={from}"), Is.True);
        }
    }
}
