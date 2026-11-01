using Jolly.Contracts;

namespace Jolly.Core.Connectivity
{
    // Inside this namespace the bare name binds to the namespace itself, not the contract enum.
    using Connectivity = Jolly.Contracts.Connectivity;

    /// <summary>The network part of connectivity (architecture §5.4): Online, Degraded or Offline only.
    /// Key and budget states are layered on top by <see cref="ConnectivityMonitor"/>.</summary>
    public sealed class LinkState
    {
        readonly int _failuresToOffline;
        int _consecutiveFailures;

        public LinkState(int failuresToOffline) => _failuresToOffline = failuresToOffline;

        public Connectivity State { get; private set; } = Connectivity.Online;

        public void OnReachedServer()
        {
            _consecutiveFailures = 0;
            State = Connectivity.Online;
        }

        public void OnCallFailed()
        {
            _consecutiveFailures++;
            if (State == Connectivity.Online && _consecutiveFailures < _failuresToOffline)
                State = Connectivity.Degraded;
            else
                State = Connectivity.Offline;
        }

        public void OnLost()
        {
            _consecutiveFailures = _failuresToOffline;
            State = Connectivity.Offline;
        }
    }
}
