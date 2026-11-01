# Contracts (`Jolly.Contracts`)

These are the exact public signatures that every coding agent implements or consumes. WP0 copies them verbatim into `JollyXR/Assets/Jolly/Runtime/Contracts/` as one file per section, all in namespace `Jolly.Contracts`. Changing a signature after WP0 requires an ADR note and a list of the callsites it affects. Revised before the freeze on 2026-11-01 after three reviews; accepted and rejected findings are in `docs/decisions/review-log.md`.

## 0. Conventions

- **Async:** `Cysharp.Threading.Tasks.UniTask` / `UniTask<T>` everywhere. Never `Task`, never `async void`. Every async method takes a `CancellationToken ct` as its last parameter.
- **Streams:** `IUniTaskAsyncEnumerable<T>` and nothing else. Consume them with `await foreach (var e in stream.WithCancellation(ct))` (UniTask, verified).
  - Why this and not `System.Collections.Generic.IAsyncEnumerable<T>`:
    - It is UniTask-native: no `ValueTask` or `ManualResetValueTaskSourceCore` plumbing.
    - It comes with UniTask's LINQ (`Select`, `Where`, `Merge`).
    - It composes with `UniTask.WhenAll`, and `WithCancellation` is built in.
  - Why not channels in the public surface: a `Channel<T>` exposes two ends and a completion protocol, which is a leakier contract. Adapters are free to *implement* a stream with `Channel.CreateSingleConsumerUnbounded<T>()` and expose `reader.ReadAllAsync()`. Both APIs are verified in UniTask's `Channel.cs`.
  - **Assembly:** `IUniTaskAsyncEnumerable<T>`, `WithCancellation` and `Channel` are in the core `UniTask` assembly. `UniTaskAsyncEnumerable.Create/Merge/Select/Where` are in the separate **`UniTask.Linq`** assembly (namespace `Cysharp.Threading.Tasks.Linq`, verified `Runtime/Linq/UniTask.Linq.asmdef`). Every asmdef that produces or composes streams references both. `autoReferenced:true` does not reach other asmdefs.
- **Threading:** every stream and every `UniTask` returned by a contract **resumes on the main thread**. Adapters that work off-thread switch back with `await UniTask.SwitchToMainThread(ct)` before yielding.
  - **Timeouts never use `CancellationTokenSource.CancelAfter`.** It cancels from a thread-pool timer, and UniTask's `Channel` reader runs `TrySetCanceled` synchronously in the cancel callback (`Channel.cs:295,307-311`), so the `await foreach` consumer would resume on the timer thread. Use `cts.CancelAfterSlim(...)` (PlayerLoop timer, `CancellationTokenSourceExtensions.cs:22-27`) or UniTask `TimeoutController`. `tools/ci/test.sh` greps `Assets/Jolly` for `\.CancelAfter\(` and fails on a hit.
  - Never pass `cancelImmediately:true` to `UnityWebRequestAsyncOperation.ToUniTask`: its registration calls `webRequest.Abort()` on the cancelling thread (`UnityAsyncExtensions.cs:1084-1091`). The default aborts inside `MoveNext` on the PlayerLoop (`:1149-1152`).
- **Errors:**
  - Providers throw `ProviderException`.
  - Cancellation is `OperationCanceledException`.
  - "No result" is a value, not an exception: an empty list, `false`, or `Outcome.*`.
- **Ownership:** a `PcmChunk` or `CameraFrame` received from a stream is owned by the receiver, which must `Dispose()` it. Both are classes and `Dispose` is idempotent per instance; a value copy cannot double-return a pooled buffer.
- **Disposal:** every service that owns a socket, request, native buffer, timer loop, file debounce or static-event subscription implements `IDisposable`. `JollyBootstrap` keeps a disposable list and disposes it in reverse construction order.
- **Persistence:** contract types are never passed to `JsonConvert`. `Vector3`/`Pose` self-reference through `normalized` and loop-fail in Newtonsoft, and `private set` properties deserialize silently as defaults. Stores map to Core persistence DTOs (public fields, floats) by hand; see architecture §8.
- **Units:** metres, seconds and Unity world space. Pixel coordinates have a top-left origin, on the *encoded* image, unless noted.

## 1. Common

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Jolly.Contracts
{
    public interface IClock
    {
        /// <summary>Monotonic seconds since startup (latency, scheduling). Never use for wall time.</summary>
        double Now { get; }
        DateTimeOffset UtcNow { get; }
        UniTask Delay(TimeSpan delay, CancellationToken ct);
    }

    public interface IRandom
    {
        float Range(float minInclusive, float maxInclusive);
        int Range(int minInclusive, int maxExclusive);
    }

    public enum LogLevel { Debug, Info, Warning, Error }

    public interface ILog
    {
        bool IsEnabled(LogLevel level);
        /// <summary>message is logfmt: "evt=name key=value ...". Never pass secrets, headers, bodies or base64.</summary>
        void Write(LogLevel level, string category, string message, Exception exception = null);
    }

    public interface ISecretsProvider
    {
        /// <summary>False when absent or empty. Never throws for a missing key. Values must never be logged.</summary>
        bool TryGet(string key, out string value);
    }

    public static class SecretKeys
    {
        public const string MetaApiKey = "META_API_KEY";
        public const string WitClientToken = "WIT_AI_CLIENT_TOKEN";
        public const string WitServerToken = "WIT_AI_SERVER_TOKEN"; // editor tooling only, never needed on device
        public const string WitAppId = "WIT_AI_APP_ID";
    }

    public enum ProviderErrorKind { Network, Timeout, RateLimited, Server, Unauthorized, BadRequest, BudgetExceeded, Protocol }

    public sealed class ProviderException : Exception
    {
        public ProviderException(string provider, ProviderErrorKind kind, string message,
                                 int httpStatus = 0, string requestId = null, TimeSpan? retryAfter = null,
                                 Exception inner = null) : base(message, inner)
        { Provider = provider; Kind = kind; HttpStatus = httpStatus; RequestId = requestId; RetryAfter = retryAfter; }

        public string Provider { get; }
        public ProviderErrorKind Kind { get; }
        public int HttpStatus { get; }
        public string RequestId { get; }
        public TimeSpan? RetryAfter { get; }
        public bool IsTransient => Kind is ProviderErrorKind.Network or ProviderErrorKind.Timeout
                                        or ProviderErrorKind.RateLimited or ProviderErrorKind.Server;
    }

    public enum LatencyMark { PttPressed, PttReleased, AsrFinal, LlmRequestSent, LlmFirstEvent, LlmFirstText,
                              FirstSentenceQueued, TtsFirstByte, FirstAudioPlayed, MaskShown, TurnCompleted, BargeIn }

    public interface ILatencyTracker
    {
        void BeginTurn(int turnId);
        void Mark(int turnId, LatencyMark mark);
        /// <summary>Milliseconds between two marks of the last completed turn; NaN if either is missing.</summary>
        double Between(LatencyMark from, LatencyMark to);
    }
}
```

## 2. Usage and budget

```csharp
namespace Jolly.Contracts
{
    public readonly struct UsageRecord
    {
        public UsageRecord(string model, long inputTokens, long cachedInputTokens, long outputTokens,
                           int images = 0, double audioSeconds = 0)
        { Model = model; InputTokens = inputTokens; CachedInputTokens = cachedInputTokens;
          OutputTokens = outputTokens; Images = images; AudioSeconds = audioSeconds; }

        public string Model { get; }
        public long InputTokens { get; }       // includes cached
        public long CachedInputTokens { get; }
        public long OutputTokens { get; }      // includes reasoning tokens
        public int Images { get; }             // SAM images
        public double AudioSeconds { get; }    // ASR
    }

