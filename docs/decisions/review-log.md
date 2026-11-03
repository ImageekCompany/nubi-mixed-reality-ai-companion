# Review log: architecture blueprint, 2026-11-01

Three reviews (feasibility F, quality Q, judge J) of `architecture.md`, `contracts.md`, `scope.md`, `definition-of-done.md` and the ADRs. Each finding was checked against package source in `JollyXR/Library/PackageCache` or a live probe before acting. The resulting contract changes are summarised in ADR-0016.

Live probes run for this pass (≈ $0.01, `docs/api-spend.md`):
- Cross-turn replay of `function_call` + `function_call_output` without its reasoning item: 200, coherent answer. Orphan `function_call` without output: 400 "No tool output found for function call".
- SAM `mug handle` / `handle` on `mug_1280.jpg`: one box on the handle each, c≈0.97.
- ASR file endpoint on Wit-synthesised "Hi Nubi, who are you?": heard "Hi newbie, who are you?".
- Vision with a streamed `LABEL|NOUN_PHRASE` header, `pen_640.jpg`: minimal first text 1.51 s, total 1.61 s; low first text 2.44 s.

## Rejected or modified

| Finding | Decision | Rationale |
|---|---|---|
| Q19 (part): put `AtomicFile` behind an `IFileStore` port with a fake | Rejected | EditMode tests can write to a temp directory; a port adds an interface with exactly one real implementation and no portability gain. `AtomicFile` itself is accepted. |
| Q28 (part): move memory files from `persistentDataPath` to `getFilesDir()` | Rejected | `secrets.json` must stay adb-pushable to the same app directory, scoped storage already blocks other apps, and adb/MTP access needs a developer-mode device the user controls. The GUID-based `safety_identifier` part is accepted. |
| J6 (part): use Spark's grounding box as a SAM box-prompt fallback | Modified | The streamed `LABEL\|PHRASE` format (J4) drops the box, and a box inside that header is unverified. The fallback re-runs SAM with the header's noun phrase as a text prompt, which is verified live. Hand-ray point + negative hand points (J1, J6 first step) are accepted. |
| F8 (option): Exclusive waits for Locomotion but locomotion returns `started` | Modified | Took both halves: Exclusive ignores Locomotion **and** all locomotion tools return on start (`walking`/`unreachable` + ETA). Arrival is not reported to the model. |
| F12 (option): keep the streaming `AudioClip` and only add `audioSource.Stop()` to `Flush()` | Rejected | It does not fix the early `FirstSamplePlayed` timestamp. The `OnAudioFilterRead` path is chosen; uLipSync ordering is checked in R3 and pinch → silence in R2. |
| Q6 (part): cap the total voice-turn retry budget at about 6 s | Modified | Set to 6 s first-event timeout × 2 attempts (≤ 13 s with backoff), which stays below the 20 s Thinking watchdog without failing a single slow-but-healthy first event. |
| Q18: add `Unauthorized` and `BudgetReached` | Accepted as enum values on `Connectivity` | A separate status enum would duplicate the routing switch; both new values route like Offline. |
| J7 (part): carry the memory summary over on a persona switch | Modified | Carried only between personas with `PersistMemory=true`; never into or out of Psychologist, whose sessions must not leak. |
| J12 (part): build an eval set despite "no frames on disk" | Modified | Allowed only in a Development build under `JOLLY_DEBUG_CAPTURE`, of the developer's own room, sent only on the LiveUser route; release builds are checked for the define (DoD §2). |
| J3 (part): move persona swap and `remember_place` out of the demo | Accepted as optional beats | They stay runnable in DoD §6 but are not required for the 10 clean runs. |

## Accepted (applied)

