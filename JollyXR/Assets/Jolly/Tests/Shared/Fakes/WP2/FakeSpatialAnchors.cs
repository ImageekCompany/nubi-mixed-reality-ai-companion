using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Conversation
{
    /// <summary>In-memory anchors. <see cref="Supported"/> false mimics a platform without persistence (returns null ids).</summary>
    public sealed class FakeSpatialAnchors : ISpatialAnchors
    {
        readonly Dictionary<string, Pose> _anchors = new();
        int _next;

        public bool Supported { get; set; } = true;

        public UniTask<string> CreateAndSave(Pose pose, CancellationToken ct)
        {
            if (!Supported)
                return UniTask.FromResult<string>(null);
            var id = "anchor-" + ++_next;
            _anchors[id] = pose;
            return UniTask.FromResult(id);
        }

        public UniTask<Pose?> TryResolve(string anchorId, CancellationToken ct) =>
            UniTask.FromResult(_anchors.TryGetValue(anchorId, out var pose) ? pose : (Pose?)null);

        /// <summary>Simulates a recenter: the anchor now resolves to a different world pose.</summary>
        public void Move(string anchorId, Pose pose) => _anchors[anchorId] = pose;

        public void Forget(string anchorId) => _anchors.Remove(anchorId);
    }
}
