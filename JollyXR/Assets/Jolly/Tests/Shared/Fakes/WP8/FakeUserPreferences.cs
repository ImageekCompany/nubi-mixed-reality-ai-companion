using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    public sealed class FakeUserPreferences : IUserPreferences
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
}
