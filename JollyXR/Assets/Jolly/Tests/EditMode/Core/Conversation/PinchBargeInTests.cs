using System;
using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    /// <summary>A pinch with the ring showing, wired as PointServices and AppGraph wire it (ADR-0044).</summary>
    public sealed class PinchBargeInTests
    {
        static readonly TimeSpan TapMax = TimeSpan.FromSeconds(0.25);

        ConversationFixture _f;
        FakeVoiceActivation _pinch;
        TapHoldClassifier _classifier;

        [SetUp]
        public void SetUp()
        {
            _f = new ConversationFixture().Start();
            _pinch = new FakeVoiceActivation();
            _classifier = new TapHoldClassifier(_pinch, _f.Clock, TapMax, () => true);
            _classifier.Interrupting += _f.Controller.InterruptBusyTurn;
            _classifier.Pressed += _f.Voice.Press;
            _classifier.Released += _f.Voice.Release;
        }

        [TearDown]
        public void TearDown()
        {
            _classifier.Dispose();
            _f.Dispose();
        }

        void Speaking()
        {
            _f.Agent.Say("First sentence. ").Hold().Say("Never said.");
            _f.Talk();
            _f.Queue.PlayNext();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));
        }

        [Test]
        public void WhileSpeaking_ThePressAloneSilencesTheTurn()
        {
            // Arrange
            Speaking();

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Queue.Clears, Is.EqualTo(1));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Agent.InterruptedSpoken, Is.EqualTo("First sentence. "));
        }

        [Test]
        public void WhileThinking_ThePressAloneCancelsTheTurn()
        {
            // Arrange
            _f.Agent.Hold();
            _f.Talk();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Thinking));

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_f.Agent.InterruptedSpoken, Is.Not.Null);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void WhileActing_ThePressAloneCancelsTheTurn()
        {
            // Arrange
            _f.Agent.ToolStarted("look_at").Hold();
            _f.Talk();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Acting));

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_f.Agent.InterruptedSpoken, Is.Not.Null);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void TappedDuringTheCrisisLine_DoesNotCutIt()
        {
            // Arrange
            _f.Offline.Hold = true;
            _f.Talk("I want to die");
            Assert.That(_f.Offline.Waiting, Is.EqualTo(1));

            // Act
            _pinch.Press();
            _f.Advance(0.1);
            _pinch.Release();

            // Assert
            Assert.That(_f.Output.Flushes, Is.Zero);
            Assert.That(_f.State, Is.Not.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void TappedDuringTheCrisisReply_DoesNotCutIt()
        {
            // Arrange
            _f.Agent.Say("Please call someone. ").Hold().Say("Never cut.");
            _f.Talk("I want to die");
            _f.Queue.PlayNext();

            // Act
            _pinch.Press();
            _f.Advance(0.1);
            _pinch.Release();

            // Assert
            Assert.That(_f.Output.Flushes, Is.Zero);
            Assert.That(_f.State, Is.Not.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void HeldDuringTheCrisisLine_CutsItAtTheTapTimeAndKeepsTheUsersWords()
        {
            // Arrange
            _f.Offline.Hold = true;
            _f.Talk("I want to die");

            // Act
            _pinch.Press();
            _f.Advance(TapMax.TotalSeconds + 0.1);

            // Assert
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Memory.Recent.Any(i => i.Kind == ItemKind.UserText && i.Text == "I want to die"), Is.True);
        }

        [Test]
        public void PressedWhileAnInteractionLinePlaysAtIdle_SilencesItAtThePress()
        {
            // Arrange
            _f.Output.IsPlaying = true;

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void TappedWhileSpeaking_StartsNoTurn()
        {
            // Arrange
            Speaking();
            var mics = _f.Mic.Sessions.Count;

            // Act
            _pinch.Press();
            _f.Advance(0.1);
            _pinch.Release();
            _f.Advance(1);

            // Assert
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Mic.Sessions.Count, Is.EqualTo(mics));
        }

        [Test]
        public void HeldWhileSpeaking_SilencesAtThePressThenListensAtTheTapTime()
        {
            // Arrange
            Speaking();

            // Act
            _pinch.Press();
            var early = _f.State;
            _f.Advance(0.26);

            // Assert
            Assert.That(early, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Output.Flushes, Is.EqualTo(1), "silenced once");
            Assert.That(_f.Log.Contains(LogLevel.Info, "BargeIn="), Is.True);
        }

        [Test]
        public void WhileIdle_ThePressTouchesNothing()
        {
            // Act
            _pinch.Press();

            // Assert
            Assert.That(_f.Output.Flushes, Is.Zero);
            Assert.That(_f.Queue.Clears, Is.Zero);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Log.Contains(LogLevel.Info, "BargeIn="), Is.False);
        }

        [Test]
        public void WhileListening_ThePressTouchesNothing()
        {
            // Arrange
            _f.Voice.Press();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_f.Output.Flushes, Is.Zero);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void PressWithNoTarget_BargesInOnceAsBefore()
        {
            // Arrange
            _classifier.Dispose();
            _classifier = new TapHoldClassifier(_pinch, _f.Clock, TapMax, () => false);
            _classifier.Interrupting += _f.Controller.InterruptBusyTurn;
            _classifier.Pressed += _f.Voice.Press;
            _classifier.Released += _f.Voice.Release;
            Speaking();

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
        }
    }
}
