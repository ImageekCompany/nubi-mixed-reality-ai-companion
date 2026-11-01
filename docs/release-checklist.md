# Release checklist

Audit of [`definition-of-done.md`](definition-of-done.md) on `main` at `3f35562`, 2026-11-01 (09:00–09:35 local, UTC+1). Raw outputs are in `artifacts/` (git-ignored).

Legend: ✅ met, with evidence · ⚠️ partial · ❌ not met · 🧑 needs a human (headset wearer, video, portal, or an owner decision).

**Totals: 24 DoD items: 15 ✅, 6 ⚠️, 3 ❌/🧑. Release-audit extras: 4 ✅, 1 🧑.**

## 1. Automated tests

| # | Item | State | Evidence |
|---|---|---|---|
| 1.1 | EditMode exit 0; Core/Net/Providers/Audio covered; each WP's required tests exist | ⚠️ | `tools/ci/test.sh` → `[test] EditMode total=904 passed=904 failed=0 skipped=0 inconclusive=0`, exit 0, 58 s wall for the whole script. Coverage is not measured (no coverage package), so "all logic is covered" is a claim, not a number. |
| 1.2 | PlayMode exit 0 incl. E2E voice turn, barge-in, `capture_and_identify`, offline path, `NoGcAllocInSteadyStateIdle` | ✅ | `[test] PlayMode total=216 passed=216 failed=0`. The tests exist: `VoiceTurn_ListensThinksCallsTheToolAndSpeaksWithCaptions`, `BargeIn_SilencesWithin…`, `CaptureAndIdentify_SegmentsTheStillFrameAndSpeaksTheVisionAnswer`, `Offline_PressPlaysACannedLineWithoutTheCloud`, `NoGcAllocInSteadyStateIdle`. |
| 1.3 | No test hits the network unless `[Category("Live")]` | ✅ | No `Live` tests exist. `grep -rnE "new (UnityWebRequest\|ClientWebSocket\|HttpClient)" Tests` is empty; the three test files that contain URLs assert them against `FakeHttpTransport` / `FakeWebSocket`. |

## 2. Code quality gates

| # | Item | State | Evidence |
|---|---|---|---|
| 2.1 | Zero compiler warnings in `Assets/Jolly/**`; `csc.rsp` with `-warnaserror+` beside each `Jolly.*` asmdef; no unexplained `#pragma warning disable` | ✅ | 19/19 `Jolly*.asmdef` have a sibling `csc.rsp` containing `warnaserror`. `#pragma warning disable` count in `Assets/Jolly`: 0. `test.sh` and `build.sh` grep for `Assets/Jolly/.*: (warning\|error) CS` in all logs: none. |
| 2.2 | No `async void`, no `Task` | ✅ | `test.sh` static gates passed (same regexes as the DoD). |
| 2.3 | No `FindObjectOfType`/`FindFirstObjectByType`/singletons/`Resources.Load` | ✅ | `test.sh` static gate passed. |
| 2.4 | No secret material; `.env`, `secrets.json`, `Meta+Muse+Jolly*` untracked | ✅ | `git grep -nE "LLM_[0-9]+_\|Bearer [A-Za-z0-9]"` hits only synthetic keys in `Tests/` (the DoD line now excludes `Tests/`); empty outside. `git check-ignore` matches `.env`, `StreamingAssets/secrets*.json*`, `Meta+Muse+Jolly*`. See also extra A below. |
| 2.5 | Release builds have no `JOLLY_DEBUG_CAPTURE` / `JOLLY_DEBUG_LOG` | ✅ | `ProjectSettings.asset` has `scriptingDefineSymbols: {}`; the `test.sh` gate passed. `build.log` does not print defines, so the evidence is the settings file the build used, not the log. |
| 2.6 | asmdef graph matches `architecture.md` §2, enforced by a test | ✅ | `Tests/EditMode/Architecture/AssemblyGraphTests.cs`, part of the 904 passing. |
| 2.7 | `unity-architect-reviewer` pass leaves no open critical items | ✅ | Post-merge audit (Unity architecture, security, quality) at `a078010`; every finding is fixed or explicitly deferred in `decisions/review-log.md` "Post-merge audit" and ADR-0022. Not re-run on the two fix commits since. |

