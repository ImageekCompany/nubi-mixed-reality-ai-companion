using System;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>Bones the procedural layers write after the Animator. Bound by the prefab builder.</summary>
    [Serializable]
    public sealed class NubiRigBones
    {
        public Transform chest;
        public Transform neck;
        public Transform head;
        public Transform eyeLeft;
        public Transform eyeRight;

        public bool IsComplete => chest != null && neck != null && head != null && eyeLeft != null && eyeRight != null;
    }
}
