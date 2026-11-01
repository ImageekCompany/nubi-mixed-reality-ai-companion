using System;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>The blob shadow quad and the bones it reads. Bound by the prefab builder.</summary>
    [Serializable]
    public sealed class ContactShadowRig
    {
        [Tooltip("Child quad on the floor plane with Jolly/Character/BlobShadow.")]
        public MeshRenderer shadow;
        public Transform footLeft;
        public Transform footRight;
        [Tooltip("Height of the lower foot bone above the root in the bind pose, m.")]
        public float footRestHeight;

        public bool IsComplete => shadow != null && footLeft != null && footRight != null;
    }
}
