# 0022. Audit fixes: anchor erase, self-test memory, spike assemblies, log redaction

- Status: accepted
- Date: 2026-11-01

## Context
The post-merge audit (architecture, security/privacy, code quality) found gaps between what the README promises and what the code does:
- **Clear memory** deleted `scene.json`, but the anchors of taught places and seen objects stayed persisted on the headset. A replaced or evicted place also left its anchor behind.
- The device self-test ran against the user's real memory: the stored summary went into paid self-test requests, and the scripted turns were saved into the Companion persona's conversation file.
- The `Jolly.Spikes` assembly (camera-frame and mic dumps) was compiled into every release APK, so "release builds contain no frame-capture code" was false.
- `RedactingLog` masked the message but passed the exception through untouched, and provider exceptions carry the server's error text verbatim.
- Place names (model tool arguments) and the rolling summary (model output) reached the `<context>` block unsanitised and could close it.

## Decision
- **`ISpatialAnchors.Erase(anchorId, ct)`** (contract change). `ArSpatialAnchors` removes the live anchor and calls `ARAnchorManager.TryEraseAnchorAsync` when the provider supports erase. `SceneMemory` erases, in the background and logging failures, the anchor of a replaced place, an evicted place or object, and every anchored entry on `Clear`.
- **Self-test memory** lives in `persistentDataPath/selftest/memory/`, emptied at the start of each armed run (`AppPaths.RedirectMemory`). Usage totals and the install id stay shared: the self-test spends real money and must count against the cap.
- **Spike assemblies** are define-constrained to `JOLLY_SPIKES`; a spike build sets the symbol (`docs/research/device-spike.md`).
- **`RedactingLog`** folds `exception.ToString()` into the message, redacts the whole text, and passes no exception to the sink.
- **Context block:** place names get the label sanitiser (`PromptText.Sanitize`, then lower case), also when loaded from disk; `<` and `>` are removed from the summary before it is placed in the block.
- **`ApiUsageMeter.Flush`** catches `IOException`/`UnauthorizedAccessException`, logs `evt=usage.save_failed` and keeps the state dirty, so a full disk can no longer throw out of `OnDestroy` and skip the graph's disposal.

## Consequences
- Every `ISpatialAnchors` implementation needs `Erase`; the only ones are `ArSpatialAnchors` and the test fake.
- Erase is fire-and-forget from the synchronous `ISceneMemory` methods, so an erase that fails (or a provider without erase) leaves the anchor on the device; it is logged.
- Exceptions now reach logcat as part of an Error line rather than as a separate exception entry.
- The history still holds the removed mascot renders and the first self-test clips (see the audit notes in `review-log.md`); they must be dropped by a history rewrite before the repository is first pushed.

## Alternatives
- **Making `ISceneMemory.Clear` async to await the erases:** rejected; it would ripple through `MemoryKeeper` and the Settings flow for no user-visible gain, since the erase result is not shown anyway.
- **A separate data root for the self-test:** rejected; it would also split the usage meter, and the self-test's spend must count against the device cap.
