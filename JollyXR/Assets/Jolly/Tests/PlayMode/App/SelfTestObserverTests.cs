using System;
using Jolly.App.SelfTest;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class SelfTestObserverTests
    {
        const string Part = "highlight_part";

        FakeClock _clock;
        ConversationEvents _events;
        TurnObserver _observer;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _events = new ConversationEvents();
            _observer = new TurnObserver(_events, _clock);
        }

        [TearDown]
        public void TearDown() => _observer.Dispose();

        [Test]
        public void ToolStartedAt_IsTheFirstStartOfThatTool()
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            _events.RaiseToolStarted("c1", "play_gesture");
            _clock.Advance(TimeSpan.FromSeconds(1));
            _events.RaiseToolStarted("c2", Part);
            _clock.Advance(TimeSpan.FromSeconds(1));
            _events.RaiseToolStarted("c3", Part);

            Assert.That(_observer.ToolStartedAt(Part), Is.EqualTo(2.0));
            Assert.That(_observer.ToolStartedAt("find_object"), Is.NaN);
        }

        [Test]
        public void AnswerAfter_IsTheFirstCaptionAfterTheToolSucceeded()
        {
            _events.RaiseCaptionStarted(1, "Let me look.");
            _clock.Advance(TimeSpan.FromSeconds(0.5));
            _events.RaiseToolStarted("c1", Part);
            _clock.Advance(TimeSpan.FromSeconds(1.0));
            _events.RaiseToolCompleted("c1", Part, true);
            _clock.Advance(TimeSpan.FromSeconds(1.2));
            _events.RaiseCaptionStarted(2, "Hold it by the handle.");
            _clock.Advance(TimeSpan.FromSeconds(1.0));
            _events.RaiseCaptionStarted(3, "It stays cool.");

            Assert.That(_observer.AnswerAfter(Part), Is.EqualTo(2.2).Within(1e-9), "from the tool start");
        }

        [Test]
        public void AnswerAfter_FailedOrMissingTool_IsNaN()
        {
            _events.RaiseToolStarted("c1", Part);
            _events.RaiseToolCompleted("c1", Part, false);
            _clock.Advance(TimeSpan.FromSeconds(1));
            _events.RaiseCaptionStarted(1, "I could not find it.");

            Assert.That(_observer.AnswerAfter(Part), Is.NaN);
            Assert.That(_observer.AnswerAfter("find_object"), Is.NaN);
        }

        [Test]
        public void Reset_ForgetsToolTimes()
        {
            _events.RaiseToolStarted("c1", Part);
            _events.RaiseToolCompleted("c1", Part, true);
            _events.RaiseCaptionStarted(1, "There.");

            _observer.Reset();

            Assert.That(_observer.ToolStartedAt(Part), Is.NaN);
            Assert.That(_observer.AnswerAfter(Part), Is.NaN);
        }

        [Test]
        public void ToolCallIds_ParallelToToolsStarted()
        {
            _events.RaiseToolStarted("guard_come_here", "come_here");
            _events.RaiseToolStarted("c1", "come_here");

            Assert.That(_observer.ToolCallIds, Is.EqualTo(new[] { "guard_come_here", "c1" }));
        }

        [Test]
        public void Replies_AreRecordedUntilReset()
        {
            _observer.RecordReply("round=0 tools=none finish=Stop text=\"Hi!\"");

            Assert.That(_observer.Replies, Is.EqualTo(new[] { "round=0 tools=none finish=Stop text=\"Hi!\"" }));
            _observer.Reset();
            Assert.That(_observer.Replies, Is.Empty);
            Assert.That(_observer.ToolCallIds, Is.Empty);
        }

        [Test]
        public void MaskObserver_RecordsTheLastShowAndForwardsIt()
        {
            var inner = new RecordingMaskPresenter();
            var masks = new MaskObserver(inner, _clock);
            var image = new EncodedImage(new byte[1], "image/jpeg", 1, 1, default, Pose.identity);
            var objects = new[] { Mask(1), Mask(2) };
            _clock.Advance(TimeSpan.FromSeconds(3));

            masks.Show(objects, image, 0.6f);
            masks.Clear();

            Assert.That(inner.Shown, Has.Count.EqualTo(1));
            Assert.That(inner.Clears, Is.EqualTo(1));
            Assert.That(masks.ShownAt, Is.EqualTo(3.0));
            Assert.That(masks.ShownCount, Is.EqualTo(2), "a clear does not forget what was shown");
            Assert.That(masks.ShownSource, Is.SameAs(image));
        }

        [Test]
        public void MaskObserver_NothingShown_IsNaN()
        {
            var masks = new MaskObserver(new RecordingMaskPresenter(), _clock);

            Assert.That(masks.ShownAt, Is.NaN);
            Assert.That(masks.ShownCount, Is.Zero);
            Assert.That(masks.ShownSource, Is.Null);
        }

        static SegmentedObject Mask(int id) => new(id, new RectInt(0, 0, 1, 1), 0.9f, null);
    }
}
