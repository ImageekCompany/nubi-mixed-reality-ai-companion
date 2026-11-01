using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    /// <summary>The shipped SettingsPanel prefab's on-demand visibility (ADR-0042).</summary>
    public sealed class SettingsWindowTests
    {
        FakeClock _clock;
        RecordingLog _log;
        Transform _head;
        SettingsPanel _panel;
        SettingsWindow _window;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _log = new RecordingLog();
            _panel = PresentationAssets.Instantiate<SettingsPanel>("SettingsPanel");
            _root = _panel.transform.root.gameObject;
            _panel.Construct(new EchoLocalizer(), new FakePersonaProvider(FakePersonaProvider.Profile("companion")),
                new FakeUsageMeter(0m, 5m), new InMemoryPreferencesStore(), _clock);
            _head = new GameObject("User head").transform;
            _head.SetParent(_root.transform, false);
            _head.SetPositionAndRotation(new Vector3(0.2f, 1.6f, -0.5f), Quaternion.Euler(10f, 90f, 0f));
            _window = _panel.GetComponent<SettingsWindow>();
            _window.Construct(_clock, _head, _log);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        bool Drawn => _panel.GetComponent<Canvas>().enabled;
        bool Hittable => _panel.GetComponent<BaseRaycaster>().enabled;
        Button CloseButton => _panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "Close");

        [Test]
        public void AtLaunch_IsNeitherDrawnNorHittable_ButStillServesPreferences()
        {
            Assert.That(_window.IsOpen, Is.False);
            Assert.That(Drawn, Is.False);
            Assert.That(Hittable, Is.False);
            Assert.That(_panel.Current, Is.Not.Null);
        }

        [Test]
        public void Open_PlacesItOnceInFrontOfTheFace_ThenItStaysInTheRoom()
        {
            _window.Open("menu");

            var t = _panel.transform;
            Assert.That(Drawn && Hittable, Is.True);
            Assert.That(Vector3.Distance(t.position, new Vector3(0.9f, 1.45f, -0.5f)), Is.LessThan(1e-4f),
                "0.7 m ahead along the level heading, 0.15 m below the eyes");
            Assert.That(Vector3.Angle(t.forward, Vector3.right), Is.LessThan(0.01f), "level, facing away from the user");
            Assert.That(_log.Contains(LogLevel.Info, "evt=settings.open source=menu"), Is.True);

            _head.SetPositionAndRotation(new Vector3(-1f, 1.2f, 2f), Quaternion.Euler(0f, -45f, 0f));

            Assert.That(Vector3.Distance(t.position, new Vector3(0.9f, 1.45f, -0.5f)), Is.LessThan(1e-4f),
                "world-locked, not head-locked");
        }

        [UnityTest]
        public IEnumerator Idle_ClosesAfterTwentySeconds_UsingAControlRestartsTheWait() => UniTask.ToCoroutine(async () =>
        {
            _window.Open("wrist");
            _clock.Advance(TimeSpan.FromSeconds(19));
            _panel.GetComponentsInChildren<SettingRow>(true).First().Next.onClick.Invoke();
            _clock.Advance(TimeSpan.FromSeconds(19));
            await PresentationAssets.AfterUpdate();
            Assert.That(_window.IsOpen, Is.True);

            _clock.Advance(TimeSpan.FromSeconds(1.5));
            await PresentationAssets.AfterUpdate();

            Assert.That(_window.IsOpen, Is.False);
            Assert.That(Drawn, Is.False);
            Assert.That(_log.Contains(LogLevel.Info, "evt=settings.close reason=idle"), Is.True);
        });

        [Test]
        public void CloseButton_Closes()
        {
            _window.Open("voice");

            CloseButton.onClick.Invoke();

            Assert.That(_window.IsOpen, Is.False);
            Assert.That(Hittable, Is.False);
            Assert.That(_log.Contains(LogLevel.Info, "evt=settings.close reason=button"), Is.True);
        }

        [Test]
        public void Toggle_OpensThenCloses_AndClosingAClosedPanelLogsNothing()
        {
            _window.Toggle("wrist");
            Assert.That(_window.IsOpen, Is.True);

            _window.Toggle("wrist");
            _window.Close("talk");

            Assert.That(_window.IsOpen, Is.False);
            Assert.That(_log.Entries.Count(e => e.Message.StartsWith("evt=settings.close")), Is.EqualTo(1));
        }

        [Test]
        public void Placement_LookingStraightDown_StillOpensLevel()
        {
            var pose = PanelPlacement.InFrontOf(Vector3.up, Vector3.down, 0.7f, 0.15f);

            Assert.That(pose.position.y, Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(Mathf.Abs(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.up)), Is.LessThan(1e-5f));
        }

        [Test]
        public void Visibility_OpeningAnOpenPanelOnlyRestartsTheWait()
        {
            var visibility = new PanelVisibility(20);

            Assert.That(visibility.Open(0), Is.True);
            Assert.That(visibility.Open(15), Is.False);
            Assert.That(visibility.IsIdle(30), Is.False);
            Assert.That(visibility.IsIdle(35), Is.True);
            Assert.That(visibility.Close(), Is.True);
            Assert.That(visibility.Close(), Is.False);
        }
    }
}
