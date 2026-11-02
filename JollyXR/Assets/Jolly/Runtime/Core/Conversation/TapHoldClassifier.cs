using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Splits a pinch into a tap that moves the character and a hold that talks (ADR-0044). At the press,
    /// <c>captureTarget</c> says whether a tap there would move the character (and holds that target for it). If so the
    /// press waits: released within the tap time it is <see cref="Tapped"/> and opens no turn, held past it it becomes
    /// <see cref="IVoiceActivation.Pressed"/>. With no target the press talks at once. Either way pinch barge-in keeps its
    /// 150 ms (architecture §5.2): <see cref="Interrupting"/> silences a busy turn at the press of a waiting one.
    /// <see cref="Began"/> marks a waiting press, for the microphone pre-roll.
    /// Main thread only.
    /// </summary>
    public sealed class TapHoldClassifier : IVoiceActivation, IDisposable
    {
        readonly IVoiceActivation _pinch;
        readonly IClock _clock;
        readonly TimeSpan _tapMax;
        readonly Func<bool> _captureTarget;
        CancellationTokenSource _waiting;
        bool _disposed;

        public TapHoldClassifier(IVoiceActivation pinch, IClock clock, TimeSpan tapMax, Func<bool> captureTarget)
        {
            _pinch = pinch;
            _clock = clock;
            _tapMax = tapMax;
            _captureTarget = captureTarget;
            _pinch.Pressed += OnPressed;
            _pinch.Released += OnReleased;
        }

        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;
        /// <summary>A press with a target, just before it waits: a busy turn is silenced now, tap or hold (ADR-0044), and
        /// before <see cref="Began"/>, so the pre-roll hears a silent character.</summary>
        public event Action Interrupting;
        /// <summary>A press with a target started waiting to become a tap or a hold.</summary>
        public event Action Began;
        /// <summary>A waiting press was released within the tap time.</summary>
        public event Action Tapped;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pinch.Pressed -= OnPressed;
            _pinch.Released -= OnReleased;
            EndWait();
        }

        void OnPressed()
        {
            if (!_captureTarget())
            {
                Hold();
                return;
            }
            Interrupting?.Invoke();
            _waiting = new CancellationTokenSource();
            Began?.Invoke();
            Wait(_waiting).Forget();
        }

        void OnReleased()
        {
            if (_waiting != null)
            {
                EndWait();
                Tapped?.Invoke();
                return;
            }
            if (!IsHeld)
                return;
            IsHeld = false;
            Released?.Invoke();
        }

        async UniTaskVoid Wait(CancellationTokenSource waiting)
        {
            if (await _clock.Delay(_tapMax, waiting.Token).SuppressCancellationThrow() || _waiting != waiting)
                return;
            EndWait();
            Hold();
        }

        void Hold()
        {
            IsHeld = true;
            Pressed?.Invoke();
        }

        void EndWait()
        {
            if (_waiting == null)
                return;
            _waiting.Cancel();
            _waiting.Dispose();
            _waiting = null;
        }
    }
}
