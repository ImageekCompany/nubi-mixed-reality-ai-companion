using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.App.SelfTest
{
    /// <summary>Records what the conversation shows the user during one scenario: states, captions, transcripts and tools.</summary>
    public sealed class TurnObserver : IDisposable
    {
        readonly IConversationEvents _events;
        readonly IClock _clock;

        public TurnObserver(IConversationEvents events, IClock clock)
        {
            _events = events;
            _clock = clock;
            _events.StateChanged += OnState;
            _events.UserFinal += OnUserFinal;
            _events.CaptionStarted += OnCaption;
            _events.ToolStarted += OnToolStarted;
            _events.ToolCompleted += OnToolCompleted;
        }

        public List<ConversationState> States { get; } = new();
        public List<string> Captions { get; } = new();
        /// <summary><see cref="IClock.Now"/> at each caption's first played sample, parallel to <see cref="Captions"/>.</summary>
        public List<double> CaptionTimes { get; } = new();
        public List<string> ToolsStarted { get; } = new();
        public List<string> ToolsCompleted { get; } = new();
        public string UserFinal { get; private set; }
        public double FirstCaptionAt { get; private set; } = double.NaN;
        public double FirstToolStartedAt { get; private set; } = double.NaN;
        public ConversationState State { get; private set; }

        public void Reset()
        {
            States.Clear();
            Captions.Clear();
            CaptionTimes.Clear();
            ToolsStarted.Clear();
            ToolsCompleted.Clear();
            UserFinal = null;
            FirstCaptionAt = double.NaN;
            FirstToolStartedAt = double.NaN;
        }

        public string StatePath() => string.Join(">", States);

        public void Dispose()
        {
            _events.StateChanged -= OnState;
            _events.UserFinal -= OnUserFinal;
            _events.CaptionStarted -= OnCaption;
            _events.ToolStarted -= OnToolStarted;
            _events.ToolCompleted -= OnToolCompleted;
        }

        void OnState(ConversationState from, ConversationState to)
        {
            State = to;
            States.Add(to);
        }

        void OnUserFinal(string text) => UserFinal = text;

        void OnCaption(int utteranceId, string text)
        {
            if (Captions.Count == 0)
                FirstCaptionAt = _clock.Now;
            Captions.Add(text);
            CaptionTimes.Add(_clock.Now);
        }

        void OnToolStarted(string callId, string name)
        {
            if (ToolsStarted.Count == 0)
                FirstToolStartedAt = _clock.Now;
            ToolsStarted.Add(name);
        }

        void OnToolCompleted(string callId, string name, bool ok) => ToolsCompleted.Add(ok ? name : name + ":failed");
    }
}
