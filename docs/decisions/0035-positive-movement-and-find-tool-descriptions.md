# 0035. Positive tool triggers for `come_here` and `find_object`, measured by a live tool-selection eval

- Status: accepted
- Date: 2026-11-01

## Context
- Round 1 device runs at f311081 (`docs/perf/selftest-report.md`): "Nubi, come here" got no tool call in 2 of 3 runs, and "Nubi, where is my mug? Show me." called `look_at` instead of `find_object` once.
- ADR-0032 stopped greetings from walking by writing the movement guidance in the negative: `come_here` was "only when the user asks you to come to them or closer; never for a greeting, an introduction or small talk", and the tool rules said "Move only when the user asks you to go somewhere". `look_at` said "Use it when you talk about something in the room", which also covers an object the user is looking for.
- Every live turn runs at `ReasoningEffort.Minimal` and sends no `tool_choice`. Nothing measured tool routing except three-run device self-tests, which cost ~$0.025 each and mix in ASR and vision.

## Decision
- **A live tool-selection eval.** `ToolSelectionEval` (PlayMode, `[Explicit]`, `[Category("LiveEval")]`) sends 20 text-only turns with the real `PromptBuilder` instructions, default persona and `ToolCatalog` definitions through the app's `ResponsesChatModel`, and writes the first-round tools per utterance as a markdown table (`tools/eval/tool-selection.sh [effort] [tags]`). `tools/ci/test.sh` never runs it and needs no key. Grading (`ToolSelectionCase`, `ToolSelectionReport`) is unit-tested.
- **Descriptions lead with when to call.** `come_here`: "Call this whenever the user asks you to come, come here, come closer or come to them, including after a greeting ('Hi, come here'): walk to the user and face them. Not for a greeting on its own." The example omits the character's name, because characters are swappable (ADR-0026). Tool rules: "If the user asks you to come or to go somewhere, call the movement tool in this turn; answer a greeting alone in words, with at most a gesture." `find_object`: "Locate a named object the user is searching for ('where is my X', 'find my X'), even one you have seen before: …". `look_at` adds "never for finding or locating an object the user is looking for; use find_object for that".
- **Effort stays `minimal`.** `low` was not measured: the batch's eval budget went on the baseline and the re-run.

## Consequences
- Eval (`docs/perf/tool-selection-eval.md`), same 12 targeted cases, before → after: greetings that stayed put 3/3 → 3/3, come-here variants → `come_here` 2/5 → 2/5, where/find variants → `find_object` 4/4 → 4/4. Baseline over all 20 cases: 16/20.
- **The come-here miss is not fixed.** Each run missed a different three of the five phrasings, so at `minimal` the call is close to a coin flip per turn, whatever the description says. The acceptance target (every come-here variant in a single pass) is not met. The descriptions stay because they did no harm in the eval, and they state the intended routing for both tools.
- The device's `look_at`-for-the-mug was not reproduced in text, even with the mug in scene memory. The device turn also carried the identify turn's history, which the eval does not send.
- An eval run costs ~$0.02–0.03, mostly output tokens (~300 per call, reasoning included). This batch spent $0.053, $0.003 over its $0.05 cap.
- Next options, each measurable with the eval: `low` effort on the come-here cases (latency cost per turn), `tool_choice: required` on turns a cheap classifier flags as movement requests, or a few-shot example in the tool rules.

## Alternatives
- **Force `tool_choice` for come-here phrasings in code.** Not done: it needs an utterance classifier in the turn path, which ADR-0032 rejected for greetings ("deciding which utterance is a greeting is the model's job"); the eval can now measure whether it is worth it.
- **Switch live turns to `low` effort.** Not measured. It adds reasoning tokens to every turn, and release → first audio is already at p50 1.8 s against a 2.0 s budget (ADR-0034).
- **A Python harness like `tools/vision-eval`.** Rejected: it would copy the prompt and tool text, which drift. The PlayMode test builds them from the shipped code and assets.
