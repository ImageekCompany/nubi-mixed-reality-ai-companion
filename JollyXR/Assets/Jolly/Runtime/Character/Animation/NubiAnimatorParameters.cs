using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>Parameter, layer and state names shared by the runtime and the controller builder.</summary>
    public static class NubiAnimatorParameters
    {
        public const string SpeedName = "Speed";
        public const string LocomotionRateName = "LocomotionRate";
        public const string ListeningName = "Listening";
        public const string ThinkingName = "Thinking";
        public const string TalkingName = "Talking";
        public const string MoodName = "Mood";
        public const string GestureIdName = "GestureId";
        public const string GestureName = "Gesture";
        /// <summary>While set, a looping gesture state does not leave at its exit time (<see cref="ICharacterController.PlayLooped"/>).</summary>
        public const string GestureHoldName = "GestureHold";

        public const string ConversationLayer = "Conversation";
        public const string GestureLayer = "Gesture";
        /// <summary>Full-body gestures (Dance, Jump, Bow), unmasked and on top of every other layer.</summary>
        public const string ActionLayer = "Action";
        public const string LocomotionState = "Locomotion";
        public const string EmptyState = "Empty";
        public const string GestureTag = "Gesture";

        public static readonly int Speed = Animator.StringToHash(SpeedName);
        public static readonly int LocomotionRate = Animator.StringToHash(LocomotionRateName);
        public static readonly int Listening = Animator.StringToHash(ListeningName);
        public static readonly int Thinking = Animator.StringToHash(ThinkingName);
        public static readonly int Talking = Animator.StringToHash(TalkingName);
        public static readonly int Mood = Animator.StringToHash(MoodName);
        public static readonly int GestureId = Animator.StringToHash(GestureIdName);
        public static readonly int Gesture = Animator.StringToHash(GestureName);
        public static readonly int GestureHold = Animator.StringToHash(GestureHoldName);
        public static readonly int GestureTagHash = Animator.StringToHash(GestureTag);
        public static readonly int LocomotionStateHash = Animator.StringToHash(LocomotionState);
    }
}
