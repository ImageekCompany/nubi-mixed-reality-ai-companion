using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// Point and tap to send the character (ADR-0044): a pinch tap with a target showing, or a controller trigger press
    /// on one, starts a walk there through the same <see cref="ICharacterController.TryStartMoveTo"/> as the locomotion
    /// tools, watching the user so it ends facing them (ADR-0041). It never touches the conversation: a tap while the
    /// character speaks moves it and the reply goes on. Logs <c>evt=move.request</c> and <c>evt=move.arrived</c> (or
    /// <c>evt=move.ended</c>). Main thread only.
    /// </summary>
    public sealed class PointToMove : IDisposable
    {
        const string LogCategory = "move";

        readonly TapHoldClassifier _pinch;
        readonly IPointerInput _pointer;
        readonly PointerTargeting _targeting;
        readonly MoveTargetResolver _resolver;
        readonly ICharacterController _character;
        readonly PointMoveSettings _settings;
        readonly IMoveMarker _marker;
        readonly IClock _clock;
        readonly ILog _log;
        readonly CancellationTokenSource _lifetime = new();
        bool _disposed;

        public PointToMove(TapHoldClassifier pinch, IPointerInput pointer, PointerTargeting targeting, MoveTargetResolver resolver,
                           ICharacterController character, PointMoveSettings settings, IMoveMarker marker, IClock clock, ILog log)
        {
            _pinch = pinch;
            _pointer = pointer;
            _targeting = targeting;
            _resolver = resolver;
            _character = character;
            _settings = settings;
            _marker = marker;
            _clock = clock;
            _log = log;
            _pinch.Tapped += OnTapped;
            _pinch.Pressed += _targeting.Suspend;
            _pinch.Released += _targeting.Resume;
            _pointer.TriggerPressed += OnTrigger;
        }

        /// <summary>A walk was started; the self-test's evidence.</summary>
        public event Action<MoveTarget, PointerSource> Moved;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pinch.Tapped -= OnTapped;
            _pinch.Pressed -= _targeting.Suspend;
            _pinch.Released -= _targeting.Resume;
            _pointer.TriggerPressed -= OnTrigger;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        void OnTapped()
        {
            if (_targeting.TryTake(out var target))
                Go(target, PointerSource.Hand);
        }

        void OnTrigger(Ray ray)
        {
            if (_resolver.TryResolve(ray, out var target))
                Go(target, PointerSource.Controller);
        }

        void Go(MoveTarget target, PointerSource source)
        {
            var from = _character.Position;
            var distance = new Vector2(target.Point.x - from.x, target.Point.z - from.z).magnitude;
            _character.LookAt(LookTarget.User);
            var walking = _character.TryStartMoveTo(target.Point, _settings.StopDistance, out var eta);
            _log.Write(walking ? LogLevel.Info : LogLevel.Warning, LogCategory,
                $"evt=move.request source={Name(source)} kind={target.Kind.ToString().ToLowerInvariant()}" +
                (target.SurfaceLabel != null ? " surface=" + target.SurfaceLabel : "") +
                $" dist_m={Format(distance)} status={(walking ? "walking" : "unreachable")}" +
                (walking ? " eta_s=" + Format(eta) : ""));
            if (!walking)
                return;
            _marker.Confirm(target.Point);
            Moved?.Invoke(target, source);
            LogArrival(target.Point, _clock.Now).Forget();
        }

        async UniTaskVoid LogArrival(Vector3 target, double startedAt)
        {
            MoveOutcome outcome;
            try
            {
                outcome = await _character.WhenArrived(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            var ms = (_clock.Now - startedAt) * 1000;
            if (outcome == MoveOutcome.Arrived)
            {
                var at = _character.Position;
                var error = new Vector2(target.x - at.x, target.z - at.z).magnitude;
                _log.Write(LogLevel.Info, LogCategory, $"evt=move.arrived ms={ms:0} error_m={Format(error)}");
            }
            else
            {
                _log.Write(LogLevel.Info, LogCategory, $"evt=move.ended outcome={outcome} ms={ms:0}");
            }
        }

        static string Name(PointerSource source) => source == PointerSource.Hand ? "hand" : "controller";

        static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
