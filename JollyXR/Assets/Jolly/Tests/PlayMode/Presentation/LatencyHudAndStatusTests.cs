using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class LatencyHudAndStatusTests
    {
        FakeConversationEvents _events;
        EchoLocalizer _localizer;
        FakeUserPreferences _preferences;
        GameObject _root;
        GameObject _panelRoot;

        [SetUp]
        public void SetUp()
        {
            _events = new FakeConversationEvents();
            _localizer = new EchoLocalizer();
            _preferences = new FakeUserPreferences();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
            if (_panelRoot != null)
                Object.Destroy(_panelRoot);
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
            hud.Construct(_events, tracker, _preferences, _localizer);
            tracker.Set(LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed, 1834.6);

            _events.RaiseState(ConversationState.Speaking, ConversationState.Idle);

            Assert.That(hud.Text, Does.Contain("[en] hud.release_to_audio: 1835"));
            Assert.That(hud.Text, Does.Contain("[en] hud.llm_first_text: –"), "a missing mark shows a dash, not NaN");
        }

        [Test]
        public void Hud_ListsTheLatestModelCallsNewestFirst()
        {
            var hud = Spawn<LatencyHud>("LatencyHud");
            hud.Construct(_events, new FakeLatencyTracker(), _preferences, _localizer);

            _events.RaiseModelCall("muse-spark-1.3", 1210);
            _events.RaiseModelCall("sam-3.1", 840);

            Assert.That(hud.Text.IndexOf("SAM 3.1: 840", System.StringComparison.Ordinal),
                Is.LessThan(hud.Text.IndexOf("Muse Spark: 1210", System.StringComparison.Ordinal)));
        }

        [Test]
        public void Hud_IsHiddenInAwake_BeforeConstructBringsPreferences()
        {
            var hud = Spawn<LatencyHud>("LatencyHud");

            Assert.That(hud.IsVisible, Is.False);
        }

        [Test]
        public void Hud_IsHiddenOnAFreshSnapshot()
        {
            var hud = Spawn<LatencyHud>("LatencyHud");

            hud.Construct(_events, new FakeLatencyTracker(), _preferences, _localizer);

            Assert.That(hud.IsVisible, Is.False);
        }

        [Test]
        public void Hud_IsVisibleOnConstruct_WhenTheStoredFlagIsOn()
        {
            var hud = Spawn<LatencyHud>("LatencyHud");
            _preferences.Set(new UserPreferencesSnapshot { ShowLatencyStats = true });

            hud.Construct(_events, new FakeLatencyTracker(), _preferences, _localizer);

            Assert.That(hud.IsVisible, Is.True);
        }

        [Test]
        public void Hud_FollowsTheSettingsRow_ShowingThenHiding()
        {
            var hud = Spawn<LatencyHud>("LatencyHud");
            var panel = PresentationAssets.Instantiate<SettingsPanel>("SettingsPanel");
            _panelRoot = panel.transform.root.gameObject;
            panel.Construct(_localizer, new FakePersonaProvider(FakePersonaProvider.Profile("companion")),
                new FakeUsageMeter(0m, 5m), new InMemoryPreferencesStore(), new FakeClock());
            hud.Construct(_events, new FakeLatencyTracker(), panel, _localizer);
            var row = panel.GetComponentsInChildren<SettingRow>(true).Single(r => r.name == "LatencyStats");
            Assert.That(hud.IsVisible, Is.False);

            row.Next.onClick.Invoke();
            Assert.That(hud.IsVisible, Is.True);

            row.Next.onClick.Invoke();
            Assert.That(hud.IsVisible, Is.False);
        }

        [Test]
        public void Hud_KeepsRecordingWhileHidden_AndShowsItOnReveal()
        {
            var tracker = new FakeLatencyTracker();
            var hud = Spawn<LatencyHud>("LatencyHud");
            hud.Construct(_events, tracker, _preferences, _localizer);
            tracker.Set(LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed, 1834.6);

            _events.RaiseState(ConversationState.Speaking, ConversationState.Idle);
            _events.RaiseModelCall("sam-3.1", 840);
            Assert.That(hud.IsVisible, Is.False);

            _preferences.Set(new UserPreferencesSnapshot { ShowLatencyStats = true });

            Assert.That(hud.IsVisible, Is.True);
            Assert.That(hud.Text, Does.Contain("[en] hud.release_to_audio: 1835"));
            Assert.That(hud.Text, Does.Contain("SAM 3.1: 840"));
        }

        // Text is only the backing string. TMP builds no mesh while the canvas is disabled, so what the wearer sees after a
        // reveal depends on the canvas re-dirtying its graphics when it is enabled.
        [UnityTest]
        public IEnumerator Hud_RevealedBetweenTurns_BuildsTheCurrentText() => UniTask.ToCoroutine(async () =>
        {
            var tracker = new FakeLatencyTracker();
            var hud = Spawn<LatencyHud>("LatencyHud");
            hud.Construct(_events, tracker, _preferences, _localizer);
            var text = hud.GetComponentInChildren<TMP_Text>(true);
            await UniTask.DelayFrame(2);

            _preferences.Set(new UserPreferencesSnapshot { ShowLatencyStats = true });
            await UniTask.DelayFrame(2);
            var firstReveal = text.textInfo.characterCount;
            Assert.That(firstReveal, Is.GreaterThan(0));

            _preferences.Set(new UserPreferencesSnapshot { ShowLatencyStats = false });
            await UniTask.DelayFrame(2);
            tracker.Set(LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed, 1834.6);
            _events.RaiseModelCall("sam-3.1", 840);
            await UniTask.DelayFrame(2);
            Assert.That(text.textInfo.characterCount, Is.EqualTo(firstReveal), "built while hidden");

            _preferences.Set(new UserPreferencesSnapshot { ShowLatencyStats = true });
            await UniTask.DelayFrame(2);

            Assert.That(text.textInfo.characterCount, Is.GreaterThan(firstReveal));
        });

        [UnityTest]
        public IEnumerator Hud_Destroy_UnsubscribesFromPreferences() => UniTask.ToCoroutine(async () =>
        {
            var hud = Spawn<LatencyHud>("LatencyHud");
            hud.Construct(_events, new FakeLatencyTracker(), _preferences, _localizer);
            Assert.That(_preferences.SubscriberCount, Is.EqualTo(1));

            Object.Destroy(_root);
            await UniTask.NextFrame();

            Assert.That(_preferences.SubscriberCount, Is.Zero);
        });

        [Test]
        public void Hud_ConstructingAgain_DoesNotDoubleSubscribe()
        {
            var hud = Spawn<LatencyHud>("LatencyHud");
            hud.Construct(_events, new FakeLatencyTracker(), _preferences, _localizer);

            hud.Construct(_events, new FakeLatencyTracker(), _preferences, _localizer);

            Assert.That(_preferences.SubscriberCount, Is.EqualTo(1));
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
