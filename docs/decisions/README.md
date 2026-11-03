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
| [0020](0020-summary-budget-and-prompt-cache-order.md) | Summary token budget, truncation-safe summaries and a cache-stable prompt order | accepted |
| [0021](0021-controller-push-to-talk.md) | Controller push-to-talk: hold A or X, bound by device layout | accepted |
| [0022](0022-audit-fixes-anchors-selftest-memory-spikes.md) | Audit fixes: anchor erase, self-test memory, spike assemblies, log redaction | accepted |
| [0023](0023-turn-gc-websocket-upgrade-and-wit-warmup.md) | Turn GC: own WebSocket upgrade, packed ASR frames, slim Responses envelopes; Wit warm-up | accepted |
| [0024](0024-real-world-occlusion-and-contact-shadow.md) | Real-world occlusion (environment depth, scene-mesh fallback) and a feet-driven contact shadow | accepted |
| [0025](0025-mr-room-awareness.md) | Room awareness: Space Setup offer, fallback floor, real furniture | accepted |
| [0026](0026-swappable-characters-and-local-demo-skins.md) | Swappable characters: a definition asset per character, local demo skins that never reach git | accepted |
| [0030](0030-hands-interaction-rig.md) | Hand-tracked XR rig: the XRI Hands Interaction Demo rig variant | accepted |
| [0031](0031-character-voice-profile.md) | Character voice as data (`VoiceProfile`, persona delivery); Nubi speaks as Wit Rubie, pitch 110 | accepted |
| [0032](0032-greeting-stays-put-text-done-and-staggered-launch.md) | Device-regression fixes: a greeting stays put, a text item ends speech, XR managers start one per frame | accepted |
| [0033](0033-selftest-sam-text-tools-and-hand-debounce.md) | Device self-test for `highlight_part` and `find_object`; hand meshes follow a 200 ms debounced tracked state | accepted |
| [0034](0034-latency-turns-and-stale-frames-are-device-state.md) | `selftest.sh --turns N` for a 20-turn latency sample; stale frames are headset state (reboot before a demo); the camera keeps its post-graph start | accepted |
| [0035](0035-positive-movement-and-find-tool-descriptions.md) | Positive `come_here` / `find_object` triggers and a live tool-selection eval; `come_here` still missed 3 of 5 at `minimal` | accepted |
| [0036](0036-local-come-here-guard.md) | A local `ComeGuard` starts `come_here` on a short come request before the model; self-test runs log each model reply (`evt=llm.reply`) | accepted; the model round after a guard walk superseded by 0037 |
| [0037](0037-guard-walk-skips-the-model-and-find-object-gaze.md) | A guard walk answers with a canned ComingOver line and no model round; `find_object` results say Nubi is already looking | accepted; the kept release acknowledgement superseded by 0040 |
| [0038](0038-hedged-first-event.md) | A voice request with no first event after 2.5 s is hedged with an identical second request; the sequential voice retry is dropped | accepted |
| [0039](0039-dark-room-vision-guard.md) | A camera frame below mean luma 25 skips SAM and Spark (`too_dark`); the model asks for light in one sentence | accepted |
| [0040](0040-acknowledge-questions-only.md) | The acknowledgement plays at the final and only for a question the guards left to the model; commands and guard walks get none | accepted |
| [0041](0041-attend-to-the-user.md) | The character attends the user in every conversation turn and when near: body faces them past 15°, a tool's look at a point holds 2.5 s, `evt=look.user` per turn | accepted |
| [0042](0042-settings-on-demand.md) | Settings on demand: hidden at launch; wrist button, left menu button or "open settings"; placed once in front, world-locked; closes on X, talk, 20 s idle; side views pulled into the recorded frame | accepted; "Side views" superseded by 0050 |
| [0043](0043-name-as-heard-in-local-guards.md) | Local guards take one unknown word set apart by punctuation as the name as heard; `voice_come_here` plays the selected character's own line | accepted |
| [0044](0044-point-and-tap-to-move.md) | Point and tap to move: a ring on walkable floor under the hand or controller ray; with it showing a pinch under 0.25 s moves (no ASR, no model), longer talks with the microphone pre-rolled from the pinch; the trigger moves | accepted |
| [0045](0045-shell-fur.md) | Shell fur: `Jolly/Character/ShellFur` shells appended as extra materials on the skinned renderer's last submesh (skinned once), alpha-tested with alpha = visibility, 3 distance levels, spring inertia | accepted |
| [0046](0046-model-facing.md) | `CharacterDefinition.modelYaw` turns the model to face the root's +z (Nubi and Jolly export facing -z: 180); the validator and AttentionTests measure the rendered face (eye-skinned vertices), not the bones | accepted |
| [0047](0047-character-action-catalog.md) | The character's body actions are tools in the `actions` namespace (`CharacterActionCatalog`): jump, dance, bow, spin, celebrate, move_relative, each offered only when the character can play the clips it needs (`ICharacterController.CanPlay`); full-body clips on an unmasked Action layer, optional per character; the instructions say to call an action, never to claim one | accepted |
| [0049](0049-keyed-test-build.md) | `build.sh --embed-secrets` puts the keys in a private test APK's processed scene only; a pushed `secrets.json` wins; normal builds stay key-free | accepted |
| [0048](0048-web-search-grounding.md) | Meta's built-in `web_search` (low context, `max_tool_calls` 2) for Online personas that allow `web_search`; location never finer than a city (`set_home_location` or the device locale, IANA time zone); local time in the per-turn context block; `SpokenText` strips markdown and citations; tool namespaces sent eagerly | accepted |
| [0050](0050-demo-layout-from-the-measured-window.md) | Head-following views placed in the measured off-centre recording window (+13.6°, −24.1°); captions raised to y = +0.08 m with a `ZTest Always` material over the character, one-line tool badges the same way (amends 0024 for those two); latency HUD off unless the new Settings row is on | accepted |
| [0051](0051-remember-a-shown-place-and-point-at-it.md) | `remember_place` takes where the user is showing; `show_place` and `capture_and_identify` lead the way and point; `PlaceGuard` runs the plain place phrases with no model round | accepted |

Numbers 0027–0029 are unused and were not reserved on a dropped branch: no commit on any branch (`git log --all`) adds or mentions an ADR with those numbers.

Review findings and their accept/reject decisions: [review-log.md](review-log.md).
