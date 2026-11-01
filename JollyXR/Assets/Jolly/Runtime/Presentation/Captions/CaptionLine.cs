using TMPro;

namespace Jolly.Presentation
{
    /// <summary>One caption text with an expiry time in IClock seconds.</summary>
    sealed class CaptionLine
    {
        readonly TMP_Text _text;
        double _hideAt = double.PositiveInfinity;

        public CaptionLine(TMP_Text text)
        {
            _text = text;
            Hide();
        }

        public string Text => _text.enabled ? _text.text : string.Empty;

        public void Show(string text, double hideAt)
        {
            _text.text = text;
            _text.enabled = true;
            _hideAt = hideAt;
        }

        public void Hide()
        {
            _text.enabled = false;
            _text.text = string.Empty;
            _hideAt = double.PositiveInfinity;
        }

        public void HideIfExpired(double now)
        {
            if (now >= _hideAt)
                Hide();
        }
    }
}
