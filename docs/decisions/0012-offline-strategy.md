# 0012. Offline and degraded strategy

- Status: accepted
- Date: 2026-11-01

## Context
Requirement 23 says Nubi stays alive offline, using predefined lines when the user interacts and idle behaviour otherwise. A Glimmer-class LLM cannot run on Quest 3 (17 GB+).

## Decision
- `IConnectivityMonitor` has five states: Online, Degraded, Offline, Unauthorized (key missing or 401; the probe is disabled until the key changes) and BudgetReached.
  - It is fed by every provider result and by `MetaHealthProbe` (`GET /v1/models`, 3 s timeout, backoff 5 s → 30 s), with `Application.internetReachability` as a fast pre-check.
- While Offline, when the API key is missing, or when the budget cap is reached:
  - A PTT turn goes to `IOfflineResponder` (state Idle → Speaking via `OfflinePttPressed`, since cloud STT is unavailable): a canned line (baked clip plus caption) by category.
  - The `IdleScheduler` keeps running, but only while Nubi is not walking or following.
  - Interaction triggers (gaze dwell, open palm, poke) play rate-limited canned lines.
- A LAN Glimmer host behind `IChatModel` (OpenAI-compatible base URL) is cut for the hackathon (post-hackathon backlog).

## Consequences
- The demo survives Wi-Fi loss. The "Wi-Fi off" beat is part of the video.
- Offline detection takes ≤ 2 s after a failed call.

## Alternatives
- **On-device small LLM:** licence and perf work, out of scope.
- **`internetReachability` alone:** it reports Wi-Fi without internet as reachable.
