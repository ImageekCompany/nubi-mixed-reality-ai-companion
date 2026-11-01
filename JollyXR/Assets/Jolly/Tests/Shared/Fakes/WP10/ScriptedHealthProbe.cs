using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing.Offline
{
    /// <summary>IHealthProbe that answers <see cref="Result"/> at once, or stays pending while <see cref="Hold"/> is set
    /// until <see cref="Resolve"/>.</summary>
    public sealed class ScriptedHealthProbe : IHealthProbe
    {
        UniTaskCompletionSource<bool> _pending;

        public bool Result { get; set; } = true;
        public bool Hold { get; set; }
        public int Calls { get; private set; }

        public UniTask<bool> Ping(CancellationToken ct)
        {
            Calls++;
            if (!Hold)
                return UniTask.FromResult(Result);
            _pending = new UniTaskCompletionSource<bool>();
            ct.Register(() => _pending.TrySetCanceled(ct));
            return _pending.Task;
        }

        public void Resolve(bool result) => _pending.TrySetResult(result);
    }
}
