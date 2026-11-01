# 0013. Test strategy

- Status: accepted
- Date: 2026-11-01

## Context
Technical depth is 20% of the score. PCA is unavailable in the Editor. API calls cost money from a fixed $140 budget.

## Decision
- **Unity Test Framework 1.8.0** (built-in) with NUnit `Assert.That`.
  - No FluentAssertions: v8 has a non-commercial licence and v7 adds a dependency.
  - No NSubstitute: hand-written fakes in `Jolly.Testing` double as Editor providers.
- **EditMode, pure and fast:**
  - SSE line parser and Responses event reducer, fed with recorded fixtures (`Tests/Fixtures/sse/*.txt` captured from live calls)
  - SAM grammar parser and one_bit decoder (fixtures from `meta-model-api.md` §4; compared against decoded PNG coverage)
  - tool registry and schema validator
  - sentence splitter
  - state machine (an exhaustive transition table)
  - agent loop with `ScriptedChatModel` (multi-round, parallel tools, `EndsTurn`, cancellation)
  - retry policy (seeded random)
  - usage meter
  - memory caps and persistence
  - secrets parsing
  - crisis guard
  - resampler
  - Wit chunker (odd-byte carry)
- **PlayMode:**
  - an e2e voice turn with fakes (scripted ASR events, scripted SSE, a fake TTS sine wave): asserts the state sequence, captions, tool calls on `RecordingCharacterController`, and that barge-in stops audio within 150 ms of scaled time
  - the offline path
  - character NavMesh walk on a test floor
  - `NoGcAllocInSteadyStateIdle`
- **Live smoke tests:** `[Category("Live")]`, excluded by default. They run only with `-testCategory Live` and a key, on `ModelRoute.Synthetic` (the contributor model) with synthetic fixtures. Each run is logged to `docs/api-spend.md`.
- **On device:** the scripted checklist in `definition-of-done.md`, plus the `turn.latency` log parse.
- Commands and exit codes are in `definition-of-done.md`.

## Consequences
- CI needs no headset and no key.
- Device-only behaviour (PCA, mic, thermals) is verified manually against the checklist.

## Alternatives
- Mocking frameworks: rejected, see above.
- Recording and replaying HTTP at the transport layer: the fixture approach at the parser level is simpler.
