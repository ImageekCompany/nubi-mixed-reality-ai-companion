using System;
using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Usage;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Usage
{
    public sealed class PriceTableTests
    {
        public static PriceTable MetaPrices() => new(new Dictionary<string, ModelPrice>
        {
            ["muse-spark-1.3"] = new(inputPerMillion: 1.25m, cachedInputPerMillion: 0.15m, outputPerMillion: 4.25m,
                perWebSearch: 0.0025m),
            ["muse-spark-1.3-contributor"] = new(inputPerMillion: 0.10m, cachedInputPerMillion: 0.002m, outputPerMillion: 0.20m),
            ["sam-3.1"] = new(0m, 0m, 0m, perImage: 0.0025m),
            ["muse-voice-transcribe-1.0"] = new(0m, 0m, 0m, perAudioHour: 0.18m),
        });

        [Test]
        public void Cost_Tokens_BillsUncachedCachedAndOutputSeparately()
        {
            var cost = MetaPrices().Cost(new UsageRecord("muse-spark-1.3", inputTokens: 1000, cachedInputTokens: 200, outputTokens: 500));

            Assert.That(cost, Is.EqualTo((800 * 1.25m + 200 * 0.15m + 500 * 4.25m) / 1_000_000m));
        }

        [Test]
        public void Cost_SamImages_ArePricedPerImage()
        {
            var cost = MetaPrices().Cost(new UsageRecord("sam-3.1", 0, 0, 0, images: 2));

            Assert.That(cost, Is.EqualTo(0.005m));
        }

        [Test]
        public void Cost_WebSearches_ArePricedPerSearchOnTopOfTheTokens()
        {
            // Act
            var cost = MetaPrices().Cost(new UsageRecord("muse-spark-1.3", 1000, 0, 0, webSearches: 2));

            // Assert
            Assert.That(cost, Is.EqualTo(1000 * 1.25m / 1_000_000m + 2 * 0.0025m));
        }

        [Test]
        public void Cost_AsrAudio_IsPricedPerHour()
        {
            var cost = MetaPrices().Cost(new UsageRecord("muse-voice-transcribe-1.0", 0, 0, 0, audioSeconds: 1800));

            Assert.That(cost, Is.EqualTo(0.09m));
        }

        [Test]
        public void Cost_UnknownModel_UsesTheMostExpensiveKnownRateOfEachKind()
        {
            var prices = MetaPrices();

            var cost = prices.Cost(new UsageRecord("muse-wonder-1.0", 1_000_000, 0, 1_000_000, images: 1, audioSeconds: 3600));

            Assert.That(cost, Is.EqualTo(1.25m + 4.25m + 0.0025m + 0.18m));
        }

        [Test]
        public void Constructor_RejectsAnEmptyTable()
        {
            Assert.That(() => new PriceTable(new Dictionary<string, ModelPrice>()), Throws.TypeOf<ArgumentException>());
        }
    }
}
