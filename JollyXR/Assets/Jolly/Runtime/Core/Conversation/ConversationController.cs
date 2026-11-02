using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// The thin conversation loop (architecture §3, §5.2, §6): maps PTT presses to routed turns, barges in on a busy turn,
    /// owns <c>sessionCts</c> (renewed on a persona switch or locale change) and <c>turnCts</c> (cancelled when its turn
    /// ends), runs the state watchdogs, keeps memory after each turn, applies a pending memory clear or persona switch
    /// once the conversation is Idle with no turn running, and gives the persona's safety notice at startup. Main thread
    /// only.
    /// </summary>
    public sealed class ConversationController : IDisposable
    {
        readonly IVoiceActivation _voice;
        readonly TurnRouter _router;
        readonly TurnPipeline _pipeline;
        readonly BargeInCoordinator _bargeIn;
        readonly ConversationStateMachine _machine;
        readonly PersonaSwitcher _personas;
        readonly MemoryKeeper _memory;
        readonly TurnWatchdog _watchdog;
        readonly ILocalizer _localizer;
        readonly ILog _log;
        CancellationToken _appCt;
        CancellationTokenSource _sessionCts;
        CancellationTokenSource _turnCts;
        UniTaskCompletionSource _released;
        int _turnId;
        bool _turnRunning;
        bool _switching;
        bool _clearPending;
        bool _disposed;

        public ConversationController(IVoiceActivation voice, TurnRouter router, TurnPipeline pipeline,
                                      BargeInCoordinator bargeIn, ConversationStateMachine machine, PersonaSwitcher personas,
                                      MemoryKeeper memory, ILocalizer localizer, IClock clock, ILog log,
                                      ConversationTimings timings)
        {
            _voice = voice;
            _router = router;
            _pipeline = pipeline;
            _bargeIn = bargeIn;
            _machine = machine;
            _personas = personas;
            _memory = memory;
            _localizer = localizer;
            _log = log;
            _watchdog = new TurnWatchdog(clock, timings, OnWatchdog);
        }

        /// <summary>Completes normally when <paramref name="appCt"/> is cancelled.</summary>
        public async UniTask RunAsync(CancellationToken appCt)
        {
            _appCt = appCt;
            try
            {
                _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(appCt);
                await _memory.Load(appCt);
                _voice.Pressed += OnPressed;
                _voice.Released += OnReleased;
                _machine.Changed += OnStateChanged;
                _personas.Requested += OnSwitchRequested;
                _localizer.Changed += OnLocaleChanged;
                if (!TryApplyPending())
                    Announce(null, _personas.SafetyNotice);
                var stopped = new UniTaskCompletionSource();
                using (appCt.Register(() => stopped.TrySetResult()))
                    await stopped.Task;
            }
            catch (OperationCanceledException) when (appCt.IsCancellationRequested)
            {
            }
            finally
            {
                Dispose();
            }
        }

        /// <summary>Cancels the running turn and silences it (watchdogs; the composition root on teardown).</summary>
        public void Interrupt() => _bargeIn.Interrupt(_turnCts);

        /// <summary>
        /// A pinch that may yet be a tap (ADR-0044) silences a busy turn at the press: a resting hand ray made waiting for
        /// the tap time cost barge-in about 0.4 s. The turn ends here, as a tap will open no other; a hold opens one from Idle.
        /// A line playing at Idle stops too, or a hold would lose its first word to it. A safety turn waits for the hold.
        /// </summary>
        public void InterruptBusyTurn()
        {
            var state = _machine.State;
            if (_switching || state == ConversationState.Listening)
                return;
            if (state == ConversationState.Idle)
            {
                if (_bargeIn.IsPlaying)
                    _bargeIn.Interrupt(_turnCts);
                return;
            }
            if (_pipeline.IsSafetyTurn(_turnId))
                return;
            _bargeIn.BargeIn(_turnCts, _turnId);
            _machine.Fire(ConversationTrigger.Watchdog);
        }

        /// <summary>
        /// Settings' Clear memory. Deferred like a persona switch: a running turn would commit itself into the emptied
        /// store and the keeper would write it back to disk.
        /// </summary>
        public void ClearMemory()
        {
            _clearPending = true;
            TryApplyPending();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _voice.Pressed -= OnPressed;
            _voice.Released -= OnReleased;
            _machine.Changed -= OnStateChanged;
            _personas.Requested -= OnSwitchRequested;
            _localizer.Changed -= OnLocaleChanged;
            _watchdog.Dispose();
            Interrupt();
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
        }

        void OnPressed()
        {
            var state = _machine.State;
            if (state == ConversationState.Listening || _switching)
                return;
            if (state != ConversationState.Idle)
                _bargeIn.BargeIn(_turnCts, _turnId);
            else if (_bargeIn.IsPlaying)
                _bargeIn.Interrupt(_turnCts); // an interaction line plays outside any turn and would be transcribed

            var route = _router.Route(_pipeline.CanListen);
            var cts = NewTurn();
            if (route == TurnRoute.Online)
            {
                _released = new UniTaskCompletionSource();
                _machine.Fire(ConversationTrigger.PttPressed);
                Run(_turnId, cts, _pipeline.RunOnline(_turnId, _released.Task, cts.Token)).Forget();
            }
            else
            {
                _machine.Fire(ConversationTrigger.OfflinePttPressed);
                Run(_turnId, cts, _pipeline.RunOffline(_turnId, TurnRouter.LineFor(route), cts.Token)).Forget();
            }
            // An offline press while Speaking stays in Speaking and raises no Changed; the new turn still gets a full limit.
            _watchdog.Enter(_machine.State);
        }

        void OnReleased()
        {
            if (_released == null)
                return;
            _released.TrySetResult();
            _released = null;
            if (_machine.State == ConversationState.Listening)
                _watchdog.Released();
        }

        void OnStateChanged(ConversationState from, ConversationState to)
        {
            _watchdog.Enter(to);
            if (to == ConversationState.Idle)
                TryApplyPending();
        }

        void OnWatchdog(ConversationState state)
        {
            _log.Write(LogLevel.Warning, "conversation", $"evt=watchdog state={state} turn={_turnId}");
            Abort();
        }

        void Abort()
        {
            Interrupt();
            _machine.Fire(ConversationTrigger.Watchdog);
        }

        /// <summary>The rest of a turn would be in the old language (architecture §5.2).</summary>
        void OnLocaleChanged()
        {
            if (_disposed)
                return;
            Abort();
            RenewSession();
        }

        void RenewSession()
        {
            _sessionCts.Cancel();
            _sessionCts.Dispose();
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(_appCt);
        }

        /// <summary>
        /// The source stays alive until its turn has finished, so late cleanup never meets a disposed token; it is then
        /// cancelled, which ends work that outlived the turn (a filler line started with Forget).
        /// </summary>
        CancellationTokenSource NewTurn()
        {
            _turnCts = CancellationTokenSource.CreateLinkedTokenSource(_sessionCts.Token);
            _turnId++;
            _turnRunning = true;
            return _turnCts;
        }

        async UniTaskVoid Run(int turnId, CancellationTokenSource cts, UniTask turn)
        {
            try
            {
                await turn;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Error, "conversation", $"evt=turn.crashed turn={turnId}", e);
                if (turnId == _turnId)
                    OnWatchdog(_machine.State);
            }
            finally
            {
                if (_turnCts == cts)
                    _turnCts = null;
                cts.Cancel();
                cts.Dispose();
            }
            if (turnId != _turnId || _disposed)
                return;
            _turnRunning = false;
            if (!TryApplyPending())
                await _memory.AfterTurn(_sessionCts.Token);
        }

        void OnSwitchRequested() => TryApplyPending();

        /// <summary>True when it cleared memory or started a switch, either of which replaces the keeper's save.</summary>
        bool TryApplyPending()
        {
            if (!CanApplyPending)
                return false;
            var cleared = TryApplyClear();
            if (_personas.PendingId == null)
                return cleared;
            ApplySwitch().Forget();
            return true;
        }

        bool TryApplyClear()
        {
            if (!_clearPending || !CanApplyPending)
                return false;
            _clearPending = false;
            _memory.Clear();
            return true;
        }

        bool CanApplyPending => !_disposed && !_switching && !_turnRunning && _sessionCts != null
                                && _machine.State == ConversationState.Idle;

        async UniTaskVoid ApplySwitch()
        {
            _switching = true;
            bool applied;
            try
            {
                RenewSession();
                await _memory.Save(_appCt);
                applied = await _personas.ApplyPending(_appCt);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Error, "conversation", "evt=persona.switch_crashed", e);
                return;
            }
            finally
            {
                _switching = false;
            }
            if (applied && !_disposed && _machine.State == ConversationState.Idle)
                Announce(_personas.Greeting, _personas.SafetyNotice);
            else
                TryApplyClear();
        }

        void Announce(string greeting, string notice)
        {
            if (string.IsNullOrEmpty(greeting) && string.IsNullOrEmpty(notice))
                return;
            var cts = NewTurn();
            var online = _router.CanSynthesize;
            Run(_turnId, cts, _pipeline.Announce(_turnId, greeting, notice, online, cts.Token)).Forget();
        }
    }
}
