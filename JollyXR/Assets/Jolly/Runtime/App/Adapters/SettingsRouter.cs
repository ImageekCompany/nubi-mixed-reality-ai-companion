using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Presentation;

namespace Jolly.App.Adapters
{
    /// <summary>
    /// Routes the settings panel's requests that are not preferences (contracts §9.1): a persona switch is recorded and
    /// applied by the controller when Idle, a language goes to the localizer, and Clear memory goes to the controller,
    /// which also defers it until Idle.
    /// </summary>
    public sealed class SettingsRouter : IDisposable
    {
        readonly SettingsPanel _panel;
        readonly IPersonaProvider _personas;
        readonly ILocalizer _localizer;
        readonly ConversationController _conversation;
        readonly ILog _log;
        readonly CancellationToken _appCt;

        public SettingsRouter(SettingsPanel panel, IPersonaProvider personas, ILocalizer localizer,
                              ConversationController conversation, ILog log, CancellationToken appCt)
        {
            _panel = panel;
            _personas = personas;
            _localizer = localizer;
            _conversation = conversation;
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

        void OnClearMemory() => _conversation.ClearMemory();
    }
}
