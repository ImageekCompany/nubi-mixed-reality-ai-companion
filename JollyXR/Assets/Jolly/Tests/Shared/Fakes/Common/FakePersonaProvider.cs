using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Personas;

namespace Jolly.Testing
{
    public sealed class FakePersonaProvider : IPersonaProvider, IPersonaDirectory
    {
        readonly Dictionary<string, PersonaProfile> _profiles = new();

        public FakePersonaProvider(params PersonaProfile[] profiles)
        {
            if (profiles.Length == 0)
                profiles = new[] { Profile("companion") };
            foreach (var p in profiles)
                _profiles[p.Id] = p;
            Current = profiles[0];
        }

        public PersonaProfile Current { get; private set; }
        public IReadOnlyList<string> AvailableIds => new List<string>(_profiles.Keys);
        public string PendingSwitchId { get; private set; }
        public int ApplyCount { get; private set; }
        public Dictionary<string, string> Greetings { get; } = new();
        public event Action<PersonaProfile> Changed;
        public event Action SwitchRequested;

        public static PersonaProfile Profile(string id, bool persistMemory = true,
                                             string safetyNotice = null) => new()
        {
            Id = id, DisplayName = id, SystemPrompt = "You are " + id + ".", SafetyNotice = safetyNotice,
            Voice = new VoiceSpec { VoiceId = "voice-" + id }, PersistMemory = persistMemory,
        };

        public bool RequestSwitch(string id)
        {
            if (!_profiles.ContainsKey(id))
                return false;
            PendingSwitchId = id;
            SwitchRequested?.Invoke();
            return true;
        }

        public UniTask<bool> ApplySwitch(string id, CancellationToken ct)
        {
            if (!_profiles.TryGetValue(id, out var profile))
                return UniTask.FromResult(false);
            ApplyCount++;
            PendingSwitchId = null;
            Current = profile;
            Changed?.Invoke(profile);
            return UniTask.FromResult(true);
        }

        public string GreetingFor(string id) => Greetings.TryGetValue(id, out var greeting) ? greeting : null;
    }
}
