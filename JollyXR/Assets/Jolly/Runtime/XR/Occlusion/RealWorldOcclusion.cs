using System;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Jolly.XR.Occlusion
{
    /// <summary>
    /// Hides Nubi behind real objects (ADR-0023). Each frame <see cref="OcclusionPolicy"/> picks one source:
    /// - Environment depth: AROcclusionManager + ARShaderOcclusion on the main camera. ARShaderOcclusion publishes the
    ///   depth texture, reprojection matrices and XR_HARD/SOFT_OCCLUSION keywords; Nubi's shaders read them
    ///   (Content/Rendering/Shaders/EnvironmentOcclusion.hlsl). The manager ships disabled and PermissionGate enables it
    ///   with USE_SCENE: started without it, the Meta provider latches a permission error until restart
    ///   (MetaOpenXROcclusionSubsystem.cs:22-25,160-164).
    /// - Scene meshes: <see cref="SceneOccluders"/>, whenever depth frames stop arriving.
    /// Turning shader occlusion off disables ARShaderOcclusion, which zeroes _IsOcclusionOn but leaves the global
    /// keywords set (ARShaderOcclusion.cs:152-157,184-187), so the shaders test _IsOcclusionOn as well.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealWorldOcclusion : MonoBehaviour
    {
        [SerializeField] AROcclusionManager occlusionManager;
        [Tooltip("Ships disabled; enabled only while depth is the active source.")]
        [SerializeField] ARShaderOcclusion shaderOcclusion;
        [SerializeField] SceneOccluders sceneOccluders;

        IClock _clock;
        ILog _log;
        OcclusionSettings _settings;
        double _lastDepthFrame = double.NegativeInfinity;
        bool _subscribed;

        public OcclusionMode Mode { get; private set; } = OcclusionMode.None;
        public bool IsConstructed => _settings != null;

        /// <summary>The occlusion manager when the settings want depth, else null; PermissionGate starts it with USE_SCENE.</summary>
        public Behaviour DepthManager =>
            _settings?.preference == OcclusionPreference.EnvironmentDepth ? occlusionManager : null;

        public void Construct(IClock clock, ILog log, OcclusionSettings settings)
        {
            if (IsConstructed)
                throw new InvalidOperationException("RealWorldOcclusion is already constructed.");
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (occlusionManager == null || shaderOcclusion == null || sceneOccluders == null)
                throw new InvalidOperationException("RealWorldOcclusion references are not bound in the scene.");

            shaderOcclusion.enabled = false;
            sceneOccluders.Visible = false;
            Apply(Select());
        }

        void Update()
        {
            if (!IsConstructed)
                return;
            SubscribeOnceStarted();
            Apply(Select());
        }

        // Only once the manager has a subsystem: its OnDisable raises frameReceived through subsystem.GetShaderKeywords2()
        // and throws without one (AROcclusionManager.cs:439-447,488-498), which is every Editor run.
        void SubscribeOnceStarted()
        {
            if (_subscribed || DepthManager == null || occlusionManager.subsystem == null)
                return;
            occlusionManager.frameReceived += OnDepthFrame;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (_subscribed && occlusionManager != null)
                occlusionManager.frameReceived -= OnDepthFrame;
        }

        OcclusionMode Select()
        {
            var live = OcclusionPolicy.IsDepthLive(_clock.Now, _lastDepthFrame, _settings.depthStaleSeconds);
            return OcclusionPolicy.Select(_settings.preference, live, _settings.sceneMeshFallback);
        }

        // The disable path also raises frameReceived, with a default frame that has no poses.
        void OnDepthFrame(AROcclusionFrameEventArgs args)
        {
            if (args.TryGetPoses(out _))
                NotifyDepthFrame();
        }

        /// <summary>Records a depth frame with poses at the current clock time. Public for tests, which have no
        /// occlusion provider.</summary>
        public void NotifyDepthFrame() => _lastDepthFrame = _clock.Now;

        void Apply(OcclusionMode mode)
        {
            if (mode == Mode)
                return;
            Mode = mode;
            // The setter switches the global keywords at once, so it runs only when depth frames are already bound.
            // Main.unity serializes SoftOcclusion so the preprocess shader is in the build (shader-occlusion.md:46-52).
            if (mode == OcclusionMode.EnvironmentDepth)
                shaderOcclusion.occlusionShaderMode = _settings.softEdges
                    ? AROcclusionShaderMode.SoftOcclusion
                    : AROcclusionShaderMode.HardOcclusion;
            shaderOcclusion.enabled = mode == OcclusionMode.EnvironmentDepth;
            sceneOccluders.Visible = mode == OcclusionMode.SceneMeshes;
            _log.Write(LogLevel.Info, "xr", "evt=occlusion.mode mode=" + ModeName(mode));
        }

        static string ModeName(OcclusionMode mode) => mode switch
        {
            OcclusionMode.EnvironmentDepth => "depth",
            OcclusionMode.SceneMeshes => "scene_meshes",
            _ => "none",
        };
    }
}
