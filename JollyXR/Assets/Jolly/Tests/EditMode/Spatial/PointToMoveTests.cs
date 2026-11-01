using System;
using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Spatial;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class PointToMoveTests
    {
        sealed class FakePointer : IPointerInput
        {
            public Ray? Aim;
            public PointerSource Source = PointerSource.Hand;

            public event Action<Ray> TriggerPressed;

            public bool TryGetAim(out Ray ray, out PointerSource source)
            {
                ray = Aim ?? default;
                source = Source;
                return Aim.HasValue;
            }

            public void PullTrigger(Ray ray) => TriggerPressed?.Invoke(ray);
        }

        sealed class RecordingMarker : IMoveMarker
        {
            public readonly List<string> Calls = new();
            public Vector3? Shown;

            public void Show(Vector3 floorPoint)
            {
                Shown = floorPoint;
                Calls.Add("show");
            }

            public void Hide()
            {
                Shown = null;
                Calls.Add("hide");
            }

            public void Confirm(Vector3 floorPoint) => Calls.Add("confirm");
        }

        static readonly Vector3 Head = new(0f, 1.6f, 0f);
        static readonly Vector3 Spot = new(1f, 0f, 1.2f);

        FakeVoiceActivation _rawPinch;
        FakeClock _clock;
        FakePointer _pointer;
        RecordingMarker _marker;
        FakeWorldQuery _world;
        RecordingCharacterController _character;
        RecordingLog _log;
        PointMoveSettings _settings;
        TapHoldClassifier _pinch;
        PointerTargeting _targeting;
        PointToMove _move;
        List<(MoveTarget Target, PointerSource Source)> _moves;

        [SetUp]
        public void SetUp()
        {
            _rawPinch = new FakeVoiceActivation();
            _clock = new FakeClock();
            _pointer = new FakePointer();
            _marker = new RecordingMarker();
            // No geometry to hit: rays meet the floor plane at the character's feet, all of it walkable.
            _world = new FakeWorldQuery();
            _character = new RecordingCharacterController { Position = new Vector3(0f, 0f, 1f) };
            _log = new RecordingLog();
            _settings = new PointMoveSettings();
            var resolver = new MoveTargetResolver(_world, _character, _settings, new LocomotionSettings());
            _targeting = new PointerTargeting(_pointer, resolver, _marker);
            _pinch = new TapHoldClassifier(_rawPinch, _clock, _settings.TapMax, _targeting.TryCapture);
            _move = new PointToMove(_pinch, _pointer, _targeting, resolver, _character, _settings, _marker, _clock, _log);
            _moves = new List<(MoveTarget, PointerSource)>();
            _move.Moved += (target, source) => _moves.Add((target, source));
        }

        [TearDown]
        public void TearDown()
        {
            _move.Dispose();
            _pinch.Dispose();
        }

        static Ray Toward(Vector3 point) => new(Head, point - Head);

        void Tap()
        {
            _rawPinch.Press();
            _clock.Advance(TimeSpan.FromSeconds(0.1));
            _rawPinch.Release();
        }

        [Test]
        public void Tick_ShowsTheRingWhereTheRayMeetsWalkableFloor_AndHidesItOtherwise()
        {
            _pointer.Aim = Toward(Spot);
            _targeting.Tick();
            Assert.That(Vector3.Distance(_marker.Shown.Value, Spot), Is.LessThan(1e-3f));

            _pointer.Aim = null;
            _targeting.Tick();
            Assert.That(_marker.Shown, Is.Null);
        }

        [Test]
        public void TapOnTheFloor_WalksThereFacingTheUser()
        {
            _pointer.Aim = Toward(Spot);

            Tap();

            Assert.That(Vector3.Distance(_character.MoveTarget.Value, Spot), Is.LessThan(1e-3f));
            Assert.That(_character.MoveStopDistance, Is.EqualTo(_settings.StopDistance));
            Assert.That(_character.LastLook?.Kind, Is.EqualTo(LookTargetKind.User));
            Assert.That(_marker.Calls[^1], Is.EqualTo("confirm"));
            Assert.That(_moves, Has.Count.EqualTo(1));
            Assert.That(_moves[0].Source, Is.EqualTo(PointerSource.Hand));
            Assert.That(_log.Contains(LogLevel.Info, "evt=move.request source=hand kind=floor dist_m=1.02 status=walking"),
                Is.True, string.Join("\n", _log.Entries));
            Assert.That(_log.Contains(LogLevel.Info, "evt=move.arrived"), Is.True);
        }

        [Test]
        public void TheTargetIsTheOneUnderTheRayAtThePinch_NotAtTheRelease()
        {
            _pointer.Aim = Toward(Spot);
            _rawPinch.Press();
            _pointer.Aim = Toward(new Vector3(-1f, 0f, 2f));
            _targeting.Tick();
            _rawPinch.Release();

            Assert.That(Vector3.Distance(_character.MoveTarget.Value, Spot), Is.LessThan(1e-3f));
        }

        [Test]
        public void Hold_TalksAndNeverMoves_AndHidesTheRingUntilReleased()
        {
            _pointer.Aim = Toward(Spot);
            var pressed = 0;
            _pinch.Pressed += () => pressed++;

            _rawPinch.Press();
            _clock.Advance(_settings.TapMax);
            _targeting.Tick();

            Assert.That(pressed, Is.EqualTo(1));
            Assert.That(_marker.Shown, Is.Null);
            _rawPinch.Release();
            Assert.That(_character.MoveTarget, Is.Null);
            _targeting.Tick();
            Assert.That(_marker.Shown, Is.Not.Null, "the ring returns after the talk");
        }

        [Test]
        public void PinchWithNoTarget_IsTalkAtOnce_AndMovesNothing()
        {
            _pointer.Aim = new Ray(Head, Vector3.up);
            var pressed = 0;
            _pinch.Pressed += () => pressed++;

            Tap();

            Assert.That(pressed, Is.EqualTo(1));
            Assert.That(_character.MoveTarget, Is.Null);
            Assert.That(_moves, Is.Empty);
        }

        [Test]
        public void TriggerOnTheFloor_WalksThere()
        {
            _pointer.PullTrigger(Toward(Spot));

            Assert.That(Vector3.Distance(_character.MoveTarget.Value, Spot), Is.LessThan(1e-3f));
            Assert.That(_moves[0].Source, Is.EqualTo(PointerSource.Controller));
            Assert.That(_log.Contains(LogLevel.Info, "evt=move.request source=controller"), Is.True);
        }

        [Test]
        public void TriggerIntoTheAir_DoesNothing()
        {
            _pointer.PullTrigger(new Ray(Head, Vector3.forward));

            Assert.That(_character.MoveTarget, Is.Null);
            Assert.That(_log.Entries, Is.Empty);
        }

        [Test]
        public void UnreachableTarget_IsLoggedAndNotConfirmed()
        {
            _character.Reachable = false;
            _pointer.Aim = Toward(Spot);

            Tap();

            Assert.That(_marker.Calls, Does.Not.Contain("confirm"));
            Assert.That(_moves, Is.Empty);
            Assert.That(_log.Contains(LogLevel.Warning, "status=unreachable"), Is.True);
        }

        [Test]
        public void TapWhileTheCharacterSpeaks_OnlyMoves()
        {
            // The conversation sees nothing of a tap: no Pressed, so no barge-in and no turn.
            _pointer.Aim = Toward(Spot);
            var talk = 0;
            _pinch.Pressed += () => talk++;
            _pinch.Released += () => talk++;

            Tap();

            Assert.That(talk, Is.Zero);
            Assert.That(_character.MoveTarget, Is.Not.Null);
        }
    }
}
