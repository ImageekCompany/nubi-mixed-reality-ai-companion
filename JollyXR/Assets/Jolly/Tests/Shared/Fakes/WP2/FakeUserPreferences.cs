using System;
using Jolly.Contracts;

namespace Jolly.Testing.Conversation
{
    public sealed class FakeUserPreferences : IUserPreferences
    {
        public UserPreferencesSnapshot Current { get; } = new();
        public event Action<UserPreferencesSnapshot> Changed;

        public void RaiseChanged() => Changed?.Invoke(Current);
    }
}
