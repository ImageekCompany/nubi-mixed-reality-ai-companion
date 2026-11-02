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
