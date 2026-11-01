# 0039. A frame too dark to see skips SAM and Spark; the model asks for light

- Status: accepted
- Date: 2026-11-01

## Context
- Only the self-test checked for darkness: `VisionScenario` measured the mean luma of a 160 px probe frame and switched to the bundled still below 25. The live vision tools sent whatever the camera gave them.
- Every device report so far ran in a dim room (camera mean luma 0.5–45 of 255, `docs/perf/selftest-report.md`). On a dim live frame the vision stream once completed with nothing to say, and the turn ended silently after the preamble (`selftest-report.md` "Bugs the self-test found"; fixed then by the `vision_empty` hold-closer line, which still blames the distance, not the light). A dim venue would put that on beat 2 of the demo, after paying for a SAM image and a vision call.

## Decision
- **One luma helper.** `Jolly.Core.Imaging.FrameLuma.Mean(ReadOnlySpan<byte>)`: Rec. 601 mean of an RGBA32 frame, NaN when empty, integer weights, evenly sampling at most 16,384 pixels. It runs on the CPU frame `FrameGrabber` already holds for the JPEG encode, so there is no extra readback or allocation; the 640 px vision frame is sampled every ~15th pixel, the self-test's 160 px probe in full.
- **`FrameGrabber` remembers the luma of its last capture** (`MeanLumaOf(image)`, NaN for any other image) and owns the threshold (`IsTooDark`, `VisionSettings.MinMeanLuma`, from `JollyConfig.visionMinMeanLuma`, default 25 to match the self-test). NaN is never too dark, so a frame of unknown luma behaves as before.
- **`capture_and_identify` and `find_object` check before any provider call.** Below the threshold they return `{"error":"too_dark","detail":"…switch on a light or hold it closer to one"}` without calling SAM or Spark, log `evt=vision.too_dark luma=… min=…`, and leave `ISceneMemory.LastCapture` unchanged. This covers the PTT-release pre-capture too, since it goes through the same grabber.
- **`highlight_part` has no check of its own.** It never captures: it segments `LastCapture`, which a too-dark capture no longer replaces.
- **The model says it, in one sentence.** A `PromptBuilder` tool rule: on `too_dark`, say in your own voice that it is too dark to see and ask them to switch on a light or hold the thing closer to one. The result does not end the turn.
- **Self-test.** `VisionScenario` uses `FrameLuma` and the grabber's threshold. It still switches to the bundled still (mean luma 118, checked by an EditMode test) before the tool runs, so the still path is unchanged.

## Consequences
- A dark room costs one model round (the reply) instead of a SAM image, a vision call and an answer about a black frame, and the user hears what to do: more light.
- The model's reply adds a round after the tool (about one voice turn's latency) where the old path streamed a vision answer.
- The probe (160 px) and the tool (640 px) measure slightly different frames; near the threshold the self-test can keep the live frame and the tool can still answer `too_dark`. That shows up as a `vision.too_dark` line in the run's log, not a crash.

## Alternatives
- **A canned "turn on a light" line with `EndsTurn`, like `vision_timeout`.** Faster and free, but not in the persona's voice, and a baked clip per character and locale for a rare case. Rejected for now; it is a one-line change in the two tools if the extra round shows up on stage.
- **Brighten the frame before sending it.** Rejected: gain on a near-black frame is noise; SAM and Spark still see nothing useful, and the call is still paid.
- **A luma check in `VisionToolkit.Segment` only.** Rejected: Spark would still be called on the black frame, and its answer is the problem.
