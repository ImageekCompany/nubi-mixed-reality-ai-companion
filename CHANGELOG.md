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

### WP8
- Unity Localization set up: settings asset, locales `en` (default) and `pl`, the `UI` string table (en + pl), Addressables with `BuildWithPlayer`, and `LocaleVoiceMap.asset` (Polish: ASR bias only, no Wit voice).
- `UnityLocalizer`: preloads tables at startup and on locale change, synchronous `Get` with key fallback and a warning, `evt=loc.ready` per table, static-event unsubscribe on `Dispose`.
- Views in `Jolly.Presentation`: `CaptionView`, `MaskOverlayView` (`IMaskPresenter`, `Jolly/MaskOutline` fill + outline shader), `ToolBadgeView`, `LatencyHud`, `StatusIndicator` (`IStatusSink`) and `SettingsPanel` (`IUserPreferences`; caption size, speech rate, volume, seated, PTT hand, persona, language, clear memory, API usage), shipped as world-space prefabs.
- TMP Essential Resources imported (needed by every TMP text).
- 51 PlayMode tests under `Tests/PlayMode/Presentation` (localizer, caption timing, overlay placement, badges, HUD, status, settings); contract fakes shared in `Tests/Shared/Fakes/WP8`.
- Review fixes: a cancelled `SetLanguage` no longer leaves the language half-switched; Polish falls back to English per entry; running tool badges end when the turn ends; the mask outline sets its texel size explicitly; stored preferences are clamped on load.

### WP9

### WP10

### WP11

### WP12
