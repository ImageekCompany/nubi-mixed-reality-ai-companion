using Jolly.XR.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class CharacterAimTests
    {
        const float Radius = 0.25f;
        static readonly Vector3 Hand = new(0f, 1.1f, 0f);
        static readonly Vector3 Feet = new(0f, 0f, 1.5f);

        static Ray Through(float x, float y, float z) => new(Hand, new Vector3(x, y, z) - Hand);

        [Test]
        public void FloorJustBehindTheCharacter_IsTheFloor()
        {
            // Arrange
            var floorBehind = Through(0f, 0f, 2.1f);

            // Act
            var past = CharacterAim.LandsJustPast(floorBehind, Feet, Radius);

            // Assert
            Assert.That(past, Is.True);
        }

        [TestCase(0.9f, TestName = "AtTheHead")]
        [TestCase(0.5f, TestName = "AtTheChest")]
        public void AimedAtTheBody_IsTheCharacter(float height)
        {
            // Arrange
            var atBody = Through(0f, height, 1.5f);

            // Act
            var past = CharacterAim.LandsJustPast(atBody, Feet, Radius);

            // Assert
            Assert.That(past, Is.False);
        }

        [Test]
        public void AimedAtTheFeet_IsTheCharacter()
        {
            // Arrange
            var atFeet = Through(0f, 0f, 1.6f);

            // Act
            var past = CharacterAim.LandsJustPast(atFeet, Feet, Radius);

            // Assert
            Assert.That(past, Is.False);
        }

        /// <summary>The shipped Nubi collider: radius 0.3, height 0.9, standing on the floor.</summary>
        static CapsuleCollider Standing(float metersFromHead, GameObject holder)
        {
            var capsule = holder.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 0.9f;
            capsule.center = new Vector3(0f, 0.45f, 0f);
            holder.transform.position = new Vector3(0f, 0f, metersFromHead);
            Physics.SyncTransforms();
            return capsule;
        }

        [Test]
        public void AtNormalStandingDistance_ABodyAim_IsTheCharacter(
            [Values(0.7f, 1.0f, 1.25f, 1.5f)] float metersFromHead,
            [Values(0.8f, 0.5f, 0.25f)] float aimedHeight)
        {
            // Arrange: a resting hand 0.3 m ahead of the head, at 1.1 m; head, chest and knee.
            var holder = new GameObject("character");
            try
            {
                var character = Standing(metersFromHead, holder);
                var hand = new Vector3(0f, 1.1f, 0.3f);
                var ray = new Ray(hand, new Vector3(0f, aimedHeight, metersFromHead) - hand);

                // Act
                var atCharacter = CharacterAim.AimsAt(character, ray, 10f);

                // Assert
                Assert.That(atCharacter, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void FromFarAway_ARayThatEntersBelowTheKnee_IsTheFloorJustPast()
        {
            // Arrange: only a long flat ray clips the legs this low and still lands past the footprint.
            var holder = new GameObject("character");
            try
            {
                var character = Standing(3f, holder);
                var hand = new Vector3(0f, 1.1f, 0f);
                var ray = new Ray(hand, new Vector3(0f, 0f, 3.35f) - hand);

                // Act
                var atCharacter = CharacterAim.AimsAt(character, ray, 10f);

                // Assert
                Assert.That(atCharacter, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ARayThatMissesTheCollider_IsNotTheCharacter()
        {
            // Arrange
            var holder = new GameObject("character");
            try
            {
                var character = Standing(1f, holder);
                var hand = new Vector3(0f, 1.1f, 0.3f);
                var ray = new Ray(hand, new Vector3(1f, 0f, 1f) - hand);

                // Act
                var atCharacter = CharacterAim.AimsAt(character, ray, 10f);

                // Assert
                Assert.That(atCharacter, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ADisabledCollider_IsNotTheCharacter()
        {
            // Arrange
            var holder = new GameObject("character");
            try
            {
                var character = Standing(1f, holder);
                character.enabled = false;
                var hand = new Vector3(0f, 1.1f, 0.3f);

                // Act
                var atCharacter = CharacterAim.AimsAt(character, new Ray(hand, new Vector3(0f, 0.5f, 1f) - hand), 10f);

                // Assert
                Assert.That(atCharacter, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void AimedUpward_IsTheCharacter()
        {
            // Arrange
            var upward = new Ray(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.2f, 1f));

            // Act
            var past = CharacterAim.LandsJustPast(upward, Feet, Radius);

            // Assert
            Assert.That(past, Is.False);
        }
    }
}
