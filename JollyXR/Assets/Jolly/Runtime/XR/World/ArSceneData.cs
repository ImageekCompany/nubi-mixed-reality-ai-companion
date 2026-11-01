using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.XR.Navigation;
using Jolly.XR.Permissions;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Jolly.XR.World
{
    /// <summary>
    /// ISceneData over the scene's AR managers (ADR-0025). Requery stops and restarts the plane and box managers, which
    /// stops and starts their subsystems (SubsystemLifecycleManager.cs:61-115); whether Meta's provider reads a new
    /// Space Setup on restart is UNVERIFIED on device. The fallback floor goes at the XR Origin's height, which is the
    /// real floor only in the Floor tracking origin mode, so any other mode refuses it rather than guess.
    /// </summary>
    public sealed class ArSceneData : ISceneData
    {
        readonly PermissionGate _permissions;
        readonly FloorNavMeshBuilder _floor;
        readonly XROrigin _origin;
        readonly IReadOnlyList<Behaviour> _sceneManagers;
        readonly ISceneCaptureLauncher _capture;

        public ArSceneData(PermissionGate permissions, FloorNavMeshBuilder floor, XROrigin origin,
                           IReadOnlyList<Behaviour> sceneManagers, ISceneCaptureLauncher capture)
        {
            _permissions = permissions;
            _floor = floor;
            _origin = origin;
            _sceneManagers = sceneManagers;
            _capture = capture;
        }

        public bool PermissionGranted => _permissions.SceneGranted;
        public bool HasScannedFloor => _floor.HasScannedFloor;
        public bool CanRequestCapture => _capture.CanRequest;

        public bool TryRequestCapture() => _capture.TryRequest();

        public void Requery()
        {
            if (!_permissions.SceneGranted)
                return;
            foreach (var manager in _sceneManagers)
            {
                if (manager == null)
                    continue;
                manager.enabled = false;
                manager.enabled = true;
            }
        }

        public bool SetFallbackFloor(Vector3 center, IReadOnlyList<Vector2> outline)
        {
            if (_origin == null || _origin.RequestedTrackingOriginMode != XROrigin.TrackingOriginMode.Floor)
                return false;
            return _floor.SetFallbackFloor(center, outline, _origin.Origin.transform.position.y);
        }

        public void ClearFallbackFloor() => _floor.ClearFallbackFloor();
    }
}
