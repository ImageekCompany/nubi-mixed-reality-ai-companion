# Tool-selection eval

Does the model pick the right tool for a spoken request? Device runs at f311081 answered "Nubi, come here" in words in 2 of 3 runs and routed "Nubi, where is my mug? Show me." to `look_at` (`selftest-report.md`). This eval measures routing without a headset.

## Method

- `Jolly.Tests.PlayMode.Eval.ToolSelectionEval` (`[Explicit]`, category `LiveEval`, so `tools/ci/test.sh` never runs it) sends each utterance of `ToolSelectionCase.All` as one text-only turn: the real `PromptBuilder` instructions for the default persona (`companion`, resolved through `UnityLocalizer` and `PersonaRepository` with Nubi's name), the 13 tool definitions in `ToolCatalog` order, and the app's `ResponsesChatModel` (`muse-spark-1.3`, `prompt_cache_key`, voice-turn timeouts). One utterance has a `white mug` in scene memory, so the real context block lists it, as after the device's identify turn.
- It records the tools called in the first round. A case passes when one of its expected tools is called (other calls such as a gesture, `play_gesture` before ADR-0047's review and `actions.wave` since, are allowed); a greeting or small-talk case passes when no movement tool (`come_here`, `walk_to`, `follow_user`, `go_to_surface`) is called.
- Run: `tools/eval/tool-selection.sh [minimal|low] [tags]` (tags select a subset, e.g. `greeting,come_here,find_object`). It reads `META_API_KEY` from the repository `.env` and writes `artifacts/tool-selection-<effort>.md`. Cost is metered with the app's price table.
- One pass per utterance: the model is not deterministic, so a single run shows misses, not rates. Treat a row as one sample.

## Baseline: `88047fe`, effort `minimal`, all 20 cases

| Utterance | Expected | Called | Response ms | Result |
|---|---|---|---|---|
| Hi Nubi | no movement tool | (none) | 1667 | PASS |
| Hello, who are you? | no movement tool | (none) | 2904 | PASS |
| Good morning Nubi, how are you today? | no movement tool | (none) | 2443 | PASS |
| Nubi, come here | `come_here` | (none) | 2427 | **FAIL** |
| Hi Nubi, come here | `come_here` | `come_here` | 1986 | PASS |
| come closer | `come_here` | (none) | 2556 | **FAIL** |
| come to me please | `come_here` | `come_here` | 1676 | PASS |
| can you come over here? | `come_here` | (none) | 1817 | **FAIL** |
| follow me | `follow_user` | `follow_user` | 2837 | PASS |
| where is my mug? | `find_object` | `find_object` | 1521 | PASS |
| can you find my glasses | `find_object` | `find_object` | 2084 | PASS |
| where did I put my keys | `find_object` | `find_object` | 1953 | PASS |
| Nubi, where is my mug? Show me. (white mug already seen) | `find_object` | `find_object` | 3077 | PASS |
| what is this? | `capture_and_identify` | `capture_and_identify` | 851 | PASS |
| look at me | `look_at` | `look_at` | 1891 | PASS |
| stop | `stop` | (none) | 3070 | **FAIL** |
| what's your favourite colour? | no movement tool | (none) | 1866 | PASS |
| tell me a joke | no movement tool | (none) | 1547 | PASS |
| it's a lovely day today, isn't it? | no movement tool | (none) | 2175 | PASS |
| thank you, that's all for now | no movement tool | (none) | 1625 | PASS |

Pass rate: **16 / 20 (80 %)**. Metered cost $0.0307 (token counts were not recorded in this first run).

## After ADR-0035 descriptions: `c52b250`, effort `minimal`, greeting + come_here + find_object cases

The budget for this batch ($0.05) did not cover a second full run, so the re-run is the 12 cases the change targets.

| Utterance | Expected | Called | Response ms | Result |
|---|---|---|---|---|
| Hi Nubi | no movement tool | `play_gesture` | 2790 | PASS |
| Hello, who are you? | no movement tool | (none) | 1224 | PASS |
| Good morning Nubi, how are you today? | no movement tool | `play_gesture` | 2907 | PASS |
| Nubi, come here | `come_here` | (none) | 1395 | **FAIL** |
| Hi Nubi, come here | `come_here` | (none) | 2220 | **FAIL** |
| come closer | `come_here` | `come_here` | 1325 | PASS |
| come to me please | `come_here` | (none) | 1977 | **FAIL** |
| can you come over here? | `come_here` | `come_here` | 1346 | PASS |
| where is my mug? | `find_object` | `find_object` | 1618 | PASS |
| can you find my glasses | `find_object` | `find_object` | 2054 | PASS |
| where did I put my keys | `find_object` | `find_object` | 2251 | PASS |
| Nubi, where is my mug? Show me. (white mug already seen) | `find_object` | `find_object` | 1180 | PASS |

Pass rate: **9 / 12 (75 %)**; the same 12 cases in the baseline: 9 / 12. Metered cost $0.0221; tokens: input 26,079 (cached 23,771), output 3,679.

## Results

| Group (same cases) | Baseline `88047fe` | After `c52b250` |
|---|---|---|
| Greetings stay put | 3 / 3 | 3 / 3 (two with `play_gesture`) |
| Come-here variants → `come_here` | 2 / 5 | 2 / 5 |
| Where/find variants → `find_object` | 4 / 4 | 4 / 4 |
| Other (follow, identify, look at me, stop, small talk) | 7 / 8 | not re-run |

- **`come_here` is not fixed by descriptions.** Each run missed three of five come-here phrasings, but not the same three ("Hi Nubi, come here" and "come to me please" passed before and failed after; "come closer" and "can you come over here?" the reverse). At `minimal` effort the call is a coin flip per turn, which matches the device (1 of 3 runs). The model answers in words instead (the eval does not record the text).
- **`find_object` routes correctly in text**, including with the mug already in scene memory, before and after. The device's `look_at` choice was not reproduced; the device turn also had the identify turn's history in `Recent`, which this eval does not send.
- `stop` with nothing moving got no call in the baseline; not targeted here.
- **`low` effort was not run**: the two runs used the batch's eval budget. Effort stays `minimal` (ADR-0035).

## Character actions (ADR-0047): `290385a+dirty`, effort `minimal`

The catalog now sends the `actions` namespace, and four action cases were added ("come closer" also accepts `actions.move_relative`).

| Utterance | Expected | Called | Result |
|---|---|---|---|
| Show me how to dance! | `actions.dance` | `actions.dance` | PASS |
| Can you jump? | `actions.jump` or `actions.celebrate` | (none) | **FAIL** |
| Spin around for me | `actions.spin` | `actions.spin` | PASS |
| Go stand behind me | `actions.move_relative` | `actions.move_relative` | PASS |
| come closer | `come_here` or `actions.move_relative` | `actions.move_relative` | PASS |

The other greeting and come-here cases of the run passed (11 / 12). "Can you jump?" was answered in words; after the namespace description added "or asks whether you can … to show them", a re-run of that one case called `actions.jump` (one sample).

## Review of ADR-0047/0048: `cfad01b+dirty`, effort `minimal`

The eval now sends `AgentLoop.LiveRequest`, exactly what a live turn sends: web search with this machine's location, the matching instructions and the context block with the local time. `play_gesture` is gone (the gesture actions replace it); "Take a bow!", "Stand on my left" and "Do a little show: jump, spin and take a bow" were added and "can you come over here?", "where did I put my keys" and "what's your favourite colour?" dropped to stay within the 24-case cap (`artifacts/tool-selection-review-run1.md`, `-run2.md`).

| Utterance | Expected | Called | Result |
|---|---|---|---|
| Show me how to dance! | `actions.dance` | `actions.dance` | PASS |
| Can you jump? | `actions.jump` or `actions.celebrate` | (none) | **FAIL** |
| Take a bow! | `actions.bow` | `actions.bow` | PASS |
| Spin around for me | `actions.spin` | `actions.spin` | PASS |
| Do a little show: jump, spin and take a bow | any of `actions.jump`, `actions.spin`, `actions.bow` | `actions.jump`, `actions.spin`, `actions.bow` | PASS |
| Go stand behind me | `actions.move_relative` | `actions.move_relative` | PASS |
| Stand on my left | `actions.move_relative` | `actions.move_relative` | PASS |

6 / 7. "Can you jump?" was answered in words again; with "or asks whether you can, call it in this turn to show them" added to the instructions' action rule, a re-run of the `actions.jump` tag still answered it in words (the show passed again), so that wording was not kept. Over all runs it has passed 1 of 4 samples.

## Runs

| Date (UTC) | Commit | Effort | Cases | Passed | Metered USD |
|---|---|---|---|---|---|
| 2026-11-01 | `88047fe` | minimal | all 20 | 16 | 0.0307 |
| 2026-11-01 | `c52b250` | minimal | 12 (greeting, come_here, find_object) | 9 | 0.0221 |
| 2026-11-02 | `290385a+dirty` | minimal | 12 (greeting, come_here, 4 action tags) | 11 | 0.0263 |
| 2026-11-02 | `290385a+dirty` | minimal | 1 (actions.jump) | 1 | 0.0052 |
| 2026-11-02 | `cfad01b+dirty` | minimal | 7 (5 action tags) | 6 | 0.0159 |
| 2026-11-02 | `cfad01b+dirty` | minimal | 2 (actions.jump, with an instruction tweak not kept) | 1 | 0.0082 |

Total **$0.0528**, $0.003 over the batch's $0.05 eval cap: the second run was sized from the first run's cost per call, and its output (~300 tokens per call, mostly reasoning even at `minimal`) is what a run costs (output is 71 % of its metered cost). The baseline ran the harness of `7e6ccc9` before it was committed, on the `88047fe` runtime.
