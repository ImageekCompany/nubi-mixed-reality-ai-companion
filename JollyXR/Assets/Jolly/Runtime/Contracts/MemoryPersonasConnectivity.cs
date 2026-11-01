using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Jolly.Contracts
{
    public sealed class SeenObject
    {
        public string Id { get; set; }            // "obj_N"; N from a persisted monotonic counter, never reused across sessions
        public string Label { get; set; }
        public Vector3 WorldPosition { get; set; }
        public string AnchorId { get; set; }      // optional
        public DateTimeOffset SeenAt { get; set; }
        public string Summary { get; set; }       // <= 140 chars
    }

    public sealed class PlaceRecord
    {
        public string Name { get; set; }          // normalised: trimmed, lower-case
        public Pose Pose { get; set; }
        public string AnchorId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public interface ISceneMemory
    {
        IReadOnlyList<SeenObject> Objects { get; }   // most recent first, capped at 64 (LRU)
        IReadOnlyList<PlaceRecord> Places { get; }   // capped at 32
        SeenObject AddObject(string label, Vector3 worldPosition, string summary, string anchorId = null);
        bool TryGetObject(string id, out SeenObject obj);
        void RememberPlace(PlaceRecord place);       // same name overwrites
        bool TryGetPlace(string name, out PlaceRecord place);
        /// <summary>Sanitised data block for a user-role item (never instructions): labels are printable, single-line,
        /// quote-free and &lt;= 40 chars; summaries come from our own template, not raw model text.</summary>
        string BuildPromptContext(int maxChars);
        /// <summary>Last encoded capture of this session (RAM only, never persisted), for highlight_part / find_object.</summary>
        EncodedImage LastCapture { get; set; }
        UniTask Load(CancellationToken ct);
        UniTask Save(CancellationToken ct);
        void Clear();
    }

    public interface IConversationMemory
    {
        string Summary { get; }
        IReadOnlyList<ConversationItem> Recent { get; }
        /// <summary>Appends one finished turn atomically. Opaque items are rejected (turn-local only). Throws if a ToolCall
        /// has no ToolResult in the same batch (the API returns 400 for an orphan call). Cross-turn replay of
        /// function_call + output without reasoning items is verified live (2026-11-01).</summary>
        void CommitTurn(IReadOnlyList<ConversationItem> items);
        /// <summary>True when Recent exceeds 16 items or 6,000 chars.</summary>
        bool NeedsSummarization { get; }
        /// <summary>Folds the oldest half into Summary (<= 1,200 chars) with one LiveUser model call. Never splits a call from its result.</summary>
        UniTask Summarize(IChatModel model, CancellationToken ct);
        /// <summary>carrySummary: the previous persona's Summary, used when the new persona has no saved memory.
        /// The controller never carries a summary into or out of a persona with PersistMemory=false.</summary>
        UniTask Load(string personaId, string carrySummary, CancellationToken ct);
        UniTask Save(CancellationToken ct);          // no-op when the persona has PersistMemory=false
        void Clear();
    }

    public sealed class PersonaProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string SystemPrompt { get; set; }               // resolved for the current locale
        public string SafetyNotice { get; set; }               // null if none; spoken and captioned once per session
        public VoiceSpec Voice { get; set; }                   // resolved for the current locale
        public IReadOnlyList<string> AllowedTools { get; set; }
        public bool PersistMemory { get; set; }
        public bool CrisisHandoff { get; set; }
        public Mood DefaultMood { get; set; }
    }

    public interface IPersonaProvider
    {
        PersonaProfile Current { get; }
        IReadOnlyList<string> AvailableIds { get; }
        /// <summary>Records a switch to apply when the conversation is next Idle (used by set_persona inside a turn).
        /// False for an unknown id. Never cancels anything itself.</summary>
        bool RequestSwitch(string id);
        string PendingSwitchId { get; }   // null when none
        /// <summary>Called by the controller only, between turns: resolves the profile for the current locale and raises Changed.
        /// The controller owns sessionCts and swaps memory; see architecture §3.</summary>
        UniTask<bool> ApplySwitch(string id, CancellationToken ct);
        event Action<PersonaProfile> Changed;
    }

    public sealed class LocaleVoiceProfile
    {
        public string LanguageTag { get; set; }        // "en", "pl"
        public string AsrLanguageBias { get; set; }    // "English", "Polish"
        public VoiceSpec TtsVoice { get; set; }
        public bool TtsAvailable { get; set; }         // false: captions + baked lines only
        public string ReplyLanguageName { get; set; }  // used in the instruction "Reply in {0}."
    }

    public interface ILocalizer
    {
        string LanguageTag { get; }
        LocaleVoiceProfile Voice { get; }
        IReadOnlyList<string> AvailableLanguageTags { get; }
        /// <summary>Returns the key itself, and logs a warning, if missing. Tables are preloaded at startup.</summary>
        string Get(string table, string key);
        UniTask SetLanguage(string languageTag, CancellationToken ct);
        event Action Changed;
    }

    /// <summary>Unauthorized: key missing or 401 (UI "API key missing or invalid"; the probe is disabled until the key changes).
    /// BudgetReached: IApiUsageMeter.IsCapReached. Both route turns to IOfflineResponder like Offline.</summary>
    public enum Connectivity { Online, Degraded, Offline, Unauthorized, BudgetReached }

    public interface IConnectivityMonitor : IDisposable
    {
        Connectivity State { get; }
        event Action<Connectivity> Changed;    // main thread
        void ReportSuccess(string provider);
        void ReportFailure(string provider, ProviderErrorKind kind);
        UniTask<Connectivity> ProbeNow(CancellationToken ct);
    }

    public interface IHealthProbe
    {
        /// <summary>Cheap authenticated reachability check (GET /v1/models). Never throws except on cancellation.</summary>
        UniTask<bool> Ping(CancellationToken ct);
    }

    public enum LineCategory { Greeting, WhoAmI, Acknowledge, Offline, Error, BudgetReached, Idle, SafetyNotice, CrisisHandoff, Unknown }

    public sealed class CannedLine
    {
        public CannedLine(string key, string text, AudioClip clip) { Key = key; Text = text; Clip = clip; }
        public string Key { get; }
        public string Text { get; }     // caption
        public AudioClip Clip { get; }  // may be null (caption only)
    }

    public interface ICannedLines
    {
        /// <summary>Random line for the current locale, avoiding the last 5 picks of the category.</summary>
        bool TryPick(LineCategory category, out CannedLine line);
    }

    /// <summary>Answers a turn without the cloud (Offline, Unauthorized, BudgetReached, or STT unavailable).
    /// Plays a canned line through ISpeechOutput with captions and completes when it has played.</summary>
    public interface IOfflineResponder
    {
        UniTask Respond(LineCategory category, int utteranceId, CancellationToken ct);
    }

    public sealed class UserPreferencesSnapshot
    {
        public bool Seated { get; set; }
        public int CaptionSize { get; set; }       // 0 small, 1 medium, 2 large
        public float SpeechRate { get; set; } = 1f; // 0.5..2; final Wit speed = clamp(persona speed * rate, 50, 200)
        public float Volume { get; set; } = 1f;
        public bool PttEitherHand { get; set; } = true;
    }

    public interface IUserPreferences
    {
        UserPreferencesSnapshot Current { get; }
        event Action<UserPreferencesSnapshot> Changed;
    }
}
