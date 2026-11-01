using System;

namespace Jolly.App.Composition
{
    /// <summary>Runs an unsubscribe action once, so event wiring can sit in the disposable stack.</summary>
    public sealed class Subscription : IDisposable
    {
        Action _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
