# 0016. Pre-freeze contract revision after the architecture reviews

- Status: accepted
- Date: 2026-11-01

## Context
Three reviews (feasibility, quality, judge) of the blueprint found contract defects that would have been frozen by WP0, plus scope choices that under-scored on the "Meta AI tooling" and "Demo" criteria. Four cheap live probes (≈ $0.01, `docs/api-spend.md`) settled the open API questions. The full finding list with accept/reject decisions is in `review-log.md`.

## Decision
Contract changes, applied before WP0 copies `contracts.md`:
- **Streams and threading:** `UniTask.Linq` is referenced wherever streams are built; timeouts use `CancelAfterSlim`/`TimeoutController`, never `CancelAfter`; `ToUniTask(cancelImmediately:true)` is banned.
- **Pooled audio:** `PcmChunk` is a pooled sealed class with idempotent `Dispose`. `ISpeechOutput.Flush` cancels pending `Play` calls. Output uses `OnAudioFilterRead`, not a streamed clip.
- **STT:** the handshake backlog is sent at once, then live audio; no 1× backlog pacing.
- **Camera:** `ICameraFrameSource : IDisposable`, `Capture(maxSide)` converts at target size, one outstanding frame, `Rgba32` is an exact sub-array. `IImageEncoder` only encodes. A frame is pre-captured at PTT release (`UserTurn.PreCaptured`).
- **Vision:** `IObjectIdentifier.Identify` streams `IdentificationEvent`s (`LABEL|NOUN_PHRASE` header, then answer deltas) and takes persona instructions; the tool speaks through `ToolContext.Speech`.
- **Locomotion:** `ICharacterController.TryStartMoveTo` + `WhenArrived(ct)`; the caller's token never stops motion. Locomotion tools return on start. `Exclusive` no longer waits for Locomotion. `IdleScheduler` respects `LocomotionMode`.
- **Turn and memory:** `IConversationMemory.CommitTurn` replaces `Append`; orphan calls get `{"error":"interrupted"}` outputs. Persona switch is `RequestSwitch` + controller-applied `ApplySwitch` between turns. `ConversationController` is split into `TurnRouter`, `TurnPipeline` and `BargeInCoordinator`. The state machine latches its guard flags and has watchdogs for every non-Idle state.
- **Ports for presentation:** `IConversationEvents`, `IMaskPresenter`, `IStatusSink`, `IOfflineResponder`, `IUserPreferences`.
- **Safety and privacy:** `MetaModelResolver` throws (not an assertion) for contributor on LiveUser; scene context moves to a sanitised user-role data item; `safety_identifier` comes from a per-install random GUID.
- **Connectivity:** adds `Unauthorized` and `BudgetReached`.
- **Usage meter:** reservations, estimates for aborted requests, explicit `Flush` on pause.
- **Persistence:** hand-mapped DTOs + `AtomicFile` (`File.Move` on first save).

## Consequences
- WP0 copies the revised contracts; no later ADR is needed for these.
- New *should*-tier tools `find_object` and `highlight_part` reuse the capture → SAM → overlay path.
- Hand-ray SAM point prompts with hand-joint negative points become *must* tier, conditional on spike R1.

## Alternatives
- Patch after the freeze with per-change ADRs: rejected; every change above would have touched several WPs in parallel.
