using System;
using System.Collections;
using System.Linq;
using Jolly.Contracts;
using Jolly.Testing;
using Jolly.XR.Hands;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    /// <summary>No XR runtime runs in the Editor test player, so the subsystem reads as stopped and no hand as tracked.</summary>
    public sealed class HandTrackingMonitorTests
    {
        GameObject _root;
        RecordingLog _log;
        FakeClock _clock;
        HandTrackingMonitor _monitor;
        Renderer _rightMesh;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("rig");
            _rightMesh = AddVisual(_root.transform, Handedness.Right);
            _log = new RecordingLog();
            _clock = new FakeClock();
            _monitor = new GameObject("monitor").AddComponent<HandTrackingMonitor>();
            _monitor.transform.SetParent(_root.transform);
            _monitor.Construct(_log, _clock, _root.transform);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        [UnityTest]
        public IEnumerator FirstPoll_ReportsSubsystemTrackingAndVisuals()
        {
            yield return null;

            Assert.That(Messages(), Is.EqualTo(new[]
            {
                "evt=hands.monitor visuals=1",
                "evt=hands.subsystem state=stopped",
                "evt=hands.tracked left=false right=false",
                "evt=hands.visual left=false right=false",
            }));
        }

        [UnityTest]
        public IEnumerator TrackedHand_HeldForTheHold_IsShownAndLoggedOnce()
        {
            var right = false;
            _monitor.ReadTrackingFrom(side => side == Handedness.Right && right);
            yield return null;
            _log.Clear();

            right = true;
            yield return null;
            Assert.That(_rightMesh.enabled, Is.False, "not yet held for 0.2 s");
            Assert.That(Messages(), Is.Empty);

            _clock.Advance(TimeSpan.FromSeconds(0.25));
            yield return null;
            yield return null;

            Assert.That(_rightMesh.enabled, Is.True);
            Assert.That(Messages(), Is.EqualTo(new[]
            {
                "evt=hands.tracked left=false right=true",
                "evt=hands.visual left=false right=true",
            }));
        }

        [UnityTest]
        public IEnumerator PhantomBlip_NeitherShowsTheHandNorLogs()
        {
            // The 6179cfc headless runs: a hand "tracked" for a moment about once a second, with nobody wearing the headset.
            var left = false;
            _monitor.ReadTrackingFrom(side => side == Handedness.Left && left);
            yield return null;
            _log.Clear();

            left = true;
            yield return null;
            _clock.Advance(TimeSpan.FromSeconds(0.1));
            yield return null;
            left = false;
            _clock.Advance(TimeSpan.FromSeconds(0.3));
            yield return null;
            yield return null;

            Assert.That(Messages(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator UntrackedHand_StaysHiddenWhateverElseShowsIt()
        {
            yield return null;
            _log.Clear();

            _rightMesh.enabled = true;
            yield return null;
            _clock.Advance(TimeSpan.FromSeconds(0.3));
            yield return null;

            Assert.That(_rightMesh.enabled, Is.False);
            Assert.That(Messages(), Is.Empty);
        }

        [Test]
        public void Construct_TakesTheShowAndHideFromTheMeshControllers()
        {
            var controller = _root.GetComponentInChildren<XRHandMeshController>();

            Assert.That(controller.showMeshWhenTrackingIsAcquired, Is.False);
            Assert.That(controller.hideMeshWhenTrackingIsLost, Is.False);
        }

        string[] Messages() => _log.Entries.Where(e => e.Level == LogLevel.Info).Select(e => e.Message).ToArray();

        // As on the rig: the XR Hands mesh controller hides its renderer while the hand is untracked, until Construct.
        static Renderer AddVisual(Transform parent, Handedness side)
        {
            // Inactive until wired: the mesh controller reads its renderer in OnEnable.
            var visual = new GameObject(side + " visual");
            visual.SetActive(false);
            visual.transform.SetParent(parent);
            var events = visual.AddComponent<XRHandTrackingEvents>();
            events.handedness = side;
            var mesh = new GameObject("mesh").AddComponent<MeshRenderer>();
            mesh.transform.SetParent(visual.transform);
            var controller = visual.AddComponent<XRHandMeshController>();
            controller.handMeshRenderer = mesh;
            controller.handTrackingEvents = events;
            visual.SetActive(true);
            return mesh;
        }
    }
}
