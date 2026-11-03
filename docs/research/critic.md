# Research critic: contradictions, blocking gaps, coverage, top risks

Date: 2026-11-01. Inputs: `docs/original-brief.md` and every file in `docs/research/*.md` (`meta-model-api.md`, `xr-stack.md`, `voice.md`, `character.md`, `licenses.md`, `hackathon.md`).
Notation:
- **[NEW]** marks a fact this pass resolved, with its source.
- **UNVERIFIED** means no primary source was found.
- `speculation:` marks my own inference.

Scratch scripts are in a throwaway scratch directory. The API key was loaded from `.env` and never printed.

---

## 0. Verdict

The research is strong on the Meta Model API wire format, PCA via meta-openxr, the asset state and licences. Four things block architecture decisions:
1. **TTS has no verified provider.** Meta has none. Wit needs a token nobody has. The Voice SDK on 6000.6 is untested. Android system TTS is probably absent on Quest.
2. **The data policy for Contributor models conflicts with requirement 7.** ToS §6.2 forbids personal information, which covers camera frames of a home and the user's speech.
3. **The character is both an IP problem and has no rig or mouth.**
4. **Nobody has run anything on the headset yet.**

Three areas have essentially no research: requirements 13 (performance), 21 (definition of done and e2e pipeline) and 24 (scope list). This file fills the parts of them that research can fill.

---

## 1. Contradictions between files

