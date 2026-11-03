# 0038. A voice request with no first event after 2.5 s is hedged; the sequential retry is dropped

- Status: accepted (replaces the voice-turn "4 s × 2 attempts" budget of architecture §7)
- Date: 2026-11-01

## Context
- Meta Responses sometimes sends nothing at all for a request. Every other call's first event came in 0.4–1.7 s (38 device calls below, 0.3–1.4 s in the 52 before), but in the `f6d8db3` runs three calls got nothing for 6 s, and in run 2 of `33e69b5` the come-here turn timed out twice in a row, so Nubi walked in silence and the turn ended in the error line (`docs/perf/selftest-report.md`).
- The voice route waited 4 s for a first event, then retried once (`voiceAttempts = 2`, `RetryPolicy` in `MetaSseClient.Open`). A stuck request cost ~4 s before the retry was even sent, and a double stall ~8 s of silence before the error line.
- A stall is per request, not per minute: the call before and the call after answer normally. A second, identical request sent while the first is still silent is therefore likely to answer at the usual speed.

## Decision
- **Hedge, do not wait.** `FirstEventHedge` (Providers/MetaApi/Chat) opens the original request; if it has no first event after `HedgeAfter` (`JollyConfig.voiceHedgeSeconds`, 2.5 s), it sends an identical second request: the same `HttpRequestSpec`, so the same body bytes and `prompt_cache_key`. The first leg whose first event arrives wins and the other is aborted at once. Only the winner's events reach the reducer, so only its usage is recorded.
- **One deadline.** Both legs end at the original's first-event deadline (`voiceFirstEventSeconds`, 4 s after the original was sent; the hedge gets the remaining 1.5 s). If neither has an event by then the call fails with `ProviderErrorKind.Timeout` as before, and the hold line / error line path runs unchanged.
- **The hedge also replaces the fast retry.** If the original fails before 2.5 s with an error `RetryPolicy.IsRetryable` accepts (network error, 429, 500, 502, 503), the hedge is sent at once instead of waiting. A non-retryable error (401, 400, …) fails at once, as before.
- **`voiceAttempts` 2 → 1.** With the hedge, a sequential retry after the 4 s deadline would only add a second 4 s wait: worst-case silence would be ~8 s again. With one attempt per leg the worst case is 4 s from the request (the hold line at 2 s still covers it), under the ~5 s target. `voiceAttempts` still applies per leg if raised.
- **Budget.** The hedge reserves its own `chatReservationUsd` before it is sent; when the meter refuses, there is no hedge (`status=skipped reason=budget`) and the original runs alone. The loser's reservation is settled with an input-token estimate (`RecordEstimate`, 0 output), not released at zero: an aborted request that reached the server may be billed for its input, which is the meter's documented abort rule. A loser rejected with an HTTP status is settled at zero input, as before.
- **Logs.** `evt=llm.hedge status=started|skipped|won_original|won_hedge|both_timeout|both_failed elapsed_ms=…` (from the original's send; `reason=silent|failed|budget`), and on every opened request `evt=llm.first_event leg=original|hedge ttfb_ms=… ttfe_ms=…`. `IHttpTransport.StreamSse` takes an optional first-byte callback for this (UnityWebRequest's first `ReceiveData`). A first-event timeout now ends in `; no byte` or `; first byte at N ms`, and `both_*` lines carry each leg's text, so a report can tell a request that never got a byte (connection or server queue) from one whose model was slow.
- **Scope.** Voice turns and vision (`CloudServices.VoiceChat`). Summarisation (`BackgroundChat`) keeps `HedgeAfter = 0`, 10 s × 3 attempts. The race runs on `IClock`; tests drive it with `FakeClock` through the real `ResponsesChatModel` and `MetaSseClient` over `FakeHttpTransport` (`FirstEventHedgeTests`).

## Consequences
- A single stall costs about 2.5 s plus the hedge's normal first event (~1 s) instead of 4 s plus a retry. A stall that hits both legs now fails at 4 s instead of ~8 s.
- In the first device run of this change (`3241225`, run 2) a stall did hit both legs: the hedge started at 2,512 ms and both timed out at 4,029 ms. The hedge alone does not cover that case; `bd6366a` adds the first-byte text so the next occurrence says why.
- A hedged turn costs up to two requests' input. At ~1,900 input tokens (mostly cached) that is about $0.0003–0.0025 extra per hedge, only on turns that were already late.
- `ttfe − ttfb` was 9–26 ms on all 38 opened requests: the first byte is the first event (`response.created`), so a late first event is a late response from the server, not a slow stream.
- `ResponsesChatModel` takes an `IClock`; `ResponsesChatOptions` has `HedgeAfter`, and `VoiceTurn()` is now the hedged production policy (4 s, 1 attempt, 2.5 s). Tests of the sequential retry path build their options explicitly.

## Alternatives
- **Shorter first-event timeout, keep the sequential retry (e.g. 2.5 s × 2).** Rejected: the retry still waits for the full timeout of a request that may be about to answer (first events up to 1.7 s were seen), and a healthy-but-slow request is aborted instead of raced.
- **Hedge with a fresh connection.** UnityWebRequest has no per-request connection control (`selftest-report.md`, 3bd9078 finding), so the hedge cannot force one.
- **Hedge every request from the start.** Rejected: doubles the cost of every turn to save time on a few per run.
- **Release the loser's reservation at zero.** Rejected: under-counts spend when the aborted request was billed; the estimate errs on the conservative side like every other abort.
