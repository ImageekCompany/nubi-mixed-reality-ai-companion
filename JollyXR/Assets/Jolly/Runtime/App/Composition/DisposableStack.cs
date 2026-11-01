using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.App.Composition
{
    /// <summary>Owns what the composition root built; disposes it in reverse construction order (contracts §0).</summary>
    public sealed class DisposableStack : IDisposable
    {
        readonly List<IDisposable> _items = new();
        readonly ILog _log;

        public DisposableStack(ILog log) => _log = log;

        public T Add<T>(T item) where T : IDisposable
        {
            _items.Add(item);
            return item;
        }

        public void Dispose()
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                try
                {
                    _items[i].Dispose();
                }
                catch (Exception e)
                {
                    _log.Write(LogLevel.Error, "app", $"evt=dispose.failed type={_items[i].GetType().Name}", e);
                }
            }
            _items.Clear();
        }
    }
}
