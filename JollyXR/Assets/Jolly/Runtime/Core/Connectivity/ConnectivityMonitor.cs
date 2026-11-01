using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Connectivity
{
    // Inside this namespace the bare name binds to the namespace itself, not the contract enum.
    using Connectivity = Jolly.Contracts.Connectivity;

    /// <summary>
    /// Five-state connectivity (architecture §5.4, ADR-0012), by priority: Unauthorized (no key, or the current key got a 401),
    /// BudgetReached, then the network link (Online/Degraded/Offline). The link is fed by provider reports, a reachability
    /// pre-check polled by <see cref="RunAsync"/>, and health probes, which run only while the link is not Online, with
    /// backoff, and never without a usable key.
    /// </summary>
    public sealed class ConnectivityMonitor : IConnectivityMonitor
    {
        const string LogCategory = "connectivity";

        readonly IHealthProbe _probe;
        readonly ISecretsProvider _secrets;
        readonly IApiUsageMeter _meter;
        readonly Func<bool> _isNetworkReachable;
        readonly IClock _clock;
        readonly ILog _log;
        readonly TimeSpan _pollInterval;
        readonly LinkState _link;
        readonly ProbeBackoff _backoff;
        readonly CancellationTokenSource _lifetime = new();
        string _rejectedKey;
        double _nextProbeAt;
        bool _disposed;

        /// <param name="isNetworkReachable">Fast local check, e.g. Application.internetReachability != NotReachable.
        /// It cannot prove internet access (Wi-Fi without internet reads reachable), so only its false is trusted.</param>
        public ConnectivityMonitor(IHealthProbe probe, ISecretsProvider secrets, IApiUsageMeter meter,
                                   Func<bool> isNetworkReachable, IClock clock, ILog log, ConnectivityConfig config = null)
        {
            config ??= new ConnectivityConfig();
            _probe = probe;
            _secrets = secrets;
            _meter = meter;
            _isNetworkReachable = isNetworkReachable;
            _clock = clock;
            _log = log;
            _pollInterval = config.PollInterval;
            _link = new LinkState(config.FailuresToOffline);
            _backoff = new ProbeBackoff(config.ProbeBackoffMin, config.ProbeBackoffMax);
            _meter.SpentChanged += OnSpentChanged;
            State = Evaluate();
        }

        public Connectivity State { get; private set; }

        public event Action<Connectivity> Changed;

        public void ReportSuccess(string provider)
        {
            _rejectedKey = null;
            MarkReached();
            Refresh();
        }

        public void ReportFailure(string provider, ProviderErrorKind kind)
        {
            switch (kind)
            {
                case ProviderErrorKind.Unauthorized:
                    _rejectedKey = _secrets.TryGet(SecretKeys.MetaApiKey, out var key) ? key : string.Empty;
                    break;
                case ProviderErrorKind.Network:
                case ProviderErrorKind.Timeout:
                case ProviderErrorKind.Server:
                    MarkCallFailed();
                    break;
            }
            Refresh();
        }

        public async UniTask<Connectivity> ProbeNow(CancellationToken ct)
        {
            if (HasUsableKey())
            {
                if (_isNetworkReachable())
                    ApplyProbe(await _probe.Ping(ct));
                else
                    MarkUnreachable();
            }
            Refresh();
            return State;
        }

        /// <summary>Probes once, then polls until ct is cancelled or the monitor is disposed. Completes as cancelled.</summary>
        public async UniTask RunAsync(CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            var token = linked.Token;
            await ProbeNow(token);
            while (true)
            {
                await _clock.Delay(_pollInterval, token);
                await Poll(token);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _meter.SpentChanged -= OnSpentChanged;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        async UniTask Poll(CancellationToken ct)
        {
            if (!_isNetworkReachable())
                MarkUnreachable();
            else if (_link.State != Connectivity.Online && _clock.Now >= _nextProbeAt && !_meter.IsCapReached)
                await ProbeNow(ct);
            Refresh();
        }

        void MarkCallFailed()
        {
            if (!_isNetworkReachable())
            {
                MarkUnreachable();
                return;
            }
            var before = _link.State;
            _link.OnCallFailed();
            if (_link.State == Connectivity.Degraded)
                _nextProbeAt = _clock.Now;
            else if (before != Connectivity.Offline)
                ScheduleNextProbe();
        }

        void MarkUnreachable()
        {
            _link.OnLost();
            _backoff.Reset();
            _nextProbeAt = _clock.Now;
        }

        void MarkReached()
        {
            _link.OnReachedServer();
            _backoff.Reset();
        }

        void ApplyProbe(bool reachedServer)
        {
            if (reachedServer)
            {
                MarkReached();
                return;
            }
            _link.OnLost();
            ScheduleNextProbe();
        }

        void ScheduleNextProbe() => _nextProbeAt = _clock.Now + _backoff.Next().TotalSeconds;

        bool HasUsableKey()
        {
            if (!_secrets.TryGet(SecretKeys.MetaApiKey, out var key))
                return false;
            if (_rejectedKey == null)
                return true;
            if (string.Equals(key, _rejectedKey, StringComparison.Ordinal))
                return false;
            _rejectedKey = null;
            return true;
        }

        Connectivity Evaluate()
        {
            if (!HasUsableKey())
                return Connectivity.Unauthorized;
            if (_meter.IsCapReached)
                return Connectivity.BudgetReached;
            return _link.State;
        }

        void Refresh()
        {
            var next = Evaluate();
            if (next == State)
                return;
            if (_log.IsEnabled(LogLevel.Info))
                _log.Write(LogLevel.Info, LogCategory, $"evt=connectivity.changed from={State} to={next}");
            State = next;
            Changed?.Invoke(next);
        }

        void OnSpentChanged(decimal spentUsd) => Refresh();
    }
}
