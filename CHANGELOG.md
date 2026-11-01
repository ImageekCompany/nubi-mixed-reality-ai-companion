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
- `Resampler`: streaming mono rate converter (linear interpolation, 63-tap Blackman-sinc anti-alias when downsampling), split-invariant and allocation-free; also serves WP7's 24 kHz → output-rate path. `Pcm16` float ↔ s16 and RMS.
- `UnityMicrophoneCapture` + `MicrophoneCaptureSession`: 48 kHz device ring → 24 kHz 80 ms pooled `PcmChunk`s, pumped on the PlayerLoop; a 500 ms stall watchdog restarts the mic (device spike §2.6) and ends the stream after three fruitless restarts; `Stop` flushes, `Dispose` aborts.
- `ClientWebSocketConnection` (`Jolly.Net.WebSockets`): main-thread-resuming `ClientWebSocket` wrapper behind `IWebSocketConnection`.
- `MuseRealtimeSpeechToText`: realtime push-to-talk ASR. Handshake backlog sent at once after the ack, live audio forwarded as it arrives and never more than 5 s ahead, `endStream` in the same continuation as source completion, cumulative partials de-duplicated, exactly one normalised Final, 5 s ack timeout on `IClock`, error frames and close codes mapped to `ProviderErrorKind`, usage reserved before connecting and settled with the audio seconds sent.
- `MuseFileSpeechToText`: `POST /v1/asr/transcribe` fallback (buffered WAV, one Final, 10 s timeout).
- Recorded ASR fixtures (`Tests/Fixtures/asr`), and shared fakes `FakeAudioCapture`, `ScriptedSpeechToText`, `StreamCollector<T>`.

### WP4

### WP5

### WP6

### WP7

### WP8

### WP9

### WP10

### WP11

### WP12
