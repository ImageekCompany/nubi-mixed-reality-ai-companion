using System;
using System.Collections;
using System.Collections.Generic;
using Jolly.Testing;
using Jolly.XR.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class WristAnchorTests
    {
        static readonly Vector3 Wrist = new(-0.15f, 1.2f, 0.35f);

        GameObject _root;
        Transform _head;
        FakeHandSource _hands;
        FakeClock _clock;
        WristAnchor _anchor;
        List<bool> _changes;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("wrist anchor test");
            _head = new GameObject("Head").transform;
            _head.SetParent(_root.transform);
            _head.position = new Vector3(0f, 1.6f, 0f);
            _hands = new FakeHandSource();
            _clock = new FakeClock();
            _anchor = new GameObject("Anchor").AddComponent<WristAnchor>();
            _anchor.transform.SetParent(_root.transform);
            _anchor.Construct(_hands, _head, _clock);
            _changes = new List<bool>();
            _anchor.ShownChanged += _changes.Add;
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        /// <summary>A left hand at <see cref="Wrist"/> whose palm normal is <paramref name="degrees"/> off the head.</summary>
        void PalmOffHead(float degrees)
        {
            var toHead = (_head.position - Wrist).normalized;
            var normal = Quaternion.AngleAxis(degrees, Vector3.Cross(toHead, Vector3.up).normalized) * toHead;
            _hands.Set(Handedness.Left, HandPoses.Open(new Pose(Wrist, Quaternion.FromToRotation(Vector3.down, normal))));
        }

        [UnityTest]
        public IEnumerator PalmTowardTheHead_ShowsTheControlAtTheWrist_ReadableFromTheHead()
        {
            PalmOffHead(0f);
            yield return null;

            Assert.That(_anchor.IsShown, Is.True);
            Assert.That(_changes, Is.EqualTo(new[] { true }));
            var position = _anchor.transform.position;
            Assert.That(Vector3.Distance(position, Wrist), Is.LessThan(0.06f));
            Assert.That(Vector3.Dot(_anchor.transform.forward, (position - _head.position).normalized), Is.GreaterThan(0.999f));
        }

        [UnityTest]
        public IEnumerator PalmDownOrRightHandOnly_StaysHidden()
        {
            _hands.Set(Handedness.Left, HandPoses.Open(new Pose(Wrist, Quaternion.identity)));
            _hands.Set(Handedness.Right, HandPoses.Open(new Pose(Wrist, Quaternion.FromToRotation(Vector3.down, Vector3.up))));
            yield return null;

            Assert.That(_anchor.IsShown, Is.False);
            Assert.That(_changes, Is.Empty);
        }

        [UnityTest]
        public IEnumerator BetweenTheShowAndHideAngles_KeepsItsState()
        {
            PalmOffHead(58f);
            yield return null;
            Assert.That(_anchor.IsShown, Is.False, "58° is too far to appear");

            PalmOffHead(0f);
            yield return null;
            PalmOffHead(58f);
            yield return null;
            Assert.That(_anchor.IsShown, Is.True, "but close enough to stay");

            PalmOffHead(70f);
            yield return null;
            Assert.That(_anchor.IsShown, Is.False);
            Assert.That(_changes, Is.EqualTo(new[] { true, false }));
        }

        [UnityTest]
        public IEnumerator LostHand_StaysForTheGrace_ThenHides()
        {
            PalmOffHead(0f);
            yield return null;

            _hands.Lose(Handedness.Left);
            yield return null;
            _clock.Advance(TimeSpan.FromSeconds(0.3));
            yield return null;
            Assert.That(_anchor.IsShown, Is.True, "the pressing hand often covers this one");

            _clock.Advance(TimeSpan.FromSeconds(0.2));
            yield return null;
            Assert.That(_anchor.IsShown, Is.False);
        }
    }
}