| # | Files | Conflict | Resolution |
|---|---|---|---|
| C1 | `xr-stack.md` §1 vs `licenses.md` §1 | xr-stack pins the editor-bundled set: openxr 1.18.0, meta-openxr 2.6.1, ARF 6.6.2, hands 1.9.0, XRI 3.6.1, management 4.7.0. Licenses audited a newer set: 1.19.0, 2.7.0, 6.7.1, 1.10.0, 3.7.0, and **management 4.6.1**, which meta-openxr cannot use because it requires 4.7.0. | Use the xr-stack pin. Licence terms are the same within each package family (UCL/UPDL), so the audit still holds. Fix the version numbers in THIRD_PARTY. |
| C2 | `meta-model-api.md` §8.2, `hackathon.md` §3.5, req 7 vs `licenses.md` §3 | The API and hackathon files say to use `muse-spark-1.3-contributor` for chat and vision, and to avoid it only for therapy content. Licenses quotes ToS §6.2: you "must not submit sensitive, confidential, or personal information to the Contributor Services". Passthrough frames of a home and voice transcripts are personal data. | **Needs a human decision and an ADR.** Proposal: Standard `muse-spark-1.3` for live user turns (camera frames and speech). Contributor for CI, synthetic tests and dev loops, which is where req 7 still saves cost. Standard is also faster (§1.3 table) and allows 3,000 RPM instead of 150. Cost per demo turn at $1.25/$4.25 per M tokens is about $0.003 (speculation: ~1k input and ~300 output tokens). |
| C3 | `licenses.md` §9 vs `hackathon.md` §1 | Licenses says the Official Rules "are not publicly reachable". Hackathon extracted them from the `/waiver` page payload and quotes them. | `hackathon.md` is correct. Delete the stale paragraph from `licenses.md`. |
| C4 | `hackathon.md` §3 (barge-in row) | It says to cancel the in-flight reply with `POST /v1/responses/{id}/cancel`. | **[NEW] Wrong.** The docs say "Only responses created for background processing can be cancelled" (https://dev.meta.ai/docs/api-reference/responses/cancel-response.md). For a streamed turn, barge-in means aborting the HTTP request (`UnityWebRequest.Abort()` through the turn's `CancellationToken`). |
| C5 | `hackathon.md` §3.3 vs `meta-model-api.md` §5.1 | Hackathon suggests Muse ASR `keywords` biasing as a wake word. Live tests found that biasing *duplicated* "Jolly" ("Hey Jolly, Jolly…"), and biasing is not keyword spotting anyway. | No wake word through ASR biasing. Use pinch push-to-talk (`voice.md` §0). A wake word is *could*-tier, via sherpa KWS. |
| C6 | `voice.md` §5 vs `character.md` §2.2.7 | Voice says to drive "one jaw-bone rotation plus O/U and I/E blendshapes". Character says a jaw bone is a poor fit because the face plate is rigid with no jaw, and recommends a separate mouth mesh with 6–8 shape keys or a sprite-swap mouth. | `character.md` wins. Feed uLipSync's A/I/U/E/O/N weights into the mouth shape keys. |
| C7 | `character.md` §2 | It sets a Quest budget of "≤30–50k tris" and calls 197k "4–6× over". That number has no source. | **[NEW]** Meta's Unity perf page gives Quest 3 ranges of **1.3M–1.8M triangles** per frame and **200–300 / 400–600 / 700–1000 draw calls** for busy / medium / light scenes (https://developers.meta.com/horizon/documentation/unity/unity-perf/). 197k is not a frame-budget blocker. Still decimate to about 25–30k, because GPU skinning cost and a clean rig matter more than the triangle count. |
| C8 | `licenses.md` §6 vs `voice.md` §4 | Licenses lists espeak-ng in prebuilt sherpa-onnx as UNVERIFIED. Voice cites sherpa-onnx **v2.0.0, #4055: "Remove espeak-ng and piper-phonemize"**. | Resolved by `voice.md`: v2.x has no espeak, while v1.13.8 and earlier link GPL espeak-ng. Pin ≥2.0.1 if sherpa is ever used. |
| C9 | `voice.md` §8 vs `meta-model-api.md` §1.4 | Voice says "the docs say `none` returns 400". In fact the docs *list* `none`, and the live call returned 400. Minor. | Use `minimal`. |
| C10 | `voice.md` §1–2 vs `licenses.md` §10 and req 25 | Voice recommends `TTSWit` from the Voice SDK. Licenses says to avoid the Meta SDKs. The MPT SDK License §1.1 limits use to "MPT approved hardware", which conflicts with the Pico and Android XR targets. | Use a raw `/synthesize` client behind `ITextToSpeech` (voice §2 already sketches it). The Wit service ToS still applies, but no SDK is linked. Keep the Voice SDK only as a fallback if the raw client stalls. |
| C11 | `xr-stack.md` §2.4 | It names `WebCamTexture` as the clean PCA fallback. | **[NEW]** Meta now publishes "Migration from WebCamTexture to PassthroughCameraAccess" (MRUK v81+). It cites timestamps, simultaneous left and right cameras, a single `HEADSET_CAMERA` permission, and extrinsics in one place. It also says PCA can be previewed **over Meta Horizon Link v2.1+**. Source: https://developers.meta.com/vr/documentation/unity/unity-pca-migration-from-webcamtexture/. By contrast, meta-openxr gives no CPU images in the Editor or over Link (xr-stack §2.2). The trade is iteration speed (MRUK) against licence and portability (meta-openxr). Keep meta-openxr as primary and decide in an ADR. |
| C12 | `meta-model-api.md` (Chat Completions) vs `hackathon.md` §3.4 (Responses for multi-turn memory) | The protocol choice is open. | **[NEW] live:** Responses streaming with `store:false` and `reasoning.effort:"minimal"` works. TTFT was 1,701 and 3,198 ms on 1.3-contributor and 1,424 and 1,874 ms on 1.3 standard (n=2, noisy; `resp_ttft.py`). The docs say Responses "is the only endpoint that carries the model's chain of thought between calls" and recommend stateless encrypted replay (`store:false` plus `include:["reasoning.encrypted_content"]`) (https://dev.meta.ai/docs/protocols/responses.md). `store` **defaults to true**, which is a privacy default to override. Responses also emits `response.created` before reasoning, which gives an early liveness signal that Chat Completions lacks. Recommendation: **Responses API with `store:false`** for the agent loop, behind `IChatModel`. Test the latency claim with more runs. |
| C13 | `hackathon.md` §3 video script vs `meta-model-api.md` §8.3 and `voice.md` §8 | The script shows a "first audio under 2 s" stopwatch. Both technical files say this cannot be reached with Contributor plus reasoning. | Either use the Standard model for voice turns (C2) and a pre-cached acknowledgement, or drop the stopwatch from the script. |
| C14 | `xr-stack.md` §5 vs `voice.md` §6 | xr-stack lists the Quest native mic rate as UNVERIFIED. Voice cites Meta's platform-audio page: default mic 48 kHz mono. | Partly resolved. Still read `Microphone.GetDeviceCaps` on device and resample to 16 or 24 kHz. |
| C15 | `hackathon.md` §1 | The live final is top 5 per the spec and the prize table, but top 10 per a participant repo. | UNVERIFIED. Check the portal or Discord. Either way, plan for a live demo. |

---

## 2. UNVERIFIED or uncited claims that block architecture decisions

| # | Claim | Blocks | How to close |
|---|---|---|---|
| B1 | **TTS provider.** Wit `/synthesize` latency, framing and voices are unmeasured because there is no Wit token. Polish voices are unknown. Voice SDK 85.0.1 on 6000.6 is untested. **[NEW]** Android system TTS on Quest is probably absent: forum reports say `getEngines()` returns 0 on Quest 2 and that RT-Voice fails on Quest 3 (https://communityforums.atmeta.com/quest-development-128/text-to-speech-tts-options-on-quest-2-106933, https://communityforums.atmeta.com/discussions/dev-quest/oculus-quest-text-to-speech/780622, https://assetstore.unity.com/packages/tools/audio/rt-voice-pro-41068/reviews). These are old or secondary sources, so it remains UNVERIFIED. The Wit ToS page loads (https://wit.ai/terms, effective 2023-12-22), but nobody has read it in full. | `ITextToSpeech`, the lipsync source, the latency budget and req 18 for Polish | **Human:** create the Wit app and token today. Spike R2 below. |
| B2 | `System.Net.WebSockets.ClientWebSocket` under IL2CPP on Quest. | `MuseRealtimeStt` | Spike R4. Fallback: the file and SSE endpoint `POST /v1/asr/transcribe` with `Accept: text/event-stream`, which is verified live (`meta-model-api.md` §5.1). [NEW] NativeWebSocket is Apache-2.0 (`LICENSE` in endel/NativeWebSocket). speculation: it wraps `ClientWebSocket` on native platforms, so it is not an independent fallback. |
| B3 | PCA details: delivered resolution on v209, the mirror/flip flag, the frame convention for Camera2 `LENS_POSE_*` versus the OpenXR centre-eye pose, and the clock domain of `XRCpuImage.timestamp`. | The pixel↔world mapping for "look at this", the SAM point prompt from the hand ray, and mask overlay placement | Spike R1. |
| B4 | Quest AEC on Unity `Microphone`, and reports of mic freezes on standalone Quest. | Whether voice barge-in is possible, and hands-free versus PTT as the default | Spike R5. Pinch barge-in is already the safe default. |
| B5 | Data policy for Contributor (C2). | Model routing and the privacy section of the README | Human decision plus an ADR. |
| B6 | Jolly IP: provenance is unknown, the character is Meta's, and the rules' warranty (c) covers trademark (`hackathon.md` §1). | The character pipeline and the demo video | Human decision: an original "Jolly-like" character in the repo (recommended), with the real Jolly asset kept local-only and git-ignored. |
| B7 | The official problem statements and the submission fields and video limit. | The README theme mapping (Impact is 30%) | Human: check the portal and Discord. |
| B8 | Key provisioning on the device. Not researched. ToS §3.1 forbids sharing the key, and an APK-embedded key is extractable. | Req 17 | Proposal: no key in the build. Read `persistentDataPath/secrets.json`, pushed with `adb push` by the developer, or entered once in a headset settings panel. Document a proxy as the production path. Needs an ADR. |
| B9 | PCA on Meta VR Glasses. **[NEW]** The PCA overview names Quest 3 and 3S only. The glasses page says you "can test on Meta Quest 3 or 3S prior to Meta VR Glasses availability" and does not mention PCA (https://developers.meta.com/horizon/essentials/get-started-with-glasses/). | Positioning glasses as the main target | Keep `ICameraFrameSource` behind an interface. Pitch the glasses as the roadmap, not as the demo device. |
| B10 | Whether multi-room Scene Models are exposed through meta-openxr. | "Walk to the kitchen" | Taught anchors (`xr-stack.md` §4.4). Demo inside one scanned room. |
| B11 | **[NEW]** Unity cannot import `.glb` without a package. `com.unity.cloud.gltfast` **6.14.1** is listed in the editor manifest (`PackageManager/Editor/manifest.json`, `mustBeBundled:false`). Draco support was left UNVERIFIED in `character.md`. | The character import path | Use glTFast 6.14.1 and export without Draco. Alternatively, export FBX from Blender (no extra package). |
| B12 | **[NEW]** UniTask latest is **2.5.11** (2026-05-19), MIT, `unity: 2018.4` minimum (`curl https://package.openupm.com/com.cysharp.unitask`; GitHub `license: MIT`). Unity 6.6 compatibility is UNVERIFIED but routine. | Req 3 | Install it in the scaffold and run an IL2CPP build once. |

---

## 3. Requirement coverage (25 items)

| # | Requirement | Status | Where it is covered, and what is missing |
|---|---|---|---|
| 1 | Unity 6.6 | COVERED | xr-stack §1: editor 6000.6.6f1 and its bundled packages. |
| 2 | Open source, public plugins only | COVERED | licenses §10–11. Gap: the Jolly asset (B6). |
| 3 | Quality: architecture, UniTask, memory, testability, **accessibility** | PARTIAL | Interfaces are sketched in voice §9 and the API files. UniTask version is in B12. Missing: **accessibility**, meaning captions for Jolly's speech, a seated mode, one-handed and hands-only operation, comfort settings and text size. Also missing: DI choice (VContainer appears only in licenses), assembly layout, and the test strategy. Plan: XR Hands plus captions from the LLM text, which is already streamed. Use Unity Test Framework 1.8.0 (editor manifest). |
| 4 | Meta AI talk plus SAM, swappable providers | COVERED | meta-model-api §1–5, §7. Offline options are LAN Glimmer or canned lines. Note: SAM segments; it does not recognise (hackathon §3). |
| 5 | Walk, talk, animations, lipsync | PARTIAL | character §2.2–2.3 and voice §5. Missing: the Animator state machine design, the idle/talk/listen/think gesture set, blink timing, and NavMeshAgent-to-procedural-gait integration. No asset exists yet. |
| 6 | Parallelism | N/A (process) | |
| 7 | Contributor models for cost | PARTIAL / CONFLICT | Measured in meta-model-api §6. Conflicts with ToS §6.2 for personal data (C2). |
| 8 | Git, conventional commits, ADRs, CHANGELOG | MISSING (process) | No research needed. Repo is not yet initialised (`hackathon.md` §1). `.gitignore` must exclude Jolly files before the first commit (character §3). |
| 9 | Remake Jolly if needed | COVERED | character §2.2 and §4: Blender 5.1 scripted pipeline, APIs checked. |
| 10 | Licences and THIRD_PARTY | COVERED | licenses.md. Fix versions (C1), add glTFast and UniTask, and add the SAM License if the mask decoder is ported (meta-model-api §4.5). Sample images are CC BY 3.0 and CC0 (meta-model-api §9). |
| 11 | PCA for SAM: permissions, minimum OS, spike first | COVERED (desk) / UNVERIFIED (device) | xr-stack §2. Device spike R1 is not yet run. |
| 12 | Hand tracking 100%, controllers optional | COVERED | xr-stack §5: XR Hands, Hand Interaction Profile, XRI modality manager, palm-up and pinch gestures. |
| 13 | Performance: 72/90 fps, thermals, battery, draw calls, memory, profiling | **PARTIAL (was MISSING)** | xr-stack §6 covers settings (foveation, SpaceWarp, refresh rate). **[NEW] budgets:** 72 fps is the VRC minimum (13.9 ms), 90 fps is 11.1 ms (https://developers.meta.com/horizon/documentation/native/android/po-perf-opt-mobile/). Quest 3 allows 200–600 draw calls and 1.3–1.8M triangles (unity-perf page, C7). OVR Metrics Tool reports `app_rss_MB`, `app_uss_MB`, GPU and CPU utilisation, and stale frames (https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/). Still missing: thermal and battery targets and a memory ceiling. Proposal: RSS < 1.5 GB (speculation), a 30-minute session without thermal throttling, recorded with OVR Metrics CSV. The camera stream costs about 1–2% GPU and 45 MB (xr-stack §2.1). |
| 14 | Voice loop: streaming, barge-in, under 2 s | COVERED with a risk | voice §8 and meta-model-api §5, §8. Under 2 s needs the Standard model and an acknowledgement clip (C13). Barge-in is by abort, not by `/cancel` (C4). |
| 15 | Tool calling | COVERED | meta-model-api §2: streaming deltas, parallel calls, `tool_choice` accepts only `auto`, client-side argument validation. Missing: the final tool catalogue (`walk_to`, `follow_user`, `look_at`, `capture_and_identify`, `remember_place`, `set_persona`) and the Responses-API shape (`function_call` and `function_call_output`) if C12 is adopted. |
| 16 | Memory, persona assets, scene context | PARTIAL | Persona as a ScriptableObject is mentioned, as are prompt caching and Responses replay (C12). Missing: the memory design (rolling summary, local persistence, the "seen objects" store with SAM box, world anchor and label), prompt-size limits, and Psychologist safety text (licenses §3). |
| 17 | Secrets local-only | PARTIAL | `.env` is git-ignored and ToS §3.1 applies. The on-device provisioning path is missing (B8). |
| 18 | Localization: strings, STT, TTS | PARTIAL | voice §7: Localization 1.5.13, Muse ASR `languageBias` Polish verified live. Polish TTS is UNVERIFIED (B1). |
| 19 | Default Unity solutions | COVERED | Unity packages are preferred throughout: ARF, XRI, Hands, NavMesh, Animation Rigging, Localization, Test Framework. |
| 20 | Don't invent APIs | COVERED | All files cite source files or URLs. Open items are listed in §2. |
| 21 | Objective "done", tests, e2e | **MISSING** | Nothing covers batch-mode test runs, CI, a device test harness, or a scripted demo replay. Proposal: EditMode tests (NUnit plus NSubstitute) for the parsers (SSE, SAM grammar, mask decoder, tool-call accumulator) using fixtures from `docs/research/samples`. A PlayMode e2e test with fake providers (`ICameraFrameSource` as a static JPEG, recorded ASR events, scripted LLM SSE). One live smoke test per provider, gated by an environment key. A device checklist with OVR Metrics. Run with `Unity -batchmode -runTests -testPlatform EditMode` (UNVERIFIED flags for 6000.6; check `Unity -help`). |
| 22 | Report format | N/A (process) | |
| 23 | Offline: alive with canned lines and idle behaviour | PARTIAL | voice §4: pre-baked Piper `en_US-joe` and `pl_PL-gosia` (CC0) clips plus baked uLipSync data. Missing: an offline detector (`Application.internetReachability` is not sufficient; use a health ping), the interaction triggers for lines (wave, gaze, poke), and an idle scheduler. |
| 24 | Must/should/could list | **MISSING** | Draft below (§5). |
| 25 | Portability (OpenXR) | COVERED with caveats | The xr-stack is OpenXR-based. Caveats: meta-openxr, MRUK and the Voice SDK are Meta-only (C10, C11), and PCA is Quest 3/3S only (B9). Android XR OpenXR 1.4.0 is bundled. |

Totals: 12 COVERED (2 with caveats), 9 PARTIAL (one also in conflict), 2 MISSING, 2 N/A.

---

## 4. Top 5 technical risks and spikes

**R1. Passthrough frame → SAM → mask anchored in the world (the hero shot).**
- **Unknowns:** permission timing, resolution, flip, extrinsics frame, latency.
- **Spike (half a day, on device):**
  1. Build a scene with `ARCameraManager` disabled.
  2. Batch-request `HEADSET_CAMERA`, `USE_SCENE` and `RECORD_AUDIO`, then enable the camera manager on grant.
  3. Call `TryAcquireLatestCpuImage` → `ConvertAsync` → downscale to 640 px → JPEG.
  4. Send it to SAM `one_bit` with a point prompt projected from the index-finger aim ray.
  5. Decode the mask with a C# port of the decoder.
  6. Raycast the mask centroid with `ARRaycastManager` (Depth) and place a sphere.
- **Pass:** the sphere lands within 3 cm of a printed marker at 1 m, and capture-to-mask takes under 2 s.
- Log `image.dimensions`, `format` and timestamp.
- If the extrinsics fail, A/B test against MRUK `PassthroughCameraAccess` on a throwaway branch. MRUK also previews over Link (C11).

**R2. No TTS (B1), so lipsync and voice are blocked.**
- **Human prerequisite:** create a Wit app and token.
- **Spike (2–3 h):**
  1. A raw `UnityWebRequest` POST to `https://api.wit.ai/synthesize?v=20250213` with `Accept: audio/raw`.
  2. A `DownloadHandlerScript` streaming into a ring-buffered `AudioClip`.
  3. Measure time to first byte for a 100-character sentence, and fetch `GET /voices` to answer the Polish question.
  4. Run it as an IL2CPP Quest build.
- **Pass:** first audio within 500 ms after the request.
- **Fallback, prepared in parallel:** the baked-clip path for scripted demo lines. speculation: if Wit fails, run Piper on the LAN host behind `ITextToSpeech`, since the desktop is not shipped.

**R3. Character: IP plus no rig, mouth or UVs.**
- **Human prerequisite:** decide on an original character.
- **Spike (half a day):** run `tools/blender/build_jolly.py` on an original sculpt (or a placeholder from `character.md` §2.2) to produce a GLB with:
  - a 15-bone rig,
  - a mouth mesh with 6 shape keys,
  - blink shape keys.
- Then:
  1. Import it with glTFast 6.14.1 (B11).
  2. Drive the mouth with uLipSync on a test clip.
  3. Drive the walk with NavMeshAgent plus a procedural TwoBoneIK gait (Animation Rigging 6.6.0).
- **Pass:** 72 fps on Quest 3 with Jolly on screen and passthrough on, measured with OVR Metrics. The mouth must read correctly on camera.

**R4. Voice-loop latency and transport on device.**
- **Spike (half a day):** an on-device harness measures, per stage:
  1. Mic → `ClientWebSocket` to `wss://api.meta.ai/v1/asr/realtime` (B2), PUSH_TO_TALK at 24 kHz.
  2. The final transcript → Responses API with `store:false`, streamed.
  3. A sentence splitter → TTS first audio.
- Log timestamps per stage across 20 turns for each of 1.3-contributor, 1.3 standard and 1.2-contributor.
- **Pass:** p50 under 2.0 s from PTT release to first Jolly audio, not counting the acknowledgement clip. This result decides the ADR on model routing (C2, C13).
- If `ClientWebSocket` fails under IL2CPP, switch to the SSE file endpoint.

**R5. Live-demo robustness: microphone, echo, network.**
- **Spike (2 h, on device):**
  1. A 15-minute mic soak test: does capture freeze (B4)?
  2. Play TTS through Jolly's spatialised `AudioSource` while running playback-gated VAD, and count false barge-ins.
  3. Toggle Wi-Fi mid-turn and check that the offline fallback lines start within 2 s and the app never hard-locks.
  4. Hit `429` on purpose with 160 RPM against the contributor model and verify backoff.
- **Pass:** zero mic freezes, at most 1 false barge-in per 10 Jolly utterances (otherwise disable voice barge-in and keep pinch), and clean offline recovery.

Not technical, but equal in weight: B6 (Jolly IP) and B7 (problem statements and submission fields). Both are human actions due today.

---

## 5. Draft must / should / could list (req 24)

| Tier | Feature | Wow | Risk |
|---|---|---|---|
| Must | MR scene with hand tracking, Jolly idle, look-at and blink | med | low |
| Must | PTT pinch → Muse ASR → Spark (streaming) → TTS with lipsync and captions | high | med (R2, R4) |
| Must | `capture_and_identify`: PCA frame → Spark vision answer plus SAM mask overlay | **very high** | high (R1) |
| Must | Tool calls `come_here`/`follow_user` and `look_at` on the NavMesh of the scanned room | high | med |
| Must | Offline canned lines and idle behaviour; persona ScriptableObjects (Mentor, Guru) | med | low |
| Must | README with theme mapping, THIRD_PARTY, ADRs, tests, demo video | — | low |
| Should | Hand-ray point prompt for SAM (the "unexpected capability" angle) | high | med |
| Should | Pinch barge-in; acknowledgement clips; latency overlay | med | low |
| Should | Scene memory of seen objects ("which part gets the water?") | high | med |
| Should | Taught room anchors ("this is the kitchen") → `walk_to` | med | med |
| Could | Voice barge-in with gated VAD, wake word through sherpa KWS | med | high |
| Could | Polish locale end to end | low (for judges) | med (Polish TTS) |
| Could | Muse Image persona props; LAN Glimmer offline brain | med | med |

---

## 6. Sources added in this pass
- Cancel endpoint scope: `curl https://dev.meta.ai/docs/api-reference/responses/cancel-response.md`
- Responses state and `store` default: `curl https://dev.meta.ai/docs/protocols/responses.md` (lines about `previous_response_id`, `store: true` default, encrypted replay)
- Responses streaming TTFT (live): scratchpad `resp_ttft.py`
- Quest 3 draw-call and triangle ranges: https://developers.meta.com/horizon/documentation/unity/unity-perf/
- Frame budgets: https://developers.meta.com/horizon/documentation/native/android/po-perf-opt-mobile/
- OVR Metrics fields: https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/
- WebCamTexture → PassthroughCameraAccess migration: https://developers.meta.com/vr/documentation/unity/unity-pca-migration-from-webcamtexture/
- Glasses page: https://developers.meta.com/horizon/essentials/get-started-with-glasses/
- glTFast 6.14.1 and Test Framework 1.8.0 in the editor: `/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/Resources/PackageManager/Editor/manifest.json`
- UniTask 2.5.11: `curl https://package.openupm.com/com.cysharp.unitask`; `gh api repos/Cysharp/UniTask`
- NativeWebSocket Apache-2.0: `curl https://raw.githubusercontent.com/endel/NativeWebSocket/master/LICENSE`
- Quest system TTS (secondary sources): the community forum and Asset Store links in B1
- Wit ToS: https://wit.ai/terms (fetched, only partly read)
