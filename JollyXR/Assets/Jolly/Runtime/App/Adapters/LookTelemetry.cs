using System;
using System.Globalization;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using UnityEngine;

namespace Jolly.App.Adapters
{
    /// <summary>
    /// <c>evt=look.user</c> once per turn, when its first audio plays (the turn's first Speaking): how far the
    /// character's body and head face away from the user's head, what it attends and where the user's head is
    /// (ADR-0041). The self-test reads <see cref="Last"/>.
    /// </summary>
    public sealed class LookTelemetry : IDisposable
    {
        public readonly struct Sample
        {
            public Sample(int turn, float bodyYawOff, float headYawOff, AttentionTarget attending, float userDistance,
                          float userHeight)
            {
                Turn = turn;
                BodyYawOff = bodyYawOff;
                HeadYawOff = headYawOff;
                Attending = attending;
                UserDistance = userDistance;
                UserHeight = userHeight;
            }

            public int Turn { get; }
            public float BodyYawOff { get; }
            public float HeadYawOff { get; }
            public AttentionTarget Attending { get; }
            /// <summary>Floor distance from the character to the user's head, m.</summary>
            public float UserDistance { get; }
            /// <summary>The user's head above the character's feet, m: about 0 means the head pose is not tracked.</summary>
            public float UserHeight { get; }
        }

        const string Category = "character";

        readonly ConversationStateMachine _machine;
        readonly NubiCharacterController _character;
        readonly IUserPoseProvider _user;
        readonly ILog _log;
        int _turns;
        bool _sampled;

        public LookTelemetry(ConversationStateMachine machine, NubiCharacterController character, IUserPoseProvider user,
                             ILog log)
        {
            _machine = machine;
            _character = character;
            _user = user;
            _log = log;
            _machine.Changed += OnChanged;
        }

        /// <summary>The latest turn's sample; Turn is 0 before the first.</summary>
        public Sample Last { get; private set; }

        public void Dispose() => _machine.Changed -= OnChanged;

        void OnChanged(ConversationState from, ConversationState to)
        {
            if (to is ConversationState.Idle or ConversationState.Listening)
                _sampled = false;
            if (to != ConversationState.Speaking || _sampled || !_character.TryMeasureUser(out var body, out var head))
                return;
            _sampled = true;
            var offset = _user.Head.position - _character.Position;
            Last = new Sample(++_turns, body, head, _character.Attending,
                              Vector3.ProjectOnPlane(offset, Vector3.up).magnitude, offset.y);
            if (_log.IsEnabled(LogLevel.Info))
                _log.Write(LogLevel.Info, Category, Format(Last));
        }

        static string Format(Sample s) => string.Format(CultureInfo.InvariantCulture,
            "evt=look.user turn={0} body_yaw_off={1:0.0} head_yaw_off={2:0.0} attending={3} user_dist={4:0.00} user_h={5:0.00}",
            s.Turn, s.BodyYawOff, s.HeadYawOff, s.Attending.ToString().ToLowerInvariant(), s.UserDistance, s.UserHeight);
    }
}
