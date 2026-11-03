# 0041. The character attends to the user through a conversation turn

- Status: accepted (amends the look-at of ADR-0017)
- Date: 2026-11-01

## Context
- Wearing the headset, the user saw the character not look at them while it spoke.
- `NubiCharacterController.TryResolveLookPoint` resolved `LookTarget.None` to no target unless following, and `IdleScheduler.Glance` ends with `LookAt(None)`. After the first idle glance (any of the 8–20 s idle picks), nothing set the user again except `come_here` or `look_at user`: the look weight blended to 0 and the body never turned, in every later turn.
- Every tool that pointed the look somewhere (`look_at`, `find_object`, `capture_and_identify`, `highlight_part`, `walk_to`, `go_to_surface`) left it there for good.
- Even with the user as the target, the body turned only past 45° (`bodyTurnBeyond`): a user 30–45° to the side got a turned head on a body facing away.
- A PlayMode test on the old controller reproduced both: after `LookAt(None)` with the user at 70°, the body stayed 70.0° off while Speaking; with the user at 35°, 35.0° off.
- Not a cause: the head transform. `SceneRefs.head` points into the XR Origin Hands rig instance, and the device run logs the user's head 1.2 m above the character's feet and 1.2 m away (`user_h`, `user_dist`), not the identity pose an unbound head reads.

## Decision
- **Attention per frame** (`Attention.Resolve`): a look-around sweep first; a tool's point while it holds; otherwise the user whenever a conversation turn is on (Listening, Thinking, Speaking, Acting), while following, when `LookTarget.User` was asked for, or when the user is within `attendWithin` (3 m); else none.
- **A point holds `pointHoldSeconds` (2.5 s) of standing still**: the hold restarts every frame while walking, so a walk's look at its destination lasts until 2.5 s after arrival. Then the character attends the user again. A new Listening state drops a held point and a running look-around.
- **Facing.** Attending the user, the body turns in place past `faceUserBeyond` (15°) and settles within 5°; toward a point it still turns only past `bodyTurnBeyond` (45°). The in-place turn eases out (4 deg/s per degree to go, 30–`bodyTurnSpeed` 150 deg/s) instead of the 240 deg/s walking turn rate.
- **Thinking** still blends head and eyes out (the Think pose keys the eye bones, art README), but the body turns to the user.
- **Per character**: the four new knobs are `LookSettings` fields in `CharacterDefinition.tuning`; definitions without them (local Jolly) take the defaults. The head's facing for telemetry is its bind-pose forward from the model asset (`CharacterRig.HeadForward`), so it holds for any bone axes.
- **Telemetry.** `LookTelemetry` logs `evt=look.user body_yaw_off head_yaw_off attending user_dist user_h` at each turn's first Speaking (first audio). Voice self-test scenarios record it as `look` and degrade past 25° body or 15° head unless a tool's point is held; `voice_come_here` checks facing after arrival instead (`look_after_arrival`), as its first audio plays while it starts walking.

## Consequences
- Device run of `dcad2c7` with `--character jolly` (`evt=character.selected id=jolly`), headless: 7 turns, body 0.1–4.9°, head 1.6–6.9° off the user's head; `look_after_arrival` body 0.4°, head 1.7°. The headset did not move, so the run checks steady facing, not turning to a moving user; the PlayMode tests cover turning (70°, 35°, 90° while Thinking, after a held point, after a look-around and after a walk), Jolly included when the local definition is present.
- Idle and within 3 m the character now turns to face the user as they move around, past 15°. A user farther away gets the old idle behaviour (glances only).
- A vision answer looks at the object for 2.5 s, then back at the user, even if the sentence about the object is longer.

## Alternatives
- **Make the glance end on `LookAt(User)`.** Fixes only the glance; tool points would still stick and the 45° body threshold would remain.
- **Release a tool's point at the end of the turn.** The character would talk about the mug to the mug for the whole answer; a fixed hold returns it to the user mid-answer.
- **Keep the head and eyes on the user while Thinking.** Layered on the Think pose the eyes fight the clip's own eye keys.