    /// <summary>
    /// Persisted, cross-session spend meter for the project's Meta API budget.
    /// Prices come from JollyConfig (per-model table); unknown models are priced at the most expensive known rate.
    /// HardCapUsd is a per-device sub-budget of the project's $140; docs/api-spend.md is the project-wide ledger.
    /// </summary>
    public interface IApiUsageMeter : IDisposable
    {
        decimal SpentUsd { get; }
        decimal HardCapUsd { get; }
        bool IsCapReached { get; }
        /// <summary>Call before every billable request. Reserves estimatedUsd (Spent + Reserved + estimate must fit the cap).
        /// False means refuse: throw ProviderException(BudgetExceeded).</summary>
        bool TryReserve(decimal estimatedUsd, out long reservationId);
        /// <summary>Settles the reservation with actual usage, persists (debounced to 2 s), returns this record's cost.</summary>
        decimal Record(long reservationId, in UsageRecord usage);
        /// <summary>Settles a request aborted before response.completed (barge-in, timeout) with an estimate:
        /// input chars / 4 and the output deltas seen. Input tokens are assumed billed on abort (speculation, conservative).</summary>
        decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens);
        /// <summary>Writes pending state now. Called from OnApplicationPause(true)/OnApplicationFocus(false); Quest does not reliably call OnDestroy.</summary>
        void Flush();
        event Action<decimal> SpentChanged;
    }
}
```

## 3. Chat model and agent loop

```csharp
namespace Jolly.Contracts
{
    // LiveUser = standard model only (ToS §6.2); Synthetic may use -contributor.
    // Enforced unconditionally (not Debug.Assert, which is stripped from release) by MetaModelResolver in every MetaApi adapter.
    public enum ModelRoute { LiveUser, Synthetic }
    public enum ReasoningEffort { Minimal, Low, Medium }
    public enum FinishReason { Stop, ToolCalls, Length, Incomplete, Failed }

    public enum ItemKind { UserText, UserImage, AssistantText, ToolCall, ToolResult, Opaque }

    /// <summary>One conversation item. Opaque carries provider-specific JSON (e.g. Responses "reasoning" items) that is replayed verbatim within a turn and never persisted.</summary>
    public sealed class ConversationItem
    {
        public ItemKind Kind { get; private set; }
        public string Text { get; private set; }          // UserText, AssistantText
        public EncodedImage Image { get; private set; }   // UserImage
        public string CallId { get; private set; }        // ToolCall, ToolResult
        public string ToolName { get; private set; }      // ToolCall
        public string Json { get; private set; }          // ToolCall args, ToolResult output, Opaque payload

        public static ConversationItem User(string text) => new() { Kind = ItemKind.UserText, Text = text };
        public static ConversationItem UserImage(EncodedImage image) => new() { Kind = ItemKind.UserImage, Image = image };
        public static ConversationItem Assistant(string text) => new() { Kind = ItemKind.AssistantText, Text = text };
        public static ConversationItem Call(string callId, string name, string argsJson) =>
            new() { Kind = ItemKind.ToolCall, CallId = callId, ToolName = name, Json = argsJson };
        public static ConversationItem Result(string callId, string outputJson) =>
            new() { Kind = ItemKind.ToolResult, CallId = callId, Json = outputJson };
        public static ConversationItem Opaque(string providerJson) => new() { Kind = ItemKind.Opaque, Json = providerJson };
    }

    public sealed class ModelRequest
    {
        public ModelRoute Route { get; set; } = ModelRoute.LiveUser;
        public string Instructions { get; set; }
        public IReadOnlyList<ConversationItem> Input { get; set; }
        public IReadOnlyList<ToolDefinition> Tools { get; set; } = Array.Empty<ToolDefinition>();
        public ReasoningEffort Effort { get; set; } = ReasoningEffort.Minimal;
        public int MaxOutputTokens { get; set; } = 1024;  // includes reasoning tokens; do not cap tightly
        public string SafetyIdentifier { get; set; }
    }

    public sealed class ToolCallRequest
    {
        public ToolCallRequest(string callId, string name, string argumentsJson)
        { CallId = callId; Name = name; ArgumentsJson = argumentsJson; }
        public string CallId { get; }
        public string Name { get; }
        public string ArgumentsJson { get; }
    }

    public sealed class ModelTurnResult
    {
        public FinishReason FinishReason { get; set; }
        public string ResponseId { get; set; }
        /// <summary>All output items in order, including Opaque reasoning, for replay in the next round.</summary>
        public IReadOnlyList<ConversationItem> OutputItems { get; set; }
        public UsageRecord Usage { get; set; }
    }

    public enum ModelEventKind { Started, TextDelta, ToolCall, Completed, TextDone }

    public readonly struct ModelEvent
    {
        public ModelEvent(ModelEventKind kind, string text = null, ToolCallRequest toolCall = null, ModelTurnResult result = null)
        { Kind = kind; Text = text; ToolCall = toolCall; Result = result; }
        public ModelEventKind Kind { get; }
        public string Text { get; }                 // TextDelta; TextDone: none, the text item ended (a call may follow)
        public ToolCallRequest ToolCall { get; }    // ToolCall: emitted once its arguments are complete
        public ModelTurnResult Result { get; }      // Completed: always the last event
    }

    public interface IChatModel
    {
        /// <summary>
        /// Streams one model response. Cancelling aborts the HTTP request (there is no server-side cancel for streamed responses).
        /// Retries happen internally only before the first event. Records usage in IApiUsageMeter.
        /// </summary>
        IUniTaskAsyncEnumerable<ModelEvent> Stream(ModelRequest request, CancellationToken ct);
    }

    public sealed class UserTurn
    {
        public int TurnId { get; set; }
        public string Text { get; set; }          // after TranscriptAliases ("newbie" → "Nubi")
        public string LanguageTag { get; set; }   // "en", "pl"
        /// <summary>Frame captured and encoded on device at PTT release (no API cost, kept in RAM only).
        /// Never sent unless a vision tool runs. Null when the camera is unavailable.</summary>
        public EncodedImage PreCaptured { get; set; }
    }

    /// <summary>Delayed: a model round has sent no first event yet within <c>AgentSettings.HoldLineAfter</c> (a slow
    /// server or a first-event timeout and retry); the pipeline fills the silence with a hold line. Carries no data.</summary>
    public enum AgentEventKind { SpeechDelta, ToolStarted, ToolCompleted, Completed, Failed, Delayed, SpeechBreak }

    public readonly struct AgentEvent
    {
        public AgentEvent(AgentEventKind kind, string text = null, string toolName = null, string callId = null,
                          ToolResult toolResult = default, ProviderException error = null)
        { Kind = kind; Text = text; ToolName = toolName; CallId = callId; ToolResult = toolResult; Error = error; }
        public AgentEventKind Kind { get; }
        public string Text { get; }               // SpeechDelta: text to speak (model deltas or ToolResult.SpokenText)
        public string ToolName { get; }
        public string CallId { get; }
        public ToolResult ToolResult { get; }     // ToolCompleted
        public ProviderException Error { get; }   // Failed (the turn ends after this)
    }

