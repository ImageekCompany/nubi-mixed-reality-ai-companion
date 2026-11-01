using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Plays a gesture clip on the Gesture layer and completes when the layer has left the gesture state. A newer
    /// gesture supersedes the running one (its awaiter completes normally); cancelling the token fades the layer out.
    /// </summary>
    public sealed class GestureDriver
    {
        const float EnterTimeoutSeconds = 0.5f;
        const float AbortFadeSeconds = 0.15f;

        readonly Animator _animator;
        readonly int _layer;
        int _generation;

        public GestureDriver(Animator animator)
        {
            _animator = animator;
            _layer = animator.GetLayerIndex(NubiAnimatorParameters.GestureLayer);
            if (_layer < 0)
                throw new ArgumentException($"Animator has no '{NubiAnimatorParameters.GestureLayer}' layer.", nameof(animator));
        }

        public bool IsPlaying { get; private set; }
        public GestureId Current { get; private set; }

        public async UniTask Play(GestureId gesture, CancellationToken ct)
        {
            if (!GestureClips.TryGetClip(gesture, out _))
                throw new ArgumentOutOfRangeException(nameof(gesture), gesture, "Gesture has no clip.");
            ct.ThrowIfCancellationRequested();

            var generation = ++_generation;
            Current = gesture;
            IsPlaying = true;
            _animator.SetInteger(NubiAnimatorParameters.GestureId, (int)gesture);
            _animator.SetTrigger(NubiAnimatorParameters.Gesture);
            try
            {
                await WhileGesturePlays(generation, ct);
            }
            catch (OperationCanceledException) when (generation == _generation && _animator != null)
            {
                _animator.ResetTrigger(NubiAnimatorParameters.Gesture);
                _animator.CrossFadeInFixedTime(NubiAnimatorParameters.EmptyState, AbortFadeSeconds, _layer);
                throw;
            }
            finally
            {
                if (generation == _generation)
                    IsPlaying = false;
            }
        }

        async UniTask WhileGesturePlays(int generation, CancellationToken ct)
        {
            var entered = false;
            var waited = 0f;
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (generation != _generation)
                    return;
                var inGesture = InGestureState();
                if (entered && !inGesture)
                    return;
                entered |= inGesture;
                if (!entered && (waited += Time.deltaTime) > EnterTimeoutSeconds)
                    return;
            }
        }

        bool InGestureState()
        {
            var tag = NubiAnimatorParameters.GestureTagHash;
            if (_animator.GetCurrentAnimatorStateInfo(_layer).tagHash == tag && !_animator.IsInTransition(_layer))
                return true;
            return _animator.IsInTransition(_layer) && _animator.GetNextAnimatorStateInfo(_layer).tagHash == tag;
        }
    }
}
