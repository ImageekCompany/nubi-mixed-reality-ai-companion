using System;
using System.Collections.Generic;
using Jolly.Core.Conversation;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class TapHoldClassifierTests
    {
        static readonly TimeSpan TapMax = TimeSpan.FromSeconds(0.25);

        FakeVoiceActivation _pinch;
        FakeClock _clock;
        bool _canTap;
        int _captures;
        TapHoldClassifier _classifier;
        List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _pinch = new FakeVoiceActivation();
            _clock = new FakeClock();
            _canTap = true;
            _captures = 0;
            _classifier = new TapHoldClassifier(_pinch, _clock, TapMax, () =>
            {
                _captures++;
                return _canTap;
            });
            _events = new List<string>();
            _classifier.Began += () => _events.Add("began");
            _classifier.Tapped += () => _events.Add("tapped");
            _classifier.Pressed += () => _events.Add("pressed");
            _classifier.Released += () => _events.Add("released");
        }

        [TearDown]
        public void TearDown() => _classifier.Dispose();

        [Test]
        public void QuickRelease_WithATarget_IsATapAndNeverTalks()
        {
            _pinch.Press();
            _clock.Advance(TimeSpan.FromSeconds(0.1));
            _pinch.Release();
            _clock.Advance(TimeSpan.FromSeconds(1));

            Assert.That(_events, Is.EqualTo(new[] { "began", "tapped" }));
            Assert.That(_classifier.IsHeld, Is.False);
        }

        [Test]
        public void HoldPastTheThreshold_WithATarget_TalksUntilRelease()
        {
            _pinch.Press();
            _clock.Advance(TimeSpan.FromSeconds(0.2));
            Assert.That(_classifier.IsHeld, Is.False, "still waiting");

            _clock.Advance(TimeSpan.FromSeconds(0.06));
            Assert.That(_classifier.IsHeld, Is.True);
            _pinch.Release();

            Assert.That(_events, Is.EqualTo(new[] { "began", "pressed", "released" }));
            Assert.That(_classifier.IsHeld, Is.False);
        }

        [Test]
        public void Press_WithATarget_InterruptsBeforeItBegins()
        {
            // Arrange
            _classifier.Interrupting += () => _events.Add("interrupting");

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_events, Is.EqualTo(new[] { "interrupting", "began" }), "so the pre-roll hears a silent character");
        }

        [Test]
        public void Press_WithNoTarget_DoesNotRaiseInterrupting()
        {
            // Arrange
            _canTap = false;
            _classifier.Interrupting += () => _events.Add("interrupting");

            // Act
            _pinch.Press();

            // Assert
            Assert.That(_events, Is.EqualTo(new[] { "pressed" }), "the conversation's own press barges in");
        }

        [Test]
        public void Press_WithNoTarget_TalksAtOnce()
        {
            _canTap = false;

            _pinch.Press();

            Assert.That(_classifier.IsHeld, Is.True, "no wait, so pinch barge-in keeps its latency");
            Assert.That(_events, Is.EqualTo(new[] { "pressed" }));
            _pinch.Release();
            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }

        [Test]
        public void QuickRelease_WithNoTarget_IsAShortTalkPress()
        {
            _canTap = false;

            _pinch.Press();
            _pinch.Release();

            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }

        [Test]
        public void TargetIsCapturedOncePerPress()
        {
            _pinch.Press();
            _clock.Advance(TimeSpan.FromSeconds(0.5));
            _pinch.Release();
            _pinch.Press();
            _pinch.Release();

            Assert.That(_captures, Is.EqualTo(2));
        }

        [Test]
        public void TapThenNewPress_ClassifiesEachOnItsOwn()
        {
            _pinch.Press();
            _clock.Advance(TimeSpan.FromSeconds(0.2));
            _pinch.Release();
            _pinch.Press();
            _clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.That(_classifier.IsHeld, Is.False, "the first press's timer must not promote the second");

            _clock.Advance(TimeSpan.FromSeconds(0.2));

            Assert.That(_events, Is.EqualTo(new[] { "began", "tapped", "began", "pressed" }));
        }

        [Test]
        public void Dispose_WhileWaiting_RaisesNothingLater()
        {
            _pinch.Press();
            _classifier.Dispose();
            _clock.Advance(TimeSpan.FromSeconds(1));
            _pinch.Release();

            Assert.That(_events, Is.EqualTo(new[] { "began" }));
            Assert.That(_clock.PendingDelays, Is.Zero);
        }
    }
}
