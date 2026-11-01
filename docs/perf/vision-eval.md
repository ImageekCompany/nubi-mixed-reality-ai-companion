# R6: vision and SAM evaluation on the demo room

Status: **harness ready, live run pending** (needs 30–50 real frames of the hero objects from the headset).
Owner: WP4. Pass criterion and fallback: `docs/scope.md` §2 (R6). Budget: ≈ $5, logged in `docs/api-spend.md`.

## Questions

1. Which SAM prompt finds the object the user means: a point at the image centre, the projected hand-ray point with
   hand-joint negative points, or the noun phrase from the vision header (the retry path, architecture §5.3)?
2. Which vision variant gives the best spoken answer within the latency budget (DoD §5: `capture_and_identify` start →
   answer audio p50 ≤ 2.5 s): `muse-spark-1.3` at `minimal`, at `low`, or `muse-wonder-1.0`?
3. How often does the `LABEL|NOUN_PHRASE` header parse, and is the label right?

## Method

- **Frames:** 30–50 JPEGs at 640 px from the PCA path (`MetaPassthroughFrameSource` → `JpegImageEncoder`, the same bytes
  the app sends), captured only in a Development build with `JOLLY_DEBUG_CAPTURE` in the developer's own room, of the
  hero objects (TV remote, moka pot, thermostat) held, pointed at, and on furniture. Never committed (`.gitignore`
  covers `artifacts/`).
- **Annotations** (`annotations.json` beside the frames, schema in the harness docstring): the object name and aliases,
  its inclusive pixel box, and, when the hand is in view, the hand-ray point and the hand key points that
  `CameraMath.WorldToPixel` projected at capture time.
- **Harness:** `tools/vision-eval/vision_eval.py` (stdlib Python). Per frame it runs every vision variant (streamed, as
  the app does) and then three SAM calls with the app's request shape (`one_bit`, confidence, streamed; latency is
  taken at `output_text.done` as `SamSegmenter` does):
  centre point (+ hand negatives), hand point (+ negatives) when annotated, and the first parsed noun phrase.
  A SAM **hit** is a returned box with IoU ≥ 0.5 against the annotated box.
- **Privacy:** frames are real home imagery, so the harness refuses `-contributor` models (ADR-0004); `store:false`
  on every request; the API key is never printed.

```bash
tools/vision-eval/vision_eval.py --selftest                                   # offline, no key
tools/vision-eval/vision_eval.py --frames artifacts/r6 --dry-run              # plan and cost estimate
tools/vision-eval/vision_eval.py --frames artifacts/r6 --yes --max-usd 6      # live; paste the printed row into docs/api-spend.md
```

Answer quality is rated by hand from `artifacts/vision-eval/results.csv` (`answer` column): 2 = correct and useful,
1 = correct object but vague, 0 = wrong.

## Results

Pending the headset session. The harness prints this table:

| Kind | Variant | n | Hit / parse rate | Label accuracy | p50 ms | p90 ms |
|---|---|---|---|---|---|---|
| sam | centre | | | — | | |
| sam | hand | | | — | | |
| sam | phrase | | | — | | |
| vision | muse-spark-1.3:minimal | | | | | |
| vision | muse-spark-1.3:low | | | | | |
| vision | muse-wonder-1.0:minimal | | | | | |

Desk baseline (CC-licensed samples, not the demo room, `docs/research/meta-model-api.md` §4.4 and `review-log.md`):
SAM 640 px total 0.8–1.5 s; vision `minimal` first text 1.51 s, `low` 2.44 s.

## Decision rule

- Keep the hand-point cascade as *must* if its hit rate beats the centre point by ≥ 15 points; otherwise ship centre
  point + noun-phrase retry (the R6 fallback in `scope.md`).
- Keep `minimal` unless `low` raises the mean answer rating by ≥ 0.3 at a p50 first-text cost under 1 s.
- If the header parse rate is below 90%, tighten `VisionRequestBuilder.FormatRules` and re-run 10 frames.
