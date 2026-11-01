# Architecture decision records

| # | Decision | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions (MADR-lite) | accepted |
| [0002](0002-openxr-meta-openxr-over-meta-xr-sdk.md) | OpenXR + meta-openxr + AR Foundation instead of the Meta XR SDK; package pins | accepted |
| [0003](0003-passthrough-camera-via-meta-openxr.md) | PCA through the meta-openxr camera subsystem, with Camera2 extrinsics | accepted, pending spike R1 |
| [0004](0004-meta-model-api-responses-and-model-routing.md) | Responses API (`store:false`); standard for live turns, contributor only for synthetic | accepted |
| [0005](0005-stt-muse-realtime-push-to-talk.md) | Muse realtime ASR with pinch push-to-talk | accepted, pending spike R4 |
| [0006](0006-tts-wit-raw-http.md) | Wit `/synthesize` over raw HTTP | accepted |
| [0007](0007-lipsync-ulipsync.md) | uLipSync, with an amplitude fallback | accepted |
| [0008](0008-async-unitask.md) | UniTask 2.5.11 and `IUniTaskAsyncEnumerable` | accepted |
| [0009](0009-plain-composition-root.md) | Plain composition root, no DI container | accepted |
| [0010](0010-secrets-provisioning.md) | Secrets in `persistentDataPath/secrets.json` (device) and `.env` (Editor) | accepted |
| [0011](0011-original-character-nubi.md) | Original CC0 character "Nubi" | accepted |
| [0012](0012-offline-strategy.md) | Offline/degraded mode: health probe, canned lines, idle scheduler | accepted |
| [0013](0013-test-strategy.md) | Test strategy: NUnit plus hand-written fakes, fixtures, gated live smoke tests | accepted |
| [0014](0014-localization.md) | Unity Localization, with a locale → ASR/TTS map | accepted |
| [0015](0015-json-and-wire-parsing.md) | Newtonsoft (bundled), reader-based parsing on hot paths | accepted |
| [0016](0016-review-revision-2026-11-01.md) | Pre-freeze contract revision after the architecture reviews | accepted |
| [0017](0017-integration-deviations-wp1-wp10.md) | Deviations accepted when merging WP1–WP10 | accepted |
| [0018](0018-wp9-integration-and-device-self-test.md) | WP9 composition, provider edges, line-clip asset, shared helpers and the device self-test | accepted |
| [0019](0019-perf-latency-warmups-and-prompt-cache.md) | Launch warm-ups (pipelines, camera, ASR TLS), prompt cache key, self-test frame diagnostics | accepted |

Review findings and their accept/reject decisions: [review-log.md](review-log.md).
