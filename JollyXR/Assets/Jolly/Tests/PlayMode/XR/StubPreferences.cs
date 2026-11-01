using System;
using Jolly.Contracts;

namespace Jolly.Tests.PlayMode.XR
{
    sealed class StubPreferences : IUserPreferences
    {
        public UserPreferencesSnapshot Current { get; } = new();
#pragma warning disable CS0067 // never raised: these tests set Current directly
        public event Action<UserPreferencesSnapshot> Changed;
#pragma warning restore CS0067
    }
}
