using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App.Composition;
using Jolly.App.Platform;
using Jolly.App.SelfTest;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>
    /// Composition root of Main.unity (architecture §3, ADR-0009): builds the graph once, places Nubi, runs the app until
    /// the scene is destroyed and disposes everything in reverse order. Pause and focus loss flush usage and memory;
    /// resuming reads the scene again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JollyBootstrap : MonoBehaviour
    {
        [SerializeField] JollyConfig config;
        [SerializeField] SceneRefs scene = new();
        [SerializeField] SelfTestAssets selfTestAssets;
        [SerializeField] CharacterChoice characters = new();

        readonly CancellationTokenSource _app = new();
        Edges _edges;

        /// <summary>Null until the graph is built (a few frames after Start).</summary>
        public AppGraph Graph { get; private set; }

        /// <summary>Tests replace the provider edges here, between scene load and Start.</summary>
        public void UseEdges(Edges edges) => _edges = edges;

        void Start() => Boot(_app.Token).Forget();

        async UniTaskVoid Boot(CancellationToken ct)
        {
            if (config == null)
            {
                Debug.LogError("JollyBootstrap has no JollyConfig; the app cannot start.", this);
                return;
            }
            var edges = _edges ?? Edges.ForPlayer();
            var foundation = Foundation.Build(config, edges);
            CharacterSelection.Apply(scene.nubi, config, characters, edges.Paths, foundation.Log);
            GraphicsWarmup.Start(config.graphicsStates, foundation.Log);
            var selfTest = SelfTestSession.TryStart(edges, selfTestAssets, foundation, scene);
            selfTest?.Frames.Run(ct).Forget();
            try
            {
                Graph = await AppGraph.Build(foundation, config, scene, edges, ct);
                NubiPlacement.PlaceInFrontOfUser(scene, foundation.Clock, ct).Forget();
                selfTest?.Run(Graph, ct).Forget();
                await Graph.Run(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                foundation.Log.Write(LogLevel.Error, "app", "evt=app.failed", e);
            }
        }

        // Space Setup pauses the app and resumes it when the user is done (meta-openxr session.md "Life cycle").
        void OnApplicationPause(bool paused)
        {
            if (paused)
                Graph?.Flush();
            else
                Graph?.Resume();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Graph?.Flush();
            else
                Graph?.Resume();
        }

        void OnDestroy()
        {
            _app.Cancel();
            Graph?.Flush();
            Graph?.Dispose();
            _app.Dispose();
        }
    }
}
