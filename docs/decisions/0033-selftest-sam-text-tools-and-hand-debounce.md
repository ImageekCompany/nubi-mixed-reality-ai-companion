# 0033. Self-test for the SAM text-prompt tools; debounced hand visuals

- Status: accepted
- Date: 2026-11-01

## Context
- The device self-test (ADR-0018) covered one vision tool, `capture_and_identify`. `highlight_part` (the second half of the demo's vision beat, "which part do I hold?") and `find_object` ("where are my glasses?") send SAM a text prompt and had only fake-backed tests, so nothing showed them working on the headset.
- Neither tool reports what it outlined through `IConversationEvents`; the self-test saw only that a tool started and completed.
- With nobody wearing the headset, `evt=hands.tracked` flipped 38–56 times per run (selftest-report.md, 6179cfc). Each flip came from the XR Hands subsystem, and `XRHandMeshController` (package code) showed or hid the hand mesh on every change, so a phantom blip drew a hand for a moment.

## Decision
- **Two scenarios after `vision_capture_and_identify`, before `barge_in`.** `vision_highlight_part` asks "Which part of the mug should I hold?" and must follow the identify turn: `highlight_part` segments the scene memory's last capture and never takes a new frame. It checks that `highlight_part` was called and succeeded, that a mask was shown on that same capture (`ReferenceEquals` on the `EncodedImage`; there is no capture id), and that an answer was spoken after it. `vision_find_object` asks "Nubi, where is my mug? Show me." and checks that `find_object` outlined at least one instance. `capture_and_identify` is a fair reading of a "where is" question, so another tool or none degrades with its name (`TurnChecks.PreferredTool`) instead of failing; a missing `highlight_part` fails, because the part question has no other tool.
- **Both run on the bundled still** (`still_mug.bytes`), whatever the room's light, because their questions are about the mug in it. When the identify turn used the live camera, `vision_highlight_part` captures the still into scene memory first and reports `frame=seeded_still`. Using the still is not a degradation here: these scenarios test the tools, not the room.
- **`MaskObserver`**, a decorator on the scene's mask overlay installed through a new `Edges.WrapMasks` (the pattern of `WrapCamera`), records the last show: time, instance count and source image. Metrics: `tool_to_mask_ms`, `tool_to_answer_audio_ms` (first caption after the tool succeeded, from its start; `TurnObserver.AnswerAfter`) and `found`. The answer budget is the DoD's 2.5 s, as for `capture_and_identify`; both tools return to the model before it answers, so expect DEGRADED on that budget.
- **Debounced hand visuals.** `HandTrackingMonitor` turns off the mesh controllers' own show and hide (`showMeshWhenTrackingIsAcquired`, `hideMeshWhenTrackingIsLost`) and sets each hand renderer from `TrackingDebounce`: a hand's tracked state changes only after the raw state has held for 200 ms (`TrackingDebounce.DefaultHoldSeconds`). `evt=hands.tracked` now logs the debounced state. Tracking is read every frame so the hold is measured, not quantised to the 4 Hz poll. A controller the hand visualizer disabled (meshes off) is left alone.

## Consequences
- A self-test run makes two more voice turns and 1–2 more SAM calls (about +$0.005).
- A real hand appears 200 ms after tracking starts and stays 200 ms after it is lost. The pinch push-to-talk and the XRI interactors still read the raw subsystem; only the mesh is debounced.
- First device run (selftest-report.md, 31bfbea): `vision_highlight_part` PASS (part mask 514 ms, answer 1.3 s after the tool start); in `vision_find_object` the model answered with `look_at` on the remembered mug, so `find_object` still has no device evidence; `evt=hands.tracked` fell from 38–56 lines per run to 3 (one 320 ms phantom passed the hold).
- `TrackingDebounce` lives in `Jolly.XR`, which the EditMode test assembly does not reference, so its tests are plain `[Test]`s in PlayMode next to `OcclusionPolicyTests`, as for the other pure XR logic.

## Alternatives
- **Debounce in a fork of `XRHandMeshController`:** it is package code; a copy would have to follow every XR Hands update.
- **A longer window (0.5 s):** would hide longer phantom blips but delays a real hand by as much. 200 ms is the shortest window worth trying first; the `evt=hands.tracked` count of the next device run (selftest-report.md) says whether it is enough.
- **A capture id on `EncodedImage`:** a contract change for one test check; reference identity already says "the same capture".
