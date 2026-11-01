using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Jolly.Contracts
{
    public interface IClock
    {
        /// <summary>Monotonic seconds since startup (latency, scheduling). Never use for wall time.</summary>
        double Now { get; }
        DateTimeOffset UtcNow { get; }
        UniTask Delay(TimeSpan delay, CancellationToken ct);
    }

    public interface IRandom
    {
        float Range(float minInclusive, float maxInclusive);
        int Range(int minInclusive, int maxExclusive);
    }

    public enum LogLevel { Debug, Info, Warning, Error }

    public interface ILog
    {
        bool IsEnabled(LogLevel level);
        /// <summary>message is logfmt: "evt=name key=value ...". Never pass secrets, headers, bodies or base64.</summary>
        void Write(LogLevel level, string category, string message, Exception exception = null);
    }

    public interface ISecretsProvider
    {
        /// <summary>False when absent or empty. Never throws for a missing key. Values must never be logged.</summary>
        bool TryGet(string key, out string value);
    }

    public static class SecretKeys
    {
        public const string MetaApiKey = "META_API_KEY";
        public const string WitClientToken = "WIT_AI_CLIENT_TOKEN";
        public const string WitServerToken = "WIT_AI_SERVER_TOKEN"; // editor tooling only, never needed on device
        public const string WitAppId = "WIT_AI_APP_ID";
    }

    public enum ProviderErrorKind { Network, Timeout, RateLimited, Server, Unauthorized, BadRequest, BudgetExceeded, Protocol }

    public sealed class ProviderException : Exception
    {
        public ProviderException(string provider, ProviderErrorKind kind, string message,
                                 int httpStatus = 0, string requestId = null, TimeSpan? retryAfter = null,
                                 Exception inner = null) : base(message, inner)
        { Provider = provider; Kind = kind; HttpStatus = httpStatus; RequestId = requestId; RetryAfter = retryAfter; }

        public string Provider { get; }
        public ProviderErrorKind Kind { get; }
        public int HttpStatus { get; }
        public string RequestId { get; }
        public TimeSpan? RetryAfter { get; }
        public bool IsTransient => Kind is ProviderErrorKind.Network or ProviderErrorKind.Timeout
                                        or ProviderErrorKind.RateLimited or ProviderErrorKind.Server;
    }

    public enum LatencyMark { PttPressed, PttReleased, AsrFinal, LlmRequestSent, LlmFirstEvent, LlmFirstText,
                              FirstSentenceQueued, TtsFirstByte, FirstAudioPlayed, MaskShown, TurnCompleted, BargeIn }

    public interface ILatencyTracker
    {
        void BeginTurn(int turnId);
        void Mark(int turnId, LatencyMark mark);
        /// <summary>Milliseconds between two marks of the last completed turn; NaN if either is missing.</summary>
        double Between(LatencyMark from, LatencyMark to);
    }
}
