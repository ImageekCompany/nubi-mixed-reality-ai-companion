using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// The blob shadow quad and the feet it reads, taken from the bound character at Construct (ADR-0024, ADR-0026).
    /// </summary>
    public sealed class ContactShadowRig
    {
        /// <summary>Child quad on the floor plane with Jolly/Character/BlobShadow.</summary>
        public MeshRenderer shadow;
        public Transform footLeft;
        public Transform footRight;
        /// <summary>Height of the lower foot bone above the character root in the bind pose, m.</summary>
        public float footRestHeight;

        public bool IsComplete => shadow != null && footLeft != null && footRight != null;
    }
}
