using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing.Conversation
{
    /// <summary>
    /// ITool with a fixed result. When <see cref="Manual"/> is set, each Execute waits for <see cref="Complete"/> (or its
    /// token), so tests can observe concurrency and cancellation.
    /// </summary>
    public sealed class ScriptedTool : ITool
    {
        public const string EmptySchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

        readonly Queue<UniTaskCompletionSource<ToolResult>> _pending = new();

        public ScriptedTool(string name, ToolConcurrency concurrency = ToolConcurrency.Parallel, string schema = EmptySchema,
                            TimeSpan? timeout = null)
        {
            Definition = new ToolDefinition(name, "test tool " + name, schema, timeout ?? TimeSpan.FromSeconds(5), concurrency);
        }

        public ToolDefinition Definition { get; }
        public ToolResult Result { get; set; } = ToolResult.Success("{\"status\":\"ok\"}");
        public bool Manual { get; set; }
        public Action<IToolArguments, ToolContext> OnExecute { get; set; }
        public int Started { get; private set; }
        public int Cancelled { get; private set; }
        public int Running => _pending.Count;
        public ToolContext LastContext { get; private set; }

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            Started++;
            LastContext = context;
            OnExecute?.Invoke(args, context);
            if (!Manual)
                return Result;

            var source = new UniTaskCompletionSource<ToolResult>();
            _pending.Enqueue(source);
            using (ct.Register(() =>
                   {
                       if (source.TrySetCanceled(ct))
                           Cancelled++;
                   }))
            {
                return await source.Task;
            }
        }

        /// <summary>Completes the oldest running call with <paramref name="result"/>, or <see cref="Result"/>.</summary>
        public void Complete(ToolResult? result = null)
        {
            while (_pending.Count > 0)
            {
                if (_pending.Dequeue().TrySetResult(result ?? Result))
                    return;
            }
            throw new InvalidOperationException($"{Definition.Name} has no running call");
        }
    }
}
