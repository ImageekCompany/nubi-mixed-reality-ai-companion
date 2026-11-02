using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Testing.Conversation
{
    /// <summary>
    /// IChatModel that plays one scripted round per Stream call. A round emits Started, its steps in order, then Completed
    /// with OutputItems = [reasoning Opaque, assistant text?, calls...]. A <see cref="Round.Gate"/> step pauses until
    /// <see cref="Round.Release"/>, so tests can act mid-stream. Every request's input is snapshotted.
    /// </summary>
    public sealed class ScriptedChatModel : IChatModel
    {
        public const string Model = "muse-spark-1.3";
        public const string ReasoningJson = "{\"type\":\"reasoning\",\"encrypted_content\":\"x\"}";

        readonly Queue<Round> _rounds = new();

        public List<ModelRequest> Requests { get; } = new();
        public List<IReadOnlyList<ConversationItem>> Inputs { get; } = new();
        public int Remaining => _rounds.Count;

        public Round Enqueue()
        {
            var round = new Round();
            _rounds.Enqueue(round);
            return round;
        }

        public IUniTaskAsyncEnumerable<ModelEvent> Stream(ModelRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            Inputs.Add(new List<ConversationItem>(request.Input));
            if (_rounds.Count == 0)
                throw new InvalidOperationException("no scripted round left");
            var round = _rounds.Dequeue();
            return UniTaskAsyncEnumerable.Create<ModelEvent>((writer, token) => round.Play(writer, ct));
        }

        public sealed class Round
        {
            readonly List<Func<IAsyncWriter<ModelEvent>, CancellationToken, UniTask>> _steps = new();
            readonly List<ConversationItem> _output = new() { ConversationItem.Opaque(ReasoningJson) };
            readonly UniTaskCompletionSource _gate = new();
            readonly System.Text.StringBuilder _text = new();
            int _calls;
            FinishReason? _finish;
            bool _lateStart;

            public bool Started { get; private set; }
            public bool Finished { get; private set; }

            public Round Text(string delta)
            {
                _text.Append(delta);
                _steps.Add((w, ct) => w.YieldAsync(new ModelEvent(ModelEventKind.TextDelta, delta)));
                return this;
            }

            /// <summary>The message item is done (Responses <c>response.output_item.done</c>).</summary>
            public Round TextDone()
            {
                _steps.Add((w, ct) => w.YieldAsync(new ModelEvent(ModelEventKind.TextDone)));
                return this;
            }

            public Round Call(string callId, string name, string argsJson = "{}")
            {
                _calls++;
                _steps.Add((w, ct) => w.YieldAsync(new ModelEvent(ModelEventKind.ToolCall,
                    toolCall: new ToolCallRequest(callId, name, argsJson))));
                _output.Add(ConversationItem.Call(callId, name, argsJson));
                return this;
            }

            /// <summary>The server's web search: started, then completed with its item kept for replay (ADR-0048).</summary>
            public Round WebSearch(string id = "ws_1", bool ok = true)
            {
                _steps.Add((w, ct) => w.YieldAsync(new ModelEvent(ModelEventKind.WebSearchStarted, id)));
                _steps.Add((w, ct) => w.YieldAsync(new ModelEvent(ModelEventKind.WebSearchCompleted, id, ok: ok)));
                _output.Add(ConversationItem.Opaque("{\"type\":\"web_search_call\",\"id\":\"" + id + "\",\"status\":\"" +
                                                    (ok ? "completed" : "failed") + "\"}"));
                return this;
            }

            public Round Gate()
            {
                _steps.Add((w, ct) => _gate.Task.AttachExternalCancellation(ct));
                return this;
            }

            /// <summary>Holds back even the Started event until <see cref="Release"/>: a server slow to answer at all.</summary>
            public Round DelayStart()
            {
                _lateStart = true;
                return this;
            }

            /// <summary>Like <see cref="Gate"/> but deaf to the token, so a cancel lands only after Release (a later frame).</summary>
            public Round Stall()
            {
                _steps.Add((w, ct) => _gate.Task);
                return this;
            }

            public Round Fail(ProviderErrorKind kind = ProviderErrorKind.Server)
            {
                _steps.Add((w, ct) => throw new ProviderException("scripted", kind, "scripted failure"));
                return this;
            }

            /// <summary>Overrides the finish reason (default Stop, or ToolCalls when the round has calls).</summary>
            public Round Finish(FinishReason reason)
            {
                _finish = reason;
                return this;
            }

            public void Release() => _gate.TrySetResult();

            internal async UniTask Play(IAsyncWriter<ModelEvent> writer, CancellationToken ct)
            {
                Started = true;
                if (_lateStart)
                    await _gate.Task.AttachExternalCancellation(ct);
                await writer.YieldAsync(new ModelEvent(ModelEventKind.Started));
                foreach (var step in _steps)
                {
                    ct.ThrowIfCancellationRequested();
                    await step(writer, ct);
                }
                ct.ThrowIfCancellationRequested();
                if (_text.Length > 0)
                    _output.Insert(1, ConversationItem.Assistant(_text.ToString()));
                Finished = true;
                await writer.YieldAsync(new ModelEvent(ModelEventKind.Completed, result: new ModelTurnResult
                {
                    FinishReason = _finish ?? (_calls > 0 ? FinishReason.ToolCalls : FinishReason.Stop),
                    ResponseId = "resp_test",
                    OutputItems = _output,
                    Usage = new UsageRecord(Model, 100, 0, 10),
                }));
            }
        }
    }
}
