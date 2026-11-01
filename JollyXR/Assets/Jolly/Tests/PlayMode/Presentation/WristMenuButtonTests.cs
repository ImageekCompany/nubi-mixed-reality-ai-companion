using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class WristMenuButtonTests
    {
        WristMenuButton _button;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _button = PresentationAssets.Instantiate<WristMenuButton>("WristMenuButton");
            _root = _button.transform.root.gameObject;
            _button.Construct(new EchoLocalizer());
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        [Test]
        public void StartsHidden_ShowsOnRequest_AndRaisesPressed()
        {
            var canvas = _button.GetComponent<Canvas>();
            var raycaster = _button.GetComponent<BaseRaycaster>();
            var presses = 0;
            _button.Pressed += () => presses++;
            Assert.That(canvas.enabled || raycaster.enabled, Is.False);

            _button.SetVisible(true);
            _button.GetComponentInChildren<Button>().onClick.Invoke();

            Assert.That(canvas.enabled && raycaster.enabled, Is.True);
            Assert.That(presses, Is.EqualTo(1));
            Assert.That(_button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("[en] settings.title"));
        }

        [Test]
        public void IsSmall_AWristSizedTarget()
        {
            var rect = (RectTransform)_button.transform;
            var size = rect.sizeDelta * rect.lossyScale.x;

            Assert.That(size.x, Is.InRange(0.06f, 0.1f), "metres wide");
            Assert.That(size.y, Is.InRange(0.03f, 0.05f), "metres tall: still a comfortable poke target");
        }
    }
}
