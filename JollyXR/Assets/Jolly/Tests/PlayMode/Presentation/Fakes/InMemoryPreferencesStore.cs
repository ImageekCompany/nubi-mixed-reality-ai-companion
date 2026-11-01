using Jolly.Contracts;
using Jolly.Presentation;

namespace Jolly.Tests.PlayMode.Presentation
{
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
