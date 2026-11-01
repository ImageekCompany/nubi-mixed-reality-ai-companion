using Jolly.Character;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>The bones are never rewritten here, as when the Animator is culled offscreen.</summary>
    public sealed class LookAtDriverTests
    {
        static readonly Vector3 Target = new(1f, 0.8f, 1f);

        GameObject _body;
        NubiRigBones _bones;
        LookAtDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Body");
            var chest = Child(_body.transform, "Chest", 0.3f);
            var neck = Child(chest, "Neck", 0.15f);
            var head = Child(neck, "Head", 0.1f);
            _bones = new NubiRigBones
            {
                chest = chest, neck = neck, head = head,
                eyeLeft = Child(head, "Eye_L", 0.05f), eyeRight = Child(head, "Eye_R", 0.05f),
            };
            _driver = new LookAtDriver(_bones, _body.transform, new LookSettings());
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_body);

        [Test]
        public void Apply_BonesNotReanimated_DoesNotAccumulate()
        {
            // Arrange
            _driver.Apply(true, Target, false, 1f);
            var head = _bones.head.rotation;
            var eye = _bones.eyeLeft.rotation;

            // Act
            for (var frame = 0; frame < 10; frame++)
                _driver.Apply(true, Target, false, 1f);

            // Assert
            Assert.That(Quaternion.Angle(_bones.head.rotation, head), Is.LessThan(1e-3f));
            Assert.That(Quaternion.Angle(_bones.eyeLeft.rotation, eye), Is.LessThan(1e-3f));
        }

        [Test]
        public void Apply_TargetDropped_ReturnsToTheAnimatedPose()
        {
            // Arrange
            _driver.Apply(true, Target, false, 1f);
            Assert.That(Quaternion.Angle(_bones.head.localRotation, Quaternion.identity), Is.GreaterThan(5f));

            // Act
            _driver.Apply(false, default, false, 1f);

            // Assert
            Assert.That(Quaternion.Angle(_bones.head.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
            Assert.That(Quaternion.Angle(_bones.chest.localRotation, Quaternion.identity), Is.LessThan(1e-3f));
        }

        [Test]
        public void Apply_AnimatorWritesANewPose_LooksFromThatPose()
        {
            // Arrange
            var animated = Quaternion.Euler(0f, 0f, 10f);
            _driver.Apply(true, Target, false, 1f);
            _bones.neck.localRotation = animated;

            // Act
            _driver.Apply(false, default, false, 1f);

            // Assert
            Assert.That(Quaternion.Angle(_bones.neck.localRotation, animated), Is.LessThan(1e-3f));
        }

        static Transform Child(Transform parent, string name, float up)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = new Vector3(0f, up, 0f);
            return child;
        }
    }
}
