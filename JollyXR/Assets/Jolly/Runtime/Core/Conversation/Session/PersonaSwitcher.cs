using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Personas;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Applies a requested persona between turns (architecture §3): resolve the new profile, load its memory (carrying the
    /// summary only between personas that both persist), and set its mood. The caller saves the old memory first.
    /// </summary>
    public sealed class PersonaSwitcher
    {
        readonly IPersonaProvider _personas;
        readonly IPersonaDirectory _directory;
        readonly IConversationMemory _memory;
        readonly ICharacterController _character;
        readonly ILog _log;

        public PersonaSwitcher(IPersonaProvider personas, IPersonaDirectory directory, IConversationMemory memory,
                               ICharacterController character, ILog log)
        {
            _personas = personas;
            _directory = directory;
            _memory = memory;
            _character = character;
            _log = log;
        }

        public string PendingId => _personas.PendingSwitchId;

        public event Action Requested
        {
            add => _directory.SwitchRequested += value;
            remove => _directory.SwitchRequested -= value;
        }

        /// <summary>The current persona's greeting (null when it has none).</summary>
        public string Greeting => _directory.GreetingFor(_personas.Current.Id);

        /// <summary>The current persona's safety notice (null when it has none).</summary>
        public string SafetyNotice => _personas.Current.SafetyNotice;

        /// <summary>True when the pending persona is now current.</summary>
        public async UniTask<bool> ApplyPending(CancellationToken ct)
        {
            var id = PendingId;
            if (id == null)
                return false;
            var previous = _personas.Current;
            if (!await _personas.ApplySwitch(id, ct))
            {
                _log.Write(LogLevel.Warning, "persona", $"evt=persona.switch_failed id={id}");
                return false;
            }

            var next = _personas.Current;
            var carry = previous.PersistMemory && next.PersistMemory ? _memory.Summary : null;
            await _memory.Load(next.Id, carry, ct);
            _character.SetMood(next.DefaultMood);
            _log.Write(LogLevel.Info, "persona", $"evt=persona.switched from={previous.Id} to={next.Id}");
            return true;
        }
    }
}
