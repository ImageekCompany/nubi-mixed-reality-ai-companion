using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>In-memory anchors with ids "anchor_N". Persistence can be switched off to exercise the null-id path.</summary>
    public sealed class FakeSpatialAnchors : ISpatialAnchors
    {
        public Dictionary<string, Pose> Anchors { get; } = new();
        public bool SupportsPersistence { get; set; } = true;

        public UniTask<string> CreateAndSave(Pose pose, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!SupportsPersistence)
                return UniTask.FromResult<string>(null);
            var id = "anchor_" + Anchors.Count;
            Anchors[id] = pose;
            return UniTask.FromResult(id);
        }

        public UniTask<Pose?> TryResolve(string anchorId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return UniTask.FromResult(Anchors.TryGetValue(anchorId, out var pose) ? pose : (Pose?)null);
        }
    }
}