## 3. Build

| # | Item | State | Evidence |
|---|---|---|---|
| 3.1 | Exit 0, APK exists, no `error CS`, no `Jolly.*` warnings | ✅ | `tools/ci/build.sh` → `[QuestBuild] Result=Succeeded errors=0 warnings=2`, `artifacts/Nubi.apk` 58 MB, `[build] OK`, 53 s. `error CS` count in `build.log`: 0. The 2 report warnings are not from `Jolly.*` (no `Assets/Jolly` diagnostics). |
| 3.2 | `adb install -r` on Quest 3 succeeds; app launches into passthrough | ⚠️ 🧑 | `selftest.sh` installed and launched it on `2G0YC1ZF7P090N` (Horizon OS v209); the live camera frame was read (`frame=live`, `mean_luma=26.4`). That passthrough is actually *displayed* needs someone to look through the headset. |
| 3.3 | Localized string and a `LineClips` clip resolve in the player | ✅ | Device logcat: `evt=loc.ready table=Lines lang=en entries=33` (also UI 50, Personas 13, Crisis 3); `offline_greeting` PASS plays a canned clip (`caption_ms=13.8`). |
| 3.4 | Permission flow on a fresh install (prompts once; deny camera → passthrough + `camera_unavailable`; deny scene → `unreachable`) | ❌ 🧑 | Not run: the self-test grants all three with `pm grant`. Needs a wearer to answer the system prompts after `adb uninstall com.nubi.companion`. |
| 3.5 | Without `secrets.json` the app runs offline and shows "API key missing" | ⚠️ 🧑 | Device: `secrets.json` moved aside, app launched, ran 20 s with no exception (`evt=warmup.done`), file restored. The label is only on screen; no log line states the status. The label itself is PlayMode-tested (`LatencyHudAndStatusTests`, `StatusKind.ApiKeyMissing`). A wearer must see it once. |

## 4. Performance

| # | Item | State | Evidence |
|---|---|---|---|
| 4.1 | All rows met, or deviations documented with a mitigation | ⚠️ 🧑 | This run (`perf_idle`, 30 s): **72.0 fps**, p95 13.9 ms, draw calls 15, SetPass 12, triangles 29,426, system memory 516 MB, idle GC gate passes. Compositor stale frames over the whole run **1.06 %** (includes the ~320 ms launch hitch; turns-only was 0.55–0.99 % in the perf pass). **Over:** GC per turn 11–42 KB/frame (target ≤ 4 KB), documented in `perf/selftest-report.md` and README limitations without a fix. **Unmeasured:** CPU main-thread p95 (Profiler), `app_rss_MB`, 10-min stale frames, 30-min thermal, battery. These need an OVR Metrics + Profiler session on a development build. |

## 5. Latency

| # | Item | State | Evidence |
|---|---|---|---|
| 5.1 | p50 targets met over 20 consecutive LiveUser turns, or the gap is in an ADR | ⚠️ 🧑 | Perf pass (ADR-0019): release → first audio p50 1.44 s over 16 turns; ASR p50 208 ms; first text p50 0.84 s; barge-in 28–54 ms; mask 0.72–1.13 s; vision answer p50 2.24 s. **This audit run** (4 online turns, release → first audio): 3,150 / 2,221 / 1,550 / 1,915 ms, p50 ≈ 2.07 s, **over** 2.0 s; turn 2's first text took 2.49 s after ASR final. The DoD's 20-consecutive-turn run with a wearer on Wi-Fi has not been done. |

## 6. End-to-end demo

| # | Item | State | Evidence |
|---|---|---|---|
| 6.1 | 10 consecutive clean runs of the six required beats; failures logged in `demo-script.md` | ❌ 🧑 | The rehearsal log in `demo-script.md` is empty. Walking needs a room with Space Setup (`voice_come_here` DEGRADED: `navmesh=no`). |

## 7. Documentation

