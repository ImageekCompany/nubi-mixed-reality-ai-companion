using System;
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
    public sealed class IdleSchedulerTests
    {
        const float LookAroundRoll = 0.5f;
        const float StretchRoll = 1.5f;
        const float WanderRoll = 2.5f;
        const float GlanceRoll = 3.5f;

        FakeCharacter _character;
        FakeClock _clock;
        FakeRandom _random;
        bool _conversationIdle;
        CancellationTokenSource _cts;
        UniTask _run;

        static IdleConfig Config() => new()
        {
            MinInterval = TimeSpan.FromSeconds(8),
            MaxInterval = TimeSpan.FromSeconds(20),
            LookAroundWeight = 1f,
            StretchWeight = 1f,
            WanderWeight = 1f,
            GlanceWeight = 1f,
            WanderMinDistance = 0.5f,
            WanderRadius = 1.5f,
            GlanceDuration = TimeSpan.FromSeconds(2.5),
        };

        [SetUp]
        public void SetUp()
        {
            _character = new FakeCharacter { Position = new Vector3(1f, 0.2f, 2f) };
            _clock = new FakeClock();
            _random = new FakeRandom();
            _conversationIdle = true;
            _cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        void Start()
        {
            var scheduler = new IdleScheduler(_character, _clock, _random, Config());
            _run = scheduler.RunAsync(() => _conversationIdle, _cts.Token);
        }

        void Advance(double seconds) => _clock.Advance(TimeSpan.FromSeconds(seconds));

        [Test]
        public void RunAsync_ActsOnlyAfterTheRandomInterval()
        {
            _random.EnqueueFloat(12f, LookAroundRoll);
            Start();

            Advance(11.9);
            var before = _character.ActionCount;
            Advance(0.1);

            Assert.That(before, Is.Zero);
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.LookAround }));
        }

        [Test]
        public void RunAsync_IntervalIsDrawnBetweenMinAndMax()
        {
            _random.EnqueueFloat(1f, LookAroundRoll, 99f, StretchRoll);
            Start();

            Advance(8);
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.LookAround }));
            Advance(19.9);
            Assert.That(_character.Gestures.Count, Is.EqualTo(1));
            Advance(0.1);
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.LookAround, GestureId.Stretch }));
        }

        [Test]
        public void RunAsync_StretchRoll_PlaysStretch()
        {
            _random.EnqueueFloat(8f, StretchRoll);
            Start();

            Advance(8);

            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.Stretch }));
        }

        [Test]
        public void RunAsync_WanderRoll_StartsAMoveWithinTheRadiusOnTheCharactersFloor()
        {
            _random.EnqueueFloat(8f, WanderRoll, 0f, 1f);
            Start();

            Advance(8);

            Assert.That(_character.MoveTargets.Count, Is.EqualTo(1));
            var target = _character.MoveTargets[0];
            Assert.That(Vector3.Distance(target, new Vector3(2f, 0.2f, 2f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void RunAsync_WanderDistance_IsDrawnWithinTheConfiguredRange()
        {
            _random.EnqueueFloat(8f, WanderRoll, 1f, 99f);
            Start();

            Advance(8);

            var offset = _character.MoveTargets[0] - _character.Position;
            Assert.That(offset.y, Is.EqualTo(0f));
            Assert.That(offset.magnitude, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void RunAsync_GlanceRoll_LooksAtTheUserThenReleasesTheLook()
        {
            _random.EnqueueFloat(8f, GlanceRoll);
            Start();

            Advance(8);
            Assert.That(_character.Looks.Count, Is.EqualTo(1));
            Assert.That(_character.Looks[0].Kind, Is.EqualTo(LookTargetKind.User));
            Advance(2.5);

            Assert.That(_character.Looks.Count, Is.EqualTo(2));
            Assert.That(_character.Looks[1].Kind, Is.EqualTo(LookTargetKind.None));
        }

        [Test]
        public void RunAsync_GlanceEndingAfterAConversationStarted_LeavesTheLookAlone()
        {
            _random.EnqueueFloat(8f, GlanceRoll);
            Start();
            Advance(8);

            _conversationIdle = false;
            Advance(2.5);

            Assert.That(_character.Looks.Count, Is.EqualTo(1));
        }

        [Test]
        public void RunAsync_WhenTheConversationIsNotIdle_DoesNothing()
        {
            _conversationIdle = false;
            Start();

            Advance(120);

            Assert.That(_character.ActionCount, Is.Zero);
        }

        [Test]
        public void RunAsync_WhenTheCharacterIsMoving_DoesNothing()
        {
            _character.IsMoving = true;
            Start();

            Advance(120);

            Assert.That(_character.ActionCount, Is.Zero);
        }

        [TestCase(LocomotionMode.Following)]
        [TestCase(LocomotionMode.MovingTo)]
        public void RunAsync_WhenTheCharacterHasALocomotionMode_DoesNothing(LocomotionMode mode)
        {
            _character.Mode = mode;
            Start();

            Advance(120);

            Assert.That(_character.ActionCount, Is.Zero);
        }

        [Test]
        public void RunAsync_ResumesOnceTheConversationIsIdleAgain()
        {
            _conversationIdle = false;
            _random.EnqueueFloat(8f, 8f, LookAroundRoll);
            Start();
            Advance(8);

            _conversationIdle = true;
            Advance(8);

            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.LookAround }));
        }

        [Test]
        public void RunAsync_WhenCancelled_StopsActing()
        {
            Start();

            _cts.Cancel();
            Advance(120);

            Assert.That(_run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_character.ActionCount, Is.Zero);
        }
    }
}