| Finding | Where |
|---|---|
| F1 `CancelAfter` resumes off the main thread; ban `cancelImmediately:true` | contracts §0, architecture §11, ADR-0008, DoD §1 grep. Verified `Channel.cs:295,307-311`, `CancellationTokenSourceExtensions.cs:22-27`, `UnityAsyncExtensions.cs:1084-1091,1149-1152` |
| F2 `UniTask.Linq` missing | architecture §2, contracts §0, ADR-0008. Verified `Runtime/Linq/UniTask.Linq.asmdef`, `Create.cs:7-9` |
| F3 Localization handle needs `UniTask.Addressables` + `Unity.ResourceManager`; preload then sync get | architecture §2/§10, ADR-0014. Verified asmdefs, `LocalizedStringDatabase.cs:146`, `LocalizedDatabase.cs:470` |
| F4 Camera manager must start whatever the permission result (passthrough lives in its `Start()`) | ADR-0003, architecture §11 Performance, DoD §3. Verified `MetaOpenXRCameraSubsystem.cs:438-466` |
| F5 Depth-only raycasts; gate scene managers on `USE_SCENE` | contracts §7 `IWorldQuery`, architecture §5.3/§5.5, ADR-0003. Verified `MetaOpenXRRaycastSubsystem.cs:72-90,136,232` |
| F6 / Q9 STT backlog pacing | contracts §5 `ISpeechToText`, architecture §5.1, WP3 tests |
| F7 / Q1 Barge-in stops walking | contracts §7 `TryStartMoveTo`/`WhenArrived`, §4.2 note, architecture §5.2/§5.5, WP6 test |
| F8 Exclusive behind Locomotion | contracts §4/§4.2, architecture §7 |
| F9 Newtonsoft cannot round-trip `Vector3`/`Pose`/private setters | contracts §0, architecture §8, ADR-0015 |
| F10 / Q19 `File.Replace` on first save | contracts §9 `AtomicFile`, architecture §8 |
| F11 / Q10 `PcmChunk` struct double return | contracts §5 (pooled sealed class), WP7 test |
| F12 Streaming-clip read-ahead | architecture §11 Audio output, ADR-0007, R2/R3 criteria |
| F13 / Q8 Timeout conflict | one table: SAM 5 s, vision 8 s, tool 12 s (architecture §5.3/§11, contracts §4.2) |
| F14 / Q6 State machine gaps | architecture §6 (latched flags, TurnFailed from all busy states, watchdogs for Listening/Thinking/Acting/Speaking that call `Interrupt()`, offline transition), contracts §9 |
| F15 Box tops walkable | architecture §5.5 (`NavMeshModifier` Not Walkable, floor-height filter), contracts §7. Verified `NavMeshModifier.cs:52` |
| F16 `csc.rsp` location | architecture §2/§4, DoD §2, WP0 |
| F17 `DownloadHandlerScript` per-callback allocation | architecture §11, ADR-0006 |
| F18 Convert at target size with mirror flag | contracts §6 (`Capture(maxSide)`, encoder only encodes), architecture §11, ADR-0003 |
| F19 / Q27 `JsonString` escaping, null output | contracts §4 |
| Q2 / J7 `set_persona` cancels its own turn | contracts §8 `RequestSwitch`/`ApplySwitch`, §4.2, architecture §3 |
| Q3 No Core → Presentation ports; no offline responder interface | contracts §8/§8.1 (`IConversationEvents`, `IMaskPresenter`, `IStatusSink`, `IOfflineResponder`), §7 `IInteractionSignals` |
| Q4 `ConversationController` god class | contracts §9 (`TurnRouter`, `TurnPipeline`, `BargeInCoordinator`, thin controller; `SpeechQueue` no longer takes `ICannedLines`/`ILocalizer`) |
| Q5 Memory inconsistent on cancel; double append; orphan calls | contracts §3/§8 `CommitTurn`, architecture §5.2/§7; orphan 400 and cross-turn replay verified live |
| Q7 Idle scheduler cancels follow/walk | contracts §7 `LocomotionMode`, §9 `IdleScheduler`, architecture §5.4 |
| Q11 `CameraFrame` buffer reuse / length / disposal | contracts §6 |
| Q12 No disposal surface; pause flush; static event unsubscribe | contracts §0/§2, architecture §3 |
| Q13 Usage meter under-counts | contracts §2 (`TryReserve`, `RecordEstimate`, `Flush`), architecture §8 |
| Q14 ToS guard stripped in release | contracts §3/§9 `MetaModelResolver`, architecture §7, ADR-0004 |
| Q15 `[Conditional]` on interface members | architecture §11 Logging |
| Q16 Prompt injection via scene context | architecture §7 (user-role data item, sanitised labels, template summaries, untrusted-camera rule), contracts §8 |
| Q17 / J5 Vision skips persona and safety | contracts §6 `IdentifyRequest.PersonaInstructions`, §4 `ToolContext.PersonaInstructions` |
| Q20 Object ids reused; stale unanchored positions | contracts §8, architecture §8 |
| Q21 Pinch PTT vs UI select and system gesture | contracts §5 `IVoiceActivation`, architecture §11 Accessibility |
| Q22 Seated mode and speech rate | contracts §8 `IUserPreferences`, §4 `DefinitionsFor`, architecture §5.5/§11 |
| Q23 `Flush` vs pending `Play` | contracts §5 |
| Q24 Assembly graph errors | architecture §2 (RetryPolicy in Net, MetaApi no longer → Core, `CameraMath` in Core/Spatial, `EditorStillFrameSource` in Jolly.XR) |
| Q25 Overlapping ownership, unowned work | scope §3 (per-owner tables, per-asmdef `link.xml`, `Fakes/WPn`, `Audio/Common` → WP0, `MetaApi/Common` → WP1, detectors/Editor/Build/ProjectSettings → WP5, Addressables settings → WP8, validators → WP9, asmdef-graph test → WP0) |
| Q26 APK may ship without localization tables | architecture §10, ADR-0014, DoD §3. Verified `AddressableAssetSettings.cs:2047`, `AddressablesPlayerBuildProcessor.cs:156-168` |
| Q28 Device-tied safety identifier | architecture §11 (random per-install GUID) |
| J1 Creative Meta AI usage only in should tier | scope §1 (hand-ray + negative points must if R1 passes), architecture §5.3 |
| J2 DoD demo needs should-tier features; "kitchen" outside the NavMesh | DoD §6 (required vs optional beats; "sofa") |
| J3 Demo too long / weak objects / hero user missing | DoD §6, scope §1 (6 beats, TV remote, older adult), WP12 |
| J4 Late, non-streaming capture | pre-capture at PTT release (contracts §3 `UserTurn.PreCaptured`), streamed `LABEL\|PHRASE` vision with `minimal` (contracts §6), DoD §5 target tightened to 2.5 s. Verified live |
| J8 Device validation bottleneck | scope §2/§3: the existing `DeviceSpike` APK already bundles R1/R2/R4/R5; one session on Nov 2 AM; thin E2E slice on Nov 2 |
| J9 Recording/casting during camera use | R1 pass criteria |
| J10 "Stop" has LLM latency | contracts §9 `StopGuard`, architecture §5.1 |
| J11 "Nubi" misheard | contracts §9 `TranscriptAliases`; verified live ("Hi newbie") |
| J12 Budget barely used for quality | scope §2 R6 (≈ $5 vision/SAM eval incl. `muse-wonder-1.0`) |
| J smaller fix: occlusion | scope §1 Should 3, architecture §5.5 `SceneOccluders` |
| J reorder should tier; cuts; badges + HUD to must | scope §1 |
| J additions A `find_object`, B part highlighting | contracts §4.2, architecture §5.3.1, scope §1. Part phrase verified live |

