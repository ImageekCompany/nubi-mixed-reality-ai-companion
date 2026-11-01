using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App.Composition;
using Jolly.App.SelfTest;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>
    /// Composition root of Main.unity (architecture §3, ADR-0009): builds the graph once, places Nubi, runs the app until
    /// the scene is destroyed and disposes everything in reverse order. Pause and focus loss flush usage and memory.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JollyBootstrap : MonoBehaviour
    {
        [SerializeField] JollyConfig config;
        [SerializeField] SceneRefs scene = new();
        [SerializeField] SelfTestAssets selfTestAssets;

        readonly CancellationTokenSource _app = new();
        Edges _edges;

        public JollyConfig Config => config;
        public SceneRefs Scene => scene;
        /// <summary>Null until the graph is built (a few frames after Start).</summary>
        public AppGraph Graph { get; private set; }
        public event Action<AppGraph> Ready;

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
            var selfTest = SelfTestSession.TryStart(edges, selfTestAssets, foundation, scene);
            try
            {
                Graph = await AppGraph.Build(foundation, config, scene, edges, ct);
                NubiPlacement.PlaceInFrontOfUser(scene, foundation.Clock, ct).Forget();
                Ready?.Invoke(Graph);
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

        void OnApplicationPause(bool paused)
        {
            if (paused)
                Graph?.Flush();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Graph?.Flush();
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