    public interface IAgentLoop
    {
        /// <summary>
        /// Runs one user turn: up to 4 model rounds, executing tool calls as they stream in.
        /// Buffers the turn's items and commits them once with IConversationMemory.CommitTurn when the turn ends.
        /// On cancellation it still commits: the user text, the assistant text actually spoken (spokenSoFar, read when ct fires)
        /// plus " [interrupted]", and {"error":"interrupted"} for every ToolCall without a ToolResult
        /// (Responses rejects an orphan function_call with 400 "No tool output found", live 2026-11-01).
        /// Provider failures surface as a Failed event; cancellation then throws OperationCanceledException.
        /// </summary>
        IUniTaskAsyncEnumerable<AgentEvent> RunTurn(UserTurn turn, Func<string> spokenSoFar, CancellationToken ct);
    }
}
```

## 4. Tools

```csharp
namespace Jolly.Contracts
{
    public enum ToolConcurrency
    {
        Parallel,     // runs alongside anything
        Locomotion,   // a newer Locomotion call supersedes the running one; returns once motion has started (see §4.2)
        Exclusive     // waits until the round's other non-Locomotion calls finish; never waits for Locomotion
    }

    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, string description, string parametersJsonSchema,
                              TimeSpan timeout, ToolConcurrency concurrency)
        { Name = name; Description = description; ParametersJsonSchema = parametersJsonSchema;
          Timeout = timeout; Concurrency = concurrency; }
        public string Name { get; }                  // ^[a-zA-Z0-9_.-]+$
        public string Description { get; }           // model-facing, English
        public string ParametersJsonSchema { get; }  // JSON Schema subset, see §4.1
        public TimeSpan Timeout { get; }
        public ToolConcurrency Concurrency { get; }
    }

    public interface IToolArguments
    {
        bool TryGetString(string name, out string value);
        bool TryGetNumber(string name, out double value);
        bool TryGetBool(string name, out bool value);
    }

    public readonly struct ToolResult
    {
        public ToolResult(bool ok, string outputJson, string spokenText = null, bool endsTurn = false)
        { Ok = ok; _outputJson = outputJson; SpokenText = spokenText; EndsTurn = endsTurn; }
        public bool Ok { get; }
        /// <summary>Sent to the model as function_call_output. default(ToolResult) yields "{}".</summary>
        public string OutputJson => _outputJson ?? "{}";
        readonly string _outputJson;
        /// <summary>When set, the agent speaks it immediately. Null when the tool already streamed its speech via ToolContext.Speech.</summary>
        public string SpokenText { get; }
        /// <summary>If every result in a round has EndsTurn, the agent skips the next model round (saves ~1–1.5 s).</summary>
        public bool EndsTurn { get; }

        public static ToolResult Success(string outputJson, string spokenText = null, bool endsTurn = false)
            => new(true, outputJson, spokenText, endsTurn);
        /// <summary>code is snake_case, e.g. "unknown_place", "camera_unavailable", "timeout", "invalid_arguments".</summary>
        public static ToolResult Failure(string code, string detail = null)
            => new(false, "{\"error\":" + JsonString(code) + (detail == null ? "" : ",\"detail\":" + JsonString(detail)) + "}");

        /// <summary>RFC 8259 string escaping: quote, backslash, and every char below U+0020 (\n \r \t short forms, others \uXXXX).</summary>
        public static string JsonString(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length + 2).Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }

    /// <summary>Lets a tool stream speech into the turn's SpeechQueue (e.g. the vision answer) before it returns.</summary>
    public interface ISpokenTextSink
    {
        void Append(string delta);
    }

    public sealed class ToolContext
    {
        public UserTurn Turn { get; set; }
        public PersonaProfile Persona { get; set; }
        /// <summary>persona instructions + safety text + language directive, for tools that make their own model call.</summary>
        public string PersonaInstructions { get; set; }
        public ISpokenTextSink Speech { get; set; }
        public IUserPreferences Preferences { get; set; }
    }

    public interface ITool
    {
        ToolDefinition Definition { get; }
        /// <summary>Arguments are already validated against the schema. Must honour ct and must not throw for expected failures; return ToolResult.Failure.</summary>
        UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct);
    }

    public interface IToolRegistry
    {
        void Register(ITool tool);                                     // throws on a duplicate name
        bool TryGet(string name, out ITool tool);
        /// <summary>Filtered by persona.AllowedTools; in seated mode walk_to, go_to_surface and follow_user are removed (come_here stays).</summary>
        IReadOnlyList<ToolDefinition> DefinitionsFor(PersonaProfile persona, IUserPreferences preferences);
        /// <summary>Validates against the tool's schema subset. On failure, error is a model-facing message.</summary>
        bool TryValidate(string name, string argumentsJson, out IToolArguments args, out string error);
    }
}
```

`ToolRegistry` (Core, `sealed class ToolRegistry : IToolRegistry`) parses each schema once at `Register`. A tool **must not** be registered without a definition that passes `JsonSchemaSubset.Validate` (WP2).

### 4.1 Supported JSON Schema subset

Only these keywords:
- `type: object` with `properties`, `required` and `additionalProperties:false`
- property types `string` (with optional `enum` and `maxLength`), `number` (with optional `minimum`/`maximum`) and `boolean`

Nothing nested. That covers the whole catalogue and keeps the validator at about 100 lines.

### 4.2 Tool catalogue

Wire format is the Responses flat function tool: `{"type":"function","name":…,"description":…,"parameters":<schema>}` (verified live). Parameter schemas:

| name | description (model-facing) | parameters | timeout | concurrency | result `output` JSON |
|---|---|---|---|---|---|
| `walk_to` | Walk Nubi to a named place the user taught, a seen object (its id like obj_3 or its label like 'mug') or a furniture type (table, couch, bed, storage, screen). | `{"type":"object","properties":{"place":{"type":"string","maxLength":64,"description":"Taught place name, seen object id or label (obj_3, mug), or furniture type"}},"required":["place"],"additionalProperties":false}` | 3 s | Locomotion | `{"status":"walking"\|"unreachable","place":"sofa","eta_s":4}`; error `unknown_place`. Returns once a path exists; arrival is not awaited (motion runs on the controller's `commandCts`). |
| `follow_user` | Start following the user around at a comfortable distance until told to stop or sent elsewhere. | `{"type":"object","properties":{},"additionalProperties":false}` | 1 s | Locomotion | `{"status":"following"}` (returns immediately; the mode persists) |
| `come_here` | Call this whenever the user asks you to come, come here, come closer or come to them, including after a greeting ('Hi, come here'): walk to the user and face them. Not for a greeting on its own. | `{"type":"object","properties":{},"additionalProperties":false}` | 3 s | Locomotion | `{"status":"walking"\|"unreachable","eta_s":3}` (returns once a path exists). A short "come here" final is started locally by `ComeGuard` before the model call (ADR-0036); when it walks, the turn ends with a canned ComingOver line and no model call (ADR-0037). |
| `look_at` | Turn Nubi's head and eyes toward the user, the object the user is pointing at, a seen object, or a piece of furniture from the room scan. Use it when you talk about something in the room; never for finding or locating an object the user is looking for; use find_object for that. | `{"type":"object","properties":{"target":{"type":"string","maxLength":40,"description":"'user', 'pointed', a seen object id or label (obj_3, mug), or furniture type"}},"required":["target"],"additionalProperties":false}` | 1 s | Parallel | `{"status":"looking","target":"obj_3"}`; error `no_pointing`, `unknown_object` |
| `capture_and_identify` | Look through the headset camera at what the user is showing or pointing at, highlight it, and answer the question about it. Use it whenever the user asks what something is, how to use it, or says 'look at this'. | `{"type":"object","properties":{"question":{"type":"string","maxLength":200,"description":"The user's question about the object"},"object_hint":{"type":"string","maxLength":40,"description":"Optional short noun phrase if known, e.g. 'remote'"}},"required":["question"],"additionalProperties":false}` | 12 s (inner: SAM 5 s, vision 8 s) | Exclusive | `{"object_id":"obj_4","label":"TV remote","answer":"…","masked":true}`, `EndsTurn=true`, `SpokenText=null` (the answer was streamed through `ToolContext.Speech`); errors `camera_unavailable`, `too_dark` (frame mean luma below `JollyConfig.visionMinMeanLuma`, default 25: no SAM or Spark call; the model asks for light in one sentence, ADR-0039), `no_object`, `vision_timeout` (with the canned "hold it closer" line spoken), `timeout` |
| `find_object` (*should*) | Locate a named object the user is searching for ('where is my X', 'find my X'), even one you have seen before: a fresh look that highlights every visible instance, e.g. 'glasses' or 'keys'. | `{"type":"object","properties":{"noun_phrase":{"type":"string","maxLength":40,"description":"1-3 word noun phrase, e.g. 'reading glasses'"}},"required":["noun_phrase"],"additionalProperties":false}` | 8 s (SAM 5 s) | Exclusive | `{"found":2,"nearest_id":"obj_7","distance_m":1.8,"looking":true,"next":"you are already looking at it; answer in words, no other tool is needed"}` or `{"found":0}`; error `camera_unavailable`, `too_dark` (as `capture_and_identify`, no SAM call). SAM text prompt on a fresh capture; the tool itself turns Nubi's head and eyes to the nearest instance, so no `look_at` round follows (ADR-0037). |
| `highlight_part` (*should*) | Highlight a part of an object seen this session, e.g. 'handle' of obj_3. Use it when the user asks which part or button to use. | `{"type":"object","properties":{"object_id":{"type":"string","maxLength":16},"part":{"type":"string","maxLength":40,"description":"part noun phrase, e.g. 'mug handle'"}},"required":["part"],"additionalProperties":false}` | 6 s (SAM 5 s) | Exclusive | `{"status":"highlighted","part":"mug handle"}` or error `no_match`, `no_frame`. SAM text prompt on the last capture kept in RAM (a `too_dark` capture never replaces it) (part phrases verified live: "mug handle" → one box, c≈0.97). Nubi looks at it. |
| `remember_place` | Remember a location under a name, e.g. 'sofa', so Nubi can walk there later. | `{"type":"object","properties":{"name":{"type":"string","maxLength":32}},"required":["name"],"additionalProperties":false}` | 5 s | Parallel | `{"status":"saved","name":"sofa","anchored":true}` |
| `stop` | Stop walking, following and talking immediately. | `{"type":"object","properties":{},"additionalProperties":false}` | 1 s | Parallel | `{"status":"stopped"}` (`EndsTurn=true`). Usually pre-empted by the local `StopGuard` (§9), which halts locomotion on the final transcript before any model call. |
| `play_gesture` | Play an expressive body gesture when it fits, e.g. wave hello or nod. | `{"type":"object","properties":{"name":{"type":"string","enum":["wave","nod","shake_head","shrug","point","happy_bounce","think"]}},"required":["name"],"additionalProperties":false}` | 4 s | Parallel | `{"status":"played"}` |
| `set_persona` (*should*) | Switch Nubi's personality when the user asks for another mode. | `{"type":"object","properties":{"id":{"type":"string","enum":["companion","mentor","guru"]}},"required":["id"],"additionalProperties":false}` | 1 s | Exclusive | `{"status":"switching","id":"guru"}`, `EndsTurn=true`. The tool only calls `IPersonaProvider.RequestSwitch(id)`; the controller applies it once the turn is Idle and the new persona greets in its own voice. The enum is generated from `IPersonaProvider.AvailableIds` minus `psychologist`, which can only be chosen in Settings. |
| `go_to_surface` (ADR-0025) | Walk Nubi to a piece of the user's real furniture from the room scan, e.g. the couch or the table, and stop beside it facing it. | `{"type":"object","properties":{"label":{"type":"string","enum":["couch","table","desk","bed","chair","storage","screen"],"description":"Furniture from the room scan"}},"required":["label"],"additionalProperties":false}` | 3 s | Locomotion | `{"status":"walking"\|"unreachable","label":"table","eta_s":3}` (`desk` is reported as `table`); errors `room_not_scanned`, `no_surface`. Withheld in seated mode. |
| `request_room_scan` (ADR-0025) | Open the headset's Space Setup so the user can scan the room; afterwards Nubi can walk around the room and to the furniture. Call it only after the user agrees to scan. The app pauses until they finish. | `{"type":"object","properties":{},"additionalProperties":false}` | 2 s | Exclusive | `{"status":"opening"}`, `EndsTurn=true`, `SpokenText` = the `room.scan_opening` line; Space Setup opens once Nubi is idle and silent. Error `scan_unavailable` off Meta's runtime. |

Locomotion tools never await arrival under `turnCts`: a barge-in cancels the turn, not the walk (`ICharacterController.MoveTo`, §7). Results are fast, so a locomotion call never holds the next model round or an `EndsTurn` round.

`remember_place` pose:
- the user's floor-projected head position if the user says "here" (default),
- otherwise Nubi's position.
`ISpatialAnchors.CreateAndSave` anchors it.

## 5. Audio, speech-to-text, text-to-speech, lipsync

```csharp
namespace Jolly.Contracts
{
    /// <summary>
    /// Pooled mono s16le PCM. A class, not a struct: a struct copy (await foreach variables are read-only, so Dispose
    /// runs on a defensive copy) would keep the array and could return it to the pool twice, letting two renters share it.
    /// The receiver owns it and calls Dispose; Dispose is idempotent per instance and returns both the buffer and the
    /// instance to their pools. Never touch a chunk after Dispose.
    /// </summary>
    public sealed class PcmChunk : IDisposable
    {
        static readonly System.Collections.Concurrent.ConcurrentBag<PcmChunk> Pool = new();
        int _disposed;
        PcmChunk() { }

