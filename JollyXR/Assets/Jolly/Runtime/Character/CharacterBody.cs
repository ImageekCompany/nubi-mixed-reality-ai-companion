using UnityEngine;
using UnityEngine.AI;

namespace Jolly.Character
{
    /// <summary>
    /// Sizes the character root's parts from its definition: model scale and facing, agent, collider and contact shadow. Shared
    /// by the prefab builder and the runtime swap, so a definition edit needs no prefab rebuild. Null parts are skipped.
    /// </summary>
    public static class CharacterBody
    {
        public static void Apply(CharacterDefinition d, Transform model, NavMeshAgent agent, CapsuleCollider collider,
                                 Transform contactShadow)
        {
            if (model != null)
            {
                model.localScale = Vector3.one * d.scale;
                model.localRotation = Quaternion.Euler(0f, d.modelYaw, 0f);
            }
            if (agent != null)
            {
                agent.radius = d.agentRadius;
                agent.height = d.height;
            }
            if (collider != null)
            {
                collider.center = new Vector3(0f, 0.5f * d.height, 0f);
                collider.radius = d.colliderRadius;
                collider.height = d.height;
            }
            // The shadow is a unit quad lying on the floor (rotated 90° about X), so its local y runs front to back.
            if (contactShadow != null)
                contactShadow.localScale = new Vector3(2f * d.contactShadowRadius.x, 2f * d.contactShadowRadius.y, 1f);
        }
    }
}
