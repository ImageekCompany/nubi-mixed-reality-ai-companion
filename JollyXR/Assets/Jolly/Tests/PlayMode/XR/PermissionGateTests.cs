using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.XR.Permissions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class PermissionGateTests
    {
        sealed class ScriptedPermissions : IRuntimePermissions
        {
            public readonly HashSet<string> Granted = new();
            public readonly HashSet<string> GrantOnRequest = new();
            public readonly List<string> Requested = new();
            public Exception RequestFailure;

            public bool IsGranted(string permission) => Granted.Contains(permission);

            public UniTask Request(IReadOnlyList<string> permissions, CancellationToken ct)
            {
                Requested.AddRange(permissions);
                if (RequestFailure != null)
                    return UniTask.FromException(RequestFailure);
                foreach (var p in permissions)
                {
                    if (GrantOnRequest.Contains(p))
                        Granted.Add(p);
                }
                return UniTask.CompletedTask;
            }
        }

        /// <summary>Stands in for ARCameraManager: counts OnEnable, which is where the real one starts its subsystem.</summary>
        sealed class StartCounter : MonoBehaviour
        {
            public int Starts;
            public int StartFrame = -1;

            void OnEnable()
            {
                Starts++;
                StartFrame = Time.frameCount;
            }
        }

        GameObject _root;
        ScriptedPermissions _permissions;
        StartCounter _camera;
        StartCounter _planes;
        PermissionGate _gate;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("xr");
            _permissions = new ScriptedPermissions();
            _camera = _root.AddComponent<StartCounter>();
            _planes = _root.AddComponent<StartCounter>();
            _camera.enabled = false;
            _planes.enabled = false;
            _camera.Starts = 0;
            _gate = _root.AddComponent<PermissionGate>();
            _gate.Construct(_permissions, _camera, new Behaviour[] { _planes });
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        [UnityTest]
        public IEnumerator Run_RequestsOnlyMissingPermissions() => UniTask.ToCoroutine(async () =>
        {
            _permissions.Granted.Add(XrPermissions.RecordAudio);

            await _gate.Run(CancellationToken.None);

            Assert.That(_permissions.Requested, Is.EquivalentTo(new[] { XrPermissions.HeadsetCamera, XrPermissions.UseScene }));
        });

        [UnityTest]
        public IEnumerator Run_CameraDenied_StillEnablesTheCameraManagerForPassthrough() => UniTask.ToCoroutine(async () =>
        {
            await _gate.Run(CancellationToken.None);

            Assert.That(_camera.enabled, Is.True);
            Assert.That(_gate.CameraGranted, Is.False);
        });

        [UnityTest]
        public IEnumerator Run_RequestThrows_StillEnablesTheCameraManagerAndRethrows() => UniTask.ToCoroutine(async () =>
        {
            _permissions.RequestFailure = new InvalidOperationException("dialog failed");

            var thrown = false;
            try { await _gate.Run(CancellationToken.None); }
            catch (InvalidOperationException) { thrown = true; }

            Assert.That(thrown, Is.True);
            Assert.That(_camera.enabled, Is.True, "a failed dialog must not leave the user in a black room");
        });

        [UnityTest]
        public IEnumerator Run_Cancelled_DoesNotEnableTheCameraManager() => UniTask.ToCoroutine(async () =>
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            _permissions.RequestFailure = new OperationCanceledException(cts.Token);

            var thrown = false;
            try { await _gate.Run(cts.Token); }
            catch (OperationCanceledException) { thrown = true; }

            Assert.That(thrown, Is.True);
            Assert.That(_camera.enabled, Is.False);
        });

        [UnityTest]
        public IEnumerator Run_SceneGranted_StartsTheCameraAndEachSceneManagerOnItsOwnFrame() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: on the Quest these starts together made one 410-436 ms launch frame (selftest-report.md, 4d228c8).
            var boxes = _root.AddComponent<StartCounter>();
            boxes.enabled = false;
            _gate.Construct(_permissions, _camera, new Behaviour[] { _planes, null, boxes });
            _permissions.GrantOnRequest.Add(XrPermissions.HeadsetCamera);
            _permissions.GrantOnRequest.Add(XrPermissions.UseScene);

            // Act
            await _gate.Run(CancellationToken.None);

            // Assert
            Assert.That(new[] { _camera.enabled, _planes.enabled, boxes.enabled }, Is.All.True);
            Assert.That(new[] { _camera.StartFrame, _planes.StartFrame, boxes.StartFrame }, Is.Unique);
            Assert.That(_gate.SceneGranted, Is.True);
        });

        [UnityTest]
        public IEnumerator Run_SceneDenied_KeepsSceneManagersDisabled() => UniTask.ToCoroutine(async () =>
        {
            _permissions.GrantOnRequest.Add(XrPermissions.HeadsetCamera);

            await _gate.Run(CancellationToken.None);

            Assert.That(_planes.enabled, Is.False);
            Assert.That(_gate.SceneGranted, Is.False);
            Assert.That(_gate.CameraGranted, Is.True);
        });

        [UnityTest]
        public IEnumerator Run_SceneGranted_EnablesSceneManagers() => UniTask.ToCoroutine(async () =>
        {
            _permissions.GrantOnRequest.Add(XrPermissions.UseScene);

            await _gate.Run(CancellationToken.None);

            Assert.That(_planes.enabled, Is.True);
        });

        [UnityTest]
        public IEnumerator Recheck_CameraGrantedLater_RestartsTheCameraManager() => UniTask.ToCoroutine(async () =>
        {
            await _gate.Run(CancellationToken.None);
            var changes = 0;
            _gate.Changed += () => changes++;
            Assert.That(_camera.Starts, Is.EqualTo(1));

            _permissions.Granted.Add(XrPermissions.HeadsetCamera);
            _gate.Recheck();
            _gate.Recheck();

            Assert.That(_camera.Starts, Is.EqualTo(2), "re-running Start() is what clears the latched NoPermission");
            Assert.That(_camera.enabled, Is.True);
            Assert.That(changes, Is.EqualTo(1));
        });

        [Test]
        public void Recheck_BeforeRun_DoesNotEnableTheCamera()
        {
            _permissions.Granted.Add(XrPermissions.HeadsetCamera);

            _gate.Recheck();

            Assert.That(_camera.enabled, Is.False);
        }
    }
}
