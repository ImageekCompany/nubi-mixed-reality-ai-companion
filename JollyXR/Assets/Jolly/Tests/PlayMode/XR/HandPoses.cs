using Jolly.XR.Hands;
using UnityEngine;

namespace Jolly.Tests.PlayMode.XR
{
    /// <summary>Synthetic right-hand snapshots in a hand frame: wrist at the origin, fingers along +Z, palm facing -Y
    /// (OpenXR / XR Hands joint convention), then placed with a world pose.</summary>
    static class HandPoses
    {
        public static HandSnapshot Open(Pose world) => Build(world, indexOut: true, othersOut: true, pinchGap: 0.06f);
        public static HandSnapshot Pointing(Pose world) => Build(world, indexOut: true, othersOut: false, pinchGap: 0.06f);
        public static HandSnapshot Fist(Pose world) => Build(world, indexOut: false, othersOut: false, pinchGap: 0.05f);
        public static HandSnapshot Pinch(Pose world, float gap) => Build(world, indexOut: false, othersOut: true, pinchGap: gap);

        static HandSnapshot Build(Pose world, bool indexOut, bool othersOut, float pinchGap)
        {
            Vector3 W(Vector3 local) => world.position + world.rotation * local;
            Vector3 Tip(float x, bool extended) => extended ? new Vector3(x, 0f, 0.18f) : new Vector3(x, -0.04f, 0.07f);

            var indexTip = Tip(0.02f, indexOut);
            var thumbTip = indexTip + new Vector3(-pinchGap, 0f, 0f);
            return new HandSnapshot(
                wrist: W(Vector3.zero),
                palm: W(new Vector3(0f, 0f, 0.05f)),
                palmRotation: world.rotation,
                thumbTip: W(thumbTip),
                indexProximal: W(new Vector3(0.02f, 0f, 0.09f)),
                indexTip: W(indexTip),
                middleProximal: W(new Vector3(0f, 0f, 0.09f)),
                middleTip: W(Tip(0f, othersOut)),
                ringTip: W(Tip(-0.02f, othersOut)),
                littleTip: W(Tip(-0.04f, othersOut) * 0.85f));
        }
    }
}
