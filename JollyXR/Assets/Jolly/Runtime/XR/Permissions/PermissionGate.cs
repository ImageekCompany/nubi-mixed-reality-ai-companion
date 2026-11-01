using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Jolly.XR.Permissions
{
    /// <summary>
    /// Owns the runtime permission flow (ADR-0003, xr-stack.md §2.3). The scene ships with the camera manager and the
    /// scene-data managers disabled.
    /// - The camera manager is enabled once the request returns, whatever the answer and even if it throws: its Start()
    ///   creates the passthrough layer and latches the camera permission, so a denied camera still gets passthrough.
    /// - A camera grant that arrives later (Settings, focus regained) toggles the manager to re-run Start().
    /// - Scene managers start only with USE_SCENE, otherwise their subsystems latch permission errors.
    /// Permission state is read with the Android API: ARCameraManager.permissionGranted reports false while frames
    /// flow on Quest (device-spike.md §2.2).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PermissionGate : MonoBehaviour
    {
        readonly List<string> _missing = new(XrPermissions.All.Length);
        IRuntimePermissions _permissions;
        Behaviour _cameraManager;
        IReadOnlyList<Behaviour> _sceneManagers;
        bool _requested;

        public bool CameraGranted { get; private set; }
        public bool SceneGranted { get; private set; }
        public bool MicrophoneGranted { get; private set; }
        /// <summary>Raised on the main thread when any of the three grants changes after Run.</summary>
        public event Action Changed;

        public void Construct(IRuntimePermissions permissions, Behaviour cameraManager, IReadOnlyList<Behaviour> sceneManagers)
        {
            _permissions = permissions;
            _cameraManager = cameraManager;
            _sceneManagers = sceneManagers;
        }

        public async UniTask Run(CancellationToken ct)
        {
            _missing.Clear();
            foreach (var permission in XrPermissions.All)
            {
                if (!_permissions.IsGranted(permission))
                    _missing.Add(permission);
            }
            try
            {
                if (_missing.Count > 0)
                    await _permissions.Request(_missing, ct);
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                    StartManagers();
            }
        }

        void StartManagers()
        {
            _requested = true;
            if (_cameraManager != null)
                _cameraManager.enabled = true;
            Apply(restartCameraOnGrant: false);
        }

        /// <summary>Re-reads the grants; also runs on application focus, since the user may grant from Settings.</summary>
        public void Recheck()
        {
            if (_requested)
                Apply(restartCameraOnGrant: true);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                Recheck();
        }

        void Apply(bool restartCameraOnGrant)
        {
            var camera = _permissions.IsGranted(XrPermissions.HeadsetCamera);
            var scene = _permissions.IsGranted(XrPermissions.UseScene);
            var microphone = _permissions.IsGranted(XrPermissions.RecordAudio);
            var changed = camera != CameraGranted || scene != SceneGranted || microphone != MicrophoneGranted;

            if (restartCameraOnGrant && camera && !CameraGranted && _cameraManager != null)
            {
                _cameraManager.enabled = false;
                _cameraManager.enabled = true;
            }
            if (scene && _sceneManagers != null)
            {
                foreach (var manager in _sceneManagers)
                {
                    if (manager != null)
                        manager.enabled = true;
                }
            }

            CameraGranted = camera;
            SceneGranted = scene;
            MicrophoneGranted = microphone;
            if (changed && restartCameraOnGrant)
                Changed?.Invoke();
        }
    }
}
