# 0021. Controller push-to-talk: hold A or X, bound by device layout

- Status: accepted
- Date: 2026-11-01

## Context
- Requirement 12: hand tracking must cover everything, and controllers should work too. The scene's controller binding (`PinchPushToTalk.controllerTalk`) shipped empty, so push-to-talk was pinch only.
- The obvious binding, XRI Starter's `Activate` (`<XRController>{LeftHand}/{TriggerButton}`), is usage-based. The WP9 report flagged that it may also fire on a hand pinch. A pinch can reach the Input System as a controller: the Hand Interaction profile's `HandInteraction` device is an `XRController`, a runtime can report a pinch as a controller trigger, and the KHR simple controller's `select` carries the `PrimaryButton` usage. A press that arrives that way skips the pinch gating (no UI hover, palm not toward the head).
- XRI uses the trigger for `Activate` and UI press, and the grip for `Select` (grab, poke, UI).

## Decision
- **Hold A (right) or X (left).** It is the only face button the Starter actions leave to the app while locomotion is off (`Jump` on A has no provider in the scene), it can't collide with UI clicks or grabs, and it is a deliberate press, so both controllers always work and the dominant-hand setting applies to pinches only.
- **`ControllerPushToTalk`** (`Jolly.XR.Voice`) owns two code-defined `InputAction`s bound by layout name, never by usage: `<OculusTouchController|QuestTouchPlusController|QuestProTouchController>{LeftHand|RightHand}/primaryButton`. It takes no `InputActionReference`, so it cannot be pointed at `Activate`.
- **Modality.** Like `XRInputModalityManager`, which puts a side into hand mode when that hand is tracked, the button on a side whose hand `IHandSource` reports as tracked is ignored. The manager's per-side mode is `internal`, so the rule is mirrored rather than read.
- **`PinchPushToTalk`** handles hands only. **`CompositeVoiceActivation`** (`Jolly.Core.Conversation`) merges the two for `ConversationController`: it is held while any source is held, so a second source neither presses again (no self barge-in) nor releases early. It owns its subscriptions, not the sources. `AppGraph` builds it when `Edges.Voice` is null.

## Consequences
- PlayMode tests on `InputTestFixture` with the real OpenXR layouts: A/X press and release, trigger, grip and B/Y don't talk, Hand Interaction and KHR simple presses can't press, a tracked hand gates its side, and disabling releases. The test assembly references `Unity.InputSystem.TestFramework` and `Unity.XR.OpenXR`.
- Unverified on the headset: no controller session has been run yet.
- No rebinding UI. Changing the button means changing the binding list in `ControllerPushToTalk`.

## Alternatives
- **Trigger (XRI `Activate`):** the most common VR talk button, but it is UI press under a ray and the path a pinch can take into the Input System.
- **Grip:** XRI `Select`, so holding to talk would also grab or poke Nubi.
- **Read `XRInputModalityManager.currentInputMode`:** it is global and holds whichever side changed last, so it can't gate one controller.
