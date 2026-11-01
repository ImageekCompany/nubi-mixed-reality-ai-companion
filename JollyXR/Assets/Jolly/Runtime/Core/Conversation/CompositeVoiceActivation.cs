using System;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// One push-to-talk over several sources (pinch, controller button): held while any source is held, so pressing a
    /// second source while the first is held neither presses again (no barge-in) nor releases. Owns only its
    /// subscriptions; the sources belong to whoever created them.
    /// </summary>
    public sealed class CompositeVoiceActivation : IVoiceActivation, IDisposable
    {
        readonly IVoiceActivation[] _sources;
        readonly Action _refresh;

        public CompositeVoiceActivation(params IVoiceActivation[] sources)
        {
            _sources = (IVoiceActivation[])sources.Clone();
            _refresh = Refresh;
            foreach (var source in _sources)
            {
                source.Pressed += _refresh;
                source.Released += _refresh;
            }
            IsHeld = AnyHeld();
        }

        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;

        public void Dispose()
        {
            foreach (var source in _sources)
            {
                source.Pressed -= _refresh;
                source.Released -= _refresh;
            }
        }

        void Refresh()
        {
            var held = AnyHeld();
            if (held == IsHeld)
                return;
            IsHeld = held;
            if (held)
                Pressed?.Invoke();
            else
                Released?.Invoke();
        }

        bool AnyHeld()
        {
            foreach (var source in _sources)
                if (source.IsHeld)
                    return true;
            return false;
        }
    }
}
