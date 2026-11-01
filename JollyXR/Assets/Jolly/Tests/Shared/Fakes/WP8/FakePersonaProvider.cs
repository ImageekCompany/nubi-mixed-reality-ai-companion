using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing
{
    public sealed class FakePersonaProvider : IPersonaProvider
    {
        public PersonaProfile Current { get; private set; } = new() { Id = "companion" };
        public IReadOnlyList<string> AvailableIds { get; } = new[] { "companion", "mentor", "guru", "psychologist" };
        public string PendingSwitchId => null;
        public event Action<PersonaProfile> Changed;

        public bool RequestSwitch(string id) => false;

        public UniTask<bool> ApplySwitch(string id, CancellationToken ct)
        {
            Current = new PersonaProfile { Id = id };
            Changed?.Invoke(Current);
            return UniTask.FromResult(true);
        }
    }
}
