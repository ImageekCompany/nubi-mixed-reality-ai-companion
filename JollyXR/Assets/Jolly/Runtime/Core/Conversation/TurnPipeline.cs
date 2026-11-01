using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// One turn, start to finish: listen → guards → agent → speech (online), a canned line (offline), or an unprompted
    /// announcement. It drives the state machine; the controller owns tokens and watchdogs.
    /// </summary>
    public sealed class TurnPipeline
    {
        readonly TurnListener _listener;
        readonly TurnGuards _guards;
        readonly TurnSpeech _speech;
        readonly IAgentLoop _agent;
        readonly IOfflineResponder _offline;
        readonly IPersonaProvider _personas;
        readonly ConversationStateMachine _machine;
        readonly ConversationEvents _events;
        readonly ILatencyTracker _latency;
        readonly ILocalizer _localizer;
        readonly ILog _log;
        readonly ConversationTimings _timings;
        readonly Func<string> _spokenThisTurn;

        public TurnPipeline(TurnListener listener, TurnGuards guards, TurnSpeech speech, IAgentLoop agent,
                            IOfflineResponder offline, IPersonaProvider personas, ConversationStateMachine machine,
                            ConversationEvents events, ILatencyTracker latency, ILocalizer localizer, ILog log,
                            ConversationTimings timings)
        {
            _listener = listener;
            _guards = guards;
            _speech = speech;
            _agent = agent;
            _offline = offline;
            _personas = personas;
            _machine = machine;
            _events = events;
            _latency = latency;
            _localizer = localizer;
            _log = log;
            _timings = timings;
            _spokenThisTurn = () => _speech.SpokenThisTurn;
        }

        public bool CanListen => _listener.CanListen;

        public async UniTask RunOnline(int turnId, UniTask released, CancellationToken turnCt)
        {
            _latency.BeginTurn(turnId);
            _latency.Mark(turnId, LatencyMark.PttPressed);
            if (_timings.AcknowledgeOnRelease)
                Acknowledge(released, turnCt).Forget();

            HeardTurn heard;
            try
            {
                heard = await _listener.Listen(turnId, released, turnCt);
            }
            catch (ProviderException e)
            {
                _log.Write(LogLevel.Warning, "conversation", $"evt=stt.failed kind={e.Kind} status={e.HttpStatus}");
                _machine.Fire(ConversationTrigger.SttFailed);
                return;
            }
            if (string.IsNullOrWhiteSpace(heard.Text))
            {
                _machine.Fire(ConversationTrigger.FinalEmpty);
                return;
            }

            _latency.Mark(turnId, LatencyMark.AsrFinal);
            _events.RaiseUserFinal(heard.Text);
            _machine.Fire(ConversationTrigger.FinalText);
            var text = await ApplyGuards(heard.Text, turnCt);
            await RunAgent(new UserTurn
            {
                TurnId = turnId, Text = text, LanguageTag = _localizer.LanguageTag, PreCaptured = heard.PreCaptured,
            }, turnCt);
        }

        /// <summary>The OfflinePttPressed path: one canned line, then Idle.</summary>
        public async UniTask RunOffline(int turnId, LineCategory category, CancellationToken turnCt)
        {
            _latency.BeginTurn(turnId);
            _latency.Mark(turnId, LatencyMark.PttPressed);
            await _offline.Respond(category, _speech.NextUtteranceId(), turnCt);
            EndSpeakingTurn(turnId);
        }

        /// <summary>
        /// Unprompted speech from Idle in the current persona's voice: a greeting, then the safety notice, which is captioned
        /// even if TTS fails (either may be null). Without the network for TTS the notice is the canned SafetyNotice line
        /// instead and the greeting is dropped.
        /// </summary>
        public async UniTask Announce(int turnId, string greeting, string notice, bool online, CancellationToken turnCt)
        {
            _latency.BeginTurn(turnId);
            _machine.Fire(ConversationTrigger.Announce);
            if (!online)
            {
                if (!string.IsNullOrEmpty(notice))
                    await _offline.Respond(LineCategory.SafetyNotice, _speech.NextUtteranceId(), turnCt);
                EndSpeakingTurn(turnId);
                return;
            }
            BeginSpeech(turnId, turnCt);
            if (!string.IsNullOrEmpty(greeting))
                _speech.Append(greeting);
            if (!string.IsNullOrEmpty(notice))
                _speech.SpeakEnsuringCaption(notice);
            await FinishSpeaking(turnId, turnCt);
        }

        async UniTask<string> ApplyGuards(string text, CancellationToken ct)
        {
            _guards.ApplyStop(text);
            if (await _guards.ApplyComeHere(text, ct))
                text += TurnGuards.ComeHereNote;
            if (!_guards.NeedsCrisisHandoff(text))
                return text;
            await _offline.Respond(LineCategory.CrisisHandoff, _speech.NextUtteranceId(), ct);
            return text + TurnGuards.CrisisNote;
        }

        async UniTask RunAgent(UserTurn turn, CancellationToken ct)
        {
            BeginSpeech(turn.TurnId, ct);
            var toolsRunning = 0;
            ProviderException failure = null;
            await foreach (var e in _agent.RunTurn(turn, _spokenThisTurn, ct).WithCancellation(ct))
            {
                switch (e.Kind)
                {
                    case AgentEventKind.SpeechDelta:
                        _speech.Append(e.Text);
                        break;
                    case AgentEventKind.SpeechBreak:
                        _speech.Finish();
                        break;
                    case AgentEventKind.ToolStarted:
                        // The preamble often ends without trailing whitespace, so the splitter would hold it and glue the
                        // next round's first sentence onto it ("On my way!Here I am."). Speak it now (architecture §5.1).
                        _speech.Finish();
                        toolsRunning++;
                        _machine.Fire(ConversationTrigger.ToolStarted);
                        _events.RaiseToolStarted(e.CallId, e.ToolName);
                        break;
                    case AgentEventKind.ToolCompleted:
                        _events.RaiseToolCompleted(e.CallId, e.ToolName, e.ToolResult.Ok);
                        if (--toolsRunning == 0)
                            _machine.Fire(ConversationTrigger.ToolsDone);
                        break;
                    case AgentEventKind.Failed:
                        failure = e.Error;
                        break;
                    case AgentEventKind.Delayed:
                        HoldLine(ct).Forget();
                        break;
                }
            }
            if (toolsRunning > 0)
                _machine.Fire(ConversationTrigger.ToolsDone);
            if (failure == null)
            {
                await FinishSpeaking(turn.TurnId, ct);
                return;
            }

            // The error line follows whatever was already being said.
            _speech.Finish();
            await _speech.WhenIdle();
            ct.ThrowIfCancellationRequested();
            _machine.Fire(ConversationTrigger.TurnFailed);
            await _offline.Respond(LineCategory.Error, _speech.NextUtteranceId(), ct);
            EndSpeakingTurn(turn.TurnId);
        }

        void BeginSpeech(int turnId, CancellationToken ct) =>
            _speech.Begin(turnId, _personas.Current?.Voice, _localizer.Voice?.TtsAvailable ?? true, ct);

        /// <summary>TurnEnded is latched before the queue drains, so the drain goes straight to Idle.</summary>
        async UniTask FinishSpeaking(int turnId, CancellationToken ct)
        {
            _speech.Finish();
            _machine.Fire(ConversationTrigger.TurnCompleted);
            await _speech.WhenIdle();
            ct.ThrowIfCancellationRequested();
            EndSpeakingTurn(turnId);
        }

        /// <summary>Latches TurnEnded and reports the speech as drained, which returns the machine to Idle.</summary>
        void EndSpeakingTurn(int turnId)
        {
            _machine.Fire(ConversationTrigger.TurnCompleted);
            _machine.Fire(ConversationTrigger.SpeechDrained);
            _latency.Mark(turnId, LatencyMark.TurnCompleted);
        }

        async UniTaskVoid Acknowledge(UniTask released, CancellationToken ct)
        {
            if (await released.AttachExternalCancellation(ct).SuppressCancellationThrow())
                return;
            await PlayFiller("evt=ack.failed", ct);
        }

        /// <summary>The model is late (AgentEventKind.Delayed): a short canned "One moment." instead of silence. The
        /// Acknowledge lines are reused (no new clips to bake per character); ICannedLines avoids repeating the release one.</summary>
        async UniTaskVoid HoldLine(CancellationToken ct) => await PlayFiller("evt=hold_line.failed", ct);

        async UniTask PlayFiller(string failedEvent, CancellationToken ct)
        {
            try
            {
                await _offline.Respond(LineCategory.Acknowledge, _speech.NextUtteranceId(), ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Warning, "conversation", failedEvent, e);
            }
        }
    }
}
