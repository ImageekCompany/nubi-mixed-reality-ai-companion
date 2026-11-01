# Definition of done

The project is "done" only when every box below is ticked, with evidence (a log, a CSV or a screenshot) stored under `artifacts/` (git-ignored) or `docs/perf/` (committed summaries). A WP is done when the rows for its own scope pass.

## 1. Automated tests (batchmode)

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity
PROJ="$PWD/JollyXR"; mkdir -p artifacts

# EditMode
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode \
  -testResults "$PWD/artifacts/editmode.xml" -logFile "$PWD/artifacts/editmode.log"; echo "exit=$?"

# PlayMode (drop -nographics only if a test needs a GPU)
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform PlayMode \
  -testResults "$PWD/artifacts/playmode.xml" -logFile "$PWD/artifacts/playmode.log"; echo "exit=$?"

# Optional live smoke (costs money; Synthetic route only; log it in docs/api-spend.md)
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode -testCategory Live \
  -testResults "$PWD/artifacts/live.xml" -logFile "$PWD/artifacts/live.log"
```

The default runs must exclude the `Live` category. Either mark those tests `[Explicit]`, or add `-testCategory "!Live"`; the negation syntax is UNVERIFIED, so prefer `[Explicit]`.

Flag provenance:
- `Unity -help` printed nothing to stdout on this Mac.
- `-runTests`, `-testPlatform`, `-testResults`, `-testFilter`, `-testCategory`, `-assemblyNames`, `-runSynchronously`, `-retry`, `-repeat` and `-playerHeartbeatTimeout` were read from `com.unity.test-framework` 1.8.0 `UnityEditor.TestRunner/CommandLineTest/SettingsBuilder.cs`.
- **Do not pass `-quit` with `-runTests`.** The framework logs "Running tests from command line arguments will not work when quit is specified" (same file).
- Exit codes come from `Executer.cs`: `0` Ok, `2` tests failed, `3` run error (including compile errors), `4` platform not found.
- `-batchmode`, `-nographics`, `-projectPath`, `-logFile`, `-executeMethod`, `-buildTarget` and `-quit` are standard editor flags per the Unity Manual "Command line arguments". They are not re-verified for 6000.6.

`tools/ci/test.sh` runs both suites and fails on non-zero exit. It also fails if any compiler warning comes from our code:
```bash
! grep -hE "Assets/Jolly/.*: warning CS[0-9]{4}" artifacts/*.log
! grep -rnE "\.CancelAfter\(" JollyXR/Assets/Jolly --include=*.cs                      # use CancelAfterSlim / TimeoutController
! grep -rn "cancelImmediately: *true" JollyXR/Assets/Jolly --include=*.cs
```

- [ ] EditMode exit 0. All Core, Net, Providers and Audio logic is covered. Each WP's required tests exist (`scope.md`).
- [ ] PlayMode exit 0, including the E2E voice turn, barge-in, `capture_and_identify` with fakes, the offline path, and `NoGcAllocInSteadyStateIdle`.
- [ ] No test hits the network unless it is `[Category("Live")]`.

## 2. Code quality gates

- [ ] **Zero compiler warnings** in `Assets/Jolly/**`.
  - A `csc.rsp` containing `-warnaserror+` sits beside **each** `Jolly.*` asmdef (Unity reads `Assets/csc.rsp` for predefined assemblies and an asmdef-adjacent `csc.rsp` for that assembly; a `csc.rsp` in a folder without an asmdef is ignored). WP0 proves it on 6000.6 with one deliberate warning that must fail the build, then removes it.
  - No `#pragma warning disable` without a comment saying why.
- [ ] No `async void` and no `Task`-returning methods in `Jolly.*`: `grep -rnE "async void|[^.]Task<|[^a-zA-Z.]Task " Assets/Jolly --include=*.cs` is empty, except test attributes.
- [ ] No `FindObjectOfType`/`FindFirstObjectByType`, no `static .* Instance` singletons, and no `Resources.Load` in `Jolly.*`.
- [ ] No secret material in the repo: `git grep -nE "LLM_[0-9]+_|Bearer [A-Za-z0-9]"` is empty. `.env` and `secrets.json` are untracked. `Meta+Muse+Jolly*` is untracked.
- [ ] Release builds contain no `JOLLY_DEBUG_CAPTURE` or `JOLLY_DEBUG_LOG` define (check `PlayerSettings` scripting defines in `build.log`).
- [ ] The asmdef reference graph matches `architecture.md` §2. A unit test in `Jolly.Tests.EditMode` loads each asmdef JSON and asserts the allowed references.
- [ ] `unity-architect-reviewer` pass on `Assets/Jolly/Runtime` leaves no open *critical* items.

## 3. Build

```bash
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -buildTarget Android \
  -executeMethod Jolly.Editor.Build.QuestBuild.BuildApk -nubiOut "$PWD/artifacts/Nubi.apk" \
  -logFile "$PWD/artifacts/build.log"; echo "exit=$?"
```

`Jolly.Editor.Build.QuestBuild.BuildApk` already exists in `Assets/Jolly/Editor/Build/QuestBuild.cs`:
- It applies `QuestProjectSetup` (IL2CPP, ARM64, Vulkan, min API 32, `PackageName = "com.nubi.companion"`).
- It calls `BuildPipeline.BuildPlayer`.
- It exits with `EditorApplication.Exit(0|1)` itself, so no `-quit` is needed.
- It accepts `-nubiOut`, `-nubiScenes` and `-nubiDevelopment`.

- [ ] Exit 0. The APK exists. The log has no `error CS` and no warnings from `Jolly.*` assemblies.
- [ ] `adb install -r artifacts/Nubi.apk` succeeds on a Quest 3 (Horizon OS ≥ v74; we use v209). The app launches into passthrough.
- [ ] A localized string and a `LineClips` clip resolve in the player (startup log line `evt=loc.ready table=Lines entries=N`, N > 0): proves Addressables content was built with the player.
- [ ] The permission flow works on a fresh install:
  - camera, scene and mic prompts appear once
  - denying the camera still shows passthrough (the camera manager is enabled after the callback whatever the result), the app runs, and `capture_and_identify` returns `camera_unavailable`
  - denying scene still runs the app; Nubi stays in place and locomotion tools return `unreachable`
- [ ] An app without `secrets.json` runs offline and shows "API key missing".

## 4. Performance (Quest 3, release build, passthrough on, Nubi visible, during a scripted conversation)

Measure with the **OVR Metrics Tool** CSV (`app_rss_MB`, `app_uss_MB`, GPU and CPU utilisation, stale frames; field names per https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/) and the **Unity Profiler** over ADB on a Development build. Store the CSV summary in `docs/perf/`.

| Metric | Target | Source |
|---|---|---|
| Frame rate | ≥ 72 fps sustained (13.9 ms). 90 fps is a stretch goal | Meta VRC minimum (critic §3 row 13) |
| Stale frames | < 1% over 10 min | OVR Metrics |
| CPU main thread | p95 ≤ 11 ms | Profiler |
| Draw calls | ≤ 200 (Meta's busy-scene budget is 200–300) | Profiler, Frame Debugger |
| SetPass calls | ≤ 60 | Profiler |
| Triangles | ≤ 300k scene; Nubi ≤ 30k | Profiler |
| GC alloc | 0 B/frame steady idle; ≤ 4 KB/frame during a voice turn; no GC spikes > 2 ms | Profiler, PlayMode test |
| Memory | `app_rss_MB` ≤ 1,500 MB peak (speculation, critic) | OVR Metrics |
| Thermal | 30-min continuous session (a turn every ~30 s plus 5 captures) with no frame-rate drop below 72 caused by throttling, and no thermal warning dialog | OVR Metrics CSV over time |
| Battery | ≤ 20% drain per 30 min (speculation; record actual) | OVR Metrics / `adb shell dumpsys battery` |

- [ ] All rows met, or the deviation is documented in `docs/perf/report.md` with a mitigation (foveation level, refresh rate, mask overlay cost).

## 5. Latency (on device, Wi-Fi, LiveUser route = `muse-spark-1.3`, 20 consecutive turns)

These are parsed from the `evt=turn.latency` log lines (`adb logcat -s Unity | grep turn.latency`).

| Stage | p50 target | p90 target | Desk evidence |
|---|---|---|---|
| PTT release → acknowledgement clip audible | ≤ 150 ms | ≤ 250 ms | local clip |
| PTT release → ASR final | ≤ 300 ms | ≤ 600 ms | 145 ms live (`meta-model-api.md` §5.2) |
| ASR final → LLM first text delta | ≤ 1,300 ms | ≤ 2,200 ms | 0.98–1.4 s standard minimal |
| First text → first sentence queued | ≤ 150 ms | ≤ 400 ms | splitter |
| Sentence queued → TTS first byte | ≤ 400 ms | ≤ 700 ms | 186–435 ms Wit live |
| **PTT release → first answer audio** | **≤ 2,000 ms** | ≤ 3,000 ms | sum |
| Pinch (barge-in) → silence | ≤ 150 ms | ≤ 250 ms | flush |
| `capture_and_identify` start → mask visible | ≤ 2,000 ms | ≤ 3,000 ms | SAM 0.8–1.5 s (pre-captured frame, no capture wait) |
| `capture_and_identify` start → answer audio | ≤ 2,500 ms | ≤ 4,000 ms | Spark vision `minimal` first text 1.5 s (live 2026-11-01) + first sentence + TTS |
| Network loss → offline canned line | ≤ 2,000 ms | — | |

- [ ] p50 targets met, or the gap and its mitigation (e.g. the 1.2 model for chit-chat) are recorded in an ADR.

## 6. End-to-end demo script (on device)

Hero user: an older adult at home. Hero object: a TV remote (alternatives: moka pot, thermostat; never medication). The video is ≤ 2:45 and shows exactly these six beats; everything else is b-roll or README.

**Required beats (must tier only; 10 consecutive clean runs before recording):**
1. Launch. Nubi appears on the floor, idles, blinks and looks at the user. Pinch-hold: "Hi Nubi, come here." Live partial caption (with "Nubi", not "newbie"), ack sound, the answer starts in ≤ 2 s with lipsync and a caption; Nubi walks around the furniture (hidden behind it if occluders are in) and stops about 1 m in front of the user.
2. Hold up the TV remote, point at it: "What is this and how do I turn on subtitles?" Mask outline on the real object ≤ 2 s (hand-ray point prompt if R1 passed); the answer is spoken as it streams; tool badges show SAM 3.1 and Spark; Nubi looks at the object.
3. "Follow me." The user walks about 3 m and Nubi follows. "Stop." Nubi stops at once (local `StopGuard`).
4. Start a long answer, then pinch mid-sentence. Audio stops ≤ 150 ms, Nubi listens, and a walk in progress continues.
5. Turn Wi-Fi off and pinch: "Are you there?" Nubi plays an offline canned line with lipsync. Idle behaviours continue, and a palm gesture triggers a line. Turn Wi-Fi on: the next turn is online.
6. Check `docs/api-spend.md`: the run's cost is logged, and the usage meter in Settings matches it within 10%.

**Optional beats (should tier; run only if that tier shipped; the video uses at most two):**
- "Which button do I press for the guide?" → `highlight_part` outlines it on the cached frame, answer uses scene memory, Nubi looks at it.
- "Nubi, where are my glasses?" → `find_object` highlights every pair in view, Nubi walks to the nearest.
- "Remember this place as the sofa." Move away, then "Walk to the sofa." (a place inside the scanned room).
- "Switch to Guru." Nubi greets at once in the new voice and style.

- [ ] 10 consecutive runs of the required beats without a crash, hang, or manual restart. Failures are logged in `docs/demo-script.md`.

## 7. Documentation

- [ ] `README.md`:
  - pitch and theme mapping ("Build for Your World", `hackathon.md` §2, official wording quoted)
  - hero user
  - architecture diagram (from `architecture.md`)
  - Meta tooling used (Spark via Responses with tools, SAM 3.1, Muse Voice Transcribe realtime, Wit TTS)
  - quick start (Unity 6000.6.6f1, open `JollyXR/`, `.env` keys, build, `tools/push-secrets.sh`)
  - privacy and data note (Device User Data; `store:false`; standard vs contributor routing)
  - budget and usage meter
  - known limitations (Polish TTS, glasses PCA UNVERIFIED)
  - ADR index
  - test commands
- [ ] `THIRD_PARTY.md`: every dependency with its licence and how it enters the repo (`licenses.md` §10, versions per C1), plus UniTask, uLipSync (commit pin), glTFast, Newtonsoft, the SAM License for the decoder port, sample-image attributions (CC BY 3.0 pen, CC0 mug), and Piper voice licences. Meta SDK licences are stated honestly.
- [ ] `CHANGELOG.md` (Keep a Changelog), with entries per WP under 0.1.0.
- [ ] `docs/decisions/` 0001–0016 are present and none is still "proposed". Any decision changed during the build has a superseding ADR.
- [ ] `docs/api-spend.md` is up to date, and the total is ≤ $140.
- [ ] Demo video ≤ 2:45 (until the portal limit is confirmed), uploaded, link in the README.
