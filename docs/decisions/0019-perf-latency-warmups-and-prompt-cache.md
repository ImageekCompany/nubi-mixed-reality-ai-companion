# 0019. Launch warm-ups, a prompt cache key and self-test frame diagnostics

- Status: accepted
- Date: 2026-11-01

## Context
The device self-test (ADR-0018) left three gaps against DoD §4–§5: stale frames during turns at 1.15 % (target < 1 %) with a 0.3–0.5 s hitch at launch, a vision answer whose header arrived 2.8–3.1 s after the tool call on a dim frame (budget 2.5 s to answer audio), and a first ASR session per launch that acknowledged in 1.5–2.3 s instead of 0.7–0.9 s. The release player has no Profiler, so the causes were unknown.

## Decision
- **Self-test frame diagnostics.** `FrameSpikeLog` logs every main-thread frame over 28 ms and a per-phase window (frames, spikes, worst frame, GC bytes per frame in development players). Temporary timing probes around the suspects (not committed) attributed the spikes: the Camera2 lens-pose JNI read at the first capture (20 ms inside a 53 ms frame at the first PTT release) and the passthrough camera start at launch; the app's own code around the other spikes (graph build, request writing, mask upload, clip conversion, barge-in) stayed under 10 ms, which leaves the first draw of each caption, badge and mask (pipeline creation) as the cause there. `ProfilerRecorder` on markers returned nothing in a development player without an attached Profiler, so the log carries times, not marker names.
- **Pipeline warm-up from a recorded `GraphicsStateCollection`.** A development-player self-test traces the graphics states it draws and saves them; `GraphicsStateImport` puts the file in the project and on `JollyConfig`; `GraphicsWarmup` calls `WarmUp()` at boot, before the graph is built. `ShaderVariantCollection` was not used: it records variants without the render state a Vulkan pipeline also depends on, which is what a `GraphicsStateCollection` adds.
- **Launch warm-up (`StartupWarmup`).** Once permissions settle: one discarded camera capture (lens pose, CPU-image conversion, JPEG encoder) and one ASR WebSocket connection opened and dropped without the handshake. Nothing reaches a model, so nothing is billed (meta-model-api.md §5.2: a session starts only with the handshake).
- **`prompt_cache_key` on every Responses request**, a hash of model, instructions and tool names (`PromptCacheKey`). Live, the Responses API cached nothing for tool-bearing requests without a key and 2,673 of 2,718 tokens with one (meta-model-api.md §1.4).
- **The vision prompt says the call has no tools.** Persona prompts tell Nubi to call `capture_and_identify`; one device reply was 40 s of reasoning about that missing tool. Verified live with the line.
- **Vision output cap 600 → 1,024 tokens** (`max_output_tokens` includes reasoning; one device call ended `finish=Length out=600`). **Summary output cap 300 → 1,024** for the same reason; a summary call that does not finish normally with text now changes nothing.
- **Kept for vision:** 640 px, JPEG quality 80, `muse-spark-1.3`, `minimal` effort, SAM and Spark started together (already so). Probes (meta-model-api.md §3) showed the header time is reasoning time (230–500 tokens at `minimal`); `muse-spark-1.2` and a "do not deliberate" prompt were slower, and image size moves only the prefill.

## Consequences
- The recorded collection must be re-recorded when shaders, materials, URP settings or the scene change (docs/perf/README.md §7); a stale one only warms less.
- Every launch opens one extra TLS connection to `api.meta.ai` and grabs one camera frame that is never sent.
- The launch still has one ~310–335 ms frame: enabling the `ARCameraManager` starts Meta's passthrough camera synchronously (the `AndroidCameraManager` lines fall inside that frame). It cannot be moved off the main thread from app code. Starting the AR managers one per frame was tried and dropped: the long frame stayed and the launch stale count did not change (4 runs with it, 2 without).

## Alternatives
- **Starting the vision request speculatively at the ASR final** (before the model calls the tool): would save the agent round (~0.6–1.0 s) but pays for a vision call on every turn that merely sounds like a vision question. Not done yet: tool → answer audio is under 2.5 s at p50 (2.2 s), with one of five runs over (3.4 s); it is the next step if that tail matters on stage.
- **A Unity Profiler capture over adb:** needs the Editor GUI attached to the headset; the self-test diagnostics run headless and in every development build.
