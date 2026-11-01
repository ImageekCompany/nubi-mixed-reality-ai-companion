using System;
using System.Collections.Generic;
using System.IO;
using Jolly.Contracts;
using Jolly.Core.Usage;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Usage
{
    public sealed class ApiUsageMeterTests
    {
        const string Spark = "muse-spark-1.3";

        string _dir;
        string _path;
        FakeClock _clock;
        readonly List<IDisposable> _meters = new();

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "jolly-usage-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "usage.json");
            _clock = new FakeClock();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var m in _meters)
                m.Dispose();
            _meters.Clear();
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Test]
        public void Record_AddsTheCostOfTheActualUsageAndRaisesSpentChanged()
        {
            var meter = Meter();
            decimal? raised = null;
            meter.SpentChanged += spent => raised = spent;
            meter.TryReserve(0.01m, out var id);

            var cost = meter.Record(id, new UsageRecord(Spark, 1000, 200, 500));

            Assert.That(cost, Is.EqualTo(0.003155m));
            Assert.That(meter.SpentUsd, Is.EqualTo(0.003155m));
            Assert.That(raised, Is.EqualTo(0.003155m));
        }

        [Test]
        public void TryReserve_CountsOpenReservationsAgainstTheCap()
        {
            var meter = Meter(cap: 0.05m);

            var first = meter.TryReserve(0.03m, out var firstId);
            var second = meter.TryReserve(0.03m, out _);

            Assert.That(first, Is.True);
            Assert.That(firstId, Is.Not.Zero);
            Assert.That(second, Is.False);
        }

        [Test]
        public void Record_ReleasesItsReservation()
        {
            var meter = Meter(cap: 0.05m);
            meter.TryReserve(0.03m, out var id);

            meter.Record(id, new UsageRecord(Spark, 1000, 0, 0));

            Assert.That(meter.TryReserve(0.03m, out _), Is.True);
        }

        [Test]
        public void RecordEstimate_PricesTheEstimatedTokensAndReleasesTheReservation()
        {
            var meter = Meter(cap: 0.05m);
            meter.TryReserve(0.03m, out var id);

            var cost = meter.RecordEstimate(id, Spark, estInputTokens: 2000, estOutputTokens: 100);

            Assert.That(cost, Is.EqualTo((2000 * 1.25m + 100 * 4.25m) / 1_000_000m));
            Assert.That(meter.SpentUsd, Is.EqualTo(cost));
            Assert.That(meter.TryReserve(0.03m, out _), Is.True);
        }

        [Test]
        public void IsCapReached_OnceSpendReachesTheCap_AndThenNothingCanBeReserved()
        {
            var meter = Meter(cap: 0.001m);
            meter.TryReserve(0m, out var id);

            meter.Record(id, new UsageRecord(Spark, 1000, 0, 0));

            Assert.That(meter.IsCapReached, Is.True);
            Assert.That(meter.TryReserve(0m, out _), Is.False);
        }

        [Test]
        public void TryReserve_NegativeEstimate_Throws()
        {
            Assert.That(() => Meter().TryReserve(-1m, out _), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Record_PersistsOnlyAfterTheTwoSecondDebounce()
        {
            var meter = Meter();
            meter.TryReserve(0.01m, out var id);
            meter.Record(id, new UsageRecord(Spark, 1000, 0, 0));

            _clock.Advance(TimeSpan.FromSeconds(1.9));
            var early = File.Exists(_path);
            _clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.That(early, Is.False);
            Assert.That(File.Exists(_path), Is.True);
            Assert.That(Meter().SpentUsd, Is.EqualTo(meter.SpentUsd));
        }

        [Test]
        public void Flush_WritesImmediatelyAndAReloadedMeterKeepsSpendAndPerModelTotals()
        {
            var meter = Meter();
            meter.TryReserve(0.01m, out var a);
            meter.Record(a, new UsageRecord(Spark, 1000, 100, 50));
            meter.TryReserve(0.01m, out var b);
            meter.RecordEstimate(b, Spark, 400, 10);

            meter.Flush();
            var reloaded = Meter();

            Assert.That(reloaded.SpentUsd, Is.EqualTo(meter.SpentUsd));
            var totals = reloaded.TotalsFor(Spark);
            Assert.That(totals.InputTokens, Is.EqualTo(1400));
            Assert.That(totals.CachedInputTokens, Is.EqualTo(100));
            Assert.That(totals.OutputTokens, Is.EqualTo(60));
            Assert.That(totals.Requests, Is.EqualTo(2));
            Assert.That(totals.EstimatedRequests, Is.EqualTo(1));
        }

        [Test]
        public void Flush_OverwritesAnExistingFile()
        {
            var meter = Meter();
            meter.TryReserve(0.01m, out var a);
            meter.Record(a, new UsageRecord(Spark, 1000, 0, 0));
            meter.Flush();
            meter.TryReserve(0.01m, out var b);
            meter.Record(b, new UsageRecord(Spark, 1000, 0, 0));

            meter.Flush();

            Assert.That(Meter().SpentUsd, Is.EqualTo(2 * 1000 * 1.25m / 1_000_000m));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
        }

        [Test]
        public void Dispose_FlushesPendingState()
        {
            var meter = new ApiUsageMeter(PriceTableTests.MetaPrices(), _path, 1m, _clock);
            meter.TryReserve(0.01m, out var id);
            meter.Record(id, new UsageRecord(Spark, 1000, 0, 0));

            meter.Dispose();

            Assert.That(Meter().SpentUsd, Is.EqualTo(1000 * 1.25m / 1_000_000m));
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void Constructor_CorruptFile_StartsFromZero()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "{not json");

            Assert.That(Meter().SpentUsd, Is.Zero);
        }

        ApiUsageMeter Meter(decimal cap = 1m)
        {
            var meter = new ApiUsageMeter(PriceTableTests.MetaPrices(), _path, cap, _clock);
            _meters.Add(meter);
            return meter;
        }
    }
}
