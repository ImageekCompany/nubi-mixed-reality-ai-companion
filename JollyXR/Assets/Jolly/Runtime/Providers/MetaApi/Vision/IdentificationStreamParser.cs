using System.Collections.Generic;
using System.Text;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// Turns streamed model text into <see cref="IdentificationEvent"/>s: the first line <c>LABEL|NOUN_PHRASE</c>,
    /// with a third <c>|PLACE</c> field when the request listed places, becomes one Header, everything after it streams as AnswerDelta, and <see cref="Complete"/> adds Completed with the
    /// whole answer. A first line without '|' (or longer than any sane header) yields an empty Header and is kept as
    /// answer text, so speech never waits on a malformed reply. Exactly one Header is always emitted.
    /// </summary>
    public sealed class IdentificationStreamParser
    {
        const int MaxHeaderLength = 120;
        static readonly char[] FieldTrim = { ' ', '\t', '*', '`', '"', '\'' };

        readonly StringBuilder _pending = new();
        readonly StringBuilder _answer = new();
        readonly Queue<IdentificationEvent> _ready = new();
        bool _headerDone;
        bool _completed;

        public void Append(string delta)
        {
            if (_completed || string.IsNullOrEmpty(delta))
                return;
            if (_headerDone)
            {
                EmitAnswer(delta);
                return;
            }

            foreach (var c in delta)
            {
                if (_pending.Length > 0 || !char.IsWhiteSpace(c))
                    _pending.Append(c);
            }

            var text = _pending.ToString();
            var newline = text.IndexOf('\n');
            if (newline >= 0)
                EmitHeader(text.Substring(0, newline).TrimEnd('\r'), text.Substring(newline + 1), text);
            else if (text.Length > MaxHeaderLength)
                EmitHeader(text, "", text);
        }

        public void Complete()
        {
            if (_completed)
                return;
            if (!_headerDone)
            {
                var text = _pending.ToString();
                EmitHeader(text, "", text);
            }
            _completed = true;
            _ready.Enqueue(new IdentificationEvent(IdentificationEventKind.Completed, text: _answer.ToString()));
        }

        public bool TryTake(out IdentificationEvent e) => _ready.TryDequeue(out e);

        void EmitHeader(string firstLine, string rest, string whole)
        {
            _headerDone = true;
            _pending.Clear();
            var bar = firstLine.Length <= MaxHeaderLength ? firstLine.IndexOf('|') : -1;
            if (bar < 0)
            {
                _ready.Enqueue(new IdentificationEvent(IdentificationEventKind.Header));
                EmitAnswer(whole);
                return;
            }
            var fields = firstLine.Split('|', 3);
            _ready.Enqueue(new IdentificationEvent(IdentificationEventKind.Header, label: Field(fields[0]),
                nounPhrase: Field(fields[1]), place: fields.Length > 2 ? PlaceField(fields[2]) : null));
            EmitAnswer(rest);
        }

        void EmitAnswer(string text)
        {
            if (text.Length == 0)
                return;
            _answer.Append(text);
            _ready.Enqueue(new IdentificationEvent(IdentificationEventKind.AnswerDelta, text: text));
        }

        static string PlaceField(string raw)
        {
            var place = Field(raw);
            return string.Equals(place, "none", System.StringComparison.OrdinalIgnoreCase) ? null : place;
        }

        static string Field(string raw)
        {
            var field = raw.Trim(FieldTrim);
            return field.Length > 0 ? field : null;
        }
    }
}
