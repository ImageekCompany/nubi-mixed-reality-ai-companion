using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class ToolBadgeViewTests
    {
        FakeConversationEvents _events;
        EchoLocalizer _localizer;
        FakeClock _clock;
        ToolBadgeView _view;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _events = new FakeConversationEvents();
            _localizer = new EchoLocalizer();
            _clock = new FakeClock();
            _view = PresentationAssets.Instantiate<ToolBadgeView>("ToolBadges");
            _root = _view.transform.root.gameObject;
            _view.Construct(_events, _localizer, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        ToolBadge[] Visible => _view.Slots.Where(s => s.IsVisible).ToArray();

        static TMP_Text Text(ToolBadge slot, string child) => slot.transform.Find(child).GetComponent<TMP_Text>();

        [Test]
        public void StartedTool_ShowsARunningBadgeWithItsLocalizedName()
        {
            _events.RaiseToolStarted("call_1", "capture_and_identify");

            var badge = Visible.Single();
            Assert.That(badge.Title, Is.EqualTo("[en] tool.capture_and_identify"));
            Assert.That(badge.State, Is.EqualTo(BadgeState.Running));
        }

        [Test]
        public void CompletedTool_UpdatesItsOwnBadge()
        {
            _events.RaiseToolStarted("call_1", "walk_to");
            _events.RaiseToolStarted("call_2", "capture_and_identify");

            _events.RaiseToolCompleted("call_1", "walk_to", false);

            var walk = Visible.Single(b => b.CallId == "call_1");
            Assert.That(walk.State, Is.EqualTo(BadgeState.Failed));
            Assert.That(walk.Detail, Is.EqualTo("[en] badge.failed"));
            Assert.That(Visible.Single(b => b.CallId == "call_2").State, Is.EqualTo(BadgeState.Running));
        }

        [Test]
        public void ModelCall_ShowsTheModelByNameAndItsDuration()
        {
            _events.RaiseModelCall("sam-3.1", 812.4);

            var badge = Visible.Single();
            Assert.That(badge.Title, Is.EqualTo("SAM 3.1"));
            Assert.That(badge.Detail, Is.EqualTo("812 ms"));
        }

        [TestCase("muse-spark-1.3", "Muse Spark")]
        [TestCase("muse-spark-1.3+vision", "Muse Spark · [en] badge.vision")]
        [TestCase("sam-3.1", "SAM 3.1")]
        [TestCase("muse-voice-transcribe-1.0", "Muse Voice Transcribe")]
        [TestCase("some-new-model", "some-new-model")]
        public void ModelLabels_NameTheMetaModels(string id, string label)
        {
            Assert.That(ModelLabels.Of(id, _localizer), Is.EqualTo(label));
        }

        [UnityTest]
        public IEnumerator FinishedBadges_HideAfterTheHold_RunningOnesStay() => UniTask.ToCoroutine(async () =>
        {
            _events.RaiseToolStarted("call_1", "follow_user");
            _events.RaiseModelCall("muse-spark-1.3", 1210);

            _clock.Advance(TimeSpan.FromSeconds(4.1));
            await PresentationAssets.AfterUpdate();

            Assert.That(Visible.Select(b => b.CallId), Is.EqualTo(new[] { "call_1" }));
        });

        [TestCase(ConversationState.Listening)]
        [TestCase(ConversationState.Idle)]
        public void TurnEndingWithoutToolCompleted_FailsTheRunningBadge(ConversationState to)
        {
            _events.RaiseToolStarted("call_1", "capture_and_identify");

            _events.RaiseState(ConversationState.Speaking, to);

            var badge = Visible.Single();
            Assert.That(badge.State, Is.EqualTo(BadgeState.Failed));
            Assert.That(badge.HideAt, Is.EqualTo(_clock.Now + 4.0).Within(1e-6));
        }

        [Test]
        public void ThinkingOrActing_LeaveTheRunningBadge()
        {
            _events.RaiseToolStarted("call_1", "walk_to");

            _events.RaiseState(ConversationState.Thinking, ConversationState.Acting);

            Assert.That(Visible.Single().State, Is.EqualTo(BadgeState.Running));
        }

        [Test]
        public void MoreEventsThanSlots_ReuseTheSlotClosestToExpiry()
        {
            for (var i = 0; i < _view.Slots.Length + 2; i++)
            {
                _clock.Advance(TimeSpan.FromSeconds(0.1));
                _events.RaiseModelCall("model_" + i, i);
            }

            Assert.That(Visible.Length, Is.EqualTo(_view.Slots.Length));
            Assert.That(Visible.Select(b => b.Title), Does.Contain("model_" + (_view.Slots.Length + 1)));
            Assert.That(Visible.Select(b => b.Title), Does.Not.Contain("model_0"));
        }

        [Test]
        public void EverySlot_TitleAndDetailCannotWrap()
        {
            foreach (var slot in _view.Slots)
            {
                Assert.That(Text(slot, "Title").textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap), slot.name);
                Assert.That(Text(slot, "Detail").textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap), slot.name);
            }
        }

        // TMP replaces a material whose atlas is not the font's with the font's own at Awake, with no error, and that one
        // loses the depth test to a nearer Jolly again. Plate and text share the queue so the canvas order holds.
        [Test]
        public void EverySlot_DrawsOverTheCharacter()
        {
            foreach (var slot in _view.Slots)
            {
                var plate = slot.GetComponent<Image>().material;
                Assert.That(plate.shader.name, Is.EqualTo("Jolly/UI/Overlay"), slot.name);
                Assert.That(plate.renderQueue, Is.EqualTo(3000), slot.name);
                foreach (var line in slot.GetComponentsInChildren<TMP_Text>(true))
                {
                    var material = line.fontSharedMaterial;
                    Assert.That(material.shader.name, Is.EqualTo("TextMeshPro/Mobile/Distance Field Overlay"), line.name);
                    Assert.That(material.renderQueue, Is.EqualTo(3000), line.name);
                    Assert.That(material.GetTexture("_MainTex") == line.font.atlasTexture, Is.True, line.name);
                }
            }
        }

        [TestCase("Muse Voice Transcribe")]
        [TestCase("Muse Spark \u00B7 vision")]
        [TestCase("Wzruszam ramionami")]
        public void LongestLabels_FitEverySlotWithoutTruncation(string label)
        {
            foreach (var slot in _view.Slots)
                slot.Show("call", label, "done", BadgeState.Succeeded, double.PositiveInfinity);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_view.transform);

            foreach (var slot in _view.Slots)
            {
                var title = Text(slot, "Title");
                title.ForceMeshUpdate(true);

                var width = title.rectTransform.rect.width;
                Assert.That(width, Is.EqualTo(224f).Within(0.01f), slot.name);
                Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(title.fontSizeMin), slot.name);
                Assert.That(title.isTextTruncated, Is.False, slot.name);
                Assert.That(title.textInfo.lineCount, Is.EqualTo(1), slot.name);
                Assert.That(title.textInfo.characterCount, Is.EqualTo(label.Length), slot.name);
                Assert.That(title.textBounds.size.x, Is.LessThanOrEqualTo(width), slot.name);
            }
        }

        [UnityTest]
        public IEnumerator Destroy_UnsubscribesFromEveryEvent() => UniTask.ToCoroutine(async () =>
        {
            Assert.That(_events.SubscriberCount, Is.GreaterThan(0));

            Object.Destroy(_root);
            await UniTask.NextFrame();

            Assert.That(_events.SubscriberCount, Is.Zero);
            Assert.That(_localizer.SubscriberCount, Is.Zero);
        });
    }
}
