using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class BargeInAndRoutingTests
    {
        ConversationFixture _f;

        [SetUp]
        public void SetUp() => _f = new ConversationFixture().Start();

        [TearDown]
        public void TearDown() => _f.Dispose();

        [Test]
        public void PressWhileSpeaking_CancelsBeforeFlushing_AndStartsListening()
        {
            _f.Agent.Say("First sentence. ").Hold().Say("Never said.");
            _f.Talk();
            _f.Queue.PlayNext();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));

            _f.Voice.Press();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Agent.InterruptedSpoken, Is.EqualTo("First sentence. "),
                "the turn's spoken record survives the queue clear");
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Queue.Clears, Is.EqualTo(1));
            Assert.That(_f.Mic.Sessions.Count, Is.EqualTo(2));
            Assert.That(_f.Log.Contains(LogLevel.Info, "BargeIn="), Is.True);
        }

        [Test]
        public void SpokenRecord_CoversOnlyTheCurrentTurn()
        {
            _f.Agent.Say("First answer.");
            _f.Talk("one");
            _f.Queue.Drain();
            _f.Agent.Say("Second answer. ").Hold();

            _f.Talk("two");
            Assert.That(_f.Agent.SpokenSoFar(), Is.Empty);
            _f.Queue.PlayNext();

            Assert.That(_f.Agent.SpokenSoFar(), Is.EqualTo("Second answer. "));
        }

        [Test]
        public void FirstSampleOfFlushedSpeech_ArrivingAfterTheBargeIn_IsIgnored()
        {
            _f.Agent.Say("One. Two.").Hold();
            _f.Talk();
            _f.Queue.PlayNext();
            _f.Voice.Press();

            _f.Output.RaiseFirstSample(_f.Queue.Enqueued[1].utteranceId);

            Assert.That(_f.Captions, Is.EqualTo(new[] { "One." }));
            Assert.That(_f.Machine.SpeechPending, Is.False);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void PressWhileActing_KeepsLocomotionRunning()
        {
            _f.Agent.ToolStarted("walk_to").Hold();
            _f.Talk("walk to the sofa");
            Assert.That(_f.State, Is.EqualTo(ConversationState.Acting));

            _f.Voice.Press();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Character.StopCount, Is.Zero);
        }

        [Test]
        public void TurnAfterBargeIn_RunsNormally()
        {
            _f.Agent.Say("Long answer. ").Hold();
            _f.Talk("first");
            _f.Voice.Press();
            _f.Agent.Say("Short.");
            _f.Stt.Final = "second";
            _f.Voice.Release();
            _f.Queue.Drain();

            Assert.That(_f.Agent.Turns.Select(t => t.Text), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(_f.Agent.Turns[1].TurnId, Is.EqualTo(2));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void PressWhileListening_IsIgnored()
        {
            _f.Stt.Hang = true;
            _f.Talk();

            _f.Voice.Press();

            Assert.That(_f.Mic.Sessions.Count, Is.EqualTo(1));
            Assert.That(_f.Output.Flushes, Is.Zero);
        }

        [TestCase(Connectivity.Offline, LineCategory.Offline)]
        [TestCase(Connectivity.Unauthorized, LineCategory.Error)]
        [TestCase(Connectivity.BudgetReached, LineCategory.BudgetReached)]
        public void NotOnline_PlaysACannedLineWithoutListening(Connectivity state, LineCategory expected)
        {
            _f.Connectivity.State = state;
            _f.Offline.Hold = true;

            _f.Voice.Press();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));
            _f.Offline.Release();
            _f.Voice.Release();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { expected }));
            Assert.That(_f.StatePath(), Is.EqualTo("Speaking>Idle"));
            Assert.That(_f.Mic.Sessions, Is.Empty);
            Assert.That(_f.Agent.Turns, Is.Empty);
        }

        [Test]
        public void BudgetCap_WinsOverOnlineConnectivity()
        {
            _f.Usage.ReachCap();

            _f.Talk();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.BudgetReached }));
        }

        [Test]
        public void MicUnavailable_PlaysTheErrorLineNotTheOfflineOne()
        {
            _f.Mic.IsAvailable = false;

            _f.Talk();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.Error }));
            Assert.That(_f.Mic.Sessions, Is.Empty);
        }

        [Test]
        public void PressWhileAnInteractionLinePlaysAtIdle_SilencesItAndListens()
        {
            _f.Output.IsPlaying = true;

            _f.Voice.Press();

            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Queue.Clears, Is.EqualTo(1));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void PressAtIdleWithNothingPlaying_FlushesNothing()
        {
            _f.Voice.Press();

            Assert.That(_f.Output.Flushes, Is.Zero);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void OfflinePressWhileSpeaking_BargesInWithTheCannedLine()
        {
            _f.Agent.Say("Online answer. ").Hold();
            _f.Talk();
            _f.Queue.PlayNext();
            _f.Connectivity.State = Connectivity.Offline;

            _f.Voice.Press();

            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.Offline }));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void CrisisWords_TheDefaultCompanionSpeaksTheHandoffBeforeTheModelAndAnnotatesTheTurn()
        {
            _f.Talk("Sometimes I want to die.");

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.CrisisHandoff }));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Sometimes I want to die." + TurnGuards.CrisisNote));
        }

        [Test]
        public void ComeHere_StartsTheWalkBeforeTheModelAndAnnotatesTheTurn()
        {
            _f.Talk("Hi Nubi, come here.");

            Assert.That(_f.Character.Mode, Is.EqualTo(LocomotionMode.MovingTo));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Hi Nubi, come here." + TurnGuards.ComeHereNote));
        }

        [Test]
        public void Greeting_LeavesMovementToTheModel()
        {
            _f.Talk("Hi Nubi, who are you?");

            Assert.That(_f.Character.Mode, Is.EqualTo(LocomotionMode.None));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Hi Nubi, who are you?"));
        }

        [Test]
        public void NoCrisisWords_NeverHandsOff()
        {
            _f.Talk("Sometimes I want a nap.");

            Assert.That(_f.Offline.Categories, Is.Empty);
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Sometimes I want a nap."));
        }
    }
}
