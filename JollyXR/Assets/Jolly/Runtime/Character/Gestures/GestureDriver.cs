using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Plays a gesture clip on its layer (upper-body Gesture, or the full-body Action layer, <see cref="GestureClips.IsFullBody"/>)
    /// and completes when that layer has left the gesture state. A newer gesture supersedes the running one (its
    /// awaiter completes normally); cancelling the token fades the layer out. Which gestures the character has is read
    /// once from its controller's states.
    /// </summary>
    public sealed class GestureDriver
    {
        const float EnterTimeoutSeconds = 0.5f;
        const float AbortFadeSeconds = 0.15f;

        readonly Animator _animator;
        /// <summary>By GestureId: the layer holding its state, or -1 when the controller has none.</summary>
        readonly int[] _layerOf;
        int _generation;

        public GestureDriver(Animator animator)
        {
            _animator = animator;
            var gestureLayer = animator.GetLayerIndex(NubiAnimatorParameters.GestureLayer);
            if (gestureLayer < 0)
                throw new ArgumentException($"Animator has no '{NubiAnimatorParameters.GestureLayer}' layer.", nameof(animator));
            var actionLayer = animator.GetLayerIndex(NubiAnimatorParameters.ActionLayer);
            var gestures = (GestureId[])Enum.GetValues(typeof(GestureId));
            _layerOf = new int[gestures.Length];
            foreach (var gesture in gestures)
            {
                var layer = GestureClips.IsFullBody(gesture) ? actionLayer : gestureLayer;
                _layerOf[(int)gesture] = layer >= 0 && GestureClips.TryGetClip(gesture, out var clip)
                                         && animator.HasState(layer, Animator.StringToHash(clip))
                    ? layer
                    : -1;
            }
        }

        public bool IsPlaying { get; private set; }
        public GestureId Current { get; private set; }

        public bool CanPlay(GestureId gesture) => _layerOf[(int)gesture] >= 0;

        /// <param name="holdSeconds">How long a looping state keeps looping; 0 plays one loop. A one-shot ignores it.</param>
        public async UniTask Play(GestureId gesture, CancellationToken ct, float holdSeconds = 0f)
        {
            if (!CanPlay(gesture))
                throw new ArgumentOutOfRangeException(nameof(gesture), gesture, "The character has no clip for this gesture.");
            ct.ThrowIfCancellationRequested();

            var layer = _layerOf[(int)gesture];
            var generation = ++_generation;
            Current = gesture;
            IsPlaying = true;
            _animator.SetBool(NubiAnimatorParameters.GestureHold, holdSeconds > 0f);
            _animator.SetInteger(NubiAnimatorParameters.GestureId, (int)gesture);
            _animator.SetTrigger(NubiAnimatorParameters.Gesture);
            try
            {
                await WhileGesturePlays(generation, layer, holdSeconds, ct);
            }
            catch (OperationCanceledException) when (generation == _generation && _animator != null)
            {
                _animator.ResetTrigger(NubiAnimatorParameters.Gesture);
                _animator.CrossFadeInFixedTime(NubiAnimatorParameters.EmptyState, AbortFadeSeconds, layer);
                throw;
            }
            finally
            {
                if (generation == _generation)
                {
                    IsPlaying = false;
                    if (_animator != null)
                        _animator.SetBool(NubiAnimatorParameters.GestureHold, false);
                }
            }
        }

        async UniTask WhileGesturePlays(int generation, int layer, float holdSeconds, CancellationToken ct)
        {
            var entered = false;
            var waited = 0f;
            var held = 0f;
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (generation != _generation)
                    return;
                var inGesture = InGestureState(layer);
                if (entered && !inGesture)
                    return;
                entered |= inGesture;
                if (!entered && (waited += Time.deltaTime) > EnterTimeoutSeconds)
                    return;
                if (entered && holdSeconds > 0f && (held += Time.deltaTime) >= holdSeconds)
                {
                    _animator.SetBool(NubiAnimatorParameters.GestureHold, false);
                    holdSeconds = 0f;
                }
            }
        }

        bool InGestureState(int layer)
        {
            var tag = NubiAnimatorParameters.GestureTagHash;
            if (_animator.GetCurrentAnimatorStateInfo(layer).tagHash == tag && !_animator.IsInTransition(layer))
                return true;
            return _animator.IsInTransition(layer) && _animator.GetNextAnimatorStateInfo(layer).tagHash == tag;
        }
    }
}
