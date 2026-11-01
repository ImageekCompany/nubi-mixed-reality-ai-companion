#if UNITY_EDITOR
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>The blob shadow on the real Nubi prefab, driven by the animated feet.</summary>
    public sealed class ContactShadowTests
    {
        static readonly int Opacity = Shader.PropertyToID("_Opacity");

        CharacterTestRig _rig;
        MeshRenderer _shadow;
        Vector3 _standingScale;
        float _standingOpacity;

        [SetUp]
        public void SetUp()
        {
            _rig = CharacterTestRig.Create();
            _shadow = _rig.Nubi.ShadowRig.shadow;
            _standingScale = _shadow.transform.localScale;
            _standingOpacity = _shadow.sharedMaterial.GetFloat(Opacity);
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        float AppliedOpacity()
        {
            var block = new MaterialPropertyBlock();
            _shadow.GetPropertyBlock(block);
            return block.isEmpty ? _standingOpacity : block.GetFloat(Opacity);
        }

        [Test]
        public void Prefab_BindsTheShadowQuadAndBothFeet()
        {
            var rig = _rig.Nubi.ShadowRig;

            Assert.That(rig.IsComplete, Is.True);
            Assert.That(rig.shadow.transform.parent, Is.SameAs(_rig.Nubi.transform), "follows the floor-level root");
            Assert.That(rig.footLeft.name, Is.EqualTo("Foot_L"));
            Assert.That(rig.footRight.name, Is.EqualTo("Foot_R"));
            Assert.That(rig.footRestHeight, Is.InRange(0.01f, 0.15f));
        }

        [UnityTest]
        public IEnumerator Standing_KeepsTheFullShadow() => UniTask.ToCoroutine(async () =>
        {
            await UniTask.DelayFrame(5);

            Assert.That(AppliedOpacity(), Is.EqualTo(_standingOpacity).Within(0.02f));
            Assert.That(_shadow.transform.localScale.x, Is.EqualTo(_standingScale.x).Within(0.02f));
        });

        [UnityTest]
        public IEnumerator FeetOffTheFloor_ShrinkAndFadeTheShadow_ThenItRecovers() => UniTask.ToCoroutine(async () =>
        {
            // The model lifted 15 cm off its root stands in for a hop: half of the 0.3 m fade height.
            var model = _rig.Animator.transform;
            model.localPosition = new Vector3(0f, 0.15f, 0f);
            await UniTask.DelayFrame(3);

            Assert.That(AppliedOpacity(), Is.EqualTo(_standingOpacity * 0.5f).Within(0.03f));
            Assert.That(_shadow.transform.localScale.x, Is.EqualTo(_standingScale.x * 0.8f).Within(0.02f));
            Assert.That(_shadow.transform.localPosition.y, Is.LessThan(0.01f), "stays on the floor");

            model.localPosition = Vector3.zero;
            await UniTask.DelayFrame(3);

            Assert.That(AppliedOpacity(), Is.EqualTo(_standingOpacity).Within(0.02f));
            Assert.That(_shadow.transform.localScale.x, Is.EqualTo(_standingScale.x).Within(0.02f));
        });
    }
}
#endif
