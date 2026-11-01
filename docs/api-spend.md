# Meta Model API spend ledger

Budget: **$140 total** for the whole project (dev, CI, demo). Append one row per batch of live calls. Prices per `docs/research/meta-model-api.md` §6: Spark 1.3 standard $1.25 / $4.25 per M input/output tokens, contributor $0.10 / $0.20, SAM 3.1 $2.50 per 1k images, ASR $0.18/h. Wit.ai is a separate free service and is listed at $0 for completeness.

Rows before 2026-11-01 02:00 (research phase, `docs/research/*.md`) were not itemised; estimate < $0.50 in total (dozens of short Spark calls, ~15 SAM images, a few minutes of ASR).

| Date (UTC) | Who / purpose | Model | Calls | Approx tokens / units | Est. cost (USD) |
|---|---|---|---|---|---|
| 2026-11-01 | research phase (all agents, not itemised) | spark 1.2/1.3 std+contrib, sam-3.1, asr | ~60 | see research docs | ≤ 0.50 |
| 2026-11-01 | architect: verify Responses API streaming tool calls + encrypted-reasoning replay (synthetic prompt) | muse-spark-1.3-contributor | 2 | in 1,442 / out 291 | 0.0002 |
| 2026-11-01 | architect: Wit `/voices` + 3× `/synthesize` TTFB probe | Wit.ai (not Meta API budget) | 4 | ~130 chars | 0.00 |
| 2026-11-01 | xr-engineer: device spike run 1 (Quest 3, IL2CPP) — ASR realtime WS with 4.3 s Wit TTS audio | muse-voice-transcribe-1.0 | 1 | ~5 s audio | 0.0003 |
| 2026-11-01 | xr-engineer: device spike run 2 — ASR WS ×2 (TTS 4.3 s + mic 3 s), Spark vision ×1 + SAM ×2 on CC-BY test image `pen_640.jpg` (room too dark for camera frame) | muse-voice-transcribe-1.0, muse-spark-1.3 (standard), sam-3.1 | 5 | ~8 s audio; vision ~0.5k in / ~0.8k out; 2 images | 0.01 |
| 2026-11-01 | architect (review pass): cross-turn replay of `function_call`+output without reasoning item (200, coherent) and orphan `function_call` without output (400 "No tool output found for function call") — synthetic prompts | muse-spark-1.3-contributor | 2 | in ~1.3k / out ~60 | 0.0002 |
| 2026-11-01 | architect (review pass): SAM part phrases `mug handle`, `handle` on CC0 `mug_1280.jpg` (both 1 object, c≈0.96, same box) | sam-3.1 | 2 | 2 images | 0.005 |
| 2026-11-01 | architect (review pass): ASR file endpoint on Wit-synthesised "Hi Nubi…" (2 clips; heard "Hi newbie" and "nubi") | muse-voice-transcribe-1.0 (+ Wit TTS, free) | 2 | ~4 s audio | 0.0002 |
| 2026-11-01 | architect (review pass): vision streaming with `LABEL\|PHRASE` header on CC-BY `pen_640.jpg`, effort minimal vs low (first text 1.51 s / 2.44 s) | muse-spark-1.3 | 2 | in 846 / out 709 | 0.004 |
| 2026-11-01 | xr-engineer: device spike runs 3–6 (script end-to-end check + mic-stall repro) — ASR WS ~8 short sessions, no vision/SAM | muse-voice-transcribe-1.0 | ~8 | ~40 s audio | 0.002 |
| 2026-11-01 | verifier: device spike re-run (run3-verify, non-paid) — ASR WS ×2 (Wit TTS 4.3 s + mic 3 s); no vision/SAM | muse-voice-transcribe-1.0 | 2 | ~7.4 s audio | 0.0004 |
| 2026-11-01 | WP1: record Responses SSE fixtures (text, commentary + function_call, parallel calls, incomplete) and verify round-2 replay of a function_call without its item id (200, cached 497) — synthetic prompts | muse-spark-1.3-contributor | 6 | in ~2.5k / out ~440 | 0.0004 |
| 2026-11-01 | WP7: canned-line bake (`CannedLineBaker`, 31 lines + 1 timed-out retry) and `/voices` listing | Wit.ai (not Meta API budget) | 33 | ~1.9k chars | 0.00 |
| 2026-11-01 | WP9: device self-test (`tools/device/selftest.sh`), 3 paid runs on Quest 3 (runs 1-2 never left Offline because of the probe URL bug, $0). Per run: ~5 voice turns (Muse ASR + Spark), 1 vision call + 1 SAM image on the CC0 mug still. Spend from the app's own meter: 0.0159 + 0.0207 + 0.0252 | muse-spark-1.3, sam-3.1, muse-voice-transcribe-1.0 (+ Wit, free) | ~15 agent + 3 vision Spark calls, 3 SAM, ~15 ASR sessions | in ~32k / out ~3k; 3 images; ~35 s audio | 0.062 |
| 2026-11-01 | integration review (device reviewer): self-test re-run on `main` 9441aa8, 1 paid run (app meter) | muse-spark-1.3, sam-3.1, muse-voice-transcribe-1.0 (+ Wit, free) | ~5 agent + 1 vision Spark calls, 1 SAM, 5 ASR sessions | ~1 image; ~12 s audio | 0.0229 |
| 2026-11-01 | integration fix pass: self-test voice clips re-synthesised (4× Wit `/synthesize`) | Wit.ai (not Meta API budget) | 4 | ~140 chars | 0.00 |
| 2026-11-01 | integration fix pass: device self-test, 3 paid runs on `fix/integration-review` (app meter: 0.0232 + 0.0243 + 0.0260) | muse-spark-1.3, sam-3.1, muse-voice-transcribe-1.0 (+ Wit, free) | ~24 Spark calls, 3 SAM, 15 ASR sessions | in ~40k / out ~4k; 3 images; ~35 s audio | 0.0735 |
| 2026-11-01 | perf-latency: vision latency probes from the dev Mac (app vision prompt; dark device frame, `mug_640.jpg`, darkened `pen_640.jpg`; 1.3 vs 1.2, prompt variants, no-tools line); 2 of them lost their printout to a script bug | muse-spark-1.3, muse-spark-1.2 | 9 | in ~0.8–1.0k / out ~250–440 each | 0.03 |
| 2026-11-01 | perf-latency: prompt caching probes, app-shaped 2,718-token request with 11 synthetic tools, with and without `prompt_cache_key` | muse-spark-1.3 | 4 | in 2,718 / out ~90–150 each | 0.016 |
| 2026-11-01 | perf-latency: 11 device self-test runs (4 development, 2 baseline, 4 after, 1 verification; app meter 0.0211 + 0.0209 + 0.0292 + 0.0242 + 0.0226 + 0.0182 + 0.0166 + 0.0305 + 0.0247 + 0.0226 + 0.0123) | muse-spark-1.3, sam-3.1, muse-voice-transcribe-1.0 (+ Wit, free) | ~70 Spark calls, 10 SAM, ~50 ASR sessions | ~11 images; ~2 min audio | 0.243 |
| 2026-11-01 | fix/llm-memory: summary diagnosis (old prompt @300 → `incomplete`, 188 reasoning; new prompt @1024 ×2 → completed; final prompt streamed + `prompt_cache_key`, recorded as `responses_summary` fixture) and prompt-cache order A/B (4 calls, all `cached_tokens: 0`); synthetic conversations only | muse-spark-1.3-contributor | 8 | in ~9.8k / out ~1.5k | 0.0013 |
| 2026-11-01 | fix/audit: 1 device self-test run after the audit fixes (app meter 0.0116) | muse-spark-1.3, sam-3.1, muse-voice-transcribe-1.0 (+ Wit, free) | ~6 Spark calls, 1–2 SAM, ~4 ASR sessions | 1 image; ~10 s audio | 0.012 |
| 2026-11-01 | release audit: 1 device self-test run on `main` 3f35562 (app meter 0.0120) | muse-spark-1.3, sam-3.1, muse-voice-transcribe-1.0 (+ Wit, free) | ~6 Spark calls, 1 SAM, 4 ASR sessions | 1 image; ~10 s audio | 0.012 |

Running total (est.): **≤ $1.00** (sum of the rows: 0.991)
