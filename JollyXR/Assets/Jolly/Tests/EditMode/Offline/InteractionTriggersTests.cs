using System;
using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Offline;
using Jolly.Testing;
using Jolly.Testing.Offline;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Offline
{
    // Same-named fakes from other WPs also live in Jolly.Testing; these tests are written against this WP's.
    using FakeInteractionSignals = Jolly.Testing.Offline.FakeInteractionSignals;

    public sealed class InteractionTriggersTests
    {
        FakeInteractionSignals _signals;
        FakeClock _clock;
        bool _canSpeak;
        List<LineCategory> _requested;
        InteractionTriggers _triggers;

        [SetUp]
        public void SetUp()
        {
            _signals = new FakeInteractionSignals();
            _clock = new FakeClock();
            _canSpeak = true;
            _requested = new List<LineCategory>();
            _triggers = new InteractionTriggers(_signals, _clock, () => _canSpeak);
            _triggers.LineRequested += _requested.Add;
        }

        [TearDown]
        public void TearDown() => _triggers.Dispose();

        void Advance(double seconds) => _clock.Advance(TimeSpan.FromSeconds(seconds));

        [Test]
        public void Signals_MapToLineCategories()
        {
            _signals.RaiseGazeDwell();
            Advance(20);
            _signals.RaiseOpenPalm();
            Advance(20);
            _signals.RaisePoke();

            Assert.That(_requested, Is.EqualTo(new[] { LineCategory.Greeting, LineCategory.Greeting, LineCategory.WhoAmI }));
        }

        [Test]
        public void Signals_AreRateLimitedToOnePer20SecondsAcrossKinds()
        {
            _signals.RaiseGazeDwell();
            Advance(10);
            _signals.RaisePoke();
            Advance(9.9);
            _signals.RaiseOpenPalm();
            Advance(0.1);
            _signals.RaiseOpenPalm();

            Assert.That(_requested, Is.EqualTo(new[] { LineCategory.Greeting, LineCategory.Greeting }));
        }

        [Test]
        public void Signal_WhileNubiCannotSpeak_IsDroppedWithoutUsingTheRateLimit()
        {
            _canSpeak = false;
            _signals.RaisePoke();

            _canSpeak = true;
            Advance(1);
            _signals.RaisePoke();

            Assert.That(_requested, Is.EqualTo(new[] { LineCategory.WhoAmI }));
        }

        [Test]
        public void Dispose_UnsubscribesFromTheSignals()
        {
            _triggers.Dispose();

            Assert.That(_signals.HasSubscribers, Is.False);
        }
    }
}
