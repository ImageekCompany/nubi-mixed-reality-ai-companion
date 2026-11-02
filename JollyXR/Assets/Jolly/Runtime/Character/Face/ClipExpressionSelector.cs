using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Picks the face preset of whatever the Animator shows on top: a full-body action, a gesture, then a conversation pose, then the
    /// base layer (inside the Locomotion blend tree, by speed).
    /// </summary>
    public sealed class ClipExpressionSelector
    {
        readonly Animator _animator;
        readonly FacePresetTable _table;
        readonly LocomotionSettings _locomotion;
        readonly int _conversationLayer;
        readonly int _gestureLayer;
        readonly int _actionLayer;
        readonly float[] _idle;
        readonly float[] _walk;
        readonly float[] _hop;

        public ClipExpressionSelector(Animator animator, FacePresetTable table, LocomotionSettings locomotion)
        {
            _animator = animator;
            _table = table;
            _locomotion = locomotion;
            _conversationLayer = animator.GetLayerIndex(NubiAnimatorParameters.ConversationLayer);
            _gestureLayer = animator.GetLayerIndex(NubiAnimatorParameters.GestureLayer);
            _actionLayer = animator.GetLayerIndex(NubiAnimatorParameters.ActionLayer);
            _idle = table.ForClip("Idle");
            _walk = table.ForClip("Walk");
            _hop = table.ForClip("Hop");
        }

        /// <summary>Weights indexed by <see cref="FaceChannel"/>, or null for a neutral face.</summary>
        public float[] Select(float speed)
        {
            if (TryLayer(_actionLayer, out var action))
                return action;
            if (TryLayer(_gestureLayer, out var gesture))
                return gesture;
            if (TryLayer(_conversationLayer, out var pose))
                return pose;

            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != NubiAnimatorParameters.LocomotionStateHash)
                return _table.ForState(state.shortNameHash);
            if (speed > _locomotion.hopAbove)
                return _hop;
            return speed > 0.05f ? _walk : _idle;
        }

        bool TryLayer(int layer, out float[] preset)
        {
            preset = null;
            if (layer < 0 || _animator.GetLayerWeight(layer) < 0.5f)
                return false;
            preset = _table.ForState(_animator.GetCurrentAnimatorStateInfo(layer).shortNameHash);
            return preset != null;
        }
    }
}
