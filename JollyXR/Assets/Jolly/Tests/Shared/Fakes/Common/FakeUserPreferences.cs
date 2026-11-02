using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>
    /// IUserPreferences over one snapshot. Tests either mutate <see cref="Current"/> and call <see cref="RaiseChanged"/>,
    /// or swap it with <see cref="Set"/>.
    /// </summary>
    public sealed class FakeUserPreferences : IUserPreferences
    {
        public UserPreferencesSnapshot Current { get; private set; } = new();
        public event Action<UserPreferencesSnapshot> Changed;
        public int SubscriberCount => Changed == null ? 0 : Changed.GetInvocationList().Length;

        public void RaiseChanged() => Changed?.Invoke(Current);

        public void SetHomeLocation(string city, string country)
        {
            Current.HomeCity = city;
            Current.HomeCountry = country;
            Changed?.Invoke(Current);
        }

        public void Set(UserPreferencesSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(snapshot);
        }
    }
}
