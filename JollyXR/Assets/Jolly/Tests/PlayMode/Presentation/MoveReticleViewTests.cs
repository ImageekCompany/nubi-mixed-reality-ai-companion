using System;
using System.Collections;
using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class MoveReticleViewTests
    {
        static readonly Vector3 Spot = new(1f, 0f, 2f);

        FakeClock _clock;
        MoveReticleView _view;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _view = new GameObject("reticle").AddComponent<MoveReticleView>();
            _view.Construct(_clock);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_view.gameObject);

        [Test]
        public void Show_PutsTheRingOnTheFloorPoint_AndHideRemovesIt()
        {
            _view.Show(Spot);

            Assert.That(_view.IsVisible, Is.True);
            Assert.That(Vector3.Distance(_view.Position, Spot), Is.LessThan(1e-4f));
            _view.Hide();
            Assert.That(_view.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Confirm_PulsesThroughShowAndHide_ThenDisappears()
        {
            _view.Show(Spot);
            _view.Confirm(Spot);
            _view.Hide();
            _view.Show(Vector3.zero);

            Assert.That(_view.IsPulsing, Is.True);
            Assert.That(_view.IsVisible, Is.True, "the pulse is seen even when the ray leaves the floor");
            Assert.That(Vector3.Distance(_view.Position, Spot), Is.LessThan(1e-4f), "a new target waits for the pulse");

            _clock.Advance(TimeSpan.FromSeconds(1));
            yield return null;

            Assert.That(_view.IsPulsing, Is.False);
            Assert.That(_view.IsVisible, Is.False);
        }
    }
}
