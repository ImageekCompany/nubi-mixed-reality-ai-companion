using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Providers.MetaApi.Asr;

namespace Jolly.App.Composition
{
    /// <summary>
    /// Moves first-use costs from the first turn to the launch (Quest, 2026-11-01): one discarded camera capture, which
    /// reads the Camera2 lens pose over JNI (20 ms on the main thread) and initialises the CPU-image conversion and the
    /// JPEG encoder (a 53 ms frame at the first PTT release before), and an ASR connection opened and dropped without a
    /// handshake. Neither is billed: nothing reaches a model.
    /// </summary>
    public static class StartupWarmup
    {
        const double CameraWaitSeconds = 10;

        public static async UniTask Run(FrameGrabber frames, MuseRealtimeSpeechToText asr, IClock clock, ILog log,
                                        CancellationToken ct)
        {
            var started = clock.Now;
            await UniTask.WhenAll(Camera(frames, clock, log, ct), asr.WarmUp(ct));
            log.Write(LogLevel.Info, "app", $"evt=warmup.done ms={(clock.Now - started) * 1000:0}");
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
