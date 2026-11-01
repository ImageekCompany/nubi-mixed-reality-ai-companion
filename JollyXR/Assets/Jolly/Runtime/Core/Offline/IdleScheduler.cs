using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Offline
{
    /// <summary>
    /// Idle animations in every connectivity state (architecture §5.4). Never speaks. Acts only while the conversation is
    /// Idle and the character neither moves nor follows, so a wander never supersedes a walk or follow that outlived a
    /// barge-in. A wander only starts the move: its arrival is not awaited.
    /// </summary>
    public sealed class IdleScheduler
    {
        readonly ICharacterController _character;
        readonly IClock _clock;
        readonly IRandom _random;
        readonly IdleConfig _config;
        readonly float _totalWeight;

        public IdleScheduler(ICharacterController character, IClock clock, IRandom random, IdleConfig config)
        {
            _character = character;
            _clock = clock;
            _random = random;
            _config = config;
            _totalWeight = config.LookAroundWeight + config.StretchWeight + config.WanderWeight + config.GlanceWeight;
        }

        /// <summary>Runs until ct is cancelled, then completes as cancelled.</summary>
        public async UniTask RunAsync(Func<bool> isConversationIdle, CancellationToken ct)
        {
            var minSeconds = (float)_config.MinInterval.TotalSeconds;
            var maxSeconds = (float)_config.MaxInterval.TotalSeconds;
            while (true)
            {
                await _clock.Delay(TimeSpan.FromSeconds(_random.Range(minSeconds, maxSeconds)), ct);
                if (CanAct(isConversationIdle) && _totalWeight > 0f)
                    await RunOne(isConversationIdle, ct);
            }
        }

        bool CanAct(Func<bool> isConversationIdle) =>
            isConversationIdle() && !_character.IsMoving && _character.Mode == LocomotionMode.None;

        UniTask RunOne(Func<bool> isConversationIdle, CancellationToken ct)
        {
            var roll = _random.Range(0f, _totalWeight);
            if ((roll -= _config.LookAroundWeight) < 0f)
                return _character.PlayGesture(GestureId.LookAround, ct);
            if ((roll -= _config.StretchWeight) < 0f)
                return _character.PlayGesture(GestureId.Stretch, ct);
            if ((roll -= _config.WanderWeight) < 0f)
            {
                Wander();
                return UniTask.CompletedTask;
            }
            return Glance(isConversationIdle, ct);
        }

        void Wander()
        {
            var angle = _random.Range(0f, 2f * Mathf.PI);
            var distance = _random.Range(_config.WanderMinDistance, _config.WanderRadius);
            var target = _character.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            _character.TryStartMoveTo(target, _config.WanderStopDistance, out _);
        }

        async UniTask Glance(Func<bool> isConversationIdle, CancellationToken ct)
        {
            _character.LookAt(LookTarget.User);
            await _clock.Delay(_config.GlanceDuration, ct);
            if (isConversationIdle())
                _character.LookAt(LookTarget.None);
        }
    }
}
