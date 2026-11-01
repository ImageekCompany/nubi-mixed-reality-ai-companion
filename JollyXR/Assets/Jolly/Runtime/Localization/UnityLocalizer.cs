using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Jolly.Localization
{
    /// <summary>
    /// ILocalizer over Unity Localization (ADR-0014). <see cref="Initialize"/> and every locale change preload the
    /// configured string tables (and those of the locale's fallbacks), so <see cref="Get"/> is a synchronous lookup.
    /// Main thread only.
    /// </summary>
    public sealed class UnityLocalizer : ILocalizer, IDisposable
    {
        const string LogCategory = "loc";

        readonly LocaleVoiceMapAsset _voices;
        readonly IList<TableReference> _preloadTables;
        readonly ILog _log;
        readonly CancellationTokenSource _lifetime = new();
        string[] _languageTags = Array.Empty<string>();
        string _requestedTag;
        bool _subscribed;
        bool _disposed;

        public UnityLocalizer(LocaleVoiceMapAsset voices, IReadOnlyList<string> preloadTables, ILog log)
        {
            if (voices == null) throw new ArgumentNullException(nameof(voices));
            _voices = voices;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _preloadTables = new TableReference[preloadTables.Count];
            for (var i = 0; i < preloadTables.Count; i++)
                _preloadTables[i] = preloadTables[i];
        }

        public string LanguageTag { get; private set; } = "en";
        public LocaleVoiceProfile Voice => _voices.Resolve(LanguageTag);
        public IReadOnlyList<string> AvailableLanguageTags => _languageTags;
        public event Action Changed;

        /// <summary>Waits for Unity Localization (startup locale: system language if available, else en), then preloads the tables.</summary>
        public async UniTask Initialize(CancellationToken ct)
        {
            await LocalizationSettings.InitializationOperation.WithCancellation(ct);
            var locales = LocalizationSettings.AvailableLocales.Locales;
            _languageTags = new string[locales.Count];
            for (var i = 0; i < locales.Count; i++)
                _languageTags[i] = locales[i].Identifier.Code;

            if (!_subscribed)
            {
                LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
                _subscribed = true;
            }
            await Reload(LocalizationSettings.SelectedLocale, ct);
        }

        public string Get(string table, string key)
        {
            var entry = LocalizationSettings.StringDatabase.GetTableEntry(table, key).Entry;
            var text = entry?.GetLocalizedString();
            if (!string.IsNullOrEmpty(text))
                return text;

            _log.Write(LogLevel.Warning, LogCategory, $"evt=loc.missing table={table} key={key} lang={LanguageTag}");
            return key;
        }

        public async UniTask SetLanguage(string languageTag, CancellationToken ct)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(languageTag);
            if (locale == null)
            {
                _log.Write(LogLevel.Warning, LogCategory, $"evt=loc.unknown_language lang={languageTag}");
                return;
            }
            if (locale.Identifier.Code == _requestedTag)
                return;
            // Claim the tag first: the setter may raise SelectedLocaleChanged synchronously, and the handler must not
            // start a second reload.
            _requestedTag = locale.Identifier.Code;
            LocalizationSettings.SelectedLocale = locale;
            await Reload(locale, ct);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_subscribed)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
                _subscribed = false;
            }
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        /// <summary>Locale changes made outside <see cref="SetLanguage"/> (e.g. the Editor's Game-view locale menu).
        /// Unity raises this only once its initialization operation is done, so it may arrive after SetLanguage's own reload.</summary>
        void OnSelectedLocaleChanged(Locale locale)
        {
            if (locale == null || locale.Identifier.Code == _requestedTag)
                return;
            Reload(locale, _lifetime.Token).Forget();
        }

        async UniTask Reload(Locale locale, CancellationToken ct)
        {
            var tag = locale.Identifier.Code;
            _requestedTag = tag;
            try
            {
                await LocalizationSettings.StringDatabase.PreloadTables(_preloadTables, locale).WithCancellation(ct);
                foreach (var fallback in locale.GetFallbacks())
                    await LocalizationSettings.StringDatabase.PreloadTables(_preloadTables, fallback).WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                // Release the claim so a retry with the same tag is not mistaken for a switch already in flight.
                if (tag == _requestedTag)
                    _requestedTag = LanguageTag;
                throw;
            }
            if (tag != _requestedTag)
                return;
            LanguageTag = tag;
            foreach (var table in _preloadTables)
            {
                var loaded = LocalizationSettings.StringDatabase.GetTable(table, locale);
                var entries = loaded == null ? 0 : loaded.Count;
                _log.Write(LogLevel.Info, LogCategory, $"evt=loc.ready table={table.TableCollectionName} lang={LanguageTag} entries={entries}");
            }
            Changed?.Invoke();
        }
    }
}
