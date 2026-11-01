using Jolly.Audio.Capture;
using Jolly.Audio.LipSync;
using Jolly.Contracts;
using Jolly.Core.Offline;
using Jolly.Core.Speech;
using Jolly.Integrations.ULipSync;

namespace Jolly.App.Composition
{
    /// <summary>
    /// Voice in and out (architecture §5.1, §11 Audio output): the microphone, the clip-less streaming output on Nubi, the
    /// lip-sync driver reading what is played, the TTS sentence queue and the canned lines with their offline responder.
    /// </summary>
    public sealed class SpeechServices
    {
        public IAudioCapture Microphone { get; private set; }
        public ISpeechOutput Output { get; private set; }
        public ILipSyncDriver LipSync { get; private set; }
        public SpeechQueue Queue { get; private set; }
        public SentenceSplitter Splitter { get; private set; }
        public ICannedLines Lines { get; private set; }
        public OfflineResponder Offline { get; private set; }

        public static SpeechServices Build(Foundation f, JollyConfig config, SceneRefs scene, Edges edges, ITextToSpeech tts,
                                           ILocalizer localizer, IUserPreferences preferences,
                                           DisposableStack disposables)
        {
            ISpeechOutput output = edges.SpeechOutput ?? scene.speechOutput;
            ILipSyncDriver lipSync = scene.lipSync != null && scene.lipSync.profile != null
                ? disposables.Add(new ULipSyncDriver(scene.lipSync, f.Clock))
                : new AmplitudeLipSyncDriver(scene.speechOutput.Level, f.Clock);
            var lines = new CannedLines(CannedLineCatalog.English, localizer, config.lineClips, f.Random);
            return new SpeechServices
            {
                Microphone = disposables.Add(new UnityMicrophoneCapture(edges.Microphone, f.Clock, f.Log)),
                Output = output,
                LipSync = lipSync,
                Queue = disposables.Add(new SpeechQueue(tts, output, preferences)),
                Splitter = new SentenceSplitter(),
                Lines = lines,
                Offline = disposables.Add(new OfflineResponder(lines, output, f.Clock, f.Log)),
            };
        }
    }
}
