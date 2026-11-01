using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// The parts of a model instance the controller drives, found by the names its <see cref="CharacterDefinition"/>
    /// gives. Binding changes nothing unless every part is found; then it points the Animator at the definition's
    /// controller.
    /// </summary>
    public sealed class CharacterRig
    {
        CharacterRig(Animator animator, SkinnedMeshRenderer face, NubiRigBones bones, Transform jaw)
        {
            Animator = animator;
            Face = face;
            Bones = bones;
            Jaw = jaw;
        }

        public Animator Animator { get; }
        public SkinnedMeshRenderer Face { get; }
        public NubiRigBones Bones { get; }
        /// <summary>Null when the mouth is blendshapes only.</summary>
        public Transform Jaw { get; }

        public static bool TryBind(GameObject model, CharacterDefinition definition, out CharacterRig rig, out string error)
        {
            rig = null;
            var root = model.transform;
            var names = definition.bones;
            var bones = new NubiRigBones
            {
                chest = Find(root, names.chest), neck = Find(root, names.neck), head = Find(root, names.head),
                eyeLeft = Find(root, names.eyeLeft), eyeRight = Find(root, names.eyeRight),
            };
            if (!bones.IsComplete)
            {
                error = $"missing a look-at bone (chest '{names.chest}', neck '{names.neck}', head '{names.head}', " +
                        $"eyes '{names.eyeLeft}'/'{names.eyeRight}')";
                return false;
            }

            Transform jaw = null;
            if (!string.IsNullOrEmpty(definition.jaw.bone) && (jaw = Find(root, definition.jaw.bone)) == null)
            {
                error = $"missing jaw bone '{definition.jaw.bone}'";
                return false;
            }

            var face = FindFace(model, definition.faceMesh);
            if (face == null)
            {
                error = string.IsNullOrEmpty(definition.faceMesh)
                    ? "no SkinnedMeshRenderer with blendshapes"
                    : $"no SkinnedMeshRenderer named '{definition.faceMesh}'";
                return false;
            }

            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = model.AddComponent<Animator>();
            if (definition.avatar != null)
                animator.avatar = definition.avatar;
            if (animator.runtimeAnimatorController != definition.animatorController)
                animator.runtimeAnimatorController = definition.animatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            rig = new CharacterRig(animator, face, bones, jaw);
            error = null;
            return true;
        }

        public static Transform Find(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        public static SkinnedMeshRenderer FindFace(GameObject model, string name)
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (string.IsNullOrEmpty(name)
                        ? renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0
                        : renderer.name == name)
                    return renderer;
            }
            return null;
        }
    }
}
