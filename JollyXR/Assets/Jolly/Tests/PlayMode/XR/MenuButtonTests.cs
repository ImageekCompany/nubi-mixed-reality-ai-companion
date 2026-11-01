using System.Collections;
using Jolly.XR.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using HapticControl = UnityEngine.XR.OpenXR.Input.HapticControl;
using Object = UnityEngine.Object;
using TouchController = UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile.OculusTouchController;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class MenuButtonTests : InputTestFixture
    {
        GameObject _root;
        int _presses;

        public override void Setup()
        {
            base.Setup();
            // As in ControllerPushToTalkTests: the fixture's reset drops the layouts OpenXR and XR Hands register.
            InputSystem.RegisterLayout<HapticControl>("Haptic");
            InputSystem.RegisterLayout(typeof(TouchController).GetProperty(nameof(TouchController.devicePose)).PropertyType, "Pose");
            InputSystem.RegisterLayout<TouchController>();
            InputSystem.RegisterLayout<MetaAimHand>();
            _root = new GameObject("menu button");
            var menu = _root.AddComponent<MenuButton>();
            _presses = 0;
            menu.Pressed += () => _presses++;
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_root);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LeftControllerMenu_PressesOncePerClick()
        {
            var controller = Add<TouchController>("LeftHand");

            Press(controller.menu);
            yield return null;
            yield return null;
            Release(controller.menu);
            yield return null;
            Press(controller.menu);
            yield return null;

            Assert.That(_presses, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RightControllerMenu_IsNotTheAppMenu()
        {
            var controller = Add<TouchController>("RightHand");

            Press(controller.menu);
            yield return null;

            Assert.That(_presses, Is.Zero, "the right hand's button is the Meta system button");
        }

        [UnityTest]
        public IEnumerator LeftHandMenuGesture_PressesOnTheFlagsRisingEdge()
        {
            var hand = Add<MetaAimHand>("LeftHand");

            Set(hand.aimFlags, (int)(MetaAimFlags.Computed | MetaAimFlags.MenuPressed));
            yield return null;
            yield return null;
            Set(hand.aimFlags, (int)MetaAimFlags.Computed);
            yield return null;
            Set(hand.aimFlags, (int)(MetaAimFlags.Computed | MetaAimFlags.MenuPressed));
            yield return null;

            Assert.That(_presses, Is.EqualTo(2));
        }

        static T Add<T>(string usage) where T : InputDevice
        {
            var device = InputSystem.AddDevice<T>();
            InputSystem.SetDeviceUsage(device, usage);
            return device;
        }
    }
}
