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

Running total (est.): **≤ $0.53**
