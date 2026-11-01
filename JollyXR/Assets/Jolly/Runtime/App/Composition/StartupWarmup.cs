using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Providers.MetaApi.Asr;
using Jolly.Providers.Wit;

namespace Jolly.App.Composition
{
    /// <summary>
    /// Moves first-use costs from the first turn to the launch (Quest, 2026-11-01): one discarded camera capture, which
    /// reads the Camera2 lens pose over JNI (20 ms on the main thread) and initialises the CPU-image conversion and the
    /// JPEG encoder (a 53 ms frame at the first PTT release before), an ASR connection opened and dropped without a
    /// handshake (ADR-0023), and one discarded Wit synthesis in the persona's voice, which warms the connection and the
    /// synthesis path for the first sentence (<see cref="WitTextToSpeech.WarmUp"/>).
    /// None is billed: nothing reaches a model. The Meta Responses connection needs no step here: the connectivity
    /// monitor's launch probe (<c>GET /v1/models</c>) already opens it.
    /// </summary>
    public static class StartupWarmup
    {
        const double CameraWaitSeconds = 10;

        public static async UniTask Run(FrameGrabber frames, MuseRealtimeSpeechToText asr, WitTextToSpeech tts, VoiceSpec voice,
                                        IClock clock, ILog log, CancellationToken ct)
        {
            var started = clock.Now;
            await UniTask.WhenAll(Camera(frames, clock, log, ct), asr.WarmUp(ct), Tts(tts, voice, clock, log, ct));
            log.Write(LogLevel.Info, "app", $"evt=warmup.done ms={(clock.Now - started) * 1000:0}");
        }

        static async UniTask Tts(WitTextToSpeech tts, VoiceSpec voice, IClock clock, ILog log, CancellationToken ct)
        {
            if (tts == null)
                return;
            var started = clock.Now;
            var status = await tts.WarmUp(voice ?? new VoiceSpec(), ct);
            log.Write(LogLevel.Info, "app", $"evt=warmup.tts status={status} ms={(clock.Now - started) * 1000:0}");
        }

        static async UniTask Camera(FrameGrabber frames, IClock clock, ILog log, CancellationToken ct)
        {
            var deadline = clock.Now + CameraWaitSeconds;
            while (!frames.IsReady)
            {
                if (clock.Now > deadline)
                    return;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            try
            {
                await frames.Capture(ct);
            }
            catch (ProviderException e)
            {
                log.Write(LogLevel.Info, "app", $"evt=warmup.camera_failed kind={e.Kind}");
            }
        }
    }
}
