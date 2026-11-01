# 0004. Meta Model API: Responses protocol and model routing

- Status: accepted
- Date: 2026-11-01

## Context
- Chat Completions and Responses both stream and both support tools (`meta-model-api.md` §1–2, critic C12).
- Responses is the only endpoint that carries reasoning between calls. `store` defaults to true.
- ToS §6.2 forbids sending personal information to `*-contributor` models (critic C2). Requirement 7 asks for contributor models to save cost.
- Measured TTFT at `minimal` effort: standard 1.3 ≈ 1.0–1.4 s, 1.3-contributor ≈ 1.6–2.3 s. Rate limits are 3,000 and 150 RPM respectively.

## Decision
- **Protocol:** `POST /v1/responses` with:
  - `stream:true`, `store:false`
  - `reasoning.effort:"minimal"`
  - `include:["reasoning.encrypted_content"]`
  - flat function tools
  - stateless replay of all output items plus `function_call_output` within a turn
- Verified live on 2026-11-01: streamed `function_call` items with `call_id`, a `message` preamble with `phase:"commentary"`, and a correct second round after replaying encrypted reasoning (`docs/api-spend.md`). Also verified the same day: replaying an earlier turn's `function_call` + `function_call_output` **without** its reasoning item works, and an orphan `function_call` with no output returns 400 "No tool output found for function call". Memory therefore commits each turn atomically and fills unanswered calls with `{"error":"interrupted"}`.
- Vision uses the same endpoint at `reasoning.effort:"minimal"` with a streamed `LABEL|NOUN_PHRASE` first line followed by plain answer text (live: first text 1.5 s vs 2.4 s at `low`).
- SAM 3.1 also uses `/v1/responses`. One SSE parser serves both.
- **Routing:** `ModelRoute.LiveUser` → `muse-spark-1.3`. This covers every request carrying user speech, camera frames or derived memory, including summaries and vision. `ModelRoute.Synthetic` → `muse-spark-1.3-contributor`, used only for CI smoke tests, dev tools and canned-line generation with synthetic inputs. `MetaModelResolver` (in `Providers/MetaApi/Common`, used by every MetaApi adapter) **throws** `ProviderException(BadRequest)` for a contributor model on the LiveUser route. It is not an assertion: `Debug.Assert` and `UnityEngine.Assertions` are stripped from release builds.
- Model ids live in `JollyConfig`.

## Consequences
- Live cost is about $0.003 per turn (speculation: about 1.5k input and 300 output tokens at $1.25/$4.25 per M). A 2-hour demo rehearsal day stays under $2.
- Requirement 7 is honoured where it is lawful. The README explains the split as a privacy and cost engineering decision.
- `IChatModel` is endpoint-agnostic. A Chat Completions adapter (for a LAN Glimmer host) can be added without touching Core, by ignoring `Opaque` items.
- **Risks:**
  - Responses event names are used exactly as observed. Unknown events are ignored.
  - Turn 1 on contributor showed `response.created` only after 5.3 s (n=1). Live turns use standard, but latency still needs measuring on device (R4).

## Alternatives
- **Chat Completions:** simpler and OpenAI-compatible, but loses reasoning between tool rounds and has no early `response.created`.
- **Contributor for everything:** about 10× cheaper, but violates ToS §6.2 for personal data, is slower, and allows only 150 RPM.
