using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>ISceneData that records requeries, capture requests and the fallback floor it was given.</summary>
    public sealed class FakeSceneData : ISceneData
    {
        public bool PermissionGranted { get; set; } = true;
        public bool HasScannedFloor { get; set; }
        public bool CanRequestCapture { get; set; } = true;
        public bool CaptureAccepted { get; set; } = true;
        /// <summary>False makes SetFallbackFloor refuse, as a non-Floor tracking origin does.</summary>
        public bool FallbackSupported { get; set; } = true;

        public int CaptureRequests { get; private set; }
        public int Requeries { get; private set; }
        public int FallbackSets { get; private set; }
        public Vector3? FallbackCenter { get; private set; }
        public List<Vector2> FallbackOutline { get; } = new();

        public bool TryRequestCapture()
        {
            CaptureRequests++;
            return CaptureAccepted;
        }

        public void Requery() => Requeries++;

        public bool SetFallbackFloor(Vector3 center, IReadOnlyList<Vector2> outline)
        {
            if (!FallbackSupported)
                return false;
            FallbackSets++;
            FallbackCenter = center;
            FallbackOutline.Clear();
            FallbackOutline.AddRange(outline);
            return true;
        }

        public void ClearFallbackFloor() => FallbackCenter = null;
    }
}
