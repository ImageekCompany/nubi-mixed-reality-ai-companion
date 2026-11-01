# 0037. A guard walk answers with a canned line and no model round; `find_object` says Nubi is already looking

- Status: accepted (supersedes the "turn still goes to the model" part of ADR-0036); "the release acknowledgement is kept" superseded by [ADR-0040](0040-acknowledge-questions-only.md)
- Date: 2026-11-01

## Context
- ADR-0036's `ComeGuard` starts the walk about 250 ms after release on "Hi Nubi, come here.", the demo's opening line, then still sends the turn to the model with a note. In the three device runs at `36778c1` (`docs/perf/selftest-report.md`) the guard walked 3 of 3 times, but `voice_come_here` never passed: first audio was 2,235 and 2,138 ms (over the 2.0 s budget, all of it model time), and in one run the Responses request got no first event twice in a row, so Nubi walked in silence and the turn ended in the error line.
- The model added nothing on that turn: with the note it said a short line ("On my way!") and no tool, which a baked clip says as well.
- `find_object` already turns Nubi's head to the nearest instance (`ICharacterController.LookAt(LookTarget.At(...))`, the call `look_at` makes for a seen object), but its result did not say so. In 2 of the 3 runs the model added a `look_at` round after a found result, so tool start → answer audio was 3.0–4.9 s against the 2.5 s budget.

## Decision
- **A guard walk ends the turn locally.** When `TurnGuards.ApplyComeHere` returns walking and no crisis words matched, `TurnPipeline` does not call the agent. It plays a `LineCategory.ComingOver` line (`coming_01..03`: "On my way!", "Coming!", "Coming right over!"; en and pl table text, baked en clips in the default character's voice under `Content/Audio/Lines/en`, and per character for a character with its own voice) through `IOfflineResponder`, and the turn completes.
- **It is a normal turn for the state machine and latency.** `TurnSpeech.ExpectClip` gives the line's utterance id, so its first played sample marks `FirstAudioPlayed` and fires `FirstAudio` (Thinking → Speaking → Idle), as a synthesized sentence does. The responder captions it as for every canned line.
- **History stays consistent.** `IOfflineResponder.Respond` now returns the line it played. The pipeline commits the user's text and the line's text with `IConversationMemory.CommitTurn`, the store the agent replays next turn, so "Thanks!" after "Come here" has the context. A barge-in during the line commits the user text alone.
- **The release acknowledgement is kept.** It is queued at release, and the final (and so the guard) can only come after release, so the ack has always started by then; the ComingOver line plays right after it in the output ring. Dropping a not-yet-started ack needs no code because that case cannot happen.
- **Unchanged:** an unreachable target returns false and the model calls `come_here` itself and explains. Stop, then come, then crisis still run in that order, and crisis words win: the hand-off line plays and the model answers with the crisis note even when the guard walked. `TurnGuards.ComeHereNote` is deleted (no caller left).
- **`find_object` results say the gaze is done.** A found result adds `"looking":true` and `"next":"you are already looking at it; answer in words, no other tool is needed"` (`FindObjectTool.NextStep`). No second gaze helper: the tool already makes the same `LookAt` call as `look_at`. The prompt's tool rules never told the model to call `look_at` after `find_object`, so they are unchanged.
- **Self-test.** `voice_come_here` reports `llm_request` (sent or none, from `LatencyMark.LlmRequestSent`) and degrades when a model request was sent or release → first audio is over 600 ms.

## Consequences
- The opening beat no longer waits on, or fails with, the model: no request is sent for it, so it costs nothing and cannot time out.
- That turn has no model-authored reply: Nubi cannot add anything to "Hi Nubi, come here." beyond the canned line, and the greeting part is not answered in words. Longer or embedded come requests still go to the model (ADR-0036).
- The 600 ms target is measured to the ComingOver line's first sample, which plays after the release acknowledgement (0.6–1.2 s clips, about 0.1 s leading silence). Device numbers are in `docs/perf/selftest-report.md`.
- `IOfflineResponder.Respond` returns `UniTask<CannedLine>`; callers that only await it are unchanged.

## Alternatives
- **Keep the model round and only shorten the note.** Rejected: the latency and the timeouts are the model's first event, not the reply length.
- **Speak the line through TTS (`TurnSpeech.Append`).** Rejected: a Wit request on the opening beat adds 150–700 ms and a network dependency for a fixed sentence.
- **Cut the release acknowledgement when the guard walks.** Rejected: it has already started playing by the time the final arrives, so cutting it is audible as a clipped word.
- **Drop `find_object`'s gaze and let the model call `look_at`.** Rejected: that is the extra round this removes.
