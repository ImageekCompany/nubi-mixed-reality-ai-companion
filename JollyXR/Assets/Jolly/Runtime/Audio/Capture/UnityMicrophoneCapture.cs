using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Audio.Capture
{
    /// <summary>
    /// <see cref="IAudioCapture"/> for the headset microphone. Pumps the active session once per frame on the
    /// PlayerLoop Update timing. Starting a new session aborts the previous one, so a barge-in press can never leave
    /// two recordings on the one device. Call <see cref="Start"/> only after the XR session has focus: a cold start
    /// during app load is the case that stalled on device (spike §2.6).
    /// </summary>
    public sealed class UnityMicrophoneCapture : IAudioCapture, IDisposable
    {
        readonly IMicrophoneDevice _device;
        readonly IClock _clock;
        readonly ILog _log;
        readonly CancellationTokenSource _lifetime = new();
        MicrophoneCaptureSession _session;

        public UnityMicrophoneCapture(IMicrophoneDevice device, IClock clock, ILog log)
        {
            _device = device;
            _clock = clock;
            _log = log;
        }

        public bool IsAvailable => _device.IsAvailable;

        public IAudioCaptureSession Start(int targetSampleRate)
        {
            if (_lifetime.IsCancellationRequested)
                throw new ObjectDisposedException(nameof(UnityMicrophoneCapture));

            if (_session is { IsCapturing: true })
            {
                _log.Write(LogLevel.Warning, "mic", "evt=mic.session_replaced");
                _session.Dispose();
            }
            _session = new MicrophoneCaptureSession(_device, targetSampleRate, _clock, _log);
            PumpAsync(_session, _lifetime.Token).Forget();
            return _session;
        }

        public void Dispose()
        {
            if (_lifetime.IsCancellationRequested)
                return;
            _lifetime.Cancel();
            _lifetime.Dispose();
            _session?.Dispose();
            _session = null;
        }

        static async UniTaskVoid PumpAsync(MicrophoneCaptureSession session, CancellationToken ct)
        {
            while (session.IsCapturing)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    return;
                session.Pump();
            }
        }
    }
}
