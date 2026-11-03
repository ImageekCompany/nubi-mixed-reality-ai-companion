using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Location;
using Jolly.Core.Spatial;
using Jolly.Core.Tools;

namespace Jolly.Core.Agent
{
    /// <summary>
    /// One user turn against the chat model: up to <see cref="AgentSettings.MaxRounds"/> rounds, tools executed while the
    /// model still streams, one memory commit at the end (also on cancel or failure). See contracts §3 and architecture §7.
    /// </summary>
    public sealed class AgentLoop : IAgentLoop
    {
        const int MaxHoldLinesPerRound = 2;

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
        readonly WebSearchPolicy _webSearch;
        readonly ShortRequest _shortRequest;

        /// <param name="room">The room line of the context block; null leaves it out.</param>
        /// <param name="webSearch">Null: no web search and no local time or home line in the context block.</param>
        /// <summary>For a tool a local guard runs with no model round (ADR-0051): the same words as <see cref="ToolContext.PersonaInstructions"/> here.</summary>
        public string PersonaInstructions(PersonaProfile persona) => _prompt.PersonaInstructions(persona);

        public AgentLoop(IChatModel model, IToolRegistry registry, IConversationMemory memory, ISceneMemory scene,
                         RoomContext room, IPersonaProvider personas, ILocalizer localizer, IUserPreferences preferences, ILog log,
                         IClock clock, ILatencyTracker latency, ConversationEvents events, AgentSettings settings,
                         WebSearchPolicy webSearch = null)
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
            _webSearch = webSearch;
            _shortRequest = new ShortRequest(localizer);
            _prompt = new PromptBuilder(localizer, preferences, memory, scene, room, settings, webSearch?.Location);
        }

