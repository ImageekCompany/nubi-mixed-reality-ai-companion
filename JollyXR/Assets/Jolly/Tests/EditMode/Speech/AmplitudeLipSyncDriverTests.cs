using System;
using Jolly.Audio.LipSync;
using Jolly.Audio.Output;
using Jolly.Contracts;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class AmplitudeLipSyncDriverTests
    {
        sealed class SettableLevel : ISpeechLevel
        {
            public float Level { get; set; }
        }

        FakeClock _clock;
        SettableLevel _level;
        AmplitudeLipSyncDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _level = new SettableLevel();
            _driver = new AmplitudeLipSyncDriver(_level, _clock);
        }

        [Test]
        public void Current_Silence_IsClosed()
        {
            Assert.That(_driver.Current, Is.EqualTo(MouthShape.Closed));
        }

        [Test]
        public void Current_Speech_OpensTheMouthImmediately()
        {
            _level.Level = 0.2f;

            var shape = _driver.Current;

            Assert.That(shape.A, Is.GreaterThan(0.5f));
            Assert.That(shape.Volume, Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void Current_LouderSpeech_OpensWiderButNeverPastOne()
        {
            _level.Level = 0.05f;
            var quiet = _driver.Current.A;
            _level.Level = 0.9f;

            var loud = _driver.Current.A;

            Assert.That(loud, Is.GreaterThan(quiet));
            Assert.That(loud, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void Current_AfterSilence_ClosesWithin100Ms()
        {
            _level.Level = 0.3f;
            _ = _driver.Current;
            _level.Level = 0f;

            _clock.Advance(TimeSpan.FromMilliseconds(40));
            var closing = _driver.Current;
            _clock.Advance(TimeSpan.FromMilliseconds(60));
            var closed = _driver.Current;

            Assert.That(closing.A, Is.GreaterThan(0f));
            Assert.That(closed, Is.EqualTo(MouthShape.Closed));
        }

        [Test]
        public void Reset_ClosesAtOnce()
        {
            _level.Level = 0.3f;
            _ = _driver.Current;
            _level.Level = 0f;

            _driver.Reset();

            Assert.That(_driver.Current, Is.EqualTo(MouthShape.Closed));
        }
    }
}
