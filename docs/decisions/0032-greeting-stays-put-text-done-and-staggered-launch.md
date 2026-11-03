# 0032. Device-regression fixes: a greeting stays put, a text item ends speech, XR managers start one per frame

- Status: accepted
- Date: 2026-11-01

## Context
The final verification of `main` 6179cfc (`docs/perf/selftest-report.md`) found three regressions on the Quest:
- "Hi Nubi, who are you?" called `come_here` in 4 of 7 release runs. The companion prompt said "you can walk around, come over, follow the person", and `come_here` was described only as "Walk to the user and face them."
- One ~410 ms launch frame between `evt=hands.subsystem state=running` and `evt=occlusion.mode mode=depth`. The log lines inside it show the app-graph build, the AR camera manager start (Meta's `AndroidCameraManager` opening both Camera2 streams), every scene manager start including the occlusion manager, and the depth texture creation in the same main-thread frame. The pipeline collection warmed at boot (ADR-0019) was recorded before the hand rig (ADR-0030) and occlusion (ADR-0024), so their pipelines compiled at first draw.
- Release → first audio p50 2,090 ms against 2,000 ms. First text → first sentence queued was up to 235 ms on tool turns: a preamble such as "Let me check." has no trailing whitespace, so `SentenceSplitter` held it until the tool call's arguments had streamed and `ToolStarted` flushed it.

## Decision
- **Move only when asked.** `PromptBuilder`'s static rules add "Move only when the user asks you to go somewhere; answer a greeting or a question about yourself in words, with at most a gesture." `come_here` and `follow_user` descriptions say they are only for the user's request (`come_here`: "never for a greeting, an introduction or small talk"). The companion prompt (en, pl) offers walking, coming over and following "when the person asks". The seated rule no longer reads as an order to come over. The self-test's `voice_greeting` (`GreetingScenario`) degrades when a movement tool starts (`TurnChecks.StayedPut`).
- **`ModelEventKind.TextDone` → `AgentEventKind.SpeechBreak`.** `ResponsesStreamReducer` raises `TextDone` when a message output item with text is done; `AgentLoop` forwards it as `SpeechBreak`; `TurnPipeline` flushes the splitter. Both enum values are appended, so existing consumers that switch on known kinds ignore them (`Summarizer`, `SparkObjectIdentifier`).
- **Launch.** `PermissionGate` enables the camera manager one frame after `Run` is called (the frame that built the graph) and each scene manager on its own following frame, skipping ones already enabled. `Content/Rendering/QuestVulkan.graphicsstate` is re-recorded on a development player with the hand rig and depth occlusion active.

## Consequences
- Device, 3 release runs each: greetings that moved 4 of 7 → 0 of 7; release → first audio p50 2,090 → 1,776 ms (n=12; the model's time to first text still varies 0.4–2.4 s, so the margin is small); first text → sentence queued p50 112 → 0 ms; worst launch frame 410 → ~283 ms.
- The ~100 ms launch target is not met: what remains is the camera manager's own start, one synchronous call into the Meta OpenXR provider that also creates the passthrough layer, so it cannot be deferred without losing passthrough at launch.
- `PermissionGate.Run` completes a few frames later (one per scene manager), which delays the start of the conversation loops by ~70 ms.
- The pipeline collection must be re-recorded after shader, material or rig changes (docs/perf/README.md §7); a stale one silently moves pipeline compiles back to first draw.

## Alternatives
- **Filter `come_here` out of greeting turns in code.** Rejected: deciding which utterance is a greeting is the model's job, and a keyword filter would break "Hi, come here".
- **Emit a newline `TextDelta` at the end of a message** instead of new event kinds. Rejected: it changes the text every consumer concatenates (summaries, vision answers).
- **Defer the camera manager until the first vision turn.** Rejected: its start creates the passthrough layer; without it the room is black at launch.
