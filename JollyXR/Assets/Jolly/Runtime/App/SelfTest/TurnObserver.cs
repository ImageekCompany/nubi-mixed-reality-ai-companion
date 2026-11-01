using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.App.SelfTest
{
    /// <summary>Records what the conversation shows the user during one scenario: states, captions, transcripts and tools.</summary>
    public sealed class TurnObserver : IDisposable
    {
        /// <summary>Appended to a tool name in <see cref="ToolsCompleted"/> when its result was not ok.</summary>
        public const string FailedSuffix = ":failed";

        readonly IConversationEvents _events;
        readonly IClock _clock;
        readonly List<double> _toolStartedTimes = new();
        readonly List<double> _toolCompletedTimes = new();

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
        /// <summary>Parallel to <see cref="ToolsStarted"/>.</summary>
        public List<string> ToolCallIds { get; } = new();
        /// <summary>Each model round's summary (<c>evt=llm.reply</c>), from <see cref="RecordReply"/>.</summary>
        public List<string> Replies { get; } = new();
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
            ToolCallIds.Clear();
            Replies.Clear();
            ToolsCompleted.Clear();
            _toolStartedTimes.Clear();
            _toolCompletedTimes.Clear();
            UserFinal = null;
            FirstCaptionAt = double.NaN;
            FirstToolStartedAt = double.NaN;
        }

        /// <summary><see cref="IClock.Now"/> of the first start of <paramref name="name"/>; NaN when it never started.</summary>
        public double ToolStartedAt(string name)
        {
            var i = ToolsStarted.IndexOf(name);
            return i < 0 ? double.NaN : _toolStartedTimes[i];
        }

        /// <summary>
        /// The first caption after <paramref name="name"/> first succeeded, in seconds from its start: the spoken answer
        /// of a tool that hands its result back to the model (a preamble is spoken before the tool). NaN when it never
        /// succeeded or nothing was said after it.
        /// </summary>
        public double AnswerAfter(string name)
        {
            var i = ToolsCompleted.IndexOf(name);
            if (i < 0)
                return double.NaN;
            var completedAt = _toolCompletedTimes[i];
            foreach (var at in CaptionTimes)
            {
                if (at >= completedAt)
                    return at - ToolStartedAt(name);
            }
            return double.NaN;
        }

        public string StatePath() => string.Join(">", States);

        public void RecordReply(string reply) => Replies.Add(reply);

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
            ToolCallIds.Add(callId);
            _toolStartedTimes.Add(_clock.Now);
        }

        void OnToolCompleted(string callId, string name, bool ok)
        {
            ToolsCompleted.Add(ok ? name : name + FailedSuffix);
            _toolCompletedTimes.Add(_clock.Now);
        }
    }
}