        public short[] Buffer { get; private set; }     // length >= Length; never read past Length
        public int Length { get; private set; }
        public int SampleRate { get; private set; }
        public double CaptureTime { get; private set; } // IClock.Now of the first sample; 0 for synthesized audio

        public static PcmChunk Rent(int length, int sampleRate, double captureTime = 0)
        {
            if (!Pool.TryTake(out var c)) c = new PcmChunk();
            c._disposed = 0;
            c.Buffer = System.Buffers.ArrayPool<short>.Shared.Rent(length);
            c.Length = length; c.SampleRate = sampleRate; c.CaptureTime = captureTime;
            return c;
        }
        public ReadOnlySpan<short> Span => new(Buffer, 0, Length);
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
            System.Buffers.ArrayPool<short>.Shared.Return(Buffer);
            Buffer = null; Length = 0;
            Pool.Add(this);
        }
    }

    public interface IAudioCapture
    {
        bool IsAvailable { get; }
        /// <summary>Starts the mic, resampling to targetSampleRate (16000 or 24000). One session at a time.</summary>
        IAudioCaptureSession Start(int targetSampleRate);
    }

    public interface IAudioCaptureSession : IDisposable
    {
        /// <summary>~80 ms chunks. Completes normally after Stop(); Dispose() aborts.</summary>
        IUniTaskAsyncEnumerable<PcmChunk> Chunks { get; }
        float Level { get; }   // RMS 0..1 of the last chunk, for UI
        void Stop();
    }

    public enum TranscriptKind { Partial, Final }

    public readonly struct TranscriptEvent
    {
        public TranscriptEvent(TranscriptKind kind, string text, double audioMs) { Kind = kind; Text = text; AudioMs = audioMs; }
        public TranscriptKind Kind { get; }
        public string Text { get; }     // Partial: cumulative (replace previous). Final: cased, normalised punctuation
        public double AudioMs { get; }
    }

    public sealed class SttOptions
    {
        public string LanguageBias { get; set; } = "English";   // Muse ASR languageBias value
        public int SampleRate { get; set; } = 24000;            // 16000 | 24000
    }

    public interface ISpeechToText
    {
        /// <summary>
        /// Connects immediately and buffers audio during the handshake (≤5 s). After the ack it sends the buffered backlog
        /// at once, then forwards live chunks as they arrive (never more than 5 s ahead of the wall clock, the server limit).
        /// When the source completes it sends endStream within one frame, then yields exactly one Final (Text may be empty).
        /// Strict 1× pacing of the backlog is forbidden: it would delay the final by the whole handshake (~1 s).
        /// </summary>
        IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options, CancellationToken ct);
    }

    /// <summary>One resolved Wit voice. Defaults are Wit's own; the character's voice comes from <see cref="ICharacterVoice"/>.</summary>
    public sealed class VoiceSpec
    {
        public const int MinSpeed = 50, MaxSpeed = 200, MinPitch = 25, MaxPitch = 200;

        public string VoiceId { get; set; } = "Charlie";  // Wit voice name
        public string Style { get; set; }                  // null = provider default
        public int Speed { get; set; } = 100;              // Wit 50..200
        public int Pitch { get; set; } = 100;              // Wit 25..200
        public string SfxCharacter { get; set; }           // SSML <sfx character>, e.g. "chipmunk"; null = none
        public string SfxEnvironment { get; set; }         // SSML <sfx environment>, e.g. "room"; null = none

        public VoiceSpec Copy() => (VoiceSpec)MemberwiseClone();
    }

    /// <summary>The voice a character speaks with, per language tag ("en", "pl"). Never null; a fresh spec per call.</summary>
    public interface ICharacterVoice
    {
        VoiceSpec VoiceFor(string languageTag);
    }

    public interface ITextToSpeech
    {
        int SampleRate { get; }        // Wit audio/raw: 24000
        int MaxCharacters { get; }     // 240 (callers split; Wit SDK limit is 280)
        bool Supports(string languageTag);
        /// <summary>Streams PCM for one utterance as it arrives. The receiver owns each chunk.</summary>
        IUniTaskAsyncEnumerable<PcmChunk> Synthesize(string text, VoiceSpec voice, CancellationToken ct);
    }

    public interface ISpeechOutput
    {
        bool IsPlaying { get; }
        /// <summary>Appends a stream to the playback ring; completes when its last sample has been played. Fires FirstSamplePlayed per call.</summary>
        UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct);
        UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct);
        /// <summary>Immediate silence (20 ms fade) and clears all queued audio. Every pending Play/PlayClip completes
        /// with OperationCanceledException, so SpeechQueue.WhenDrained never hangs after a barge-in. Safe to call any time.</summary>
        void Flush();
        /// <summary>utteranceId; raised on the main thread when the DSP actually consumed the first sample
        /// (OnAudioFilterRead path, not a streaming-clip read-ahead).</summary>
        event Action<int> FirstSamplePlayed;
    }

    /// <summary>Vowel weights 0..1 for the mouth rig, plus overall loudness.</summary>
    public readonly struct MouthShape
    {
        public MouthShape(float a, float i, float u, float e, float o, float volume)
        { A = a; I = i; U = u; E = e; O = o; Volume = volume; }
        public float A { get; } public float I { get; } public float U { get; } public float E { get; } public float O { get; }
        public float Volume { get; }
        public static MouthShape Closed => default;
    }

    public interface ILipSyncDriver
    {
        /// <summary>Latest analysed shape; polled in LateUpdate. Goes to Closed within ~100 ms of silence.</summary>
        MouthShape Current { get; }
        void Reset();
    }

    /// <summary>PTT. A pinch counts only when the hand's ray hovers no XRI interactable and the palm does not face
    /// the head (the Horizon OS system-menu gesture). Hand is configurable (dominant / either).</summary>
    public interface IVoiceActivation
    {
        bool IsHeld { get; }
        event Action Pressed;    // main thread
        event Action Released;   // main thread
    }
}
```

## 6. Camera, segmentation, identification

```csharp
namespace Jolly.Contracts
{
    public readonly struct CameraIntrinsics
    {
        public CameraIntrinsics(Vector2 focalLength, Vector2 principalPoint, Vector2Int resolution)
        { FocalLength = focalLength; PrincipalPoint = principalPoint; Resolution = resolution; }
        public Vector2 FocalLength { get; }     // px
        public Vector2 PrincipalPoint { get; }  // px, top-left origin
        public Vector2Int Resolution { get; }
        public CameraIntrinsics Scaled(float s) => new(FocalLength * s, PrincipalPoint * s,
            new Vector2Int(Mathf.RoundToInt(Resolution.x * s), Mathf.RoundToInt(Resolution.y * s)));
        /// <summary>Camera-space direction (z = 1) through a top-left-origin pixel. The camera pose has +Y = image up,
        /// so pixel y flips (ADR-0017). The one projection CameraMath and the mask overlay share (ADR-0018).</summary>
        public Vector3 PixelToCameraDir(Vector2 px) =>
            new((px.x - PrincipalPoint.x) / FocalLength.x, -(px.y - PrincipalPoint.y) / FocalLength.y, 1f);
    }

