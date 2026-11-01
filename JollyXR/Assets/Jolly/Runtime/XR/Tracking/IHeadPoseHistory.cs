using UnityEngine;

namespace Jolly.XR.Tracking
{
    public interface IHeadPoseHistory
    {
        /// <summary>Head pose at a past Time.realtimeSinceStartupAsDouble, interpolated between rendered frames.</summary>
        bool TryGetHeadAt(double realtimeSinceStartup, out Pose head);
    }
}
