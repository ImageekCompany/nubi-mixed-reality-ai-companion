using System;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>What one canonical face channel drives on a character's mesh.</summary>
    [Serializable]
    public sealed class FaceChannelBinding
    {
        [Tooltip("Canonical channel: sil, A, I, U, E, O, Smile, Frown, Blink_L, Blink_R, Happy_Eyes, Wide_Eyes, Sad_Eyes.")]
        public string channel = "";
        [Tooltip("Blendshapes driven by this channel, each at channel weight × its weight. A shape several channels " +
                 "drive takes the strongest.")]
        public BlendShapeTarget[] blendShapes = Array.Empty<BlendShapeTarget>();
        [Range(0f, 1f)]
        [Tooltip("How far this channel opens the jaw bone (visemes on a jaw-bone mouth).")]
        public float jawOpen;

        /// <summary>Every canonical channel onto the blendshape of the same name, as Nubi is built.</summary>
        public static FaceChannelBinding[] Identity()
        {
            var bindings = new FaceChannelBinding[FaceChannels.Count];
            for (var i = 0; i < bindings.Length; i++)
            {
                var name = FaceChannels.ShapeNames[i];
                bindings[i] = new FaceChannelBinding
                {
                    channel = name,
                    blendShapes = new[] { new BlendShapeTarget { name = name, weight = 1f } },
                };
            }
            return bindings;
        }
    }

    [Serializable]
    public sealed class BlendShapeTarget
    {
        public string name = "";
        [Range(0f, 1f)] public float weight = 1f;
    }

    /// <summary>Optional jaw-bone mouth. The bone is driven absolutely from its bind pose, so clips must not key it.</summary>
    [Serializable]
    public sealed class JawBinding
    {
        [Tooltip("Empty when the mouth is blendshapes only.")]
        public string bone = "";
        [Tooltip("Local Euler rotation from the bind pose at a fully open jaw, degrees.")]
        public Vector3 openRotation;
    }

    /// <summary>Bone names in the model, resolved once when the character is bound.</summary>
    [Serializable]
    public sealed class RigBoneNames
    {
        public string chest = "Chest";
        public string neck = "Neck";
        public string head = "Head";
        [Tooltip("Eye bones for the last few degrees of a look; a character without movable eyes adds unweighted ones.")]
        public string eyeLeft = "Eye_L";
        public string eyeRight = "Eye_R";
        [Tooltip("Root of the upper-body avatar mask: this bone and its children play gestures and conversation poses.")]
        public string upperBody = "Spine";
        [Tooltip("Feet that shrink and fade the contact shadow as they leave the floor; a character without either keeps " +
                 "a standing shadow.")]
        public string footLeft = "Foot_L";
        public string footRight = "Foot_R";
    }

    [Serializable]
    public sealed class ClipBinding
    {
        [Tooltip("Canonical clip name (CharacterClips).")]
        public string name = "";
        public AnimationClip clip;
    }

    /// <summary>Baked canned-line clips of one language, each named after its line key (e.g. greeting_01).</summary>
    [Serializable]
    public sealed class LineClipSet
    {
        public string languageTag = "en";
        public AudioClip[] clips = Array.Empty<AudioClip>();
    }
}
