using System;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Presentation;
using Jolly.XR.Interaction;

namespace Jolly.App.Adapters
{
    /// <summary>
    /// Opens and closes the on-demand settings panel (ADR-0042): the wrist button and the menu button toggle it, "open
    /// settings" / "close settings" set it, and a talk turn closes it so it never sits between the user and the
    /// character while they talk. The wrist button is drawn only while the wrist anchor shows it.
    /// </summary>
    public sealed class SettingsLauncher : IDisposable
    {
        readonly SettingsWindow _window;
        readonly WristAnchor _anchor;
        readonly WristMenuButton _wrist;
        readonly MenuButton _menu;
        readonly TurnGuards _guards;
        readonly IConversationEvents _events;

        public SettingsLauncher(SettingsWindow window, WristAnchor anchor, WristMenuButton wrist, MenuButton menu,
                                TurnGuards guards, IConversationEvents events)
        {
            _window = window;
            _anchor = anchor;
            _wrist = wrist;
            _menu = menu;
            _guards = guards;
            _events = events;
            _anchor.ShownChanged += _wrist.SetVisible;
            _wrist.Pressed += OnWristPressed;
            _menu.Pressed += OnMenuPressed;
            _guards.SettingsRequested += OnVoice;
            _events.StateChanged += OnStateChanged;
        }

        public void Dispose()
        {
            _anchor.ShownChanged -= _wrist.SetVisible;
            _wrist.Pressed -= OnWristPressed;
            _menu.Pressed -= OnMenuPressed;
            _guards.SettingsRequested -= OnVoice;
            _events.StateChanged -= OnStateChanged;
        }

        void OnWristPressed() => _window.Toggle("wrist");

        void OnMenuPressed() => _window.Toggle("menu");

        void OnVoice(bool open)
        {
            if (open)
                _window.Open("voice");
            else
                _window.Close("voice");
        }

        void OnStateChanged(ConversationState from, ConversationState to)
        {
            if (to == ConversationState.Listening)
                _window.Close("talk");
        }
    }
}
