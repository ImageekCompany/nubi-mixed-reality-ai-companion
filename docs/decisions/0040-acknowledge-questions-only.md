# 0040. Only a question gets the acknowledgement, at the final

- Status: accepted (supersedes "the release acknowledgement is kept" of ADR-0037)
- Date: 2026-11-01

## Context
- The acknowledgement (`LineCategory.Acknowledge`: "Hmm!", "Let me see.", "Okay!", "One moment.", "Good question.") was queued at PTT release on every online turn, before the ASR final existed, so it could not know what had been said.
- On the demo's opening line it played before the guard walk's ComingOver line: in the `8bd9560` device runs Nubi said "Good question. On my way!" and "Okay! Coming right over!" (`docs/perf/selftest-report.md`). "Good question." answers nothing, and two fillers back to back sound scripted. It also delayed the ComingOver line by the ack's length (0.6–1.2 s clips), which is why `voice_come_here` missed its 600 ms target.
- A judge panel flagged the same thing: an acknowledgement before a command ("Nubi, come here.", "Find my keys.") reads as a mistake.

## Decision
- **Decided at the final.** `TurnPipeline.ApplyGuards` plays the acknowledgement (fire and forget, as before) after the guards have run and before the model is called, and only when all of these hold: no crisis words (the hand-off line plays instead), the come guard did not walk (the ComingOver line plays instead), the stop guard did not match, and `TurnGuards.IsQuestion` is true.
- **`QuestionCue`** (Guards): after leading fillers ("Nubi", "hey", "so"), a polite request opener ("can you", "could you", "would you", "will you") is never a question; otherwise a '?' anywhere, or a question opener as the first word ("what", "where", "is", "are", "do", "did", "should", …), is one. The opener list covers finals without punctuation. Lists are `question.openers`, `question.requests` and `question.fillers` in the `Crisis` table (`GuardTables`), with built-in en and pl fallbacks like the other guards; the shipped tables do not have the entries yet, so the fallbacks are what runs.
- **Config.** `ConversationTimings.AcknowledgeOnRelease` → `AcknowledgeQuestions`, `JollyConfig.acknowledgeOnRelease` → `acknowledgeQuestions` (asset updated; default on).
- **Unchanged.** The hold line (`AgentEventKind.Delayed`, ADR-0038) still reuses the Acknowledge clips on any turn whose model is late, command or question, so no new clips need baking per character.

## Consequences
- The guard walk's ComingOver line is the first thing heard on "Nubi, come here.", about one ack-length earlier.
- A question's acknowledgement now starts at the final instead of at release: ASR final is 145–208 ms after end-of-stream on the device, so the ack is about 0.2 s later than before. The definition-of-done row "PTT release → acknowledgement clip audible" is measured to the final now and applies to questions only.
- Commands (and polite requests) get no filler: the first thing heard is the model's answer, or the hold line if the model is late.
- The cue is a word list, not a classifier. A question phrased as a statement without '?' gets no ack (silent until the answer, as for a command); a command starting with a question word ("Is…" is rare as a command) gets one. Either way the cost is one short filler or its absence.
- A late model on a command can still say "Good question." as its hold line (1 in 5 picks, on the rare stalled turn).

## Alternatives
- **Hold the release ack until the final or ~350 ms, whichever comes first** (the speculation in the `8bd9560` report). Rejected: on a slow final the ack would still play before a command, and the timeout adds a second clock to reason about.
- **Cut the ack when the guard walks.** Rejected in ADR-0037 for being audible as a clipped word; deciding at the final avoids starting it at all.
- **A separate hold-line category without "Good question."** Not done: it needs new clips baked for every character's voice for a case seen once in 39 device calls.
