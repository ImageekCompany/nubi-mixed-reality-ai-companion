using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Tests.PlayMode.Presentation
{
    /// <summary>Returns "[lang] key" so tests can see both the key and the language a view asked for.</summary>
    sealed class FakeLocalizer : ILocalizer
    {
        public string LanguageTag { get; private set; } = "en";
        public LocaleVoiceProfile Voice => new() { LanguageTag = LanguageTag };
        public IReadOnlyList<string> AvailableLanguageTags { get; } = new[] { "en", "pl" };
        public event Action Changed;
        public int SubscriberCount => Changed == null ? 0 : Changed.GetInvocationList().Length;

        public string Get(string table, string key) => $"[{LanguageTag}] {key}";

        public UniTask SetLanguage(string languageTag, CancellationToken ct)
        {
            LanguageTag = languageTag;
            Changed?.Invoke();
            return UniTask.CompletedTask;
        }
    }
}
