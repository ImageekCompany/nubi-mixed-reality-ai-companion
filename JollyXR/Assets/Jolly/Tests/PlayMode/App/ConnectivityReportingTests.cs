using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.App.Adapters;
using Jolly.Contracts;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class ConnectivityReportingTests
    {
        sealed class RecordingMonitor : IConnectivityMonitor
        {
            public readonly List<string> Reports = new();
            public Connectivity State => Connectivity.Online;
            public event Action<Connectivity> Changed { add { } remove { } }
            public void ReportSuccess(string provider) => Reports.Add("ok " + provider);
            public void ReportFailure(string provider, ProviderErrorKind kind) => Reports.Add($"fail {provider} {kind}");
            public UniTask<Connectivity> ProbeNow(CancellationToken ct) => UniTask.FromResult(State);
            public void Dispose() { }
        }

        [Test]
        public void Stream_ConsumerStoppingAtTheFirstItem_StillReportsSuccess()
        {
            var monitor = new RecordingMonitor();
            var endless = UniTaskAsyncEnumerable.Create<int>(async (writer, ct) =>
            {
                await writer.YieldAsync(1);
                await UniTask.Never(ct);
            });

            var first = ConnectivityReporting.Observe(endless, "asr", monitor).FirstAsync();

            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(monitor.Reports, Is.EqualTo(new[] { "ok asr" }));
        }

        [Test]
        public void Stream_FailingBeforeAnyItem_ReportsOnlyTheFailure()
        {
            var monitor = new RecordingMonitor();
            var failing = UniTaskAsyncEnumerable.Throw<int>(new ProviderException("asr", ProviderErrorKind.Network, "closed"));

            var all = ConnectivityReporting.Observe(failing, "asr", monitor).ToArrayAsync();

            Assert.That(all.Status, Is.EqualTo(UniTaskStatus.Faulted));
            Assert.Throws<ProviderException>(() => all.GetAwaiter().GetResult());
            Assert.That(monitor.Reports, Is.EqualTo(new[] { "fail asr Network" }));
        }
    }
}
