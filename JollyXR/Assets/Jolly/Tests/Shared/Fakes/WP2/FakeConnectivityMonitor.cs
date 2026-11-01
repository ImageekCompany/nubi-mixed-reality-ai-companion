using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing.Conversation
{
    public sealed class FakeConnectivityMonitor : IConnectivityMonitor
    {
        public Connectivity State { get; set; } = Connectivity.Online;
        public event Action<Connectivity> Changed { add { } remove { } }
        public void ReportSuccess(string provider) { }
        public void ReportFailure(string provider, ProviderErrorKind kind) { }
        public UniTask<Connectivity> ProbeNow(CancellationToken ct) => UniTask.FromResult(State);
        public void Dispose() { }
    }
}
