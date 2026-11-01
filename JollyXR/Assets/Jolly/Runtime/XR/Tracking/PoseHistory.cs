using UnityEngine;

namespace Jolly.XR.Tracking
{
    /// <summary>
    /// Fixed-capacity ring of timestamped poses, so a camera image 20–40 ms old can be paired with the head pose at
    /// its exposure instead of the pose at acquisition. Allocation-free after construction.
    /// </summary>
    public sealed class PoseHistory
    {
        readonly double[] _times;
        readonly Pose[] _poses;
        int _next;
        int _count;

        public PoseHistory(int capacity)
        {
            _times = new double[capacity];
            _poses = new Pose[capacity];
        }

        /// <summary>Timestamps must increase; an older or equal one is dropped.</summary>
        public void Add(double time, Pose pose)
        {
            if (_count > 0 && time <= _times[IndexFromNewest(0)])
                return;
            _times[_next] = time;
            _poses[_next] = pose;
            _next = (_next + 1) % _times.Length;
            if (_count < _times.Length)
                _count++;
        }

        /// <summary>Interpolated pose at time. Newer than the newest sample: the newest. Older than the oldest: false.</summary>
        public bool TrySample(double time, out Pose pose)
        {
            pose = default;
            if (_count == 0)
                return false;

            var newer = IndexFromNewest(0);
            if (time >= _times[newer])
            {
                pose = _poses[newer];
                return true;
            }

            for (var age = 1; age < _count; age++)
            {
                var older = IndexFromNewest(age);
                if (time < _times[older])
                {
                    newer = older;
                    continue;
                }
                var t = (float)((time - _times[older]) / (_times[newer] - _times[older]));
                pose = new Pose(
                    Vector3.Lerp(_poses[older].position, _poses[newer].position, t),
                    Quaternion.Slerp(_poses[older].rotation, _poses[newer].rotation, t));
                return true;
            }
            return false;
        }

        int IndexFromNewest(int age) => (_next - 1 - age + _times.Length * 2) % _times.Length;
    }
}
