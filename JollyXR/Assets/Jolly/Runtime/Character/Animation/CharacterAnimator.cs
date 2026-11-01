using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>Writes Nubi's Animator parameters (architecture §5.5) by pre-hashed id.</summary>
    public sealed class CharacterAnimator
    {
        const float ConversationFadePerSecond = 4f;

        readonly Animator _animator;
        readonly LocomotionSettings _locomotion;
        readonly int _conversationLayer;
        float _conversationWeight = 1f;

        public CharacterAnimator(Animator animator, LocomotionSettings locomotion)
        {
            _animator = animator;
            _locomotion = locomotion;
            _conversationLayer = animator.GetLayerIndex(NubiAnimatorParameters.ConversationLayer);
        }

        public Animator Animator => _animator;

        /// <summary>The upper-body conversation poses fade out while walking, so the arms keep their walk swing.</summary>
        public void SetLocomotion(float speed, float deltaTime)
        {
            _animator.SetFloat(NubiAnimatorParameters.Speed, speed);
            _animator.SetFloat(NubiAnimatorParameters.LocomotionRate, LocomotionPlayback.Rate(speed, _locomotion));
            if (_conversationLayer < 0)
                return;
            var target = speed > 0.05f ? 0f : 1f;
            _conversationWeight = Mathf.MoveTowards(_conversationWeight, target, deltaTime * ConversationFadePerSecond);
            _animator.SetLayerWeight(_conversationLayer, _conversationWeight);
        }

        public void SetConversationState(ConversationState state)
        {
            _animator.SetBool(NubiAnimatorParameters.Listening, state == ConversationState.Listening);
            _animator.SetBool(NubiAnimatorParameters.Thinking, state == ConversationState.Thinking);
            _animator.SetBool(NubiAnimatorParameters.Talking, state == ConversationState.Speaking);
        }

        public void SetMood(Mood mood) => _animator.SetInteger(NubiAnimatorParameters.Mood, (int)mood);
    }
}
