using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Localization;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Presentation
{
    /// <summary>Runs against the project's real Localization settings, locales and UI table.</summary>
    public sealed class UnityLocalizerTests
    {
        RecordingLog _log;
        UnityLocalizer _localizer;
        int _changed;

        [UnitySetUp]
        public IEnumerator SetUp() => UniTask.ToCoroutine(async () =>
        {
            _log = new RecordingLog();
            var voices = PresentationAssets.Load<LocaleVoiceMapAsset>(PresentationAssets.LocaleVoiceMap);
            _localizer = new UnityLocalizer(voices, new[] { "UI" }, _log);
            await _localizer.Initialize(CancellationToken.None);
            await _localizer.SetLanguage("en", CancellationToken.None);
            _changed = 0;
            _localizer.Changed += () => _changed++;
        });

        [UnityTearDown]
        public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
        {
            await _localizer.SetLanguage("en", CancellationToken.None);
            _localizer.Dispose();
        });

        [Test]
        public void Get_ReturnsTheTranslation()
        {
            Assert.That(_localizer.Get("UI", "settings.title"), Is.EqualTo("Settings"));
            Assert.That(_log.Contains(LogLevel.Warning, "loc.missing"), Is.False);
        }

        [Test]
        public void Get_MissingKey_FallsBackToTheKey_AndLogsAWarning()
        {
            Assert.That(_localizer.Get("UI", "no.such.key"), Is.EqualTo("no.such.key"));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=loc.missing table=UI key=no.such.key"), Is.True);
        }

        [Test]
        public void Initialize_LogsTheLoadedTable()
        {
            Assert.That(_log.Contains(LogLevel.Info, "evt=loc.ready table=UI lang=en entries="), Is.True);
        }

        [Test]
        public void AvailableLanguages_AreEnglishAndPolish()
        {
            Assert.That(_localizer.AvailableLanguageTags, Is.EquivalentTo(new[] { "en", "pl" }));
        }

        [UnityTest]
        public IEnumerator SetLanguage_SwitchesTablesAndVoice_AndRaisesChangedOnce() => UniTask.ToCoroutine(async () =>
        {
            await _localizer.SetLanguage("pl", CancellationToken.None);
            await UniTask.DelayFrame(2);

            Assert.That(_localizer.LanguageTag, Is.EqualTo("pl"));
            Assert.That(_localizer.Get("UI", "settings.title"), Is.EqualTo("Ustawienia"));
            Assert.That(_localizer.Voice.AsrLanguageBias, Is.EqualTo("Polish"));
            Assert.That(_localizer.Voice.TtsAvailable, Is.False, "the Wit app has no Polish voice (architecture §10)");
            Assert.That(_changed, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator SetLanguage_Unknown_KeepsTheCurrentLanguage() => UniTask.ToCoroutine(async () =>
        {
            await _localizer.SetLanguage("xx", CancellationToken.None);

            Assert.That(_localizer.LanguageTag, Is.EqualTo("en"));
            Assert.That(_changed, Is.Zero);
            Assert.That(_log.Contains(LogLevel.Warning, "evt=loc.unknown_language lang=xx"), Is.True);
        });

        [UnityTest]
        public IEnumerator ExternalLocaleChange_IsFollowed() => UniTask.ToCoroutine(async () =>
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("pl");
            await UniTask.WaitUntil(() => _changed > 0).TimeoutWithoutException(System.TimeSpan.FromSeconds(5));

            Assert.That(_localizer.LanguageTag, Is.EqualTo("pl"));
        });

        [UnityTest]
        public IEnumerator Dispose_Unsubscribes_FromTheStaticLocaleEvent() => UniTask.ToCoroutine(async () =>
        {
            _localizer.Dispose();

            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("pl");
            await UniTask.DelayFrame(5);

            Assert.That(_changed, Is.Zero);
            Assert.That(_localizer.LanguageTag, Is.EqualTo("en"));
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
        });
    }
}
