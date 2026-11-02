using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

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
        /// <summary>The namespaces of the dotted names in <see cref="Tools"/> (ADR-0047).</summary>
        public IReadOnlyList<ToolNamespace> Namespaces { get; set; } = Array.Empty<ToolNamespace>();
        /// <summary>Null: no web search. Otherwise the provider's built-in, server-side web search is offered (ADR-0048).</summary>
        public WebSearchOptions WebSearch { get; set; }
        public ReasoningEffort Effort { get; set; } = ReasoningEffort.Minimal;
        public int MaxOutputTokens { get; set; } = 1024;  // includes reasoning tokens; do not cap tightly
        public string SafetyIdentifier { get; set; }
    }

    /// <summary>
    /// The provider's built-in web search (ADR-0048). The location only biases results towards the user's area; every
    /// field is optional and none is ever more precise than a city.
    /// </summary>
    public sealed class WebSearchOptions
    {
        /// <summary>ISO 3166-1 alpha-2, e.g. "GB".</summary>
        public string Country { get; set; }
        public string Region { get; set; }
        public string City { get; set; }
        /// <summary>IANA, e.g. "Europe/London".</summary>
        public string Timezone { get; set; }
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

    /// <summary>WebSearchStarted / WebSearchCompleted: the provider's built-in web search (ADR-0048) began or ended;
    /// both carry the search's item id in <see cref="ModelEvent.Text"/>. Neither is a ToolCall: the server runs it.</summary>
    public enum ModelEventKind { Started, TextDelta, ToolCall, Completed, TextDone, WebSearchStarted, WebSearchCompleted }

    public readonly struct ModelEvent
    {
        public ModelEvent(ModelEventKind kind, string text = null, ToolCallRequest toolCall = null, ModelTurnResult result = null)
        { Kind = kind; Text = text; ToolCall = toolCall; Result = result; }
        public ModelEventKind Kind { get; }
        public string Text { get; }                 // TextDelta; TextDone: none, the text item ended (a call may follow); WebSearch*: item id
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
