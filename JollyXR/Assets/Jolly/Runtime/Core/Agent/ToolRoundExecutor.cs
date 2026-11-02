using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Agent
{
    /// <summary>
    /// Runs one model round's tool calls as they stream in (architecture §7). Parallel calls start at once; a Locomotion call
    /// supersedes a still-running one; an Exclusive call waits for the round's earlier non-Locomotion calls only. Each call
    /// has its own <see cref="ToolDefinition.Timeout"/>. Per-call tasks never throw, so abandoned ones are safe on cancel.
    /// A call to a registered tool that was not offered this turn (persona allow-list, seated mode) is refused like an
    /// unknown one: the allow-list is a privacy promise and must not depend on the model.
    /// </summary>
    sealed class ToolRoundExecutor
    {
        sealed class Call
        {
            public ToolCallRequest Request;
            public ToolConcurrency Concurrency;
            public readonly UniTaskCompletionSource Completion = new();
            public UniTask Done => Completion.Task;
            public ToolResult? Result;
            public TimeoutScope Scope;
            public bool Superseded;
        }

        readonly IToolRegistry _registry;
        readonly IReadOnlyList<ToolDefinition> _offered;
        readonly IClock _clock;
        readonly ToolContext _context;
        readonly ITurnEventSink _events;
        readonly CancellationToken _turnCt;
        readonly ILog _log;
        readonly List<Call> _calls = new();
        Call _locomotion;

        public ToolRoundExecutor(IToolRegistry registry, IReadOnlyList<ToolDefinition> offered, IClock clock,
                                 ToolContext context, ITurnEventSink events, CancellationToken turnCt, ILog log)
        {
            _registry = registry;
            _offered = offered;
            _clock = clock;
            _context = context;
            _events = events;
            _turnCt = turnCt;
            _log = log;
        }

        public int Count => _calls.Count;

        /// <summary>True when the round ran at least one non-Locomotion call and every such result has EndsTurn.</summary>
        public bool EndsTurn
        {
            get
            {
                var any = false;
                foreach (var call in _calls)
                {
                    if (call.Concurrency == ToolConcurrency.Locomotion)
                        continue;
                    if (call.Result is not { EndsTurn: true })
                        return false;
                    any = true;
                }
                return any;
            }
        }

        /// <summary>True when the round ran at least one call and every result, locomotion included, has EndsSpokenTurn:
        /// an unreachable move_relative beside a dance still has to be explained.</summary>
        public bool EndsSpokenTurn
        {
            get
            {
                foreach (var call in _calls)
                {
                    if (call.Result is not { EndsSpokenTurn: true })
                        return false;
                }
                return _calls.Count > 0;
            }
        }

        public void Start(ToolCallRequest request)
        {
            var call = new Call { Request = request };
            _events.Write(new AgentEvent(AgentEventKind.ToolStarted, toolName: request.Name, callId: request.CallId));

            _calls.Add(call);
            if (!IsOffered(request.Name))
            {
                Finish(call, ToolResult.Failure("unknown_tool", $"unknown tool '{request.Name}'"));
                return;
            }
            var known = _registry.TryGet(request.Name, out var tool);
            if (!_registry.TryValidate(request.Name, request.ArgumentsJson, out var args, out var error))
            {
                Finish(call, ToolResult.Failure(known ? "invalid_arguments" : "unknown_tool", error));
                return;
            }

            call.Concurrency = tool.Definition.Concurrency;
            var prerequisites = Prerequisites(call);
            if (call.Concurrency == ToolConcurrency.Locomotion)
            {
                if (_locomotion is { Result: null } previous)
                {
                    previous.Superseded = true;
                    previous.Scope?.Cancel();
                }
                _locomotion = call;
            }
            Run(call, tool, args, prerequisites).Forget();
        }

        public UniTask WhenAll()
        {
            var tasks = new UniTask[_calls.Count];
            for (var i = 0; i < tasks.Length; i++)
                tasks[i] = _calls[i].Done;
            return UniTask.WhenAll(tasks);
        }

        /// <summary>Call ids and output JSON in arrival order; unfinished calls get the interrupted output.</summary>
        public void CollectResults(List<ConversationItem> into, bool includeCalls)
        {
            foreach (var call in _calls)
            {
                if (includeCalls)
                    into.Add(ConversationItem.Call(call.Request.CallId, call.Request.Name, call.Request.ArgumentsJson));
                into.Add(ConversationItem.Result(call.Request.CallId, call.Result?.OutputJson ?? ToolResult.InterruptedOutput));
            }
        }

        bool IsOffered(string name)
        {
            foreach (var definition in _offered)
            {
                if (definition.Name == name)
                    return true;
            }
            return false;
        }

        UniTask[] Prerequisites(Call current)
        {
            if (current.Concurrency != ToolConcurrency.Exclusive)
                return Array.Empty<UniTask>();
            var waits = new List<UniTask>();
            foreach (var call in _calls)
            {
                if (call != current && call.Concurrency != ToolConcurrency.Locomotion)
                    waits.Add(call.Done);
            }
            return waits.ToArray();
        }

        async UniTaskVoid Run(Call call, ITool tool, IToolArguments args, UniTask[] prerequisites)
        {
            ToolResult result;
            try
            {
                if (prerequisites.Length > 0)
                    await UniTask.WhenAll(prerequisites);
                _turnCt.ThrowIfCancellationRequested();
                using (call.Scope = new TimeoutScope(_clock, tool.Definition.Timeout, _turnCt))
                {
                    try
                    {
                        result = await tool.Execute(args, _context, call.Scope.Token);
                    }
                    catch (OperationCanceledException) when (!_turnCt.IsCancellationRequested && call.Scope.TimedOut)
                    {
                        result = ToolResult.Failure("timeout");
                    }
                    catch (OperationCanceledException) when (!_turnCt.IsCancellationRequested && call.Superseded)
                    {
                        result = ToolResult.Failure("superseded");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                call.Completion.TrySetResult();
                return;
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Error, "agent", $"evt=tool.error tool={call.Request.Name}", e);
                result = ToolResult.Failure("tool_error");
            }
            Finish(call, result);
        }

        void Finish(Call call, ToolResult result)
        {
            call.Result = result;
            if (result.SpokenText != null)
                _events.Write(new AgentEvent(AgentEventKind.SpeechDelta, result.SpokenText));
            _events.Write(new AgentEvent(AgentEventKind.ToolCompleted, toolName: call.Request.Name,
                callId: call.Request.CallId, toolResult: result));
            call.Completion.TrySetResult();
        }
    }
}