# Integration: WP1–WP10 merge, 2026-11-02

Deviations reported by the work packages are accepted in ADR-0017; the architecture text is amended to match.

| Item | Decision | Where |
|---|---|---|
| WP2: commit text on interrupt comes from `TurnSpeech`, not `SpeechQueue.SpokenSoFar` | Comment-only contract fix | `Contracts/ChatAgent.cs` `IAgentLoop.RunTurn` xmldoc, contracts §3 and §9 |
| WP5/WP8: image y sign differs between the architecture formula and the code | Code is right for a +Y-up Unity pose; contract comment pins the convention | `Contracts/CameraVision.cs` `CameraPoseWorld`, contracts §6, architecture §5.3, ADR-0017 |
| WP1: `response.incomplete` completes instead of throwing | Accepted | architecture §7, ADR-0017 |
| WP5: `NavMeshBuilder` instead of `NavMeshSurface` | Accepted | architecture §5.5, ADR-0017 |
| WP6: procedural look-at, `Speed` in m/s, ±40° head yaw, polled motion, editor code in the runtime assembly | Accepted; editor code moved to the new `Jolly.Editor.Character`, `Unity.Animation.Rigging` reference dropped | architecture §2/§5.5, `AssemblyGraphTests`, ADR-0017 |
| WP10/WP8: provider ids, health-probe 401, settings events | Documented, no contract code change | contracts §9.1 |
| Shared `PixelToCameraDir` in Contracts, `IHealthProbe.Ping` returning a status | Deferred to WP9 (contract code changes not needed to compile) | ADR-0017 |

