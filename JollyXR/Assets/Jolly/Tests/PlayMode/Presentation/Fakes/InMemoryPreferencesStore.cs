using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;

namespace Jolly.Tests.PlayMode.Presentation
{
    sealed class FakeUserPreferences : IUserPreferences
    {
        public UserPreferencesSnapshot Current { get; private set; } = new();
        public event Action<UserPreferencesSnapshot> Changed;
        public int SubscriberCount => Changed == null ? 0 : Changed.GetInvocationList().Length;

        public void Set(UserPreferencesSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(snapshot);
        }
    }

    sealed class FakeLatencyTracker : ILatencyTracker
    {
        readonly Dictionary<(LatencyMark, LatencyMark), double> _between = new();

        public void Set(LatencyMark from, LatencyMark to, double ms) => _between[(from, to)] = ms;
        public void BeginTurn(int turnId) { }
        public void Mark(int turnId, LatencyMark mark) { }
        public double Between(LatencyMark from, LatencyMark to) => _between.TryGetValue((from, to), out var ms) ? ms : double.NaN;
    }

    sealed class FakePersonaProvider : IPersonaProvider
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

    sealed class FakeUsageMeter : IApiUsageMeter
    {
        public decimal SpentUsd { get; private set; } = 0.53m;
        public decimal HardCapUsd => 5m;
        public bool IsCapReached => false;
        public event Action<decimal> SpentChanged;

        public void Spend(decimal usd)
        {
            SpentUsd += usd;
            SpentChanged?.Invoke(SpentUsd);
        }

        public bool TryReserve(decimal estimatedUsd, out long reservationId) { reservationId = 0; return true; }
        public decimal Record(long reservationId, in UsageRecord usage) => 0m;
        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens) => 0m;
        public void Flush() { }
        public void Dispose() { }
    }

    sealed class InMemoryPreferencesStore : IPreferencesStore
    {
        public UserPreferencesSnapshot Stored = new();
        public int Saves;

        public UserPreferencesSnapshot Load() => SettingsModel.Copy(Stored);

        public void Save(UserPreferencesSnapshot preferences)
        {
            Stored = SettingsModel.Copy(preferences);
            Saves++;
        }
    }
}
