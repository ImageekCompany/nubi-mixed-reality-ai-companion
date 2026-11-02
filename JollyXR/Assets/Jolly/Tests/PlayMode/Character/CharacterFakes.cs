using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.XR.Navigation;
using UnityEngine;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>IWorldQuery over the rig's NavMeshBaker and a fixed list of scanned surfaces.</summary>
    sealed class RigWorld : IWorldQuery
    {
        readonly NavMeshBaker _navMesh;
        public RigWorld(NavMeshBaker navMesh) => _navMesh = navMesh;
        public List<SceneSurface> Surfaces { get; } = new();
        public WalkableArea WalkableArea => WalkableArea.Scanned;
        public bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit)
        {
            hit = default;
            return false;
        }
        public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point) =>
            _navMesh.TrySampleWalkable(near, radius, out point);
        public void GetSurfaces(List<SceneSurface> into)
        {
            into.Clear();
            into.AddRange(Surfaces);
        }
    }

    sealed class StubUserPose : IUserPoseProvider
    {
        public Pose Head { get; set; } = new(new Vector3(0f, 1.6f, 2f), Quaternion.Euler(0f, 180f, 0f));
        public bool TryGetPointingRay(out Ray ray)
        {
            ray = default;
            return false;
        }
        public void GetHandKeyPoints(List<Vector3> into) => into.Clear();
    }

    sealed class ScriptedLipSync : ILipSyncDriver
    {
        public MouthShape Current { get; set; }
        public void Reset() => Current = MouthShape.Closed;
    }

    /// <summary>
    /// Amplitude lip-sync over a synthetic tone: each frame takes the RMS of the samples a sine (or silence) would
    /// have produced in that frame and drives A with it, the way the amplitude fallback driver does.
    /// </summary>
    sealed class ToneLipSync : ILipSyncDriver
    {
        const int SampleRate = 24000;
        readonly float _frequency;
        readonly float _amplitude;
        long _sample;

        public ToneLipSync(float frequency, float amplitude)
        {
            _frequency = frequency;
            _amplitude = amplitude;
        }

        public bool ToneOn { get; set; }

        public MouthShape Current
        {
            get
            {
                var count = Mathf.Max(1, Mathf.RoundToInt(Time.deltaTime * SampleRate));
                var sum = 0.0;
                for (var i = 0; i < count; i++, _sample++)
                {
                    var s = ToneOn ? _amplitude * Mathf.Sin(2f * Mathf.PI * _frequency * _sample / SampleRate) : 0f;
                    sum += s * s;
                }
                var level = Mathf.Clamp01((float)System.Math.Sqrt(sum / count) * Mathf.Sqrt(2f));
                return new MouthShape(level, 0f, 0f, 0f, 0f, level);
            }
        }

        public void Reset() => ToneOn = false;
    }
}
