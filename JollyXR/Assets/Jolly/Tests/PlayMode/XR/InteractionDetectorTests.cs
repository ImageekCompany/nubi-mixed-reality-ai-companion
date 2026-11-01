using Jolly.XR.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class InteractionDetectorTests
    {
        static readonly Bounds Nubi = new(new Vector3(0f, 0.3f, 2f), new Vector3(0.4f, 0.6f, 0.4f));
        static readonly Pose LookingAtNubi = new(new Vector3(0f, 1.6f, 0f), Quaternion.LookRotation(Nubi.center - new Vector3(0f, 1.6f, 0f)));
        static readonly Pose LookingAway = new(new Vector3(0f, 1.6f, 0f), Quaternion.LookRotation(Vector3.left));

        [Test]
        public void DwellTimer_FiresOnceAfterTheDwell()
        {
            var timer = new DwellTimer(1.5);

            Assert.That(timer.Update(true, 0.0), Is.False);
            Assert.That(timer.Update(true, 1.4), Is.False);
            Assert.That(timer.Update(true, 1.5), Is.True);
            Assert.That(timer.Update(true, 5.0), Is.False, "fires once per dwell");
        }

        [Test]
        public void DwellTimer_LeavingTheTarget_RestartsTheDwell()
        {
            var timer = new DwellTimer(1.5);
            timer.Update(true, 0.0);
            timer.Update(false, 1.0);

            Assert.That(timer.Update(true, 1.2), Is.False);
            Assert.That(timer.Update(true, 2.6), Is.False);
            Assert.That(timer.Update(true, 2.7), Is.True);
        }

        [Test]
        public void GazeDwell_LookingAtNubiForTheDwell_Fires()
        {
            var gaze = new GazeDwellDetector();

            Assert.That(gaze.Update(LookingAtNubi, Nubi, 0.0), Is.False);
            Assert.That(gaze.Update(LookingAtNubi, Nubi, 1.6), Is.True);
        }

        [Test]
        public void GazeDwell_LookingAway_NeverFires()
        {
            var gaze = new GazeDwellDetector();
            gaze.Update(LookingAway, Nubi, 0.0);

            Assert.That(gaze.Update(LookingAway, Nubi, 10.0), Is.False);
        }

        [Test]
        public void GazeDwell_TargetBeyondRange_NeverFires()
        {
            var gaze = new GazeDwellDetector();
            var far = new Bounds(new Vector3(0f, 1.6f, 9f), Vector3.one * 0.5f);
            var head = new Pose(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            gaze.Update(head, far, 0.0);

            Assert.That(gaze.Update(head, far, 10.0), Is.False);
        }

        [Test]
        public void OpenPalm_HeldTowardNubi_Fires()
        {
            var hands = new FakeHandSource();
            var palm = new Pose(new Vector3(0f, 1.2f, 0.4f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            hands.Set(Handedness.Right, HandPoses.Open(palm));
            var detector = new OpenPalmDetector();

            Assert.That(detector.Update(hands, Nubi.center, 0.0), Is.False);
            Assert.That(detector.Update(hands, Nubi.center, 0.5), Is.True);
        }

        [Test]
        public void OpenPalm_FacingTheFloor_NeverFires()
        {
            var hands = new FakeHandSource();
            hands.Set(Handedness.Right, HandPoses.Open(new Pose(new Vector3(0f, 1.2f, 0.4f), Quaternion.identity)));
            var detector = new OpenPalmDetector();
            detector.Update(hands, new Vector3(0f, 1.2f, 3f), 0.0);

            Assert.That(detector.Update(hands, new Vector3(0f, 1.2f, 3f), 2.0), Is.False);
        }
    }
}
