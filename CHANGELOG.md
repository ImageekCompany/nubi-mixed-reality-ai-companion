# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Each work package appends bullets under its own heading only (`docs/scope.md` §3).

## [Unreleased]

### WP0
- Packages: `com.unity.animation.rigging` 6.6.0, uLipSync pinned to `ece3272` (git URL, ADR-0007) and `com.unity.timeline` 6.6.0, which uLipSync needs but does not declare. Existing pins unchanged.
- Every assembly of architecture §2 as an asmdef with a `-warnaserror+` `csc.rsp` (proved honoured on 6000.6.6f1: a deliberate CS0219 failed compilation), plus a `link.xml` stub per runtime assembly.
- `Jolly.Contracts` copied from `docs/contracts.md` §1–§8.1, one file per section; it compiles unchanged.
- `SpscFloatRing`: lock-free single-producer/single-consumer sample ring for the audio thread.
- `Jolly.Testing` with the shared fakes `FakeClock`, `InMemorySecrets`, `FakeRandom` and `RecordingLog`.
- Asmdef-graph test that pins the allowed references, `csc.rsp` presence, `autoReferenced:false` on runtime assemblies and test-only define constraints.
- `tools/ci/test.sh` (EditMode + PlayMode in batchmode, results XML parsed, compiler diagnostics and banned-API greps fail the run) and `tools/ci/build.sh` (IL2CPP APK via `QuestBuild.BuildApk`).
- `Scenes/Main.unity`: XRI Starter Assets XR Origin (hand interaction profile, locomotion off), AR Session, passthrough camera (`ARCameraManager`, transparent clear), directional light and an empty `JollyBootstrap`; it is the only build scene.
- `.editorconfig`.

### WP1

### WP2

### WP3

### WP4
- `OneBitMaskDecoder`: C# port of the SAM 3.1 `one_bit` mask codec (SAM License, `LICENSE-SAM.txt` beside it), bit-exact against the live pen and mug masks.
- `SamOutputParser` (box/mask grammar, inclusive boxes, multi-object, empty output) and `SamPromptFormatter` (concept, point with hand negatives, box; `w`/`h` from the encoded image).
- `SamSegmenter : ISegmenter` over a narrow `IMetaApiJsonClient` port: usage reservation per image, `one_bit` + confidence, masks decoded on the thread pool, IClock-driven 5 s timeout.
- `SparkObjectIdentifier : IObjectIdentifier` over `IChatModel`: streamed `LABEL|NOUN_PHRASE` header then speakable answer deltas, 8 s timeout, early stop disposes the model stream.
- `JpegImageEncoder : IImageEncoder` (Core/Imaging): thread-pool JPEG encode with the top-row-first → bottom-up flip ImageConversion needs.
- R6 harness `tools/vision-eval/vision_eval.py` and report skeleton `docs/perf/vision-eval.md` (live run pending real room frames).

### WP5

### WP6

### WP7

### WP8

### WP9

### WP10

### WP11

### WP12
