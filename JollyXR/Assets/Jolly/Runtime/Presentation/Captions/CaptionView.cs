using Jolly.Contracts;
using TMPro;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>
    /// Two caption lines: the user's (live partial, then final) and Nubi's (each sentence at its first played sample).
    /// A line hides after a reading-time hold; a new turn (Listening) clears Nubi's line at once.
    /// </summary>
    public sealed class CaptionView : MonoBehaviour
    {
        [SerializeField] TMP_Text _userLine;
        [SerializeField] TMP_Text _nubiLine;
        [Tooltip("Font size per UserPreferencesSnapshot.CaptionSize (small, medium, large).")]
        [SerializeField] float[] _fontSizes = { 28f, 36f, 48f };
        [SerializeField] float _userHoldSeconds = 4f;
        [Tooltip("Nubi's line stays at least this long, plus the per-character reading time.")]
        [SerializeField] float _nubiMinHoldSeconds = 2.5f;
        [SerializeField] float _secondsPerCharacter = 0.06f;

        IConversationEvents _events;
        IUserPreferences _preferences;
        IClock _clock;
        CaptionLine _user;
        CaptionLine _nubi;

        public string UserText => _user.Text;
        public string NubiText => _nubi.Text;
        public float FontSize => _nubiLine.fontSize;

        public void Construct(IConversationEvents events, IUserPreferences preferences, IClock clock)
        {
            Unsubscribe();
            _events = events;
            _preferences = preferences;
            _clock = clock;
            _user = new CaptionLine(_userLine);
            _nubi = new CaptionLine(_nubiLine);

            _events.UserPartial += OnUserPartial;
            _events.UserFinal += OnUserFinal;
            _events.CaptionStarted += OnCaptionStarted;
            _events.StateChanged += OnStateChanged;
            _preferences.Changed += ApplyPreferences;
            ApplyPreferences(_preferences.Current);
        }

        void OnUserPartial(string text) => _user.Show(text, double.PositiveInfinity);

        void OnUserFinal(string text) => _user.Show(text, _clock.Now + _userHoldSeconds);

        void OnCaptionStarted(int utteranceId, string sentence) =>
            _nubi.Show(sentence, _clock.Now + _nubiMinHoldSeconds + sentence.Length * _secondsPerCharacter);

        void OnStateChanged(ConversationState from, ConversationState to)
        {
            if (to != ConversationState.Listening)
                return;
            _nubi.Hide();
            _user.Hide();
        }

        void ApplyPreferences(UserPreferencesSnapshot preferences)
        {
            var size = _fontSizes[Mathf.Clamp(preferences.CaptionSize, 0, _fontSizes.Length - 1)];
            _userLine.fontSize = size * 0.85f;
            _nubiLine.fontSize = size;
        }

        void Update()
        {
            if (_clock == null)
                return;
            var now = _clock.Now;
            _user.HideIfExpired(now);
            _nubi.HideIfExpired(now);
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_events != null)
            {
                _events.UserPartial -= OnUserPartial;
                _events.UserFinal -= OnUserFinal;
                _events.CaptionStarted -= OnCaptionStarted;
                _events.StateChanged -= OnStateChanged;
            }
            if (_preferences != null)
                _preferences.Changed -= ApplyPreferences;
            _events = null;
            _preferences = null;
        }
    }
}
