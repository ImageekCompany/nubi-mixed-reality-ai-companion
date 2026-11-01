using Jolly.Character;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class CharacterMathTests
    {
        static readonly LocomotionSettings Locomotion = new();
        static readonly LookSettings Look = new();

        [TestCase(0f, 1f)]
        [TestCase(0.49f, 1f)]
        [TestCase(0.6f, 0.6f / 0.49f)]
        [TestCase(0.2f, 0.7f)]
        [TestCase(0.9f, 1f)]
        [TestCase(1.2f, 1.2f / 0.9f)]
        [TestCase(3f, 1.5f)]
        public void LocomotionPlayback_Rate_MatchesClipSpeedWithinClamp(float speed, float expected)
        {
            Assert.That(LocomotionPlayback.Rate(speed, Locomotion), Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void LookAtSolver_SmallAngle_EyesOnly()
        {
            var a = LookAtSolver.Solve(6f, -3f, Look);

            Assert.That(a.HeadYaw, Is.Zero);
            Assert.That(a.EyeYaw, Is.EqualTo(6f));
            Assert.That(a.HeadPitch, Is.Zero);
            Assert.That(a.EyePitch, Is.EqualTo(-3f));
        }

        [Test]
        public void LookAtSolver_MediumAngle_HeadLeadsAndEyesTakeTheLastDegrees()
        {
            var a = LookAtSolver.Solve(30f, 0f, Look);

            Assert.That(a.HeadYaw, Is.EqualTo(20f));
            Assert.That(a.EyeYaw, Is.EqualTo(10f));
        }

        [Test]
        public void LookAtSolver_BeyondRange_ClampsHeadAndEyes()
        {
            var a = LookAtSolver.Solve(-120f, 60f, Look);

            Assert.That(a.HeadYaw, Is.EqualTo(-Look.headYawMax));
            Assert.That(a.EyeYaw, Is.EqualTo(-Look.eyeYawMax));
            Assert.That(a.HeadPitch, Is.EqualTo(Look.headPitchMax));
            Assert.That(a.EyePitch, Is.EqualTo(Look.eyePitchMax));
        }

        [Test]
        public void LookAtSolver_Bearing_RightIsPositiveYawAndUpIsNegativePitch()
        {
            LookAtSolver.Bearing(new Vector3(1f, 1f, 1f), Vector3.forward, Vector3.up, out var yaw, out var pitch);

            Assert.That(yaw, Is.EqualTo(45f).Within(1e-3f));
            Assert.That(pitch, Is.EqualTo(-35.264f).Within(1e-2f));
        }

        [Test]
        public void FollowPlanner_InsideBand_StaysPut()
        {
            var moved = FollowPlanner.TryGetGoal(new Vector3(0f, 0f, 1.2f), new Vector3(0f, 1.6f, 0f), 1f, 1.5f, out _);

            Assert.That(moved, Is.False);
        }

        [Test]
        public void FollowPlanner_TooFar_GoalIsMidBandOnTheLineToTheUser()
        {
            var moved = FollowPlanner.TryGetGoal(new Vector3(0f, 0f, 5f), new Vector3(0f, 1.6f, 0f), 1f, 1.5f, out var goal);

            Assert.That(moved, Is.True);
            Assert.That(Vector3.Distance(goal, new Vector3(0f, 0f, 1.25f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void FollowPlanner_TooClose_BacksOff()
        {
            var moved = FollowPlanner.TryGetGoal(new Vector3(0.3f, 0f, 0f), Vector3.zero, 1f, 1.5f, out var goal);

            Assert.That(moved, Is.True);
            Assert.That(goal.x, Is.EqualTo(1.25f).Within(1e-4f));
        }
    }
}
