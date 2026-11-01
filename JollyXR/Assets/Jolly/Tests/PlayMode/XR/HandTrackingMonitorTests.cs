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
        public IEnumerator VisualChange_IsLoggedOnceAtTheNextPoll()
        {
            yield return null;
            _log.Clear();

            _rightMesh.enabled = true;
            yield return null;
            Assert.That(Messages(), Is.Empty, "polls at 4 Hz");

            _clock.Advance(TimeSpan.FromSeconds(0.3));
            yield return null;
            _clock.Advance(TimeSpan.FromSeconds(0.3));
            yield return null;

            Assert.That(Messages(), Is.EqualTo(new[] { "evt=hands.visual left=false right=true" }));
        }

        string[] Messages() => _log.Entries.Where(e => e.Level == LogLevel.Info).Select(e => e.Message).ToArray();

        // The XR Hands mesh controller hides its renderer while the hand is untracked, as on the rig.
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
