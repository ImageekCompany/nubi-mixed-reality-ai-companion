using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;

namespace Jolly.App.Adapters
{
    /// <summary>
    /// Routes the settings panel's requests that are not preferences (contracts §9.1): a persona switch is recorded and
    /// applied by the controller when Idle, a language goes to the localizer, and Clear memory resets both stores and
    /// deletes their files (usage is kept, architecture §8).
    /// </summary>
    public sealed class SettingsRouter : IDisposable
    {
        readonly SettingsPanel _panel;
        readonly IPersonaProvider _personas;
        readonly ILocalizer _localizer;
        readonly IConversationMemory _conversation;
        readonly ISceneMemory _scene;
        readonly ILog _log;
        readonly CancellationToken _appCt;

        public SettingsRouter(SettingsPanel panel, IPersonaProvider personas, ILocalizer localizer,
                              IConversationMemory conversation, ISceneMemory scene, ILog log, CancellationToken appCt)
        {
            _panel = panel;
            _personas = personas;
            _localizer = localizer;
            _conversation = conversation;
            _scene = scene;
            _log = log;
            _appCt = appCt;
            _panel.PersonaSelected += OnPersonaSelected;
            _panel.LanguageSelected += OnLanguageSelected;
            _panel.ClearMemoryRequested += OnClearMemory;
        }

        public void Dispose()
        {
            _panel.PersonaSelected -= OnPersonaSelected;
            _panel.LanguageSelected -= OnLanguageSelected;
            _panel.ClearMemoryRequested -= OnClearMemory;
        }

        void OnPersonaSelected(string id)
        {
            if (!_personas.RequestSwitch(id))
                _log.Write(LogLevel.Warning, "settings", "evt=settings.unknown_persona id=" + id);
        }

        void OnLanguageSelected(string languageTag) => SetLanguage(languageTag).Forget();

        async UniTaskVoid SetLanguage(string languageTag)
        {
            try
            {
                await _localizer.SetLanguage(languageTag, _appCt);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Error, "settings", "evt=settings.language_failed lang=" + languageTag, e);
            }
        }

        void OnClearMemory()
        {
            _conversation.Clear();
            _scene.Clear();
            _log.Write(LogLevel.Info, "settings", "evt=memory.cleared");
        }
    }
}
