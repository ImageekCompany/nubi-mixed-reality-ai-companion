# 0036. A local `come_here` guard for short come requests; self-test runs log each model reply

- Status: accepted; "the turn still goes to the model" superseded by [ADR-0037](0037-guard-walk-skips-the-model-and-find-object-gaze.md)
- Date: 2026-11-01

## Context
- "Nubi, come here" is the demo's opening beat. The tool-selection eval (ADR-0035) measured `come_here` on 2 of 5 come-here phrasings at `ReasoningEffort.Minimal`, a different two each run, and the round 1 device runs missed it in 2 of 3. Better descriptions did not change that.
- `StopGuard` already handles the opposite command locally: a short final made only of stop triggers and fillers halts locomotion before any model call (architecture §5.1).
- When the model skipped a tool, nothing on the device said what it did instead: the app logged no reply text, by design (architecture §11 Logging), so a miss could not be told apart from an ASR or tool problem without a rerun.

## Decision
- **`ComeGuard`** matches a final of at most 6 words made only of come triggers ("come here", "come to me", "come closer", "come over here") and fillers (the character's name, "please", "hi", "hey", "hello"), with at least one trigger. The phrases live in the `Crisis` string table (`come.triggers`, `come.fillers`, en and pl) with built-in fallbacks in `GuardTables`, as the stop phrases do. Its matching is shared with `StopGuard` (`ShortCommand`).
- **`TurnGuards.ApplyComeHere`** runs on a match, before the model call, only when the current persona is offered `come_here` (`IToolRegistry.DefinitionsFor`, the same allow-list check as the model's calls). It runs the registered `ComeHereTool`, so the walkable sampling and NavMesh path are the model path's. It raises `ToolStarted`/`ToolCompleted` with the call id `guard_come_here` (badge, self-test) and logs `evt=guard.come status=walking|unreachable`.
- **The turn still goes to the model.** When Nubi is walking, the user text gets a note like the crisis note: Nubi is already walking; reply briefly and do not call `come_here` again. A `come_here` call the model makes anyway only restarts the same walk. When the target is unreachable no note is added, so the model calls the tool and explains.
- **Reply summaries in self-test runs only.** `AgentSettings.ReplyObserved` gets `round= tools= finish= text="…"` (first 120 characters, newlines removed) for every model round, and the agent loop then logs it as `evt=llm.reply turn=…`. Only `SelfTestSession` sets it, through `Edges.ModelReplies`; scenario results carry it as `llm_reply`, and `selftest.sh` prints it under any scenario that is not PASS.
- **The self-test speaks the demo line.** `voice_come_here` now plays "Hi Nubi, come here." (Wit "Charlie", like the other clips) and reports `come_source` = guard, model, guard+model or none. `voice_greeting` still degrades on any movement tool.

## Consequences
- The opening beat no longer depends on the model's tool choice at `minimal`; the device self-test checks the walk on every run.
- Longer or embedded requests ("come here and tell me about this remote", "can you come here?") still depend on the model. Its come-here rate there is unchanged (ADR-0035).
- The model can still answer with a line that contradicts the walk ("I'll stay here"); the note makes that unlikely, and the self-test's `llm_reply` shows it when it happens.
- Self-test runs put the model's first 120 characters per round in logcat. Their input is scripted clips with nobody present. Normal and development builds still log no model text: the only other path remains `LogDebug` under `JOLLY_DEBUG_LOG`, which no committed setting defines.

## Alternatives
- **`tool_choice: come_here` for come phrasings.** Rejected: it still needs the same local match to decide when to force it, adds the model's latency before the walk starts, and a forced call can still return text first. The local call is faster and needs no model at all.
- **A greeting keyword filter.** Still rejected (ADR-0032). This guard never acts on a greeting alone: a final with no come trigger is never matched, and a greeting with any other words stays with the model.
- **Log reply text in every development build.** Not done: a development build is still used by a person talking in their own room, and architecture §11 keeps model text out of the log. The self-test, which this was needed for, is enough.