    /// <summary>One captured frame, already converted to the requested size. Pixels live in the frame source's buffer;
    /// Dispose releases it back (idempotent). The source hands out at most one frame at a time.</summary>
    public sealed class CameraFrame : IDisposable
    {
        public CameraFrame(NativeArray<byte> rgba32, int width, int height, CameraIntrinsics intrinsics,
                           Pose cameraPoseWorld, double timestamp, Action<CameraFrame> release)
        { Rgba32 = rgba32.GetSubArray(0, width * height * 4); Width = width; Height = height; Intrinsics = intrinsics;
          CameraPoseWorld = cameraPoseWorld; Timestamp = timestamp; _release = release; }
        Action<CameraFrame> _release;
        /// <summary>Exactly Width*Height*4 bytes (a sub-array of the reused buffer); row-major, top row first.</summary>
        public NativeArray<byte> Rgba32 { get; }
        public int Width { get; }
        public int Height { get; }
        public CameraIntrinsics Intrinsics { get; }  // already scaled to Width/Height
        public Pose CameraPoseWorld { get; }         // Unity pose: +Z forward through the image, +Y image up, at acquisition
        public double Timestamp { get; }             // IClock.Now at acquisition
        public void Dispose() { var r = _release; _release = null; r?.Invoke(this); }
    }

    public enum CameraAvailability { Unknown, NoPermission, NotSupported, Starting, Ready }

    public interface ICameraFrameSource : IDisposable
    {
        CameraAvailability Availability { get; }
        /// <summary>
        /// Latest frame (20–40 ms old), converted by the platform straight to max(width,height) &lt;= maxSide with the
        /// correct mirror flag (meta-openxr: XRCpuImage.ConversionParams.outputDimensions + transformation, ConvertAsync).
        /// While a previous frame is still undisposed, waits for its release (the encoder may still read it on a worker).
        /// Throws ProviderException(Protocol) when no frame arrives within 1 s.
        /// </summary>
        UniTask<CameraFrame> Capture(int maxSide, CancellationToken ct);
    }

    public sealed class EncodedImage
    {
        public EncodedImage(byte[] bytes, string mimeType, int width, int height, CameraIntrinsics intrinsics, Pose cameraPoseWorld)
        { Bytes = bytes; MimeType = mimeType; Width = width; Height = height; Intrinsics = intrinsics; CameraPoseWorld = cameraPoseWorld; }
        public byte[] Bytes { get; }
        public string MimeType { get; }               // "image/jpeg"
        public int Width { get; }
        public int Height { get; }
        public CameraIntrinsics Intrinsics { get; }   // already scaled to Width/Height
        public Pose CameraPoseWorld { get; }
    }

    public interface IImageEncoder
    {
        /// <summary>Encodes JPEG (no resize; the frame source already delivered the target size). Does not dispose the frame,
        /// and completes only after the native encode has finished reading it, even when ct is cancelled.</summary>
        UniTask<EncodedImage> EncodeJpeg(CameraFrame frame, int quality, CancellationToken ct);
    }

    public enum SegmentPromptKind { Text, Points, Box }

    public readonly struct PromptPoint
    {
        public PromptPoint(int x, int y, bool positive = true) { X = x; Y = y; Positive = positive; }
        public int X { get; } public int Y { get; } public bool Positive { get; }
    }

    public sealed class SegmentPrompt
    {
        public SegmentPromptKind Kind { get; private set; }
        public string Text { get; private set; }
        public IReadOnlyList<PromptPoint> Points { get; private set; }
        public RectInt Box { get; private set; }
        public static SegmentPrompt ForText(string nounPhrase) => new() { Kind = SegmentPromptKind.Text, Text = nounPhrase };
        public static SegmentPrompt ForPoints(IReadOnlyList<PromptPoint> points) => new() { Kind = SegmentPromptKind.Points, Points = points };
        public static SegmentPrompt ForBox(RectInt box) => new() { Kind = SegmentPromptKind.Box, Box = box };
    }

    /// <summary>Row-major bit-packed binary mask.</summary>
    public sealed class BitMask
    {
        public BitMask(int width, int height, ulong[] bits) { Width = width; Height = height; Bits = bits; }
        public int Width { get; }
        public int Height { get; }
        public ulong[] Bits { get; }
        public bool this[int x, int y] { get { int i = y * Width + x; return (Bits[i >> 6] & (1UL << (i & 63))) != 0; } }
    }

    public sealed class SegmentedObject
    {
        public SegmentedObject(int id, RectInt box, float confidence, BitMask mask)
        { Id = id; Box = box; Confidence = confidence; Mask = mask; }
        public int Id { get; }
        /// <summary>Encoded-image pixels. SAM's x2/y2 are inclusive, so Box.width = x2-x1+1.</summary>
        public RectInt Box { get; }
        public float Confidence { get; }   // NaN if not returned
        public BitMask Mask { get; }       // box-local, Mask.Width == Box.width
    }

    public interface ISegmenter
    {
        /// <summary>An empty list means no match (not an error). Records one image in IApiUsageMeter.</summary>
        UniTask<IReadOnlyList<SegmentedObject>> Segment(EncodedImage image, SegmentPrompt prompt, CancellationToken ct);
    }

    public sealed class IdentifyRequest
    {
        public string Question { get; set; }
        /// <summary>Persona style + safety text + language directive (ToolContext.PersonaInstructions). The identifier
        /// appends its own output-format rules. Camera content is untrusted data, never instructions.</summary>
        public string PersonaInstructions { get; set; }
        public string LanguageTag { get; set; }
        public ReasoningEffort Effort { get; set; } = ReasoningEffort.Minimal;
    }

