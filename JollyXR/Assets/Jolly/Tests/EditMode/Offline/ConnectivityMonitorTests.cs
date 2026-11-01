using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Connectivity;
using Jolly.Testing;
using Jolly.Testing.Offline;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Offline
{
    public sealed class ConnectivityMonitorTests
    {
        FakeClock _clock;
        ScriptedHealthProbe _probe;
        InMemorySecrets _secrets;
        StubUsageMeter _meter;
        bool _reachable;
        List<Connectivity> _changes;
        ConnectivityMonitor _monitor;
        CancellationTokenSource _cts;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _probe = new ScriptedHealthProbe();
            _secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "key-a");
            _meter = new StubUsageMeter();
            _reachable = true;
            _changes = new List<Connectivity>();
            _cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cts.Cancel();
            _cts.Dispose();
            _monitor?.Dispose();
        }

        ConnectivityMonitor Create()
        {
            _monitor = new ConnectivityMonitor(_probe, _secrets, _meter, () => _reachable, _clock, new RecordingLog());
            _monitor.Changed += _changes.Add;
            return _monitor;
        }

        void Start() => _monitor.RunAsync(_cts.Token).Forget();

        void Advance(double seconds) => _clock.Advance(TimeSpan.FromSeconds(seconds));

        [Test]
        public void State_WithKeyAndBudget_StartsOnline()
        {
            Create();

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
        }

        [Test]
        public void State_WithoutKey_IsUnauthorizedAndNeverProbes()
        {
            _secrets = new InMemorySecrets();
            Create();

            Start();
            Advance(60);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Unauthorized));
            Assert.That(_probe.Calls, Is.Zero);
        }

        [Test]
        public void ReportFailure_OnceWhileOnline_GoesDegraded()
        {
            Create();

            _monitor.ReportFailure("meta", ProviderErrorKind.Network);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Degraded));
            Assert.That(_changes, Is.EqualTo(new[] { Connectivity.Degraded }));
        }

        [Test]
        public void ReportFailure_TwiceInARow_GoesOffline()
        {
            Create();

            _monitor.ReportFailure("meta", ProviderErrorKind.Timeout);
            _monitor.ReportFailure("wit", ProviderErrorKind.Server);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Offline));
        }

        [Test]
        public void ReportSuccess_WhileDegraded_ReturnsOnlineAndResetsTheFailureCount()
        {
            Create();
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);

            _monitor.ReportSuccess("meta");
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Degraded));
        }

        [TestCase(ProviderErrorKind.RateLimited)]
        [TestCase(ProviderErrorKind.BadRequest)]
        [TestCase(ProviderErrorKind.Protocol)]
        public void ReportFailure_NotAboutConnectivity_KeepsOnline(ProviderErrorKind kind)
        {
            Create();

            _monitor.ReportFailure("meta", kind);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void ReportFailure_WhenTheNetworkIsUnreachable_GoesOfflineAtOnce()
        {
            Create();
            _reachable = false;

            _monitor.ReportFailure("meta", ProviderErrorKind.Network);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Offline));
        }

        [Test]
        public void Degraded_ProbeFailsAtTheNextPoll_GoesOffline()
        {
            Create();
            Start();
            _probe.Result = false;

            _monitor.ReportFailure("meta", ProviderErrorKind.Network);
            Advance(1);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Offline));
        }

        [Test]
        public void Degraded_ProbeSucceedsAtTheNextPoll_GoesOnline()
        {
            Create();
            Start();
            var callsAtStart = _probe.Calls;

            _monitor.ReportFailure("meta", ProviderErrorKind.Network);
            Advance(1);

            Assert.That(_probe.Calls, Is.EqualTo(callsAtStart + 1));
            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
        }

        [Test]
        public void Offline_ProbesWithBackoffFrom5To30Seconds()
        {
            _probe.Result = false;
            Create();
            var probeTimes = new List<double>();
            var lastCalls = 0;

            Start();
            for (var second = 0; second <= 100; second++)
            {
                if (_probe.Calls != lastCalls)
                {
                    probeTimes.Add(_clock.Now);
                    lastCalls = _probe.Calls;
                }
                Advance(1);
            }

            Assert.That(probeTimes, Is.EqualTo(new double[] { 0, 5, 15, 35, 65, 95 }));
            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Offline));
        }

        [Test]
        public void Offline_ProbeSucceeds_GoesOnlineAndResetsTheBackoff()
        {
            _probe.Result = false;
            Create();
            Start();
            Advance(15);

            _probe.Result = true;
            Advance(20);
            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));

            _probe.Result = false;
            var calls = _probe.Calls;
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);
            Advance(1);
            Advance(5);
            Assert.That(_probe.Calls, Is.EqualTo(calls + 2), "first probe right away, next one after 5 s");
        }

        [Test]
        public void Unreachable_GoesOfflineWithinOnePollAndDoesNotProbe()
        {
            Create();
            Start();
            var calls = _probe.Calls;

            _reachable = false;
            Advance(1);
            Advance(30);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Offline));
            Assert.That(_probe.Calls, Is.EqualTo(calls));
        }

        [Test]
        public void Unreachable_ThenReachable_ProbesAtTheNextPollAndGoesOnline()
        {
            Create();
            Start();
            _reachable = false;
            Advance(10);

            _reachable = true;
            Advance(1);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
            Assert.That(_changes, Is.EqualTo(new[] { Connectivity.Offline, Connectivity.Online }));
        }

        [Test]
        public void ReportFailure_Unauthorized_DisablesTheProbeUntilTheKeyChanges()
        {
            Create();
            Start();
            var calls = _probe.Calls;

            _monitor.ReportFailure("meta", ProviderErrorKind.Unauthorized);
            Advance(60);
            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Unauthorized));
            Assert.That(_probe.Calls, Is.EqualTo(calls));

            _secrets.With(SecretKeys.MetaApiKey, "key-b");
            Advance(1);
            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
        }

        [Test]
        public void ReportSuccess_AfterUnauthorized_ClearsIt()
        {
            Create();
            _monitor.ReportFailure("meta", ProviderErrorKind.Unauthorized);

            _monitor.ReportSuccess("meta");

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
        }

        [Test]
        public void ReportFailure_UnauthorizedFromAProviderWithoutTheMetaKey_KeepsOnlineAndProbing()
        {
            Create();
            Start();
            _probe.Result = false;
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);
            var calls = _probe.Calls;

            _monitor.ReportFailure("wit", ProviderErrorKind.Unauthorized);
            Advance(1);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Offline));
            Assert.That(_probe.Calls, Is.EqualTo(calls + 1));
        }

        [TestCase("meta")]
        [TestCase("meta.asr")]
        public void ReportFailure_UnauthorizedFromAMetaProvider_GoesUnauthorized(string provider)
        {
            Create();

            _monitor.ReportFailure(provider, ProviderErrorKind.Unauthorized);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Unauthorized));
        }

        [Test]
        public void ReportSuccess_FromAProviderWithoutTheMetaKey_KeepsUnauthorized()
        {
            Create();
            _monitor.ReportFailure("meta", ProviderErrorKind.Unauthorized);

            _monitor.ReportSuccess("wit");

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Unauthorized));
        }

        [Test]
        public void ProbeFailsAfterAReportedSuccess_KeepsOnline()
        {
            Create();
            Start();
            _probe.Hold = true;
            _monitor.ReportFailure("meta", ProviderErrorKind.Server);
            Advance(1);
            var calls = _probe.Calls;

            _monitor.ReportSuccess("meta");
            _probe.Resolve(false);

            Assert.That(calls, Is.EqualTo(2), "start-up probe plus the one at the next poll");
            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
        }

        [Test]
        public void ProbeNow_WhileAProbeIsInFlight_SharesItsResult()
        {
            Create();
            _probe.Hold = true;
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);

            var first = _monitor.ProbeNow(CancellationToken.None);
            var second = _monitor.ProbeNow(CancellationToken.None);
            _probe.Resolve(true);

            Assert.That(_probe.Calls, Is.EqualTo(1));
            Assert.That(first.GetAwaiter().GetResult(), Is.EqualTo(Connectivity.Online));
            Assert.That(second.GetAwaiter().GetResult(), Is.EqualTo(Connectivity.Online));
        }

        [Test]
        public void Offline_WithTheCapReached_NeverProbes()
        {
            _probe.Result = false;
            Create();
            Start();
            var calls = _probe.Calls;

            _meter.ReachCap();
            Advance(120);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.BudgetReached));
            Assert.That(_probe.Calls, Is.EqualTo(calls));
        }

        [Test]
        public void BudgetCapReached_GoesBudgetReached()
        {
            Create();

            _meter.ReachCap();

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.BudgetReached));
            Assert.That(_changes, Is.EqualTo(new[] { Connectivity.BudgetReached }));
        }

        [Test]
        public void Unauthorized_OutranksBudgetReachedAndOffline()
        {
            Create();
            _meter.ReachCap();
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);

            _monitor.ReportFailure("meta", ProviderErrorKind.Unauthorized);

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Unauthorized));
        }

        [Test]
        public void BudgetReached_OutranksOffline()
        {
            Create();
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);

            _meter.ReachCap();

            Assert.That(_monitor.State, Is.EqualTo(Connectivity.BudgetReached));
        }

        [Test]
        public void ProbeNow_ReturnsTheStateAfterTheProbe()
        {
            Create();
            _monitor.ReportFailure("meta", ProviderErrorKind.Network);
            _probe.Result = false;

            var probing = _monitor.ProbeNow(CancellationToken.None);

            Assert.That(probing.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(probing.GetAwaiter().GetResult(), Is.EqualTo(Connectivity.Offline));
        }

        [Test]
        public void ProbeNow_WhenCancelled_ThrowsAndKeepsTheState()
        {
            Create();
            _probe.Hold = true;
            using var cts = new CancellationTokenSource();

            var probing = _monitor.ProbeNow(cts.Token);
            cts.Cancel();

            Assert.That(probing.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_monitor.State, Is.EqualTo(Connectivity.Online));
        }

        [Test]
        public void RunAsyncAndProbeNow_AfterDispose_CompleteAsCancelled()
        {
            Create();
            _monitor.Dispose();

            Assert.That(_monitor.RunAsync(_cts.Token).Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_monitor.ProbeNow(CancellationToken.None).Status, Is.EqualTo(UniTaskStatus.Canceled));
        }

        [Test]
        public void Dispose_StopsTheLoopAndUnsubscribesFromTheMeter()
        {
            Create();
            var run = _monitor.RunAsync(_cts.Token);

            _monitor.Dispose();
            var calls = _probe.Calls;
            Advance(60);

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_meter.HasSubscribers, Is.False);
            Assert.That(_probe.Calls, Is.EqualTo(calls));
        }
    }
}
