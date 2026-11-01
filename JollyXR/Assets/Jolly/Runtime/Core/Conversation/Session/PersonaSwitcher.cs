using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Personas;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Applies a requested persona between turns (architecture §3): save the old memory, resolve the new profile, load
    /// its memory (carrying the summary only between personas that both persist), and set its mood. Returns what the
    /// new persona says at once: its greeting, then its safety notice.
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

        /// <summary>Null when there was nothing to apply; otherwise the announcement (may be empty).</summary>
        public async UniTask<string> ApplyPending(CancellationToken ct)
        {
            var id = PendingId;
            if (id == null)
                return null;
            var previous = _personas.Current;
            await _memory.Save(ct);
            if (!await _personas.ApplySwitch(id, ct))
            {
                _log.Write(LogLevel.Warning, "persona", $"evt=persona.switch_failed id={id}");
                return null;
            }

            var next = _personas.Current;
            var carry = previous.PersistMemory && next.PersistMemory ? _memory.Summary : null;
            await _memory.Load(next.Id, carry, ct);
            _character.SetMood(next.DefaultMood);
            _log.Write(LogLevel.Info, "persona", $"evt=persona.switched from={previous.Id} to={next.Id}");
            return Join(_directory.GreetingFor(next.Id), next.SafetyNotice);
        }

        static string Join(string greeting, string notice)
        {
            if (string.IsNullOrEmpty(notice))
                return greeting ?? string.Empty;
            return string.IsNullOrEmpty(greeting) ? notice : greeting + " " + notice;
        }
    }
}