    public enum IdentificationEventKind { Header, AnswerDelta, Completed }

    /// <summary>
    /// Wire format (verified live 2026-11-01, effort minimal: first text 1.5 s, total 1.6 s on pen_640.jpg):
    /// first line "LABEL|NOUN_PHRASE", newline, then plain answer text that streams straight into speech.
    /// </summary>
    public readonly struct IdentificationEvent
    {
        public IdentificationEvent(IdentificationEventKind kind, string label = null, string nounPhrase = null, string text = null)
        { Kind = kind; Label = label; NounPhrase = nounPhrase; Text = text; }
        public IdentificationEventKind Kind { get; }
        public string Label { get; }        // Header: "ballpoint pen"
        public string NounPhrase { get; }   // Header: SAM-ready concept, e.g. "pen"
        public string Text { get; }         // AnswerDelta; Completed carries the full answer
    }

    public interface IObjectIdentifier
    {
        /// <summary>LiveUser route. A malformed header (no '|' in the first line) yields Header with Label = NounPhrase = null
        /// and treats the whole text as the answer.</summary>
        IUniTaskAsyncEnumerable<IdentificationEvent> Identify(EncodedImage image, IdentifyRequest request, CancellationToken ct);
    }
}
```

## 7. World, user and character

```csharp
namespace Jolly.Contracts
{
    public interface IUserPoseProvider
    {
        Pose Head { get; }
        /// <summary>Index-finger aim ray of whichever hand is pointing (or the controller ray). False if neither hand is pointing.</summary>
        bool TryGetPointingRay(out Ray ray);
        /// <summary>Clears `into` and adds world positions of a few joints (wrist, palm, finger tips) of each tracked hand,
        /// for SAM negative points. No allocation when `into` has capacity.</summary>
        void GetHandKeyPoints(List<Vector3> into);
    }

    /// <summary>Where Nubi can walk: nowhere yet, the fallback disc around the user (no Space Setup, ADR-0025), or the
    /// scanned floor.</summary>
    public enum WalkableArea { None, Fallback, Scanned }

    /// <summary>A labelled Space Setup surface in world space: a bounding box, or a horizontal plane such as a table top
    /// or a couch seat. Pose.position is the centre; its yaw orients Size.x and Size.z.</summary>
    public readonly struct SceneSurface
    {
        public SceneSurface(string label, Pose pose, Vector3 size);
        public string Label { get; }   // couch, table, bed, chair, storage, screen, lamp, plant
        public Pose Pose { get; }
        public Vector3 Size { get; }   // full extents in the surface's own axes; y = 0 for a plane
        public float Top { get; }
    }

    public interface IWorldQuery
    {
        WalkableArea WalkableArea { get; }
        /// <summary>ARRaycastManager.Raycast(ray, hits, TrackableType.Depth) (meta-openxr supports only Depth), then
        /// Physics.Raycast against plane/box colliders on the NavGeometry layer; nearest hit wins. False before USE_SCENE is granted.</summary>
        bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit);
        /// <summary>NavMesh.SamplePosition, keeping only hits within 0.1 m of the floor plane height (never a furniture top).</summary>
        bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point);   // NavMesh.SamplePosition
        /// <summary>Clears `into` and adds every labelled plane and bounding box. Empty before USE_SCENE or Space Setup.</summary>
        void GetSurfaces(List<SceneSurface> into);
    }

    public enum RoomScanStatus { Pending, Scanned, Unscanned, NoPermission }

    /// <summary>The room-scan flow (ADR-0025): whether Space Setup data exists, and a way to open Space Setup.</summary>
    public interface IRoomScan
    {
        RoomScanStatus Status { get; }
        bool CanRequestScan { get; }   // false off Meta's runtime (Editor, Link)
        /// <summary>Queues Space Setup; it opens once Nubi is idle and silent. The app pauses while the user scans.</summary>
        bool RequestScan();
    }

    /// <summary>The XR side of the room-scan flow, driven by Core's RoomScanMonitor.</summary>
    public interface ISceneData
    {
        bool PermissionGranted { get; }
        bool HasScannedFloor { get; }
        bool CanRequestCapture { get; }
        bool TryRequestCapture();          // MetaOpenXRSessionSubsystem.TryRequestSceneCapture
        void Requery();                    // restart the plane and bounding-box managers
        /// <summary>Outline (x/z offsets around center) baked at the tracking-origin floor height; false if refused.</summary>
        bool SetFallbackFloor(Vector3 center, IReadOnlyList<Vector2> outline);
        void ClearFallbackFloor();
    }

    /// <summary>Raw interaction detections from the XR layer (WP5); InteractionTriggers (WP10) rate-limits them into canned lines.
    /// Raised on the main thread.</summary>
    public interface IInteractionSignals
    {
        event Action GazeDwell;   // gaze on Nubi >= 1.5 s
        event Action OpenPalm;    // XR Hands static gesture toward Nubi
        event Action Poke;        // XRI poke on Nubi's collider
    }

    public interface ISpatialAnchors
    {
        /// <summary>Creates and persists an anchor. Returns its id, or null if persistence is unsupported (the caller keeps the pose only).</summary>
        UniTask<string> CreateAndSave(Pose pose, CancellationToken ct);
        UniTask<Pose?> TryResolve(string anchorId, CancellationToken ct);
        /// <summary>Deletes the persisted anchor. An unknown id, or a platform without erase, is a no-op.</summary>
        UniTask Erase(string anchorId, CancellationToken ct);
    }

    public enum ConversationState { Idle, Listening, Thinking, Speaking, Acting }
    public enum Mood { Neutral, Happy, Curious, Concerned, Sleepy }
    public enum GestureId { Wave, Nod, ShakeHead, Shrug, Point, HappyBounce, Think, Stretch, LookAround }
    public enum MoveOutcome { Arrived, Unreachable, Cancelled, TimedOut }
    public enum LookTargetKind { None, User, WorldPoint }

    public readonly struct LookTarget
    {
        LookTarget(LookTargetKind kind, Vector3 point) { Kind = kind; Point = point; }
        public LookTargetKind Kind { get; }
        public Vector3 Point { get; }
        public static LookTarget None => default;
        public static LookTarget User => new(LookTargetKind.User, default);
        public static LookTarget At(Vector3 world) => new(LookTargetKind.WorldPoint, world);
    }

    public enum LocomotionMode { None, MovingTo, Following }

    public interface ICharacterController
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        bool IsMoving { get; }
        LocomotionMode Mode { get; }
        /// <summary>
        /// Supersedes any running locomotion. Returns false (Unreachable) if no path exists, otherwise starts the motion
        /// on the controller's own commandCts and returns true with the ETA. The motion ends only on arrival, Stop(), or a newer
        /// MoveTo/Follow; no caller token can stop it, so a barge-in never halts a walk.
        /// </summary>
        bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds);
        /// <summary>Awaits the current motion's outcome. ct only detaches this awaiter; it never stops the motion.
        /// Returns Cancelled if superseded or stopped.</summary>
        UniTask<MoveOutcome> WhenArrived(CancellationToken ct);
        /// <summary>Follow mode until Stop() or another MoveTo/Follow. Keeps distanceMin..distanceMax from the user.</summary>
        void Follow(float distanceMin, float distanceMax);
        /// <summary>Halts locomotion immediately (StopGuard, stop tool, Settings).</summary>
        void Stop();
        /// <summary>Moves Nubi to floorPoint without walking, hidden by a short shrink-and-grow pop: for when it is left
        /// where no NavMesh will reach (the fallback floor, ADR-0025). A running MoveTo completes Cancelled; Follow
        /// carries on from the new spot. A call before a pop has moved Nubi only retargets it.</summary>
        void Relocate(Vector3 floorPoint);
        void LookAt(LookTarget target);
        UniTask PlayGesture(GestureId gesture, CancellationToken ct);
        void SetMood(Mood mood);
        void SetConversationState(ConversationState state);
    }
}
```

## 8. Memory, personas, localization, connectivity, canned lines

```csharp
namespace Jolly.Contracts
{
    public sealed class SeenObject
    {
        public string Id { get; set; }            // "obj_N"; N from a persisted monotonic counter, never reused across sessions
        public string Label { get; set; }
        public Vector3 WorldPosition { get; set; }
        public string AnchorId { get; set; }      // optional
        public DateTimeOffset SeenAt { get; set; }
        public string Summary { get; set; }       // <= 140 chars
    }

