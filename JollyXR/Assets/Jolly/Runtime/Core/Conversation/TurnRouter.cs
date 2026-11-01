using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    // Jolly.Core.Connectivity (the namespace) would otherwise shadow the contract enum.
    using Connectivity = Jolly.Contracts.Connectivity;

    public enum TurnRoute { Online, Offline, Unauthorized, BudgetReached, MicUnavailable }

    /// <summary>Decides per PTT press whether the cloud path can run (architecture §5.4). Pure.</summary>
    public sealed class TurnRouter
    {
        readonly IConnectivityMonitor _connectivity;
        readonly IApiUsageMeter _usage;

        public TurnRouter(IConnectivityMonitor connectivity, IApiUsageMeter usage)
        {
            _connectivity = connectivity;
            _usage = usage;
        }

        public TurnRoute Route(bool sttAvailable)
        {
            if (_usage.IsCapReached)
                return TurnRoute.BudgetReached;
            return _connectivity.State switch
            {
                Connectivity.Unauthorized => TurnRoute.Unauthorized,
                Connectivity.BudgetReached => TurnRoute.BudgetReached,
                Connectivity.Offline => TurnRoute.Offline,
                _ => sttAvailable ? TurnRoute.Online : TurnRoute.MicUnavailable,
            };
        }

        /// <summary>Wit TTS needs the network, not the Meta key or budget.</summary>
        public bool CanSynthesize => _connectivity.State != Connectivity.Offline;

        public static LineCategory LineFor(TurnRoute route) => route switch
        {
            TurnRoute.Unauthorized or TurnRoute.MicUnavailable => LineCategory.Error,
            TurnRoute.BudgetReached => LineCategory.BudgetReached,
            _ => LineCategory.Offline,
        };
    }
}
