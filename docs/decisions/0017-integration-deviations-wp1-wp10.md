# 0017. Deviations accepted when merging WP1–WP10

- Status: accepted
- Date: 2026-11-02

## Context
Each work package reported where its code departs from `architecture.md` or `contracts.md` (build/review/fix reports). The WPs could not edit the shared documents, so the changes are recorded here at integration and the architecture text is amended to match the code.

## Decision
- **`response.incomplete` is a normal completion (WP1).** `ResponsesStreamReducer` turns it into `Completed` with `FinishReason.Length` (or `Incomplete`), not a `ProviderException`. The contract already has those values, and the partial answer may already have been spoken. Architecture §7 is amended.
- **Camera pose convention (WP5, WP8).** `CameraFrame.CameraPoseWorld` is an ordinary Unity pose: +Z through the image, **+Y = image up**. Pixels have a top-left origin, so `dirCam = ((px-cx)/fx, -(py-cy)/fy, 1)`. `CameraMath` (Core) and `MaskPlacement` (Presentation, which cannot reference Core) both use this sign. The contract comment on `CameraPoseWorld` now says so; architecture §5.3 is amended.
- **NavMesh is baked with `NavMeshBuilder`, not `NavMeshSurface` + `NavMeshModifier` (WP5).** `NavMeshSurface` takes the project's Humanoid agent (0.5 m radius, 0.75 m climb), which erases a living room. `NavMeshBaker` uses `NavMeshBuilder.BuildNavMeshData` with radius 0.15 m, height 0.7 m, climb 0.04 m, and marks bounding-box sources Not Walkable through the source area, which is what the modifier would do. Architecture §5.5 is amended.
- **Character (WP6).**
  - Look-at is procedural: chest, neck and head turn first and the eyes add only the last ~10°, within ±16° yaw / ±8° pitch; the head is capped at ±40° yaw and the body turns past 45°. Per-bone `MultiAimConstraint`s cannot split one angle across bones, so `Unity.Animation.Rigging` is no longer referenced by `Jolly.Character` (the package stays installed).
  - The `Speed` Animator parameter is in m/s; the clip playback rate is a separate `LocomotionRate` (clamped 0.7–1.5×).
  - Motion is polled in `Update` rather than run on a `commandCts`; only `Stop()` or a newer command ends a move, as the contract requires.
  - The import postprocessor and asset builders live in a new editor assembly, `Jolly.Editor.Character` (`Editor/Character`, references `Jolly.Character` and `Jolly.Contracts`), instead of behind `#if UNITY_EDITOR` in the runtime assembly. `AssemblyGraphTests` has the row; `Jolly.Tests.PlayMode` references it for asset paths.
- **Spoken text on interrupt (WP2).** The text committed with " [interrupted]" is `TurnSpeech`'s record of what was captioned or started playing in the current turn, read when the turn token fires. `SpeechQueue.SpokenSoFar` spans turns and is not used for commits; the `ISpeechQueue` port no longer has it.

## Consequences
- `contracts.md` and `Runtime/Contracts` change only in comments (`IAgentLoop.RunTurn`, `CameraFrame.CameraPoseWorld`); see `review-log.md`.
- WP9 must keep both projections in step until one shared helper exists (see Alternatives).

## Alternatives
- Move a `PixelToCameraDir` helper into `Jolly.Contracts` so `CameraMath` and `MaskPlacement` share one formula: deferred to WP9; it is a contract code change and both copies are tested against the same sign today.