    public sealed class PlaceRecord
    {
        public string Name { get; set; }          // normalised: PromptText-sanitised, lower-case
        public Pose Pose { get; set; }
        public string AnchorId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public interface ISceneMemory
    {
        IReadOnlyList<SeenObject> Objects { get; }   // most recent first, capped at 64 (LRU)
        IReadOnlyList<PlaceRecord> Places { get; }   // capped at 32
        SeenObject AddObject(string label, Vector3 worldPosition, string summary, string anchorId = null);
        bool TryGetObject(string id, out SeenObject obj);
        void RememberPlace(PlaceRecord place);       // same name overwrites
        bool TryGetPlace(string name, out PlaceRecord place);
        /// <summary>Sanitised data block for a user-role item (never instructions): labels are printable, single-line,
        /// quote-free and &lt;= 40 chars; summaries come from our own template, not raw model text.</summary>
        string BuildPromptContext(int maxChars);
        /// <summary>Last encoded capture of this session (RAM only, never persisted), for highlight_part / find_object.</summary>
        EncodedImage LastCapture { get; set; }
        UniTask Load(CancellationToken ct);
        UniTask Save(CancellationToken ct);
        void Clear();
    }

    public interface IConversationMemory
    {
        string Summary { get; }
        IReadOnlyList<ConversationItem> Recent { get; }
        /// <summary>Appends one finished turn atomically. Opaque items are rejected (turn-local only). Throws if a ToolCall
        /// has no ToolResult in the same batch (the API returns 400 for an orphan call). Cross-turn replay of
        /// function_call + output without reasoning items is verified live (2026-11-01).</summary>
        void CommitTurn(IReadOnlyList<ConversationItem> items);
        /// <summary>True when Recent exceeds 16 items or 6,000 chars.</summary>
        bool NeedsSummarization { get; }
        /// <summary>Folds the oldest half into Summary (<= 1,200 chars) with one LiveUser model call. Never splits a call from its result.</summary>
        UniTask Summarize(IChatModel model, CancellationToken ct);
        /// <summary>carrySummary: the previous persona's Summary, used when the new persona has no saved memory.
        /// The controller never carries a summary into or out of a persona with PersistMemory=false.</summary>
        UniTask Load(string personaId, string carrySummary, CancellationToken ct);
        UniTask Save(CancellationToken ct);          // no-op when the persona has PersistMemory=false
        void Clear();
    }

    public sealed class PersonaProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string SystemPrompt { get; set; }               // resolved for the current locale
        public string SafetyNotice { get; set; }               // null if none; spoken and captioned once per session
        public VoiceSpec Voice { get; set; }                   // resolved for the current locale
        public IReadOnlyList<string> AllowedTools { get; set; }
        public bool PersistMemory { get; set; }
        public Mood DefaultMood { get; set; }
    }

    public interface IPersonaProvider
    {
        PersonaProfile Current { get; }
        IReadOnlyList<string> AvailableIds { get; }
        /// <summary>Records a switch to apply when the conversation is next Idle (used by set_persona inside a turn).
        /// False for an unknown id. Never cancels anything itself.</summary>
        bool RequestSwitch(string id);
        string PendingSwitchId { get; }   // null when none
        /// <summary>Called by the controller only, between turns: resolves the profile for the current locale and raises Changed.
        /// The controller owns sessionCts and swaps memory; see architecture §3.</summary>
        UniTask<bool> ApplySwitch(string id, CancellationToken ct);
        event Action<PersonaProfile> Changed;
    }

    public sealed class LocaleVoiceProfile
    {
        public string LanguageTag { get; set; }        // "en", "pl"
        public string AsrLanguageBias { get; set; }    // "English", "Polish"
        public bool TtsAvailable { get; set; }         // false: captions + baked lines only
        public string ReplyLanguageName { get; set; }  // used in the instruction "Reply in {0}."
    }

    public interface ILocalizer
    {
        string LanguageTag { get; }
        LocaleVoiceProfile Voice { get; }
        IReadOnlyList<string> AvailableLanguageTags { get; }
        /// <summary>Returns the key itself, and logs a warning, if missing. Tables are preloaded at startup.</summary>
        string Get(string table, string key);
        UniTask SetLanguage(string languageTag, CancellationToken ct);
        event Action Changed;
    }

    /// <summary>Unauthorized: key missing or 401 (UI "API key missing or invalid"; the probe is disabled until the key changes).
    /// BudgetReached: IApiUsageMeter.IsCapReached. Both route turns to IOfflineResponder like Offline.</summary>
    public enum Connectivity { Online, Degraded, Offline, Unauthorized, BudgetReached }

    public interface IConnectivityMonitor : IDisposable
    {
        Connectivity State { get; }
        event Action<Connectivity> Changed;    // main thread
        void ReportSuccess(string provider);
        void ReportFailure(string provider, ProviderErrorKind kind);
        UniTask<Connectivity> ProbeNow(CancellationToken ct);
    }

    public interface IHealthProbe
    {
        /// <summary>Cheap authenticated reachability check (GET /v1/models). Never throws except on cancellation.</summary>
        UniTask<bool> Ping(CancellationToken ct);
    }

    public enum LineCategory { Greeting, WhoAmI, Acknowledge, Offline, Error, BudgetReached, Idle, SafetyNotice, CrisisHandoff, Unknown, RoomScanOffer, ComingOver }

    public sealed class CannedLine
    {
        public CannedLine(string key, string text, AudioClip clip) { Key = key; Text = text; Clip = clip; }
        public string Key { get; }
        public string Text { get; }     // caption
        public AudioClip Clip { get; }  // may be null (caption only)
    }

    public interface ICannedLines
    {
        /// <summary>Random line for the current locale, avoiding the last 5 picks of the category.</summary>
        bool TryPick(LineCategory category, out CannedLine line);
    }

    /// <summary>Answers a turn without the cloud (Offline, Unauthorized, BudgetReached, or STT unavailable).
    /// Plays a canned line through ISpeechOutput with captions and completes when it has played, with the line it
    /// played (null when the category has none).</summary>
    public interface IOfflineResponder
    {
        UniTask<CannedLine> Respond(LineCategory category, int utteranceId, CancellationToken ct);
    }

    public sealed class UserPreferencesSnapshot
    {
        public bool Seated { get; set; }
        public int CaptionSize { get; set; }       // 0 small, 1 medium, 2 large
        public float SpeechRate { get; set; } = 1f; // 0.5..2; final Wit speed = clamp(persona speed * rate, 50, 200)
        public float Volume { get; set; } = 1f;
        public bool PttEitherHand { get; set; } = true;
    }

    public interface IUserPreferences
    {
        UserPreferencesSnapshot Current { get; }
        event Action<UserPreferencesSnapshot> Changed;
    }
}
```

## 8.1 Presentation ports (Core → views)

Core never references `Jolly.Presentation`. Views implement or subscribe to these; `JollyBootstrap` wires them.

```csharp
namespace Jolly.Contracts
{
    public enum TranscriptSource { User, Nubi }

    /// <summary>Raised by Core on the main thread. Views (captions, tool badges, latency HUD, status) subscribe.</summary>
    public interface IConversationEvents
    {
        event Action<ConversationState, ConversationState> StateChanged;
        event Action<string> UserPartial;                    // cumulative partial transcript
        event Action<string> UserFinal;                      // after TranscriptAliases
        event Action<int, string> CaptionStarted;            // utteranceId, sentence text; at its first played sample
        event Action<string, string> ToolStarted;            // callId, tool name
        event Action<string, string, bool> ToolCompleted;    // callId, tool name, ok
        event Action<string, double> ModelCallCompleted;     // model id, ms (badges + HUD)
    }

