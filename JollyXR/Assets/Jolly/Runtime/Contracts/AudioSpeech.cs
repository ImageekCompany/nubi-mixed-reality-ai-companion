using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Jolly.Contracts
{
    /// <summary>
    /// Pooled mono s16le PCM. A class, not a struct: a struct copy (await foreach variables are read-only, so Dispose
    /// runs on a defensive copy) would keep the array and could return it to the pool twice, letting two renters share it.
    /// The receiver owns it and calls Dispose; Dispose is idempotent per instance and returns both the buffer and the
    /// instance to their pools. Never touch a chunk after Dispose.
    /// </summary>
    public sealed class PcmChunk : IDisposable
    {
        static readonly System.Collections.Concurrent.ConcurrentBag<PcmChunk> Pool = new();
        int _disposed;
        PcmChunk() { }

        public short[] Buffer { get; private set; }     // length >= Length; never read past Length
        public int Length { get; private set; }
        public int SampleRate { get; private set; }
        public double CaptureTime { get; private set; } // IClock.Now of the first sample; 0 for synthesized audio

        public static PcmChunk Rent(int length, int sampleRate, double captureTime = 0)
        {
            if (!Pool.TryTake(out var c)) c = new PcmChunk();
            c._disposed = 0;
            c.Buffer = System.Buffers.ArrayPool<short>.Shared.Rent(length);
            c.Length = length; c.SampleRate = sampleRate; c.CaptureTime = captureTime;
            return c;
        }
        public ReadOnlySpan<short> Span => new(Buffer, 0, Length);
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
            System.Buffers.ArrayPool<short>.Shared.Return(Buffer);
            Buffer = null; Length = 0;
            Pool.Add(this);
        }
    }

    public interface IAudioCapture
    {
        bool IsAvailable { get; }
        /// <summary>Starts the mic, resampling to targetSampleRate (16000 or 24000). One session at a time.</summary>
        IAudioCaptureSession Start(int targetSampleRate);
    }

    public interface IAudioCaptureSession : IDisposable
    {
        /// <summary>~80 ms chunks. Completes normally after Stop(); Dispose() aborts.</summary>
        IUniTaskAsyncEnumerable<PcmChunk> Chunks { get; }
        float Level { get; }   // RMS 0..1 of the last chunk, for UI
        void Stop();
    }

    public enum TranscriptKind { Partial, Final }

    public readonly struct TranscriptEvent
    {
        public TranscriptEvent(TranscriptKind kind, string text, double audioMs) { Kind = kind; Text = text; AudioMs = audioMs; }
        public TranscriptKind Kind { get; }
        public string Text { get; }     // Partial: cumulative (replace previous). Final: cased, normalised punctuation
        public double AudioMs { get; }
    }

    public sealed class SttOptions
    {
        public string LanguageBias { get; set; } = "English";   // Muse ASR languageBias value
        public int SampleRate { get; set; } = 24000;            // 16000 | 24000
    }

    public interface ISpeechToText
    {
        /// <summary>
        /// Connects immediately and buffers audio during the handshake (≤5 s). After the ack it sends the buffered backlog
        /// at once, then forwards live chunks as they arrive (never more than 5 s ahead of the wall clock, the server limit).
        /// When the source completes it sends endStream within one frame, then yields exactly one Final (Text may be empty).
        /// Strict 1× pacing of the backlog is forbidden: it would delay the final by the whole handshake (~1 s).
        /// </summary>
        IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options, CancellationToken ct);
    }

    public sealed class VoiceSpec
    {
        public string VoiceId { get; set; } = "Charlie";  // Wit voice name
        public string Style { get; set; }                  // null = provider default
        public int Speed { get; set; } = 100;              // Wit 50..200
        public int Pitch { get; set; } = 100;              // Wit 25..200
    }

    public interface ITextToSpeech
    {
        int SampleRate { get; }        // Wit audio/raw: 24000
        int MaxCharacters { get; }     // 240 (callers split; Wit SDK limit is 280)
        bool Supports(string languageTag);
        /// <summary>Streams PCM for one utterance as it arrives. The receiver owns each chunk.</summary>
        IUniTaskAsyncEnumerable<PcmChunk> Synthesize(string text, VoiceSpec voice, CancellationToken ct);
    }

    public interface ISpeechOutput
    {
        bool IsPlaying { get; }
        /// <summary>Appends a stream to the playback ring; completes when its last sample has been played. Fires FirstSamplePlayed per call.</summary>
        UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct);
        UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct);
        /// <summary>Immediate silence (20 ms fade) and clears all queued audio. Every pending Play/PlayClip completes
        /// with OperationCanceledException, so SpeechQueue.WhenDrained never hangs after a barge-in. Safe to call any time.</summary>
        void Flush();
        /// <summary>utteranceId; raised on the main thread when the DSP actually consumed the first sample
        /// (OnAudioFilterRead path, not a streaming-clip read-ahead).</summary>
        event Action<int> FirstSamplePlayed;
    }

    /// <summary>Vowel weights 0..1 for the mouth rig, plus overall loudness.</summary>
    public readonly struct MouthShape
    {
        public MouthShape(float a, float i, float u, float e, float o, float volume)
        { A = a; I = i; U = u; E = e; O = o; Volume = volume; }
        public float A { get; } public float I { get; } public float U { get; } public float E { get; } public float O { get; }
        public float Volume { get; }
        public static MouthShape Closed => default;
    }

    public interface ILipSyncDriver
    {
        /// <summary>Latest analysed shape; polled in LateUpdate. Goes to Closed within ~100 ms of silence.</summary>
        MouthShape Current { get; }
        void Reset();
    }

    /// <summary>PTT. A pinch counts only when the hand's ray hovers no XRI interactable and the palm does not face
    /// the head (the Horizon OS system-menu gesture). Hand is configurable (dominant / either).</summary>
    public interface IVoiceActivation
    {
        bool IsHeld { get; }
        event Action Pressed;    // main thread
        event Action Released;   // main thread
    }
}
