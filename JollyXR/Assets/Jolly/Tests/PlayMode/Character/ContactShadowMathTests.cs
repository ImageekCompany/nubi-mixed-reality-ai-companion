using Jolly.Character;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class ContactShadowMathTests
    {
        static readonly ContactShadowSettings Settings = new() { fadeHeight = 0.3f, scaleAtFadeHeight = 0.6f };

        [TestCase(0.05f, 0.05f, 0f, 0.05f, 0f)]
        [TestCase(0.15f, 0.05f, 0f, 0.05f, 0f)]
        [TestCase(0.14f, 0.15f, 0f, 0.05f, 0.09f)]
        [TestCase(1.12f, 1.14f, 1f, 0.05f, 0.07f)]
        [TestCase(0.03f, 0.04f, 0f, 0.05f, 0f)]
        public void Lift_IsTheLowerFootAboveItsRestHeight(float left, float right, float floor, float rest, float expected)
        {
            Assert.That(ContactShadowMath.Lift(left, right, floor, rest), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void Shape_Grounded_IsFullStrengthAndSize()
        {
            var shape = ContactShadowMath.Shape(0f, Settings);

            Assert.That(shape.Opacity, Is.EqualTo(1f));
            Assert.That(shape.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void Shape_HopApex_IsLighterAndSmaller()
        {
            var shape = ContactShadowMath.Shape(0.09f, Settings);

            Assert.That(shape.Opacity, Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(shape.Scale, Is.EqualTo(0.88f).Within(1e-5f));
        }

        [TestCase(0.3f)]
        [TestCase(2f)]
        public void Shape_AtOrAboveFadeHeight_IsGoneAndClamped(float lift)
        {
            var shape = ContactShadowMath.Shape(lift, Settings);

            Assert.That(shape.Opacity, Is.Zero);
            Assert.That(shape.Scale, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void Shape_ZeroFadeHeight_NeverFades()
        {
            var shape = ContactShadowMath.Shape(0.5f, new ContactShadowSettings { fadeHeight = 0f });

            Assert.That(shape.Opacity, Is.EqualTo(1f));
            Assert.That(shape.Scale, Is.EqualTo(1f));
        }
    }
}