        public IUniTaskAsyncEnumerable<AgentEvent> RunTurn(UserTurn turn, Func<string> spokenSoFar, CancellationToken ct) =>
            UniTaskAsyncEnumerable.Create<AgentEvent>(async (writer, enumeratorCt) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, enumeratorCt);
                var events = new TurnEvents();
                var drive = Drive(turn, spokenSoFar, events, linked.Token);
                // Not cancellable: the reader ends only once Drive has committed memory and completed the channel. Drive
                // rethrows a cancel or an unexpected error here rather than through the channel, whose faulted completion
                // nobody awaits.
                await foreach (var e in events.Reader.ReadAllAsync())
                    await writer.YieldAsync(e);
                await drive;
            });

        /// <summary>
        /// A live turn's first request without its input, and the context block that goes before the history: the tools
        /// and namespaces the persona and seated mode allow, web search when <paramref name="webSearch"/> offers it, and
        /// the instructions to match. ToolSelectionEval sends exactly this.
        /// </summary>
        public static ModelRequest LiveRequest(PromptBuilder prompt, IToolRegistry registry, PersonaProfile persona,
                                               IUserPreferences preferences, WebSearchPolicy webSearch, AgentSettings settings,
                                               out ConversationItem context)
        {
            var tools = registry.DefinitionsFor(persona, preferences);
            var namespaces = registry.NamespacesOf(tools);
            var search = webSearch?.For(persona);
            context = prompt.ContextItem(webSearch?.Allows(persona) ?? false);
            return new ModelRequest
            {
                Route = ModelRoute.LiveUser,
                Instructions = prompt.Instructions(persona, search != null, Offers(namespaces, ToolNames.Actions.Namespace)),
                Tools = tools,
                Namespaces = namespaces,
                WebSearch = search,
                Effort = ReasoningEffort.Minimal,
                SafetyIdentifier = settings.SafetyIdentifier,
            };
        }

        async UniTask Drive(UserTurn turn, Func<string> spokenSoFar, TurnEvents events, CancellationToken ct)
        {
            var transcript = new TurnTranscript(turn.Text);
            ToolRoundExecutor open = null;
            // A barge-in clears the speech queue right after cancelling, while the cancel reaches this method only once the
            // model request aborts (a later frame), so what was spoken is read when the token fires.
            string spokenAtCancel = null;
            using var onCancel = ct.Register(() => spokenAtCancel = spokenSoFar());
            try
            {
                var persona = _personas.Current;
                var context = new ToolContext
                {
                    Turn = turn, Persona = persona, PersonaInstructions = _prompt.PersonaInstructions(persona),
                    Speech = events, Preferences = _preferences,
                };
                var request = LiveRequest(_prompt, _registry, persona, _preferences, _webSearch, _settings, out var contextItem);
                // What the model could choose from: a refusal like "I can't go behind you" is otherwise unexplainable on a device.
                if (_log.IsEnabled(LogLevel.Info))
                    _log.Write(LogLevel.Info, "agent",
                               $"evt=llm.offer turn={turn.TurnId} persona={persona.Id} tools={request.Tools.Count} " +
                               $"seated={(_preferences.Current?.Seated == true ? 1 : 0)} search={(request.WebSearch != null ? 1 : 0)}");

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
                    // set_home_location may have run in the previous round.
                    if (request.WebSearch != null)
                        request.WebSearch = _webSearch.Location.SearchOptions();
                    open = new ToolRoundExecutor(_registry, request.Tools, _clock, context, events, ct, _log);
                    var result = await StreamRound(turn.TurnId, round, request, open, events, ct);
                    await open.WhenAll();
                    transcript.AddRound(result.OutputItems, open);
                    // PromptBuilder has the model say only a neutral phrase before a call, so words before a move do not
                    // mean it answered "Dance and then tell me a joke": only a turn that asked for one thing ends with the move.
                    var finished = open.Count == 0 || open.EndsTurn
                                   || (open.EndsSpokenTurn && Spoke(result) && _shortRequest.Matches(turn.Text));
                    open = null;
                    if (finished)
                        break;
                }
                _memory.CommitTurn(transcript.ForCommit());
                events.Write(new AgentEvent(AgentEventKind.Completed));
            }
            catch (OperationCanceledException)
            {
                CommitInterrupted(transcript, spokenAtCancel, spokenSoFar, open);
                throw;
            }
            catch (ProviderException e)
            {
                _log.Write(LogLevel.Warning, "agent", $"evt=agent.failed kind={e.Kind} status={e.HttpStatus} req={e.RequestId}");
                CommitInterrupted(transcript, null, spokenSoFar, open);
                events.Write(new AgentEvent(AgentEventKind.Failed, error: e));
            }
            catch (Exception)
            {
                CommitInterrupted(transcript, null, spokenSoFar, open);
                throw;
            }
            finally
            {
                events.Complete();
            }
        }

        void CommitInterrupted(TurnTranscript transcript, string spokenAtCancel, Func<string> spokenSoFar,
                               ToolRoundExecutor open)
        {
            try
            {
                _memory.CommitTurn(transcript.ForInterruptedCommit(spokenAtCancel ?? spokenSoFar(), open));
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
            using var firstEvent = CancellationTokenSource.CreateLinkedTokenSource(ct);
            HashSet<string> searches = null;
            if (_settings.HoldLineAfter > TimeSpan.Zero)
                SignalWhileSilent(turnId, round, events, firstEvent.Token).Forget();
            try
            {
                await foreach (var e in _model.Stream(request, ct).WithCancellation(ct))
                {
                    switch (e.Kind)
                    {
                        case ModelEventKind.Started:
                            firstEvent.Cancel();
                            _latency.Mark(turnId, LatencyMark.LlmFirstEvent);
                            break;
                        case ModelEventKind.TextDelta:
                            _latency.Mark(turnId, LatencyMark.LlmFirstText);
                            events.Append(e.Text);
                            break;
                        case ModelEventKind.TextDone:
                            events.Write(new AgentEvent(AgentEventKind.SpeechBreak));
                            break;
                        case ModelEventKind.ToolCall:
                            tools.Start(e.ToolCall);
                            break;
                        // The server runs the search: no ToolRoundExecutor call, but the turn shows it like a tool (the
                        // state goes to Acting, the badge and the Think gesture run) until it ends. The pipeline counts
                        // running tools, so every search is started once and completed once, even when the stream
                        // repeats an in_progress event or skips it.
                        case ModelEventKind.WebSearchStarted:
                            if ((searches ??= new HashSet<string>()).Add(e.Text ?? ""))
                                events.Write(new AgentEvent(AgentEventKind.ToolStarted, toolName: ToolNames.WebSearch, callId: e.Text));
                            break;
                        case ModelEventKind.WebSearchCompleted:
                            if (!(searches ??= new HashSet<string>()).Remove(e.Text ?? ""))
                                events.Write(new AgentEvent(AgentEventKind.ToolStarted, toolName: ToolNames.WebSearch, callId: e.Text));
                            events.Write(new AgentEvent(AgentEventKind.ToolCompleted, toolName: ToolNames.WebSearch, callId: e.Text,
                                toolResult: new ToolResult(e.Ok, null)));
                            break;
                        case ModelEventKind.Completed:
                            result = e.Result;
                            break;
                    }
                }
            }
            finally
            {
                firstEvent.Cancel();
            }
            if (result == null)
                throw new ProviderException("agent", ProviderErrorKind.Protocol, "model stream ended without Completed");
            _events.RaiseModelCallCompleted(result.Usage.Model, (_clock.Now - sentAt) * 1000.0);
            if (_settings.ReplyObserved != null)
            {
                var reply = ReplySummary.Format(round, result);
                _log.Write(LogLevel.Info, "agent", $"evt=llm.reply turn={turnId} {reply}");
                _settings.ReplyObserved(reply);
            }
            return result;
        }

        static bool Spoke(ModelTurnResult result)
        {
            foreach (var item in result.OutputItems)
            {
                if (item.Kind == ItemKind.AssistantText && !string.IsNullOrWhiteSpace(item.Text))
                    return true;
            }
            return false;
        }

        static bool Offers(IReadOnlyList<ToolNamespace> namespaces, string name)
        {
            foreach (var ns in namespaces)
            {
                if (ns.Name == name)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Device runs saw the Responses API send nothing for the whole 6 s first-event window, twice in a row: without
        /// this the user heard the release acknowledgement and then 12 s of silence. Ends at the round's first event.
        /// </summary>
        async UniTaskVoid SignalWhileSilent(int turnId, int round, ITurnEventSink events, CancellationToken firstEvent)
        {
            var wait = _settings.HoldLineAfter;
            for (var line = 0; line < MaxHoldLinesPerRound; line++)
            {
                if (await _clock.Delay(wait, firstEvent).SuppressCancellationThrow())
                    return;
                if (_log.IsEnabled(LogLevel.Info))
                    _log.Write(LogLevel.Info, "agent", $"evt=agent.hold_line turn={turnId} round={round} line={line + 1}");
                events.Write(new AgentEvent(AgentEventKind.Delayed));
                wait = _settings.HoldLineRepeat;
                if (wait <= TimeSpan.Zero)
                    return;
            }
        }
    }
}
