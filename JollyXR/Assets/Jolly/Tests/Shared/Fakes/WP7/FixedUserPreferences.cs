using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>IUserPreferences holding one mutable snapshot; <see cref="Raise"/> publishes a change.</summary>
    public sealed class FixedUserPreferences : IUserPreferences
    {
        public UserPreferencesSnapshot Current { get; } = new();

        public event Action<UserPreferencesSnapshot> Changed;

        public void Raise() => Changed?.Invoke(Current);
    }
}
