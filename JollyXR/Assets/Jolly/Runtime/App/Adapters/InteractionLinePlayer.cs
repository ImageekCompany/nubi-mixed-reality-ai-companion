using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Offline;

namespace Jolly.App.Adapters
{
    /// <summary>
    /// Speaks the canned line an interaction asks for (gaze, open palm, poke: architecture §5.4) when the conversation is
    /// Idle and nothing is playing, and the room-scan offer (ADR-0025) through <see cref="Request"/>. These lines bypass
    /// the state machine, so the talking animation is driven here and handed back to the conversation state afterwards.
    /// Ids are negative so they never collide with turn utterances.
    /// </summary>
    public sealed class InteractionLinePlayer : IDisposable
    {
        readonly InteractionTriggers _triggers;
        readonly IOfflineResponder _responder;
        readonly ConversationStateMachine _machine;
        readonly ICharacterController _character;
        readonly ILog _log;
        readonly CancellationToken _appCt;
        int _nextId;

        public InteractionLinePlayer(InteractionTriggers triggers, IOfflineResponder responder, ConversationStateMachine machine,
                                     ICharacterController character, ILog log, CancellationToken appCt)
        {
            _triggers = triggers;
            _responder = responder;
            _machine = machine;
            _character = character;
            _log = log;
            _appCt = appCt;
            _triggers.LineRequested += OnLineRequested;
        }

        /// <summary>InteractionTriggers' canSpeak: Idle and silent.</summary>
        public static Func<bool> CanSpeak(ConversationStateMachine machine, ISpeechOutput output) =>
            () => machine.State == ConversationState.Idle && !output.IsPlaying;

        public void Dispose() => _triggers.LineRequested -= OnLineRequested;

        /// <summary>Plays a line now; the caller has checked <see cref="CanSpeak"/>.</summary>
        public void Request(LineCategory category) => Play(category).Forget();

        void OnLineRequested(LineCategory category) => Play(category).Forget();

        async UniTaskVoid Play(LineCategory category)
        {
            _character.SetConversationState(ConversationState.Speaking);
            try
            {
                await _responder.Respond(category, --_nextId, _appCt);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Warning, "offline", "evt=interaction_line.failed category=" + category, e);
            }
            finally
            {
                // On teardown the character may already be destroyed, and nothing needs the hand-back.
                if (!_appCt.IsCancellationRequested && _machine.State == ConversationState.Idle)
                    _character.SetConversationState(ConversationState.Idle);
            }
        }
    }
}
