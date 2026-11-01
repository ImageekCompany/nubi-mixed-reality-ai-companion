using Jolly.Core.Diagnostics;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Diagnostics
{
    public sealed class LatencySeriesTests
    {
        static LatencySeries Of(params double[] values)
        {
            var series = new LatencySeries();
            foreach (var v in values)
                series.Add(v);
            return series;
        }

        [Test]
        public void Percentile_TwentyTurns_UsesTheNearestRank()
        {
            // Arrange: 100, 200, ..., 2000 added out of order.
            var series = new LatencySeries();
            for (var i = 20; i >= 1; i--)
                series.Add(i * 100);

            // Act / Assert: p50 is the 10th of 20, p90 the 18th, max the 20th.
            Assert.That(series.Percentile(50), Is.EqualTo(1000));
            Assert.That(series.Percentile(90), Is.EqualTo(1800));
            Assert.That(series.Max, Is.EqualTo(2000));
            Assert.That(series.Count, Is.EqualTo(20));
        }

        [Test]
        public void Percentile_OddCount_IsAnObservedValue()
        {
            var series = Of(1776, 942, 3578);

            Assert.That(series.Percentile(50), Is.EqualTo(1776));
            Assert.That(series.Percentile(90), Is.EqualTo(3578));
            Assert.That(series.Percentile(0), Is.EqualTo(942));
        }

        [Test]
        public void Add_NaN_IsSkipped()
        {
            var series = Of(double.NaN, 500, double.NaN);

            Assert.That(series.Count, Is.EqualTo(1));
            Assert.That(series.Percentile(50), Is.EqualTo(500));
        }

        [Test]
        public void Percentile_Empty_IsNaN()
        {
            var series = Of(double.NaN);

            Assert.That(double.IsNaN(series.Percentile(50)), Is.True);
            Assert.That(double.IsNaN(series.Max), Is.True);
        }

        [Test]
        public void Summary_ListsCountPercentilesAndMaxInWholeMilliseconds()
        {
            var series = Of(1800.4, 2100.6, 1500, 900);

            Assert.That(series.Summary(), Is.EqualTo("n=4 p50=1500 p90=2101 max=2101"));
        }

        [Test]
        public void Summary_Empty_SaysSo()
        {
            Assert.That(new LatencySeries().Summary(), Is.EqualTo("n=0"));
        }
    }
}
