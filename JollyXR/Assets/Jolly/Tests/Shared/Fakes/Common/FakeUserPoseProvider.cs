using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>IUserPoseProvider with a settable head (standing height by default), pointing ray and hand key points.</summary>
    public sealed class FakeUserPoseProvider : IUserPoseProvider
    {
        public Pose Head { get; set; } = new(new Vector3(0, 1.6f, 0), Quaternion.identity);
        /// <summary>Null means no hand is pointing.</summary>
        public Ray? PointingRay { get; set; }
        public List<Vector3> HandKeyPoints { get; } = new();

        public bool TryGetPointingRay(out Ray ray)
        {
            ray = PointingRay.GetValueOrDefault();
            return PointingRay.HasValue;
        }

        public void GetHandKeyPoints(List<Vector3> into)
        {
            into.Clear();
            into.AddRange(HandKeyPoints);
        }
    }
}
