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

### WP5

### WP6

### WP7
- `WitTextToSpeech`: raw Wit `/synthesize` (`audio/raw`, 24 kHz s16le) over `UnityWebRequest` with a pooled 32 KB `DownloadHandlerScript` buffer; `WitPcmChunker` carries odd bytes across HTTP chunks; 3 s first-chunk and 3 s inter-chunk timeouts on `IClock` (`PcmStreamTimeout`), surfaced as `ProviderException(Timeout)`; HTTP status mapping; English only (`Supports`).
- `StreamingSpeechOutput`: clip-less `AudioSource` fed from `OnAudioFilterRead`. `SpeechRingPlayer` (audio thread: SPSC ring, exact integer-ratio linear resampling 24 kHz → output rate, 20 ms flush fade, block RMS) + `SpeechOutput` (main thread: per-call prefetch, strict call-order writes, `FirstSamplePlayed` and completion from the consumed position, `Flush` cancels every pending `Play`/`PlayClip`).
- `AmplitudeLipSyncDriver`: RMS → mouth "A", instant attack, closed within 100 ms of silence.
- `SentenceSplitter` (terminators + closers, abbreviations/initials, decimals, newline, 60-char first chunk on a comma, 120-char comma/space split, 240 cap, `。`) and `SpeechQueue` (prefetch depth 1, user speech rate clamp 50–200, `Clear` on barge-in, `WhenDrained` never hangs after a flush, `SpokenSoFar`, `SentenceStarted`/`SentenceFailed`).
- `CannedLines : ICannedLines` (no repeat of the category's last 5 picks, localized text with English fallback, clip via the `ILineClipSource` port) and `CannedLineCatalog` (31 English lines over every `LineCategory`).
- `CannedLineBaker` (editor menu + batch method) bakes the catalog through the runtime Wit client; 31 English lines baked to `Content/Audio/Lines/en` with Wit voice `Charlie` (Decompress On Load, ADPCM, mono).

### WP8

### WP9

### WP10

### WP11

### WP12
