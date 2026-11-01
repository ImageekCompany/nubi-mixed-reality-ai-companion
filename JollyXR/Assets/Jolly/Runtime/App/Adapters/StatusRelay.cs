using System;
using Jolly.Contracts;
using Jolly.XR.Permissions;

namespace Jolly.App.Adapters
{
    /// <summary>
    /// Shows the most important degraded condition on the status indicator: connectivity first (it decides whether Nubi
    /// can think at all), then a missing microphone, then a missing camera.
    /// </summary>
    public sealed class StatusRelay : IDisposable
    {
        readonly IConnectivityMonitor _connectivity;
        readonly PermissionGate _permissions;
        readonly IStatusSink _sink;

        public StatusRelay(IConnectivityMonitor connectivity, PermissionGate permissions, IStatusSink sink)
        {
            _connectivity = connectivity;
            _permissions = permissions;
            _sink = sink;
            _connectivity.Changed += OnConnectivityChanged;
            _permissions.Changed += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            _connectivity.Changed -= OnConnectivityChanged;
            _permissions.Changed -= Refresh;
        }

        public static StatusKind For(Connectivity state) => state switch
        {
            Connectivity.Degraded => StatusKind.Degraded,
            Connectivity.Offline => StatusKind.Offline,
            Connectivity.Unauthorized => StatusKind.ApiKeyMissing,
            Connectivity.BudgetReached => StatusKind.BudgetReached,
            _ => StatusKind.None,
        };

        void OnConnectivityChanged(Connectivity _) => Refresh();

        void Refresh()
        {
            var status = For(_connectivity.State);
            if (status == StatusKind.None && !_permissions.MicrophoneGranted)
                status = StatusKind.MicUnavailable;
            if (status == StatusKind.None && !_permissions.CameraGranted)
                status = StatusKind.CameraUnavailable;
            _sink.Show(status);
        }
    }
}
