using System.Collections;
using System.Collections.Generic;
using Jolly.XR.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using HapticControl = UnityEngine.XR.OpenXR.Input.HapticControl;
using Object = UnityEngine.Object;
using HandInteraction = UnityEngine.XR.OpenXR.Features.Interactions.HandInteractionProfile.HandInteraction;
using KHRSimpleController = UnityEngine.XR.OpenXR.Features.Interactions.KHRSimpleControllerProfile.KHRSimpleController;
using TouchController = UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile.OculusTouchController;

namespace Jolly.Tests.PlayMode.XR
{
    /// <summary>Against the OpenXR device layouts the Quest build enables (Touch and Hand Interaction profiles) plus the
    /// KHR simple controller, the profile that gives a pinch the PrimaryButton usage.</summary>
    public sealed class ControllerPushToTalkTests : InputTestFixture
    {
        static readonly Pose PalmDown = new(new Vector3(0.2f, 1.1f, 0.35f), Quaternion.identity);

        GameObject _root;
        FakeHandSource _hands;
        ControllerPushToTalk _ptt;
        List<string> _events;

        public override void Setup()
        {
            base.Setup();
            // OpenXR registers these control layouts when the editor loads, and the fixture's reset drops them. The
            // profiles use OpenXR's or the Input System's PoseControl depending on USE_INPUT_SYSTEM_POSE_CONTROL.
            InputSystem.RegisterLayout<HapticControl>("Haptic");
            InputSystem.RegisterLayout(typeof(TouchController).GetProperty(nameof(TouchController.devicePose)).PropertyType, "Pose");
            InputSystem.RegisterLayout<TouchController>();
            InputSystem.RegisterLayout<HandInteraction>();
            InputSystem.RegisterLayout<KHRSimpleController>();
            _root = new GameObject("controller ptt");
            _hands = new FakeHandSource();
            _ptt = _root.AddComponent<ControllerPushToTalk>();
            _ptt.Construct(_hands);
            _events = new List<string>();
            _ptt.Pressed += () => _events.Add("pressed");
            _ptt.Released += () => _events.Add("released");
        }

        public override void TearDown()
        {
            // Before the fixture restores the input system, so the component's actions are disposed against the test one.
            Object.DestroyImmediate(_root);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator HoldingAOrX_PressesThenReleases([Values("LeftHand", "RightHand")] string hand)
        {
            var controller = Add<TouchController>(hand);

            Press(controller.primaryButton);
            yield return null;
            Assert.That(_ptt.IsHeld, Is.True);

            Release(controller.primaryButton);
            yield return null;

            Assert.That(_ptt.IsHeld, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }

        [UnityTest]
        public IEnumerator TriggerAndGrip_DoNotTalk()
        {
            var controller = Add<TouchController>("RightHand");

            Press(controller.triggerPressed);
            Set(controller.trigger, 1f);
            Press(controller.gripPressed);
            Set(controller.grip, 1f);
            Press(controller.secondaryButton);
            yield return null;

            Assert.That(_events, Is.Empty, "trigger is XRI Activate and UI press, grip is XRI Select");
        }

        [UnityTest]
        public IEnumerator HandDevices_CannotPress()
        {
            var hand = Add<HandInteraction>("RightHand");
            var simple = Add<KHRSimpleController>("LeftHand");

            foreach (var button in new ButtonControl[] { hand.pinchTouched, hand.pointerActivated, hand.graspFirm, simple.select })
                Press(button);
            yield return null;

            Assert.That(_events, Is.Empty, "a pinch reported as a controller must go through PinchPushToTalk's gating");
        }

        [UnityTest]
        public IEnumerator Button_OnASideWhoseHandIsTracked_IsIgnored()
        {
            var controller = Add<TouchController>("RightHand");
            _hands.Set(Handedness.Right, HandPoses.Open(PalmDown));

            Press(controller.primaryButton);
            yield return null;
            Assert.That(_ptt.IsHeld, Is.False, "hand mode on that side, as XRInputModalityManager decides it");

            _hands.Lose(Handedness.Right);
            yield return null;

            Assert.That(_ptt.IsHeld, Is.True);
        }

        [UnityTest]
        public IEnumerator Disable_WhileHeld_Releases()
        {
            var controller = Add<TouchController>("LeftHand");
            Press(controller.primaryButton);
            yield return null;

            _ptt.enabled = false;

            Assert.That(_ptt.IsHeld, Is.False);
            Assert.That(_events, Is.EqualTo(new[] { "pressed", "released" }));
        }

        static T Add<T>(string usage) where T : InputDevice
        {
            var device = InputSystem.AddDevice<T>();
            InputSystem.SetDeviceUsage(device, usage);
            return device;
        }
    }
}