| # | Item | State | Evidence |
|---|---|---|---|
| 7.1 | README sections (pitch + theme quote, hero user, diagram, Meta tooling, quick start, privacy, budget/meter, limitations, ADR index, test commands) | ✅ | All present. Fixed in this audit: test counts (891/207 → 904/216), the Status "as of" line, "under $1" → "about $1", and the Device User Data sentence that `architecture.md` §11 said the README had. The 42 relative links resolve (extra D). |
| 7.2 | `THIRD_PARTY.md` complete | ✅ | Every non-module package in `Packages/manifest.json` is listed at its exact version; UniTask, uLipSync commit pin, glTFast, Newtonsoft, SAM License (`LICENSE-SAM.txt`), CC BY 3.0 pen, CC0 mug, Wit clip terms, Piper (not shipped), Meta SDKs (not used) are covered. |
| 7.3 | `CHANGELOG.md` with per-WP entries under 0.1.0 | ⚠️ | Keep a Changelog format with WP0–WP11 and the fix sections, but all under `[Unreleased]`; no `0.1.0` section is cut. Cut it at submission. |
| 7.4 | ADRs 0001–0016 present, none "proposed"; changes superseded | ✅ | 0001–0022 present, all `accepted`. ADR-0003 reads "accepted (pending on-device spike R1)": the pipeline half of R1 passed, the extrinsics marker test is still open (see 4.1/3.2). |
| 7.5 | `api-spend.md` up to date, total ≤ $140 | ✅ | Row added for this run ($0.012, app meter). Sum of the rows $0.991; total restated as ≤ $1.00 (it read ≤ $1.02, above the row sum). |
| 7.6 | Demo video ≤ 2:45, uploaded, linked in README | ❌ 🧑 | README line 12: "link added at submission". |

## Release-audit extras

| # | Check | State | Evidence |
|---|---|---|---|
| A | Secret scan: the first 8 characters of each of the 4 `.env` values in `git log -p --all` and the whole working tree (incl. ignored `artifacts/` logs; excluding `.env`, `Library/`, `Temp/`) | ✅ | 0 hits in history, 0 hits in the tree. Only paths would have been printed. |
| B | No `Meta+Muse+Jolly*` file tracked, now or ever | ✅ | `git ls-files` and `git log --all --diff-filter=A --name-only`: none. The `.glb`/`.stl` sit untracked at the repo root, matched by `.gitignore:29`. |
| C | No device capture tracked, now or ever | ✅ | `docs/research/samples/device/` never appears in history; tracked samples are the CC pen/mug images and SAM masks. `QuestVulkan.graphicsstate` is a PSO list, not camera or mic data. |
| D | README relative links resolve | ✅ | 42 relative links (incl. `#anchors` and `<img src>`): 0 broken, every target tracked. |
| E | History free of non-redistributable content before the first push | 🧑 | ADR-0022: the removed Jolly mascot renders (`docs/research/samples/jolly_*.png`) and the macOS `say` clips (from `304fff6`) are still in history. No remote is configured yet; the rewrite is destructive and left to the owner. |

## Human actions remaining

1. **Rewrite history before the first push** (owner): drop `docs/research/samples/jolly_*.png` and the `say` clips from `304fff6` (e.g. `git filter-repo`), then add the remote.
2. **Fresh-install permission flow** (wearer): uninstall, launch, answer camera/scene/mic prompts once; repeat with camera denied (passthrough still shows, `capture_and_identify` → `camera_unavailable`) and scene denied (`unreachable`).
3. **Visual checks** (wearer): passthrough is displayed at launch; "API key missing" appears without `secrets.json`.
4. **Space Setup + walking** (wearer, lit room): `come_here`, follow/stop, and the camera extrinsics marker test (≤ 3 cm at 1 m, ADR-0003 R1).
5. **Perf capture** (wearer): OVR Metrics CSV for 10-min stale frames, `app_rss_MB`, a 30-min thermal/battery session, and a Profiler session for CPU main-thread p95 and per-turn GC; summarise in `perf/selftest-report.md`.
6. **20 consecutive LiveUser turns** on Wi-Fi with a wearer; record p50/p90 or put the gap in an ADR.
7. **10 consecutive clean demo rehearsals**, logged in `demo-script.md`.
8. **Record and upload the demo video** (≤ 2:45), link it in the README, and submit on the portal.
9. **Cut `0.1.0`** in `CHANGELOG.md` at submission.