# Post-merge audit, 2026-11-01

Three audits (Unity architecture, security/privacy, code quality) over `main` at `d18420c`. Fixed on `fix/audit`; decisions in ADR-0022.

| Finding | Decision | Where |
|---|---|---|
| Usage save throws out of `OnDestroy`, skipping teardown | Fixed: logged, retried on next flush | `ApiUsageMeter.Flush` |
| Interaction line's `finally` touches a destroyed character on teardown | Fixed: no hand-back once the app token is cancelled | `InteractionLinePlayer` |
| Jolly mascot renders committed | Fixed in the tree; the history rewrite before the first push is left to the owner (destructive) | `docs/research/samples/` |
| macOS `say` clips reachable from history | Not rewritten here (destructive); same pre-push rewrite, objects from `e280be6` | git history |
| Persisted anchors never erased | Fixed: `ISpatialAnchors.Erase` | `SceneMemory`, `ArSpatialAnchors` |
| Spike assembly in release APKs | Fixed: `JOLLY_SPIKES` define constraint | `Assets/Spikes/*.asmdef` |
| Self-test uses the user's memory | Fixed: scratch memory directory | `SelfTestSession`, `AppPaths` |
| Place names and summary can close `<context>` | Fixed | `SceneMemory`, `PromptBuilder` |
| Exceptions bypass redaction | Fixed | `RedactingLog` |
| "Key read per request" comment false | Fixed comment; `push-secrets.sh` force-stops the app | `MetaApiEndpoint`, ADR-0010 |
| `.env` world-readable | Fixed locally (`chmod 600`); script warns | `push-secrets.sh` |
| Wit.ai output licence unreviewed | Terms read and recorded; clips ship under the Wit licence, not MIT | `THIRD_PARTY.md`, `licenses.md` |
| Duplicated fakes; ADR-0018 reason | Partly right: aliases did exist (the audit's grep missed indented `using`s) and several pairs did differ, but others were copies or unused. Merged; distinct names for real variants | `Tests/Shared/Fakes/Common`, ADR-0018 |
| ASR fake meter allows a double settle | Fixed: `RecordingUsageMeter` | ASR tests |
| File-ASR fallback never wired | Removed (R4 passed) | ADR-0005 |
| architecture.md names missing types | Fixed | architecture §2, §5.4, §5.5 |
| Health probe URL comment | Fixed | `MetaHealthProbe` |
| `MetaApiEndpoint.Get` unused, probe duplicates auth | `Get` removed; the probe keeps its status-only seam (`IHttpStatusClient`) | `MetaApiEndpoint` |
| `ClipPcm` third resampler | Fixed: uses `Resampler`, tested | `ClipPcm` |
| `LogDebug` unused, README claim | Docs reworded; the extension is kept as the only allowed path for text logging | README, architecture §11 |
| Tests bake a different NavMesh | Fixed: production `NavMeshBaker` | `CharacterTestRig` |
| `Jolly.XR` references `Unity.AI.Navigation` | Removed (and from the PlayMode tests) | asmdefs, `AssemblyGraphTests` |
| Two `<summary>` on `IApiUsageMeter` | Merged | contracts §2 |
| CHANGELOG gaps | Fixed | CHANGELOG |
| Smoke test asserts only Unity | Fixed: `LogAssert.NoUnexpectedReceived` after destroy | `PlayModeSmokeTests` |
