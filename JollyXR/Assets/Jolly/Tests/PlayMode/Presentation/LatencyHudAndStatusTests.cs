using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class LatencyHudAndStatusTests
    {
        FakeConversationEvents _events;
        FakeLocalizer _localizer;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _events = new FakeConversationEvents();
            _localizer = new FakeLocalizer();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        T Spawn<T>(string prefab) where T : Component
        {
            var view = PresentationAssets.Instantiate<T>(prefab);
            _root = view.transform.root.gameObject;
            return view;
        }

        [Test]
        public void Hud_ShowsTheLastTurnsBreakdown_WhenTheTurnEnds()
        {
            var tracker = new FakeLatencyTracker();
            var hud = Spawn<LatencyHud>("LatencyHud");
            hud.Construct(_events, tracker, _localizer);
            tracker.Set(LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed, 1834.6);

            _events.RaiseState(ConversationState.Speaking, ConversationState.Idle);

            Assert.That(hud.Text, Does.Contain("[en] hud.release_to_audio: 1835"));
            Assert.That(hud.Text, Does.Contain("[en] hud.llm_first_text: –"), "a missing mark shows a dash, not NaN");
        }

        [Test]
        public void Hud_ListsTheLatestModelCallsNewestFirst()
        {
            var hud = Spawn<LatencyHud>("LatencyHud");
            hud.Construct(_events, new FakeLatencyTracker(), _localizer);

            _events.RaiseModelCall("muse-spark-1.3", 1210);
            _events.RaiseModelCall("sam-3.1", 840);

            Assert.That(hud.Text.IndexOf("sam-3.1: 840", System.StringComparison.Ordinal),
                Is.LessThan(hud.Text.IndexOf("muse-spark-1.3: 1210", System.StringComparison.Ordinal)));
        }

        [Test]
        public void Status_ShowsTheLocalizedKind_AndHidesForNone()
        {
            var status = Spawn<StatusIndicator>("StatusIndicator");
            status.Construct(_localizer);
            var label = status.GetComponentInChildren<TMPro.TMP_Text>(true);

            status.Show(StatusKind.ApiKeyMissing);
            Assert.That(label.gameObject.activeInHierarchy, Is.True);
            Assert.That(label.text, Is.EqualTo("[en] status.api_key_missing"));

            status.Show(StatusKind.None);
            Assert.That(label.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void Status_RelocalizesWhenTheLanguageChanges()
        {
            var status = Spawn<StatusIndicator>("StatusIndicator");
            status.Construct(_localizer);
            status.Show(StatusKind.Offline);

            _localizer.SetLanguage("pl", CancellationToken.None).Forget();

            Assert.That(status.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("[pl] status.offline"));
        }
    }
}
