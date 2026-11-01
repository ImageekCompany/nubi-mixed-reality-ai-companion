using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    public sealed class FakeUserPoseProvider : IUserPoseProvider
    {
        public Pose Head { get; set; } = Pose.identity;
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
