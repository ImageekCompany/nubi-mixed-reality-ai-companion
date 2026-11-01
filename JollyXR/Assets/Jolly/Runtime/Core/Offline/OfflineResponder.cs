using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Offline
{
    /// <summary>
    /// Plays one canned line per call. An Offline request alternates Offline lines with WhoAmI / Greeting fillers, so a user
    /// who keeps talking hears that Nubi is offline every other time without the same apology on repeat. A missing
    /// category falls back to the requested one, then to Error. Repeats are avoided by ICannedLines (last 5 per category),
    /// which also bounds repeats across the mixed sequence.
    /// </summary>
    public sealed class OfflineResponder : IOfflineResponder, IDisposable
    {
        const string LogCategory = "offline";
        const double CaptionSecondsPerChar = 0.06;
        const double MinCaptionSeconds = 1.5;
        const double MaxCaptionSeconds = 6;

        readonly ICannedLines _lines;
        readonly ISpeechOutput _speech;
        readonly IClock _clock;
        readonly ILog _log;
        int _offlineResponses;
        int _captionUtteranceId;
        string _pendingCaption;
        bool _disposed;

        public OfflineResponder(ICannedLines lines, ISpeechOutput speech, IClock clock, ILog log)
        {
            _lines = lines;
            _speech = speech;
            _clock = clock;
            _log = log;
            _speech.FirstSamplePlayed += OnFirstSamplePlayed;
        }

        /// <summary>utteranceId, caption text; raised at the line's first played sample (at once for a caption-only line).
        /// Relay it to IConversationEvents.CaptionStarted.</summary>
        public event Action<int, string> CaptionStarted;

        public async UniTask Respond(LineCategory category, int utteranceId, CancellationToken ct)
        {
            if (!TryPickLine(category, out var line))
            {
                if (_log.IsEnabled(LogLevel.Warning))
                    _log.Write(LogLevel.Warning, LogCategory, $"evt=offline.no_line category={category}");
                return;
            }

            if (line.Clip == null)
            {
                CaptionStarted?.Invoke(utteranceId, line.Text);
                await _clock.Delay(CaptionHold(line.Text), ct);
                return;
            }

            _captionUtteranceId = utteranceId;
            _pendingCaption = line.Text;
            try
            {
                await _speech.PlayClip(line.Clip, utteranceId, ct);
            }
            finally
            {
                if (_captionUtteranceId == utteranceId)
                    _pendingCaption = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _speech.FirstSamplePlayed -= OnFirstSamplePlayed;
        }

        bool TryPickLine(LineCategory requested, out CannedLine line)
        {
            var resolved = Resolve(requested);
            return _lines.TryPick(resolved, out line)
                || (resolved != requested && _lines.TryPick(requested, out line))
                || (requested != LineCategory.Error && _lines.TryPick(LineCategory.Error, out line));
        }

        LineCategory Resolve(LineCategory requested)
        {
            if (requested != LineCategory.Offline)
                return requested;
            var n = _offlineResponses++;
            if (n % 2 == 0)
                return LineCategory.Offline;
            return n / 2 % 2 == 0 ? LineCategory.WhoAmI : LineCategory.Greeting;
        }

        void OnFirstSamplePlayed(int utteranceId)
        {
            if (_pendingCaption == null || utteranceId != _captionUtteranceId)
                return;
            var text = _pendingCaption;
            _pendingCaption = null;
            CaptionStarted?.Invoke(utteranceId, text);
        }

        static TimeSpan CaptionHold(string text) =>
            TimeSpan.FromSeconds(Math.Clamp(text.Length * CaptionSecondsPerChar, MinCaptionSeconds, MaxCaptionSeconds));
    }
}
