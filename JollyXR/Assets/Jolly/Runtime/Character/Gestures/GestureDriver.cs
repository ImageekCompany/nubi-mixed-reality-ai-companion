using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Plays a gesture clip on its layer (upper-body Gesture, or the full-body Action layer, <see cref="GestureClips.IsFullBody"/>)
    /// and completes when that layer has left the gesture state. The layers are independent: a newer gesture supersedes
    /// the running one on its own layer only (its awaiter completes normally), so a nod during a dance leaves the dance
    /// alone. Cancelling the token fades the layer out. Which gestures the character has is read once from its
    /// controller's states.
    /// </summary>
    public sealed class GestureDriver
    {
        const float EnterTimeoutSeconds = 0.5f;
        const float AbortFadeSeconds = 0.15f;
        /// <summary>A 4 s request on a 4.0x s clip still means one loop.</summary>
        const float LoopSlack = 0.01f;

        sealed class Slot
        {
            public int Generation;
            public bool IsPlaying;
            public GestureId Current;
        }

        readonly Animator _animator;
        /// <summary>By GestureId: the layer holding its state, or -1 when the controller has none.</summary>
        readonly int[] _layerOf;
        readonly int _actionLayer;
        readonly Slot _gestureSlot = new();
        readonly Slot _actionSlot = new();

        public GestureDriver(Animator animator)
        {
            _animator = animator;
            var gestureLayer = animator.GetLayerIndex(NubiAnimatorParameters.GestureLayer);
            if (gestureLayer < 0)
                throw new ArgumentException($"Animator has no '{NubiAnimatorParameters.GestureLayer}' layer.", nameof(animator));
            _actionLayer = animator.GetLayerIndex(NubiAnimatorParameters.ActionLayer);
            var gestures = (GestureId[])Enum.GetValues(typeof(GestureId));
            _layerOf = new int[gestures.Length];
            foreach (var gesture in gestures)
            {
                var layer = GestureClips.IsFullBody(gesture) ? _actionLayer : gestureLayer;
                _layerOf[(int)gesture] = layer >= 0 && GestureClips.TryGetClip(gesture, out var clip)
                                         && animator.HasState(layer, Animator.StringToHash(clip))
                    ? layer
                    : -1;
            }
        }

        public bool CanPlay(GestureId gesture) => _layerOf[(int)gesture] >= 0;

        /// <summary>True while <paramref name="gesture"/> is the one playing on its layer.</summary>
        public bool IsPlaying(GestureId gesture)
        {
            var layer = _layerOf[(int)gesture];
            if (layer < 0)
                return false;
            var slot = SlotOf(layer);
            return slot.IsPlaying && slot.Current == gesture;
        }

        /// <param name="holdSeconds">A looping state keeps looping until the first loop end at or after this long; 0 plays
        /// one loop. A one-shot ignores it.</param>
        public async UniTask Play(GestureId gesture, CancellationToken ct, float holdSeconds = 0f)
        {
            if (!CanPlay(gesture))
                throw new ArgumentOutOfRangeException(nameof(gesture), gesture, "The character has no clip for this gesture.");
            ct.ThrowIfCancellationRequested();

            var layer = _layerOf[(int)gesture];
            var slot = SlotOf(layer);
            var generation = ++slot.Generation;
            slot.Current = gesture;
            slot.IsPlaying = true;
            // Only the Action layer's looping states read the hold, so a Gesture-layer play never touches it.
            var holds = layer == _actionLayer;
            if (holds)
                _animator.SetBool(NubiAnimatorParameters.GestureHold, holdSeconds > 0f);
            _animator.SetInteger(NubiAnimatorParameters.GestureId, (int)gesture);
            _animator.SetTrigger(NubiAnimatorParameters.Gesture);
            try
            {
                await WhileGesturePlays(slot, generation, layer, holds ? holdSeconds : 0f, ct);
            }
            catch (OperationCanceledException) when (generation == slot.Generation && _animator != null)
            {
                _animator.ResetTrigger(NubiAnimatorParameters.Gesture);
                _animator.CrossFadeInFixedTime(NubiAnimatorParameters.EmptyState, AbortFadeSeconds, layer);
                throw;
            }
            finally
            {
                if (generation == slot.Generation)
                {
                    slot.IsPlaying = false;
                    if (holds && _animator != null)
                        _animator.SetBool(NubiAnimatorParameters.GestureHold, false);
                }
            }
        }

        Slot SlotOf(int layer) => layer == _actionLayer ? _actionSlot : _gestureSlot;

        async UniTask WhileGesturePlays(Slot slot, int generation, int layer, float holdSeconds, CancellationToken ct)
        {
            var entered = false;
            var waited = 0f;
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                if (generation != slot.Generation)
                    return;
                var inGesture = InGestureState(layer, out var state);
                if (entered && !inGesture)
                    return;
                entered |= inGesture;
                if (!entered && (waited += Time.deltaTime) > EnterTimeoutSeconds)
                    return;
                // The exit transition fires near the end of whichever loop is playing once the hold is off, so the hold
                // is released as the last wanted loop begins: a 4 s dance on a 4 s loop is one loop, not two.
                if (entered && holdSeconds > 0f && state.length > 0f)
                {
                    var loops = Mathf.Max(1, Mathf.CeilToInt(holdSeconds / state.length - LoopSlack));
                    if (state.normalizedTime >= loops - 1)
                    {
                        _animator.SetBool(NubiAnimatorParameters.GestureHold, false);
                        holdSeconds = 0f;
                    }
                }
            }
        }

        /// <summary><paramref name="state"/> is the gesture state's info, the current one or the one being entered.</summary>
        bool InGestureState(int layer, out AnimatorStateInfo state)
        {
            var tag = NubiAnimatorParameters.GestureTagHash;
            var inTransition = _animator.IsInTransition(layer);
            state = _animator.GetCurrentAnimatorStateInfo(layer);
            if (state.tagHash == tag && !inTransition)
                return true;
            if (!inTransition)
                return false;
            state = _animator.GetNextAnimatorStateInfo(layer);
            return state.tagHash == tag;
        }
    }
}
