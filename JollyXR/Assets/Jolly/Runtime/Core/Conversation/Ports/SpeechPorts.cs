using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// The shape of WP7's <c>SpeechQueue</c> (contracts §9) as a port, so the turn pipeline builds and tests without it.
    /// WP7's class implements it unchanged.
    /// </summary>
    public interface ISpeechQueue
    {
        void Enqueue(string sentence, VoiceSpec voice, int utteranceId);
        /// <summary>Completes when nothing is queued or playing; also completes after <see cref="Clear"/>.</summary>
        UniTask WhenDrained(CancellationToken ct);
        void Clear();
        /// <summary>Text of the sentences that have started playing since the last Clear (for the interrupted commit).</summary>
        string SpokenSoFar { get; }
    }

    /// <summary>The shape of WP7's <c>SentenceSplitter</c> (contracts §9) as a port.</summary>
    public interface ISentenceSplitter
    {
        void Append(string delta);
        bool TryTake(out string sentence);
        /// <summary>Returns and clears whatever is buffered (empty when nothing is).</summary>
        string Flush();
    }
}
