using System;
using Cysharp.Threading.Tasks;
using Jolly.Audio.Resampling;
using Jolly.Contracts;

namespace Jolly.Audio.Capture
{
    /// <summary>
    /// One push-to-talk recording: device ring → resampler → 80 ms pooled chunks. Driven by <see cref="Pump"/> once per
    /// frame on the main thread (see <see cref="UnityMicrophoneCapture"/>); allocation-free per pump. A head that stops
    /// moving for 500 ms restarts the device, and after three fruitless restarts the stream ends normally, so the STT
    /// returns an empty final instead of the turn hanging. A restart counts as fruitful only once 500 ms of audio has
    /// arrived after it: the stall seen on device delivers one buffer and then nothing.
    /// </summary>
    public sealed class MicrophoneCaptureSession : IAudioCaptureSession
    {
        const double ChunkSeconds = 0.08;
        const double StallTimeoutSeconds = 0.5;
        const int MaxStallRestarts = 3;
        const int ReadBlock = 2048;

        readonly IMicrophoneDevice _device;
        readonly IClock _clock;
        readonly ILog _log;
        readonly Channel<PcmChunk> _channel = Channel.CreateSingleConsumerUnbounded<PcmChunk>();
        readonly MicrophoneRingReader _reader = new();
        readonly MicrophoneStallWatchdog _watchdog = new(StallTimeoutSeconds);
        readonly float[] _input = new float[ReadBlock];
        readonly Resampler _resampler;
        readonly float[] _resampled;
        readonly PcmChunker _chunker;
        int _restarts;
        long _samplesSinceRestart;
        bool _rebaseTime = true;

        public MicrophoneCaptureSession(IMicrophoneDevice device, int targetSampleRate, IClock clock, ILog log)
        {
            if (targetSampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetSampleRate), targetSampleRate, "Sample rate must be positive.");

            _device = device;
            _clock = clock;
            _log = log;
            Chunks = _channel.Reader.ReadAllAsync();
            _chunker = new PcmChunker(_channel.Writer, targetSampleRate, (int)(targetSampleRate * ChunkSeconds));

            IsCapturing = device.Start();
            if (!IsCapturing)
            {
                _log.Write(LogLevel.Error, "mic", "evt=mic.start_failed");
                _channel.Writer.TryComplete(new InvalidOperationException("The microphone did not start."));
                ObserveCompletion();
                return;
            }
            _resampler = new Resampler(device.SampleRate, targetSampleRate);
            _resampled = new float[_resampler.MaxOutputLength(ReadBlock)];
            _watchdog.Reset(clock.Now);
        }

        public IUniTaskAsyncEnumerable<PcmChunk> Chunks { get; }
        public float Level => _chunker.Level;
        /// <summary>False once stopped, disposed or given up; the pump loop ends then.</summary>
        public bool IsCapturing { get; private set; }

        public void Pump()
        {
            if (!IsCapturing)
                return;

            var now = _clock.Now;
            if (_watchdog.IsStalled(_device.Position, now))
            {
                RecoverFromStall(now);
                return;
            }

            var pending = _reader.Available(_device);
            if (pending == 0)
                return;
            if (_rebaseTime)
            {
                _chunker.Rebase(now - (double)pending / _device.SampleRate);
                _rebaseTime = false;
            }
            _samplesSinceRestart += pending;
            if (_samplesSinceRestart >= _device.SampleRate * StallTimeoutSeconds)
                _restarts = 0;

            int read;
            while ((read = _reader.Read(_device, _input)) > 0)
            {
                var count = _resampler.Process(new ReadOnlySpan<float>(_input, 0, read), _resampled);
                _chunker.Append(new ReadOnlySpan<float>(_resampled, 0, count));
            }
        }

        /// <summary>Reads what the device still holds, emits the partial chunk and completes <see cref="Chunks"/>.</summary>
        public void Stop()
        {
            if (!IsCapturing)
                return;
            Pump();
            _chunker.Flush();
            End(null);
        }

        /// <summary>Aborts: drops the partial chunk and ends <see cref="Chunks"/> with OperationCanceledException.</summary>
        public void Dispose()
        {
            if (!IsCapturing)
                return;
            _chunker.Discard();
            End(new OperationCanceledException("The capture session was disposed."));
        }

        void RecoverFromStall(double now)
        {
            if (_restarts >= MaxStallRestarts)
            {
                _log.Write(LogLevel.Error, "mic", "evt=mic.dead restarts=3");
                _chunker.Flush();
                End(null);
                return;
            }

            _restarts++;
            _log.Write(LogLevel.Warning, "mic", "evt=mic.stall action=restart");
            _device.Stop();
            if (!_device.Start())
            {
                _log.Write(LogLevel.Error, "mic", "evt=mic.start_failed");
                End(null);
                return;
            }
            _reader.Reset();
            _watchdog.Reset(now);
            _samplesSinceRestart = 0;
            _rebaseTime = true;
        }

        void End(Exception error)
        {
            IsCapturing = false;
            _device.Stop();
            _channel.Writer.TryComplete(error);
        }

        /// <summary>
        /// A channel faulted before anything read it keeps an ExceptionResultSource as its Completion; nobody awaits
        /// that, so its finalizer would publish the error to UniTaskScheduler as unobserved on a later GC. The
        /// reader of <see cref="Chunks"/> still gets the error.
        /// </summary>
        void ObserveCompletion()
        {
            try { _channel.Reader.Completion.GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { }
        }
    }
}
