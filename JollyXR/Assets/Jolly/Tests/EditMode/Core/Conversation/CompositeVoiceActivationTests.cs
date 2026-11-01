using System.Collections.Generic;
using Jolly.Core.Conversation;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class CompositeVoiceActivationTests
    {
        FakeVoiceActivation _pinch;
        FakeVoiceActivation _controller;
        CompositeVoiceActivation _voice;
        List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _pinch = new FakeVoiceActivation();
            _controller = new FakeVoiceActivation();
            _voice = new CompositeVoiceActivation(_pinch, _controller);
            _events = new List<string>();
            _voice.Pressed += () => _events.Add("pressed");
            _voice.Released += () => _events.Add("released");
        }

        [TearDown]
        public void TearDown() => _voice.Dispose();

        [Test]
        public void EitherSource_PressesAndReleases()
        {
            _pinch.Press();
            Assert.That(_voice.IsHeld, Is.True);
            _pinch.Release();
            _controller.Press();
            _controller.Release();

            Assert.That(_voice.IsHeld, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released", "pressed", "released" }));
        }

        [Test]
        public void SecondSourceWhileHeld_NeitherPressesAgainNorReleasesEarly()
        {
            _pinch.Press();
            _controller.Press();
            _pinch.Release();

            Assert.That(_voice.IsHeld, Is.True, "the controller still holds");
            Assert.That(_events, Is.EqualTo(new[] { "pressed" }), "no second press, so no barge-in into its own turn");

            _controller.Release();

            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }

        [Test]
        public void SourceHeldAtConstruction_StartsHeld()
        {
            _controller.Press();

            using var voice = new CompositeVoiceActivation(_pinch, _controller);

            Assert.That(voice.IsHeld, Is.True);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            _voice.Dispose();

            _pinch.Press();

            Assert.That(_voice.IsHeld, Is.False);
            Assert.That(_events, Is.Empty);
        }
    }
}
