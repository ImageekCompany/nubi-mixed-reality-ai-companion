using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>Outcome of one streamed identification. The times are <see cref="IClock.Now"/> values, NaN if never reached.</summary>
    public readonly struct VisionAnswer
    {
        public VisionAnswer(string label, string nounPhrase, string text, bool timedOut, ProviderException error,
                            double headerAt, double firstTextAt)
        {
            Label = label; NounPhrase = nounPhrase; Text = text; TimedOut = timedOut; Error = error;
            HeaderAt = headerAt; FirstTextAt = firstTextAt;
        }
        public string Label { get; }
        public string NounPhrase { get; }
        public string Text { get; }
        public bool TimedOut { get; }
        public ProviderException Error { get; }
        public double HeaderAt { get; }
        public double FirstTextAt { get; }
    }

    /// <summary>
    /// Streams <see cref="IObjectIdentifier.Identify"/> into the turn's speech under the vision timeout. Publishes the
    /// header's noun phrase (or null once the stream ends without one) for the SAM retry. Only the caller's cancel throws.
    /// </summary>
    sealed class VisionAnswerStream
    {
        readonly IObjectIdentifier _identifier;
        readonly IClock _clock;
        readonly TimeSpan _timeout;

        public VisionAnswerStream(IObjectIdentifier identifier, IClock clock, TimeSpan timeout)
        {
            _identifier = identifier;
            _clock = clock;
            _timeout = timeout;
        }

        public async UniTask<VisionAnswer> Run(EncodedImage image, string question, ToolContext context,
                                               UniTaskCompletionSource<string> nounPhrase, CancellationToken ct)
        {
            var request = new IdentifyRequest
            {
                Question = question, PersonaInstructions = context.PersonaInstructions, LanguageTag = context.Turn?.LanguageTag,
            };
            string label = null, phrase = null, full = null;
            double headerAt = double.NaN, firstTextAt = double.NaN;
            var spoken = new StringBuilder();
            using var scope = new TimeoutScope(_clock, _timeout, ct);
            try
            {
                await foreach (var e in _identifier.Identify(image, request, scope.Token).WithCancellation(scope.Token))
                {
                    switch (e.Kind)
                    {
                        case IdentificationEventKind.Header:
                            label = e.Label;
                            phrase = e.NounPhrase;
                            headerAt = _clock.Now;
                            nounPhrase.TrySetResult(phrase);
                            break;
                        case IdentificationEventKind.AnswerDelta:
                            if (spoken.Length == 0)
                                firstTextAt = _clock.Now;
                            spoken.Append(e.Text);
                            context.Speech?.Append(e.Text);
                            break;
                        case IdentificationEventKind.Completed:
                            full = e.Text;
                            break;
                    }
                }
                return new VisionAnswer(label, phrase, full ?? spoken.ToString(), false, null, headerAt, firstTextAt);
            }
            catch (OperationCanceledException) when (scope.TimedOut && !ct.IsCancellationRequested)
            {
                return new VisionAnswer(label, phrase, spoken.ToString(), true, null, headerAt, firstTextAt);
            }
            catch (ProviderException e)
            {
                return new VisionAnswer(label, phrase, spoken.ToString(), false, e, headerAt, firstTextAt);
            }
            finally
            {
                nounPhrase.TrySetResult(phrase);
            }
        }
    }
}
