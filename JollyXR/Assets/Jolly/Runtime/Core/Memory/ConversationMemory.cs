using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Diagnostics;

namespace Jolly.Core.Memory
{
    /// <summary>
    /// Rolling summary + recent items per persona (architecture §8), stored in <c>conversation.&lt;personaId&gt;.json</c>.
    /// Whether a persona persists is read from <see cref="IPersonaProvider.Current"/> at <see cref="Load"/>, which the
    /// controller calls after ApplySwitch; an id mismatch is treated as non-persisting so a session can never leak.
    /// </summary>
    public sealed class ConversationMemory : IConversationMemory
    {
        const string FilePrefix = "conversation.";
        const string FileSuffix = ".json";

        readonly string _directory;
        readonly IPersonaProvider _personas;
        readonly MemoryLimits _limits;
        readonly ILog _log;
        readonly List<ConversationItem> _recent = new();
        string _personaId;
        bool _persist;
        int _generation;

        public ConversationMemory(string directory, IPersonaProvider personas, MemoryLimits limits, ILog log)
        {
            _directory = directory;
            _personas = personas;
            _limits = limits;
            _log = log;
        }

        public string Summary { get; private set; } = string.Empty;
        public IReadOnlyList<ConversationItem> Recent => _recent;
        public bool NeedsSummarization => _recent.Count > _limits.RecentItems || RecentChars() > _limits.RecentChars;

        public void CommitTurn(IReadOnlyList<ConversationItem> items)
        {
            TurnValidator.Validate(items);
            _recent.AddRange(items);
        }

        public async UniTask Summarize(IChatModel model, CancellationToken ct)
        {
            var cut = SummaryCut();
            if (cut == 0)
                return;
            var folded = _recent.GetRange(0, cut);
            var generation = _generation;

            var summary = await Summarizer.Run(model, Summary, folded, _limits, ct);

            if (summary == null)
            {
                _log.Write(LogLevel.Warning, "memory", "evt=memory.summary_unusable");
                return;
            }
            if (generation != _generation || !StartsWith(folded))
                return;
            _recent.RemoveRange(0, cut);
            Summary = summary;
        }

        public UniTask Load(string personaId, string carrySummary, CancellationToken ct)
        {
            ResetInMemory();
            _personaId = personaId;
            _persist = _personas.Current?.Id == personaId && _personas.Current.PersistMemory;
            Summary = carrySummary ?? string.Empty;
            if (!_persist || !AtomicFile.TryReadAllText(PathFor(personaId), out var json))
                return UniTask.CompletedTask;
            try
            {
                Summary = ConversationFile.Read(json, _recent);
            }
            catch (Exception e) when (StoreErrors.IsCorruptFile(e))
            {
                ResetInMemory();
                _log.Write(LogLevel.Warning, "memory", $"evt=memory.load_failed store=conversation persona={personaId}", e);
            }
            return UniTask.CompletedTask;
        }

        public UniTask Save(CancellationToken ct)
        {
            if (_persist)
                AtomicFile.WriteAllText(PathFor(_personaId), ConversationFile.Write(Summary, _recent));
            return UniTask.CompletedTask;
        }

        /// <summary>Deletes every persona's file: a kept one would be carried back into the others on the next switch.</summary>
        public void Clear()
        {
            ResetInMemory();
            if (!Directory.Exists(_directory))
                return;
            foreach (var path in Directory.GetFiles(_directory, FilePrefix + "*" + FileSuffix))
                AtomicFile.Delete(path);
        }

        void ResetInMemory()
        {
            _generation++;
            _recent.Clear();
            Summary = string.Empty;
        }

        /// <summary>Folds up to the first turn start at or after half, so a call is never split from its result.</summary>
        int SummaryCut()
        {
            for (var i = Math.Max(1, _recent.Count / 2); i < _recent.Count; i++)
            {
                if (_recent[i].Kind == ItemKind.UserText)
                    return i;
            }
            return _recent.Count;
        }

        bool StartsWith(List<ConversationItem> prefix)
        {
            if (prefix.Count > _recent.Count)
                return false;
            for (var i = 0; i < prefix.Count; i++)
            {
                if (!ReferenceEquals(prefix[i], _recent[i]))
                    return false;
            }
            return true;
        }

        int RecentChars()
        {
            var chars = 0;
            foreach (var item in _recent)
                chars += (item.Text?.Length ?? 0) + (item.Json?.Length ?? 0);
            return chars;
        }

        string PathFor(string personaId) => Path.Combine(_directory, FilePrefix + personaId + FileSuffix);
    }
}
