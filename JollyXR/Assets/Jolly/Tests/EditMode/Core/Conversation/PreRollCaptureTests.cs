using System;
using Jolly.Core.Conversation;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class PreRollCaptureTests
    {
        const int Rate = 24000;
        static readonly TimeSpan TapMax = TimeSpan.FromSeconds(0.25);

        FakeVoiceActivation _pinch;
        FakeClock _clock;
        FakeAudioCapture _mic;
        bool _canPreRoll;
        TapHoldClassifier _classifier;
        PreRollCapture _capture;

        [SetUp]
        public void SetUp()
        {
            _pinch = new FakeVoiceActivation();
            _clock = new FakeClock();
            _mic = new FakeAudioCapture();
            _canPreRoll = true;
            _classifier = new TapHoldClassifier(_pinch, _clock, TapMax, () => true);
            _capture = new PreRollCapture(_mic, _classifier, Rate, () => _canPreRoll);
        }

        [TearDown]
        public void TearDown()
        {
            _capture.Dispose();
            _classifier.Dispose();
        }

        [Test]
        public void WaitingPress_StartsTheMicAtThePinch()
        {
            _pinch.Press();

            Assert.That(_mic.Sessions, Has.Count.EqualTo(1));
            Assert.That(_mic.Last.SampleRate, Is.EqualTo(Rate));
            Assert.That(_capture.IsPrimed, Is.True);
        }

        [Test]
        public void Hold_HandsThePrimedSessionToTheTurn()
        {
            _pinch.Press();
            var primed = _mic.Last;
            _clock.Advance(TapMax);

            var session = _capture.Start(Rate);

            Assert.That(session, Is.SameAs(primed));
            Assert.That(_mic.Sessions, Has.Count.EqualTo(1), "no second start, so no gap in the audio");
            _pinch.Release();
            Assert.That(primed.Disposed, Is.False, "the turn owns it now");
        }

        [Test]
        public void Tap_DisposesThePrimedSessionUnheard()
        {
            _pinch.Press();
            var primed = _mic.Last;
            _pinch.Release();

            Assert.That(primed.Disposed, Is.True);
            Assert.That(_capture.IsPrimed, Is.False);
        }

        [Test]
        public void HoldNoTurnClaimed_IsDisposedAtRelease()
        {
            _pinch.Press();
            var primed = _mic.Last;
            _clock.Advance(TapMax);
            _pinch.Release();

            Assert.That(primed.Disposed, Is.True);
        }

        [Test]
        public void WhileThePreRollIsNotAllowed_TheTurnStartsItsOwnSession()
        {
            _canPreRoll = false;
            _pinch.Press();
            Assert.That(_mic.Sessions, Is.Empty);
            _clock.Advance(TapMax);

            _capture.Start(Rate);

            Assert.That(_mic.Sessions, Has.Count.EqualTo(1));
        }

        [Test]
        public void StartAtAnotherRate_ReplacesThePrimedSession()
        {
            _pinch.Press();
            var primed = _mic.Last;

            var session = _capture.Start(16000);

            Assert.That(primed.Disposed, Is.True);
            Assert.That(session, Is.Not.SameAs(primed));
        }

        [Test]
        public void UnavailableMic_IsNotPrimed()
        {
            _mic.IsAvailable = false;

            _pinch.Press();

            Assert.That(_mic.Sessions, Is.Empty);
        }
    }
}
