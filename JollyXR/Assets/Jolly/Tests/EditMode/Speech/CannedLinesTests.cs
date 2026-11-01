using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Speech;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class CannedLinesTests
    {
        sealed class MapClipSource : ILineClipSource
        {
            public readonly Dictionary<string, AudioClip> Clips = new();

            public bool TryGet(string languageTag, string key, out AudioClip clip)
                => Clips.TryGetValue(languageTag + "/" + key, out clip);
        }

        static CannedLineEntry[] Catalog(LineCategory category, int count)
        {
            var entries = new CannedLineEntry[count];
            for (var i = 0; i < count; i++)
                entries[i] = new CannedLineEntry($"line_{i}", category, $"English {i}");
            return entries;
        }

        [Test]
        public void TryPick_UnknownCategory_ReturnsFalse()
        {
            var lines = new CannedLines(Catalog(LineCategory.Offline, 2), new TableLocalizer(), new MapClipSource(), new FakeRandom());

            var picked = lines.TryPick(LineCategory.Greeting, out var line);

            Assert.That(picked, Is.False);
            Assert.That(line, Is.Null);
        }

        [Test]
        public void TryPick_UsesLocalizedTextAndClipForCurrentLanguage()
        {
            var localizer = new TableLocalizer().With("pl", CannedLines.Table, "line_0", "Polski 0");
            localizer.SetLanguage("pl", default).Forget();
            var clips = new MapClipSource();
            var clip = AudioClip.Create("pl_line_0", 10, 1, 24000, false);
            clips.Clips["pl/line_0"] = clip;
            var lines = new CannedLines(Catalog(LineCategory.Offline, 1), localizer, clips, new FakeRandom());

            try
            {
                lines.TryPick(LineCategory.Offline, out var line);

                Assert.That(line.Key, Is.EqualTo("line_0"));
                Assert.That(line.Text, Is.EqualTo("Polski 0"));
                Assert.That(line.Clip, Is.SameAs(clip));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void TryPick_MissingTranslationAndClip_FallsBackToCatalogTextWithoutClip()
        {
            var lines = new CannedLines(Catalog(LineCategory.Offline, 1), new TableLocalizer(), new MapClipSource(), new FakeRandom());

            lines.TryPick(LineCategory.Offline, out var line);

            Assert.That(line.Text, Is.EqualTo("English 0"));
            Assert.That(line.Clip, Is.Null);
        }

        [Test]
        public void TryPick_NeverRepeatsAnyOfTheLastFivePicks()
        {
            var lines = new CannedLines(Catalog(LineCategory.Idle, 7), new TableLocalizer(), new MapClipSource(), new FakeRandom(42));
            var history = new List<string>();

            for (var i = 0; i < 200; i++)
            {
                lines.TryPick(LineCategory.Idle, out var line);
                var recent = history.GetRange(Math.Max(0, history.Count - 5), Math.Min(5, history.Count));
                Assert.That(recent, Has.No.Member(line.Key), $"pick {i}");
                history.Add(line.Key);
            }
        }

        [Test]
        public void TryPick_FewerLinesThanHistory_StillNeverRepeatsTheLastPick()
        {
            var lines = new CannedLines(Catalog(LineCategory.Error, 2), new TableLocalizer(), new MapClipSource(), new FakeRandom(7));
            string previous = null;

            for (var i = 0; i < 20; i++)
            {
                lines.TryPick(LineCategory.Error, out var line);
                Assert.That(line.Key, Is.Not.EqualTo(previous));
                previous = line.Key;
            }
        }

        [Test]
        public void TryPick_SingleLine_IsReturnedEveryTime()
        {
            var lines = new CannedLines(Catalog(LineCategory.Greeting, 1), new TableLocalizer(), new MapClipSource(), new FakeRandom());

            lines.TryPick(LineCategory.Greeting, out var first);
            lines.TryPick(LineCategory.Greeting, out var second);

            Assert.That(second.Key, Is.EqualTo(first.Key));
        }

        [Test]
        public void EnglishCatalog_KeysAreUniqueAndTextFitsOneTtsRequest()
        {
            var keys = new HashSet<string>();
            var categories = new HashSet<LineCategory>();

            foreach (var entry in CannedLineCatalog.English)
            {
                Assert.That(keys.Add(entry.Key), Is.True, entry.Key);
                Assert.That(entry.Text.Length, Is.InRange(1, SentenceSplitter.MaxLength), entry.Key);
                categories.Add(entry.Category);
            }

            foreach (LineCategory category in Enum.GetValues(typeof(LineCategory)))
                Assert.That(categories, Has.Member(category), category.ToString());
        }
    }
}