## Room awareness device checks (ADR-0024, `feat/mr-room-awareness`)

Not run: the headset was owned by another task. Automated cover: EditMode (detector timeout, fallback disc, surface targets, room line, tool argument validation) and PlayMode (fallback disc NavMesh; the real Nubi walking to the user after the area moved 1.5 m and 3 m, following across re-centres, and popping back past 4 m; `go_to_surface` with the real Nubi on a simulated room). Each check below needs a wearer; record the logcat lines (`adb logcat -s Unity | grep "evt=room"`) as evidence.

**A. Unscanned room: fallback area** (erase Space Setup for the room first: Settings → Physical space → Space Setup → clear, or use a new room)
1. Launch with all permissions granted. Within ~8 s logcat shows `evt=room.status status=Unscanned` then `evt=room.degraded walkable=fallback radius_m=1.5`.
2. Look down: Nubi stands on the real floor (feet not sinking or floating). If it floats or sinks, the XR Origin is not in Floor tracking mode or the Quest floor height is wrong; `evt=room.fallback_floor_unavailable` means the origin mode was not Floor.
3. "Nubi, come here": Nubi walks to about 1 m in front of you, faces you, no `unreachable`. With Nubi standing still, step 1.5 m and then 3 m away and repeat: the area stretches from you to Nubi (`evt=room.fallback_floor_moved` at Debug), Nubi walks the whole way to you each time, never `unreachable` and never jumping.
4. "Follow me", walk slowly around a 2 m circle: Nubi follows without freezing and without any teleport; note any visible hitch when the area rebuilds (OVR Metrics stale frames).
5. Say "stop", walk more than 4 m away from Nubi: once, Nubi shrinks away and pops back about 1 m from you on its side (`evt=room.character_returned from_m=…`), then "come here" works.
6. Self-test in the same room: `voice_come_here` reports `PASS navmesh=fallback walk=Arrived`.

**B. Space Setup prompt flow** (same unscanned room, normal launch, not the self-test)
1. Stay idle: once, and only once per launch, Nubi says the `room_scan_01` line with lip-sync and a caption. It must not interrupt a reply in progress.
2. Say "yes, scan my room": the model calls `request_room_scan` (tool badge), Nubi finishes "Okay! Look around your room…", then Space Setup opens (`evt=room.scan_requested accepted=yes`). Nothing cut off mid-word.
3. Complete Space Setup with a couch and a table. On return: `evt=room.requery`, then `evt=room.status status=Scanned` within ~8 s and `evt=room.fallback_floor_cleared`. **UNVERIFIED and most likely to fail:** that restarting the plane and box managers re-reads the new scene model. If the status stays `Unscanned`, relaunch the app and record it; the fix would be an `ARSession.Reset()` or a full app restart prompt.
4. Cancel Space Setup instead: the app resumes, the disc stays, and after ~8 s the status is `Unscanned` again with no second offer.
5. Polish locale: the offer is captioned in Polish and plays the English clip (no Polish TTS).

**C. Walking to the couch** (room with Space Setup, couch and table labelled)
1. "What furniture do you see?": the answer names the couch and table from the context line, without calling a camera tool.
2. "Go to the couch" / "go sit by the sofa": `go_to_surface` (or `walk_to`) with `couch`; Nubi walks around obstacles and stops 0.25–0.5 m from the couch edge nearest to it, facing the couch. Check it is not inside the couch or on the seat.
3. "Go to the desk": resolves to the table. "Go to the bed" with no bed: the model says there is none (`no_surface`).
4. Ask about a seen object by name after `capture_and_identify` ("walk to the mug"): `walk_to` with the label works and Nubi faces it; "look at the couch" turns Nubi's head toward the couch.
5. Box orientation: with a couch at an angle to the room, Nubi still stops beside the long side, not diagonally inside the box. This checks that `ARBoundingBox` pose is the box centre with an up-aligned rotation (assumed, not documented in AR Foundation).
6. Seated mode on: `go_to_surface` is not offered; the model says it cannot walk to furniture while the user is seated.