    /// <summary>World-space mask outline for capture_and_identify, find_object and highlight_part.</summary>
    public interface IMaskPresenter
    {
        /// <summary>Places the box-local mask(s) at distanceMeters along the capture camera's rays; fades after 6 s or on Clear.</summary>
        void Show(IReadOnlyList<SegmentedObject> objects, EncodedImage source, float distanceMeters);
        void Clear();
    }

    public enum StatusKind { None, ApiKeyMissing, Offline, Degraded, BudgetReached, CameraUnavailable, MicUnavailable }

    public interface IStatusSink
    {
        void Show(StatusKind status);   // localized by the view
    }
}
```

## 9. Core classes and their public API (not interfaces, but the shape is fixed)

| Class (Core) | Public API | Notes |
|---|---|---|
| `ConversationStateMachine` | `ConversationState State`; `bool Fire(ConversationTrigger t)`; `bool ToolsRunning, SpeechPending, TurnEnded { get; }` (latched flags, set by the triggers below and reset on `PttPressed`); `event Action<ConversationState,ConversationState> Changed` | `enum ConversationTrigger { PttPressed, OfflinePttPressed, FinalText, FinalEmpty, SttFailed, ToolStarted, ToolsDone, FirstAudio, SpeechDrained, TurnCompleted, TurnFailed, Watchdog }`. Guards read only the latched flags, so `Fire` needs no payload. Table in architecture §6 |
| `ConversationController` | ctor(`IVoiceActivation, TurnRouter, TurnPipeline, BargeInCoordinator, ConversationStateMachine, IPersonaProvider, IConversationMemory, IClock, ILog`); `UniTask RunAsync(CancellationToken appCt)`; `void Interrupt()` | Thin loop: owns `sessionCts` and `turnCts`, the watchdogs, and applies a pending persona switch when Idle |
| `TurnRouter` | ctor(`IConnectivityMonitor, IApiUsageMeter, CrisisGuard, StopGuard`); `TurnRoute Route(bool sttAvailable)` | `enum TurnRoute { Online, Offline, Unauthorized, BudgetReached, MicUnavailable }`; pure. `MicUnavailable` plays the Error line, not the offline one |
| `TurnPipeline` | ctor(`IAudioCapture, ISpeechToText, ICameraFrameSource, IImageEncoder, IAgentLoop, SpeechQueue, IOfflineResponder, ICharacterController, CrisisGuard, StopGuard, TranscriptAliases, ILocalizer, IConversationEvents` impl, `ILatencyTracker`); `UniTask RunOnline(int turnId, CancellationToken turnCt)`; `UniTask RunOffline(int turnId, LineCategory c, CancellationToken turnCt)` | One turn: mic → STT → (pre-capture at release) → guards → agent → speech |
| `BargeInCoordinator` | ctor(`ISpeechOutput, SpeechQueue, ILatencyTracker`); `void BargeIn(CancellationTokenSource turnCts)` | Cancels the turn, flushes audio, marks `BargeIn`. Memory commit on cancel is done by `AgentLoop` (contracts §3) |
| `AgentLoop : IAgentLoop` | ctor(`IChatModel, IToolRegistry, IConversationMemory, ISceneMemory, IPersonaProvider, ILocalizer, IUserPreferences, ILog, IClock, ILatencyTracker`) | |
| `SentenceSplitter` | `void Append(string delta)`; `bool TryTake(out string sentence)`; `string Flush()` | Pure, allocation-light |
| `SpeechQueue` | ctor(`ITextToSpeech, ISpeechOutput, IUserPreferences`); `void Enqueue(string sentence, VoiceSpec voice, int utteranceId)`; `UniTask WhenDrained(CancellationToken)`; `void Clear()`; `string SpokenSoFar` | Prefetch depth 1. Speech only: canned lines go through `IOfflineResponder`; the TTS-unavailable locale check lives in `TurnPipeline`. `SpokenSoFar` spans turns; the interrupt commit uses `TurnSpeech`'s per-turn record (ADR-0017) |
| `TranscriptAliases` | ctor(`IReadOnlyDictionary<string,string>`); `static ForName(string name, IEnumerable<string> aliases)`; `string Apply(string transcript)` | Whole-word, case-insensitive. Live: "Hi Nubi" was heard as "Hi newbie" (2026-11-01). The aliases are the selected character's (`CharacterDefinition.aliases`; Nubi: `newbie, noobie, nubie, newby, nobi`) |
| `StopGuard` | `bool Matches(string transcript)` | Localized "stop"/"wait"/"stay" as a whole short utterance; on a match `ICharacterController.Stop()` runs before the model call, and the turn continues |
| `MetaModelResolver` (`Providers/MetaApi/Common`) | `string Resolve(ModelRoute route, string modelId)` | Throws `ProviderException(BadRequest)` when `route == LiveUser` and the id ends in `-contributor`. Unconditional (not `Debug.Assert`); used by every MetaApi adapter |
| `RetryPolicy` (`Jolly.Net`, `Net/Retry`) | `TimeSpan? NextDelay(int attempt, ProviderException e, IRandom r)` | Full jitter, see architecture §11 |
| `ApiUsageMeter : IApiUsageMeter` | ctor(`PriceTable, string filePath, decimal hardCapUsd, IClock`) | |
| `AtomicFile` (static, `Core/Diagnostics`) | `void WriteAllText(string path, string text)` | Writes `path.tmp`, then `File.Replace(tmp, path, null)` if `path` exists, else `File.Move(tmp, path)` (`File.Replace` throws `FileNotFoundException` on a missing destination). Used by every store |
| `JsonFileSecretsProvider`, `DotEnvSecretsProvider : ISecretsProvider` | ctor(`string path`) | Parse once; tolerate CRLF, comments and quotes |
| `IdleScheduler` | ctor(`ICharacterController, IClock, IRandom, IdleConfig`); `UniTask RunAsync(Func<bool> isConversationIdle, CancellationToken)` | Runs a behaviour only when `isConversationIdle() && !character.IsMoving && character.Mode == LocomotionMode.None` |
| `CrisisGuard` | `bool Matches(string transcript)` | Localized keywords |
| `CameraMath` (`Core/Spatial`, owned by WP5) | `Ray PixelToWorldRay(Vector2 px, CameraIntrinsics k, Pose cam)`; `bool WorldToPixel(Vector3 p, CameraIntrinsics k, Pose cam, out Vector2 px)` | Pure; EditMode-tested. Used for hand-ray → SAM points and hand-joint negative points |

### 9.1 Notes from the WP1–WP10 merge

Recorded at integration so WP9 wires against what was built; none of these changed contract code.
- **Constructor shapes in the table above are superseded by the code** for `ConversationController`, `TurnPipeline` and `BargeInCoordinator` (WP2: they take `PersonaSwitcher`, `MemoryKeeper`, `TurnSpeech`, `ILocalizer` and the composed turn parts), and for `ConnectivityMonitor`, `OfflineResponder` and `InteractionTriggers` (WP10). The WP fix reports list the exact signatures.
- **Provider ids** in `ProviderException.Provider` and `IConnectivityMonitor.Report*`: `meta` (every MetaApi HTTP/SSE call), `meta.asr` (Muse ASR), `wit` (Wit TTS), `meta-passthrough` (camera). `ConnectivityMonitor` treats `meta` and `meta.*` as using `META_API_KEY`; a 401 from any other provider does not mark the key rejected.
- **`IHealthProbe.Ping` cannot report a 401.** `MetaHealthProbe` raises an `Unauthorized` event, which the composition root routes to `ReportFailure("meta", ProviderErrorKind.Unauthorized)`. A 429 counts as reachable.
- **Persona, language and clear-memory requests are not on `IUserPreferences`.** `SettingsPanel` raises `PersonaSelected`, `LanguageSelected` and `ClearMemoryRequested`; the composition root routes them to the controller and localizer.
- **WP9 integration (ADR-0018).** `TurnSpeech` takes an `IClock` (it paces captions-only replies). `MetaHealthProbe`'s `baseUrl` is the versioned API base (`MetaApiEndpoint.DefaultBaseUrl`); it appends `/models`. `CameraIntrinsics.PixelToCameraDir` (§6) is the one pixel → camera-ray projection. `MetaErrors.KindOfStatus` is the one HTTP status mapping. Provider calls reach `IConnectivityMonitor` through the composition root's decorators, not from the providers.
