# 0018. WP9 integration: composition, edges, content and the device self-test

- Status: accepted
- Date: 2026-11-01

## Context
WP9 wires every WP1–WP10 implementation into `Main.unity` (architecture §3) and owns the items the WP reports deferred to it. The requirements also ask for a self-checking pipeline that runs the real product end to end before anything is called done. Running the app on the Quest with nobody wearing it found three bugs that no fake caught, so the decisions below include the fixes.

## Decision
- **Composition is split by concern, not by WP.** `JollyBootstrap` (MonoBehaviour) builds `AppGraph`, which builds `Foundation` (clock, random, redacting log, secrets, usage meter, latency, events, state machine), `CloudServices`, `XrServices`, `SpeechServices` and `ConversationServices` (+ `ToolCatalog`) in dependency order and owns one `DisposableStack`. The localizer is initialised before `PersonaRepository` resolves any text and `SettingsPanel.Construct` runs before anything reads `IUserPreferences`.
- **Edges.** Everything that touches the outside world (files, secrets, reachability, `IHttpTransport`, the WebSocket factory, the probe's status call, Wit, the microphone device, push-to-talk, the speaker, a camera wrapper) is an `Edges` value. The player uses `Edges.ForPlayer()`. The PlayMode E2E tests load the real `Main.unity` and replace only edges, so the controller, agent loop, tools, Responses reducer, SAM parser and decoder, Muse protocol, speech queue and splitter run for real.
- **Connectivity reporting by decorators.** `ReportingChatModel/SpeechToText/TextToSpeech/Segmenter` report each call's outcome to `IConnectivityMonitor` (architecture §5.4); providers stay unaware of it. `MetaHealthProbe.Unauthorized` is routed to `ReportFailure("meta", Unauthorized)`.
- **Two Responses chat models.** One for voice turns and vision (6 s first event, 2 attempts) and one for summarisation (10 s, 3 attempts); every model id goes through `MetaModelResolver`.
- **Line clips are a plain asset.** `LineClipLibrary` (App, a ScriptableObject implementing `ILineClipSource`) replaces the `LineClips` Addressables asset table of architecture §10. The 31 clips are small, must play with no network, and an asset table would need Addressables editor references in `Jolly.Editor` only to look clips up by key. The `Lines` string table exists (en + pl) and is preloaded.
- **Assembly references.** `Jolly.App` references the XR packages and `uLipSync.Runtime` (its scene references are typed). `Jolly.Editor` references `Unity.Localization(.Editor)` for the content validators. `Jolly.Tests.PlayMode` references `Jolly.Integrations.ULipSync` and `uLipSync.Runtime` for the driver test. `AssemblyGraphTests` has the rows.
- **Shared helpers instead of copies.** `CameraIntrinsics.PixelToCameraDir` (a trivial value helper in Contracts, no signature changed) is the one projection of `CameraMath` and `MaskPlacement`. `MetaErrors.KindOfStatus` is the one HTTP status mapping (the ASR file client used its own). The health probe calls through the shared `IHttpTransport` (`TransportStatusClient`); `UnityWebRequestStatusClient` is deleted. `CaptionTiming.HoldFor` is shared by `OfflineResponder` and `TurnSpeech`.
- **Captions-only replies are paced by `TurnSpeech`.** It raises the turn's captions, so it holds each for its reading time when the locale has no TTS voice (Polish); `CaptionView` keeps showing one line. `TurnSpeech` takes an `IClock`.
- **Bugs fixed at integration.**
  - The splitter held a tool preamble without trailing whitespace and glued the next round onto it ("On my way!Here I am."); `TurnPipeline` now speaks the buffer when a tool starts (E2E test).
  - `MetaHealthProbe` appended `/v1/models` to the versioned base URL, so every probe hit `/v1/v1/models` (404) and the app could never leave Offline; it now appends `/models` (device self-test).
  - A retried SAM call billed one image whatever the earlier attempts did; it now bills every attempt that may have reached SAM (`OpenAttempts.PossiblyBilled`).
- **The device self-test.** `SelfTestSession`/`SelfTestRunner` (Runtime/App/SelfTest) are compiled into every build and inert unless `persistentDataPath/selftest.json` exists or the launch intent carries the `selftest` extra; the flag is deleted at start. It replaces only what a person provides: the network switch (a reachability override), the voice (bundled `say` clips fed into the real microphone pipeline through `ClipMicrophone`), the pinch (`ScriptedPushToTalk`; the real pinch is not forwarded because hand tracking produced phantom presses with nobody wearing the headset) and a lit room (`SwitchableCameraSource` falls back to a CC0 still and marks the result DEGRADED). `tools/device/selftest.sh` drives it headless and fails on any FAIL.

## Consequences
- `contracts.md` gains `CameraIntrinsics.PixelToCameraDir`; §9.1 records the new constructor shapes (`TurnSpeech` takes `IClock`; `MetaHealthProbe`'s base URL includes the version).
- The DoD row "`LineClips` clip resolves in player" is met by `LineClipLibrary` instead of the asset table.
- Every device self-test run spends real money (≈ $0.02); `api-spend.md` has the rows.

## Alternatives
- **A DI container for the larger graph:** rejected for the reasons of ADR-0009; the graph is still one scene, one lifetime, and the split files keep each builder short.
- **Fakes in the self-test (scripted ASR/LLM):** rejected; the point is to exercise the real cloud path on the real device. The E2E tests already cover the scripted path in CI.
- **Merging the 11 same-named test fakes and WP7's pull resampler into WP3's `Resampler`:** deferred. The fakes differ in behaviour and are test-only (aliased per file). The two resamplers run in different directions and threads (48→24 kHz with an anti-alias filter on the main thread vs 24 kHz→output rate, pull mode, on the audio thread); sharing one would put a filter on the audio thread for no gain.
