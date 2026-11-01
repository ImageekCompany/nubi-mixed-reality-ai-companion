using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Jolly.Tests.EditMode.Core
{
    /// <summary>Consumes a stream in the background so a test can act between events and inspect what arrived.</summary>
    sealed class StreamCollector<T>
    {
        StreamCollector() { }

        public List<T> Items { get; } = new();
        public UniTask Completion { get; private set; }
        public UniTaskStatus Status => Completion.Status;

        public static StreamCollector<T> Start(IUniTaskAsyncEnumerable<T> stream)
        {
            var collector = new StreamCollector<T>();
            collector.Completion = collector.Run(stream).Preserve();
            return collector;
        }

        public System.Exception Error { get; private set; }

        async UniTask Run(IUniTaskAsyncEnumerable<T> stream)
        {
            try
            {
                await foreach (var item in stream)
                    Items.Add(item);
            }
            catch (System.Exception e) when (e is not System.OperationCanceledException)
            {
                Error = e;
                throw;
            }
        }
    }
}
