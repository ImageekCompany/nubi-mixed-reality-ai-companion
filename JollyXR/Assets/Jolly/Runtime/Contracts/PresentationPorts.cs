using System;
using System.Collections.Generic;

namespace Jolly.Contracts
{
    public enum TranscriptSource { User, Nubi }

    /// <summary>Raised by Core on the main thread. Views (captions, tool badges, latency HUD, status) subscribe.</summary>
    public interface IConversationEvents
    {
        event Action<ConversationState, ConversationState> StateChanged;
        event Action<string> UserPartial;                    // cumulative partial transcript
        event Action<string> UserFinal;                      // after TranscriptAliases
        event Action<int, string> CaptionStarted;            // utteranceId, sentence text; at its first played sample
        event Action<string, string> ToolStarted;            // callId, tool name
        event Action<string, string, bool> ToolCompleted;    // callId, tool name, ok
        event Action<string, double> ModelCallCompleted;     // model id, ms (badges + HUD)
    }

    /// <summary>
    /// Model ids on <see cref="IConversationEvents.ModelCallCompleted"/>. Spark answering about a camera frame carries
    /// <see cref="VisionSuffix"/>, so the badges tell it from the agent's own Spark rounds.
    /// </summary>
    public static class ModelCallIds
    {
        public const string VisionSuffix = "+vision";

        public static string Vision(string model) => model + VisionSuffix;
    }

    /// <summary>World-space mask outline for capture_and_identify, find_object and highlight_part.</summary>
    public interface IMaskPresenter
    {
        /// <summary>Places the box-local mask(s) at distanceMeters along the capture camera's rays; fades after 6 s or on Clear.</summary>
        void Show(IReadOnlyList<SegmentedObject> objects, EncodedImage source, float distanceMeters);
        void Clear();
    }

    public enum StatusKind { None, ApiKeyMissing, Offline, Degraded, BudgetReached, CameraUnavailable, MicUnavailable }

    public interface IStatusSink
    {
        void Show(StatusKind status);   // localized by the view
    }
}
