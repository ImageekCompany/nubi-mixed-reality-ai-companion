using System.Collections;
using System.Collections.Generic;
using Jolly.XR.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class PinchPushToTalkTests
    {
        // Head at 1.6 m looking along +Z; right hand in front of the chest, palm down (away from the head).
        static readonly Pose PalmDown = new(new Vector3(0.2f, 1.1f, 0.35f), Quaternion.identity);
        // Palm rotated to face up and back toward the head: the Horizon OS system-menu gesture.
        static readonly Pose PalmToHead = new(new Vector3(0.2f, 1.1f, 0.35f), Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(30f, 0f, 0f));

        GameObject _root;
        FakeHandSource _hands;
        StubPreferences _preferences;
        PinchPushToTalk _ptt;
        List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ptt");
            var head = new GameObject("head").transform;
            head.SetParent(_root.transform);
            head.position = new Vector3(0f, 1.6f, 0f);
            _hands = new FakeHandSource();
            _preferences = new StubPreferences();
            _ptt = _root.AddComponent<PinchPushToTalk>();
            _ptt.Construct(_hands, head, _preferences);
            _events = new List<string>();
            _ptt.Pressed += () => _events.Add("pressed");
            _ptt.Released += () => _events.Add("released");
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        [UnityTest]
        public IEnumerator Pinch_PressesThenReleases()
        {
            _hands.Set(Handedness.Right, HandPoses.Pinch(PalmDown, 0.01f));
            yield return null;
            Assert.That(_ptt.IsHeld, Is.True);

            _hands.Set(Handedness.Right, HandPoses.Pinch(PalmDown, 0.06f));
            yield return null;

            Assert.That(_ptt.IsHeld, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }

        [UnityTest]
        public IEnumerator Pinch_WithThePalmTowardTheHead_IsIgnored()
        {
            _hands.Set(Handedness.Right, HandPoses.Pinch(PalmToHead, 0.01f));
            yield return null;
            yield return null;

            Assert.That(_ptt.IsHeld, Is.False);
            Assert.That(_events, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Pinch_OnTheNonDominantHand_IgnoredUnlessEitherHandIsAllowed()
        {
            _preferences.Current.PttEitherHand = false;
            _hands.Set(Handedness.Left, HandPoses.Pinch(PalmDown, 0.01f));
            yield return null;
            Assert.That(_ptt.IsHeld, Is.False);

            _preferences.Current.PttEitherHand = true;
            _hands.Set(Handedness.Left, HandPoses.Pinch(PalmDown, 0.06f));
            yield return null;
            _hands.Set(Handedness.Left, HandPoses.Pinch(PalmDown, 0.01f));
            yield return null;
            Assert.That(_ptt.IsHeld, Is.True);
        }

        [UnityTest]
        public IEnumerator TrackingLostWhileHeld_Releases()
        {
            _hands.Set(Handedness.Right, HandPoses.Pinch(PalmDown, 0.01f));
            yield return null;

            _hands.Lose(Handedness.Right);
            yield return null;

            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }

        [UnityTest]
        public IEnumerator Disable_WhileHeld_Releases()
        {
            _hands.Set(Handedness.Right, HandPoses.Pinch(PalmDown, 0.01f));
            yield return null;

            _ptt.enabled = false;

            Assert.That(_ptt.IsHeld, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }
    }
}
