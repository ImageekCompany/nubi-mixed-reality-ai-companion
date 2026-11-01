using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;

namespace Jolly.Core.Agent
{
    /// <summary>
    /// One user turn against the chat model: up to <see cref="AgentSettings.MaxRounds"/> rounds, tools executed while the
    /// model still streams, one memory commit at the end (also on cancel or failure). See contracts §3 and architecture §7.
    /// </summary>
    public sealed class AgentLoop : IAgentLoop
    {
        readonly IChatModel _model;
        readonly IToolRegistry _registry;
        readonly IConversationMemory _memory;
        readonly IPersonaProvider _personas;
        readonly ILocalizer _localizer;
        readonly IUserPreferences _preferences;
        readonly ILog _log;
        readonly IClock _clock;
        readonly ILatencyTracker _latency;
        readonly ConversationEvents _events;
        readonly AgentSettings _settings;
        readonly PromptBuilder _prompt;

        public AgentLoop(IChatModel model, IToolRegistry registry, IConversationMemory memory, ISceneMemory scene,
                         IPersonaProvider personas, ILocalizer localizer, IUserPreferences preferences, ILog log,
                         IClock clock, ILatencyTracker latency, ConversationEvents events, AgentSettings settings)
        {
            _model = model;
            _registry = registry;
            _memory = memory;
            _personas = personas;
            _localizer = localizer;
            _preferences = preferences;
            _log = log;
            _clock = clock;
            _latency = latency;
            _events = events;
            _settings = settings;
            _prompt = new PromptBuilder(localizer, preferences, memory, scene, settings);
        }

        public IUniTaskAsyncEnumerable<AgentEvent> RunTurn(UserTurn turn, Func<string> spokenSoFar, CancellationToken ct) =>
            UniTaskAsyncEnumerable.Create<AgentEvent>(async (writer, enumeratorCt) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, enumeratorCt);
                var events = new TurnEvents();
                var drive = Drive(turn, spokenSoFar, events, linked.Token);
                // Not cancellable: the reader ends only once Drive has committed memory and completed the channel.
                await foreach (var e in events.Reader.ReadAllAsync())
                    await writer.YieldAsync(e);
                await drive;
            });

        async UniTask Drive(UserTurn turn, Func<string> spokenSoFar, TurnEvents events, CancellationToken ct)
        {
            var transcript = new TurnTranscript(turn.Text);
            ToolRoundExecutor open = null;
            Exception error = null;
            try
            {
                var persona = _personas.Current;
                var context = new ToolContext
                {
                    Turn = turn, Persona = persona, PersonaInstructions = _prompt.PersonaInstructions(persona),
                    Speech = events, Preferences = _preferences,
                };
                var request = new ModelRequest
                {
                    Route = ModelRoute.LiveUser,
                    Instructions = _prompt.Instructions(persona),
                    Tools = _registry.DefinitionsFor(persona, _preferences),
                    Effort = ReasoningEffort.Minimal,
                    SafetyIdentifier = _settings.SafetyIdentifier,
                };
                var contextItem = _prompt.ContextItem();

                for (var round = 0; ; round++)
                {
                    if (round == _settings.MaxRounds)
                    {
                        var line = CoreLines.Get(_localizer, CoreLines.Tangled);
                        events.Append(line);
                        transcript.AddSpoken(line);
                        break;
                    }
                    request.Input = transcript.Input(contextItem, _memory.Recent);
                    open = new ToolRoundExecutor(_registry, _clock, context, events, ct, _log);
                    var result = await StreamRound(turn.TurnId, round, request, open, events, ct);
                    await open.WhenAll();
                    transcript.AddRound(result.OutputItems, open);
                    var finished = open.Count == 0 || open.EndsTurn;
                    open = null;
                    if (finished)
                        break;
                }
                _memory.CommitTurn(transcript.ForCommit());
                events.Write(new AgentEvent(AgentEventKind.Completed));
            }
            catch (OperationCanceledException e)
            {
                error = e;
                CommitInterrupted(transcript, spokenSoFar, open);
            }
            catch (ProviderException e)
            {
                _log.Write(LogLevel.Warning, "agent", $"evt=agent.failed kind={e.Kind} status={e.HttpStatus} req={e.RequestId}");
                CommitInterrupted(transcript, spokenSoFar, open);
                events.Write(new AgentEvent(AgentEventKind.Failed, error: e));
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                events.Complete(error);
            }
        }

        void CommitInterrupted(TurnTranscript transcript, Func<string> spokenSoFar, ToolRoundExecutor open)
        {
            try
            {
                _memory.CommitTurn(transcript.ForInterruptedCommit(spokenSoFar(), open));
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Error, "agent", "evt=agent.commit_failed", e);
            }
        }

        async UniTask<ModelTurnResult> StreamRound(int turnId, int round, ModelRequest request, ToolRoundExecutor tools,
                                                   TurnEvents events, CancellationToken ct)
        {
            if (round == 0)
                _latency.Mark(turnId, LatencyMark.LlmRequestSent);
            var sentAt = _clock.Now;
            ModelTurnResult result = null;
            await foreach (var e in _model.Stream(request, ct).WithCancellation(ct))
            {
                switch (e.Kind)
                {
                    case ModelEventKind.Started:
                        _latency.Mark(turnId, LatencyMark.LlmFirstEvent);
                        break;
                    case ModelEventKind.TextDelta:
                        _latency.Mark(turnId, LatencyMark.LlmFirstText);
                        events.Append(e.Text);
                        break;
                    case ModelEventKind.ToolCall:
                        tools.Start(e.ToolCall);
                        break;
                    case ModelEventKind.Completed:
                        result = e.Result;
                        break;
                }
            }
            if (result == null)
                throw new ProviderException("agent", ProviderErrorKind.Protocol, "model stream ended without Completed");
            _events.RaiseModelCallCompleted(result.Usage.Model, (_clock.Now - sentAt) * 1000.0);
            return result;
        }
    }
}
