# Nubi

**A mixed-reality companion for Meta Quest 3 that walks around your real room, sees what you hold up, outlines it, and talks you through using it. Powered by Muse Spark, SAM 3.1, Muse Voice Transcribe and Wit.ai.**

[![License: MIT](https://img.shields.io/badge/license-MIT-2ea44f)](LICENSE)
![Track: Indie](https://img.shields.io/badge/track-Indie-5AA9FF)
![Theme: Build for Your World](https://img.shields.io/badge/theme-Build%20for%20Your%20World-5AA9FF)
![Unity 6.6 + OpenXR](https://img.shields.io/badge/Unity%206.6-OpenXR-555)
![Meta Quest 3](https://img.shields.io/badge/Meta-Quest%203-555)

<p align="center">
  <img src="docs/art/nubi/nubi_three_quarter.png" alt="Nubi, a small coral seedling-bean character with a two-leaf sprout, three-quarter view" width="260">
  <img src="docs/art/nubi/nubi_clip_wave.png" alt="Nubi waving" width="260">
</p>

<!-- VIDEO: replace the next line with: > ▶️ **[Watch the demo video](https://YOUR-LINK)** -->
> ▶️ **Demo video:** https://youtu.be/cAkDgTrN9zI

> **The character in the video is Jolly, the code is the same.** The video shows a look-alike of Meta's Muse mascot, which we built as a local, non-commercial demo skin for this hackathon. We found no public licence for the mascot, so its model and everything built from it stay out of this repository and its history, and no public build contains them. What you clone ships **Nubi**, our original CC0 character. Characters are data with a shared skeleton, so `tools/device/build.sh` builds Nubi and `--character jolly` picks a local skin ([ADR-0052](docs/decisions/0052-demo-video-shows-jolly-repository-stays-nubi.md)). This project is not affiliated with or endorsed by Meta.

| | |
|---|---|
| **Track · theme** | Indie · Build for Your World |
| **Who it helps** | People who get stuck on everyday physical tasks with nobody in the room to ask; hero user: an older adult living alone |
| **Meta AI used** | `muse-spark-1.3` (agent, vision, built-in web search) · `sam-3.1` · `muse-voice-transcribe-1.0` · Wit.ai speech |
| **Runs on** | Meta Quest 3 (tested on Horizon OS v209), Unity 6000.6.6f1, OpenXR, IL2CPP |
| **Proof** | 1,578 EditMode and 577 PlayMode tests, 93.6 % logic coverage · self-test of the real pipeline on the headset · [49 decision records](docs/decisions/README.md) |
| **Creators** | Andrei Urbanovich and Eugene Basalai |
| **License** | [MIT](LICENSE), with third-party terms in [`THIRD_PARTY.md`](THIRD_PARTY.md) |

## The problem

In the US, [26 % of adults 65 and older live alone](https://www.pewresearch.org/short-reads/2025/12/04/a-smaller-share-of-older-us-adults-live-alone-today-than-in-1990/), and 38 % of those 85 and older. An unfamiliar remote, a new coffee machine or a label they can't read can stop them cold. Today's help is a person who isn't in the room, or a phone chatbot that can't see what they're holding and can't show them anything.

Nubi lives in the room. You hold the thing up and ask. It outlines that exact object in your view, says what it is, and walks you through it. It also comes when called, follows you, and keeps talking when the Wi-Fi drops. Learners, new workers and low-vision users get the same help ([more](docs/deep-dive.md#a-day-with-it)).

**Theme fit: Build for Your World.** Meta's models run against your own room, objects and voice. You don't search for a manual, you hold the thing up. And a language model gets something a chat window can't give it: a body that walks your scanned floor, and a pointing hand as SAM's prompt.

## What it does

| # | Say or do | What happens | Meta AI involved |
|---|---|---|---|
| 1 | "Nubi, come here." | It walks around your furniture and stops in front of you, with no model wait | Muse Voice Transcribe; local guard |
| 2 | Hold up a remote: "What is this, and which button turns it on?" | An outline lands on the real remote, the answer streams as speech, then the power button gets its own outline | Spark, SAM 3.1, Wit.ai |
| 3 | "Where can I put these things?" | It names them, walks toward the kitchen you showed it earlier, and points: "There!" | Spark vision, SAM 3.1 |
| 4 | "What's the weather today?" | A live answer for your city, spoken without links or citations | Spark web search |
| 5 | "Follow me." … "Stop." | It follows you and stops at once; "stop" is matched on the device | Local guard |
| 6 | Pinch while it talks | Silence in 27–55 ms, and it listens | Cancels the running Meta calls |
| 7 | Wi-Fi off: "Are you there?" | A baked, lip-synced offline line; idle behaviour carries on | None, by design |

Also built: dance, jump and bow on command, point and tap to send it across the floor, furniture from Space Setup, four personas as data assets, usage meter with a hard cap. See the [full list](docs/deep-dive.md#features-beyond-the-main-demo) and [controls](docs/deep-dive.md#controls).

## How Meta's models are used

![One voice turn: pinch and speak; Muse Voice Transcribe transcribes over a realtime WebSocket; on-device guards handle stop, crisis and short commands with no model call; Muse Spark 1.3 runs the agent loop on the Responses API and calls SAM 3.1, Spark vision, built-in web search and body and memory tools; Wit.ai turns its words into speech; the character speaks, lip-syncs and walks in your real room.](docs/diagrams/voice-turn.svg)

| Model | Exact use | Why it goes beyond a basic integration | Code |
|---|---|---|---|
| **Muse Spark 1.3** `muse-spark-1.3` | Streamed `POST /v1/responses`, `store:false`, `reasoning.effort:"minimal"`, up to 4 rounds per turn | Tools start **while the model is still streaming**. Its `commentary` preamble is spoken before tools finish. `reasoning.encrypted_content` is replayed verbatim between rounds. A silent request is raced by an identical hedge after 2.5 s. | `Providers/MetaApi/Chat`, `Core/Agent` |
| **Spark as a body controller** | 28 function tools: `come_here`, `walk_to`, `show_place`, `look_at` and more, plus a namespaced `actions.*` set | A language model drives a character on your scanned floor, and walks never hold a model round | `Core/Tools/Locomotion`, `Core/Tools/Actions`, `Character` |
| **Spark vision** | The same frame, in parallel with SAM; streams a first line `LABEL\|NOUN_PHRASE\|PLACE`, then the spoken answer | Spark **drives SAM and the body**: its noun phrase retries a bad mask, and a remembered place it names makes the character walk there and point | `SparkObjectIdentifier`, `CaptureAndIdentifyTool` |
| **SAM 3.1** `sam-3.1` | Point from the hand ray projected into the camera image, **negative points from hand joints**, or a text prompt | Your hand is the prompt. Three prompt types on one object. `one_bit` masks are decoded by a C# port of Meta's range decoder, bit-exact against live masks. SAM segments; Spark explains | `Providers/MetaApi/Vision`, `Core/Tools/Vision` |
| **Web search** (built into Spark) | `web_search` with `search_context_size:"low"` and a city-level location | Offered only when online, gated per persona; citations are stripped before speech and captions | `ResponsesRequestWriter`, `WebSearchPolicy` |
| **Muse Voice Transcribe** `muse-voice-transcribe-1.0` | `wss://api.meta.ai/v1/asr/realtime`; the socket opens at pinch start | Runs on the BCL WebSocket under IL2CPP on Quest. Live captions as you talk; final transcript p50 265 ms after release | `Providers/MetaApi/Asr`, `Net/WebSockets` |
| **Wit.ai TTS** | `/synthesize` over raw HTTP, 24 kHz PCM, sentence by sentence | The Model API has no TTS. Streams into a clip-less audio source, and uLipSync drives the mouth from the same samples | `Providers/Wit`, `Audio` |

Paths are under `JollyXR/Assets/Jolly/Runtime/`. Contributor models serve **only** synthetic dev traffic: `MetaModelResolver` throws if one is used on a live-user route, because the Meta ToS §6.2 forbids personal data on them.

![The hero turn. One passthrough frame and the pointing hand go to SAM 3.1 and Spark vision in parallel. SAM takes a point and negative points and returns a one-bit mask, which appears as an outline on the real object. Spark vision streams a header and the spoken answer through Wit.ai. A bad mask is retried once with Spark's noun phrase, and a remembered place makes the character walk there and point. Follow-ups use SAM text prompts on the same frame: highlight_part and find_object.](docs/diagrams/what-is-this.svg)

## Architecture

Ports and adapters in 13 runtime assemblies. Pure logic knows nothing about XR, Meta or scenes, so **any provider can be swapped** for an on-device model, which is the path to a fully private mode.

- **`Jolly.Contracts`** holds the interfaces: `IChatModel`, `ISpeechToText`, `ITextToSpeech`, `ISegmenter`, `IObjectIdentifier` and others ([contracts](docs/contracts.md)).
- **`Jolly.Core`** holds the agent loop, tools, memory and conversation state machine, and references only Contracts and UniTask. An EditMode test fails on any undeclared assembly reference.
- **Adapters** isolate each vendor. `Jolly.XR` is portable OpenXR, and only `Jolly.XR.MetaQuest` names a Meta OpenXR type at runtime, so Android XR or Pico needs one new adapter.
- **One composition root**, no DI container, no singletons, UniTask everywhere, and one cancellation tree: a pinch cancels the turn but never the walk.

Full design: [architecture](docs/architecture.md), [decision records](docs/decisions/README.md), [engineering notes](docs/deep-dive.md#technical-depth).

## Results

| What | Target | Measured | When and how |
|---|---|---|---|
| Automated tests | all pass | **EditMode 1578/1578, PlayMode 576/577** (one explicit live eval skipped; 14 skipped on a clone, which lacks the local skin), no compiler warnings | `tools/ci/test.sh`, Nov 3 |
| Line coverage of pure logic | ≥ 91.6 % gate | **93.6 %** (85.3 % of all runtime code) | same run |
| Frame rate with passthrough | ≥ 72 fps | **72.0 fps**, p95 frame 13.9 ms | headset, Nov 3 |
| Render load, idle | ≤ 200 draws, ≤ 300k tris | **7 draws, 25.7k tris** (Nubi) | headset, Nov 3 |
| Pinch to silence | ≤ 150 ms | **27–55 ms** | headset, several runs |
| Short command to first audio | | **0.3–0.45 s**, no model call | headset, Nov 3 |
| Vision tool to mask visible | ≤ 2 s | **0.43–1.35 s** | headset, Nov 1–3 |
| Release to first audio, model turn | p50 ≤ 2.0 s | **Not held in the latest runs.** p50 1.8 s over 20 turns on Nov 1; 2.7–6.0 s on Nov 3, nearly all of it model time | headset, see below |
| API cost of a day | | **about $0.18** for 30 voice turns, 5 of them vision ([working](docs/deep-dive.md#living-with-it)) | $20 hard cap per headset |

The headset self-test runs the real pipeline (real ASR, Spark, SAM, Wit, speaker) through 13 scenarios with nobody wearing it. **Its latest run on Nov 3 reports overall FAIL, and we leave that visible:** six turns missed its 4 s audio limit on model time, while the correct tool was chosen in every scenario and every on-device check passed. In that run 3 of 13 model requests stalled for 3 s or more on their first event, and one greeting ended in the spoken error line after both the request and its hedge stalled. The same day's 44 desktop probes never stalled, so the cause is still open.

Method and raw reports: [`selftest-report.md`](docs/perf/selftest-report.md), [`release-checklist.md`](docs/release-checklist.md), [all numbers](docs/deep-dive.md#performance-budget).

## Privacy and safety

- **Keys stay local.** Normal builds contain no secret; keys are pushed over adb to app storage, and each user brings their own, since the Meta ToS forbids sharing one. An opt-in `--embed-secrets` build exists only for private testers ([ADR-0049](docs/decisions/0049-keyed-test-build.md)).
- **Camera frames never touch disk.** One frame lives in RAM and goes to Meta only when a vision tool runs. `store:false` is on every request.
- **What leaves the headset:** your speech, your questions, frames you ask about, and for web search a coarse location (the city you gave or your time zone's city, country, time zone, local time). Never GPS or an address. Meta's search retention is undocumented.
- **What stays on it:** conversation and scene memory, usage totals, a random install id and settings, including your home city. **Clear memory** in Settings erases memory and spatial anchors. Transcripts are not logged, except the first 120 characters of each model round during the adb-armed self-test.
- **A crisis phrase is caught before the model**, for every persona, with a deterministic hand-off line. Camera content is treated as untrusted data, not instructions.
- **The Listener persona** keeps no memory and uses no camera. Nubi is not a therapist and never claims to be.

## Build and run

You need Unity 6000.6.6f1 with Android Build Support, Git LFS, a Quest 3 in developer mode with `adb` on your `PATH`, `python3`, a Meta Model API key, and a Wit.ai client token and app id. Without keys the app still runs, in offline mode.

```bash
git lfs install && git clone <this repo> && cd <this repo>
cp .env.example .env                                  # add META_API_KEY, WIT_AI_CLIENT_TOKEN, WIT_AI_APP_ID
tools/device/build.sh                                 # applies Quest settings, then builds the IL2CPP release APK
tools/device/install-run.sh JollyXR/Builds/Android/Nubi.apk
tools/device/push-secrets.sh                          # .env to the headset; stops the app
NUBI_SKIP_INSTALL=1 tools/device/install-run.sh       # relaunch with the keys
```

Do Space Setup in your room first, so the character can walk it. Then hold a pinch and talk.

```bash
tools/ci/gates.sh       # static gates, no editor needed; CI runs these on every push
tools/ci/test.sh        # EditMode + PlayMode + coverage gate in batchmode (needs xmllint)
tools/device/selftest.sh  # the real pipeline on the headset, nobody wearing it, about $0.05 of API calls
```

## Honest limits

- **First audio depends on model time.** A short "come here" answers in under 0.5 s through a local guard, but a full model turn took 1.8 s at p50 on Nov 1 and 2.7–6.0 s on Nov 3. A stalled model request can end in the spoken error line.
- **The model's tool choice isn't perfectly reliable.** That is why short commands run on local guards, and why an eval tracks it ([report](docs/perf/tool-selection-eval.md)).
- **Push-to-talk only.** Wake word and hands-free barge-in were cut for the hackathon.
- **A dim room gets a request for light**, not an answer, and without Space Setup the character stays within about 1.5 m of you.
- **Not measured yet:** thermals, battery, CPU main-thread p95 and a 10-minute stale-frame run. Stale frames rise with headset uptime, so reboot before a demo ([ADR-0034](docs/decisions/0034-latency-turns-and-stale-frames-are-device-state.md)).
- **Quest is the prototype, glasses are the product.** The core has no XR dependency, but the glasses path is unverified ([roadmap](docs/deep-dive.md#roadmap)).

[All known limitations](docs/deep-dive.md#known-limitations)

## Documentation

| Read this | For |
|---|---|
| [`docs/deep-dive.md`](docs/deep-dive.md) | Day in the life, controls, engineering, all numbers and limits, roadmap |
| [`docs/architecture.md`](docs/architecture.md) · [`docs/contracts.md`](docs/contracts.md) | Full design and every interface |
| [`docs/decisions/`](docs/decisions/README.md) | Why each key choice was made, with alternatives |
| [`docs/perf/`](docs/perf/README.md) · [`docs/api-spend.md`](docs/api-spend.md) | Measurements, evals and the API spend ledger |
| [`docs/demo-script.md`](docs/demo-script.md) · [`docs/demo/try-it.md`](docs/demo/try-it.md) | Demo beats, live-final runbook and what to try |
| [`docs/characters.md`](docs/characters.md) · [`docs/art/nubi/`](docs/art/nubi/README.md) | Swapping characters; how Nubi is generated |
| [`CHANGELOG.md`](CHANGELOG.md) · [`THIRD_PARTY.md`](THIRD_PARTY.md) | Changes and every licence |

`JollyXR` is the repository's codename and the Unity project's folder; it stayed after the character became Nubi.

## Creators

**Andrei Urbanovich** and **Eugene Basalai**. Built during the Meta AI Developer Hackathon 2026, with Claude Code as a pair programmer; every commit falls inside the hackathon window (first commit 2026-11-01).

## License

Our code is under the [MIT License](LICENSE). The Nubi character (FBX, texture, face presets, renders) is CC0 ([ADR-0011](docs/decisions/0011-original-character-nubi.md)). The SAM one-bit decoder port is under the SAM License ([`LICENSE-SAM.txt`](JollyXR/Assets/Jolly/Runtime/Providers/MetaApi/Vision/LICENSE-SAM.txt)). All other third-party components and their terms, including the non-OSI ones, are in [`THIRD_PARTY.md`](THIRD_PARTY.md).

"Meta", "Muse", "Quest" and "SAM" are Meta's marks, used only to describe what this project calls.
