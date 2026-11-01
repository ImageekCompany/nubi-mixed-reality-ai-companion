using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// The parts of a model instance the controller drives, found by the names its <see cref="CharacterDefinition"/>
    /// gives. Binding changes nothing unless every part is found; then it points the Animator at the definition's
    /// controller. The feet are optional: without them the contact shadow keeps its standing size.
    /// </summary>
    public sealed class CharacterRig
    {
        CharacterRig(Animator animator, SkinnedMeshRenderer face, NubiRigBones bones, Transform jaw,
                     Transform footLeft, Transform footRight, float footRestHeight, Vector3 headForward)
        {
            HeadForward = headForward;
            Animator = animator;
            Face = face;
            Bones = bones;
            Jaw = jaw;
            FootLeft = footLeft;
            FootRight = footRight;
            FootRestHeight = footRestHeight;
        }

        public Animator Animator { get; }
        public SkinnedMeshRenderer Face { get; }
        public NubiRigBones Bones { get; }
        /// <summary>Null when the mouth is blendshapes only.</summary>
        public Transform Jaw { get; }
        /// <summary>Null, with <see cref="FootRight"/>, when the model lacks either foot bone.</summary>
        public Transform FootLeft { get; }
        public Transform FootRight { get; }
        public bool HasFeet => FootLeft != null;
        /// <summary>Height of the lower foot above the model root in the bind pose, in model units (before scale).</summary>
        public float FootRestHeight { get; }
        /// <summary>The head bone's local axis that points along the model's forward in the bind pose.</summary>
        public Vector3 HeadForward { get; }

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

            var footLeft = Find(root, names.footLeft);
            var footRight = Find(root, names.footRight);
            var footRestHeight = 0f;
            if (footLeft == null || footRight == null)
                footLeft = footRight = null;
            else
                footRestHeight = BindPoseFootHeight(definition.model.transform, names);

            rig = new CharacterRig(animator, face, bones, jaw, footLeft, footRight, footRestHeight,
                                   BindPoseHeadForward(definition.model.transform, names));
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

        /// <summary>
        /// Read from the model asset, never the instance: an instance may already be animated when it is bound, while the
        /// asset's transforms always hold the bind pose.
        /// </summary>
        static float BindPoseFootHeight(Transform asset, RigBoneNames names)
        {
            var left = Find(asset, names.footLeft);
            var right = Find(asset, names.footRight);
            if (left == null || right == null)
                return 0f;
            return Mathf.Min(asset.InverseTransformPoint(left.position).y, asset.InverseTransformPoint(right.position).y);
        }

        /// <summary>
        /// From the model asset, like <see cref="BindPoseFootHeight"/>. The instance is the asset under the character root,
        /// so the head's bind rotation relative to the character root is its world rotation in the asset.
        /// </summary>
        static Vector3 BindPoseHeadForward(Transform asset, RigBoneNames names)
        {
            var head = Find(asset, names.head);
            return head == null ? Vector3.forward : Quaternion.Inverse(head.rotation) * Vector3.forward;
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
