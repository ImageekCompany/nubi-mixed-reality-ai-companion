using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// The room-scan flow (ADR-0025). Once USE_SCENE is granted and no floor plane appears within the timeout, the room
    /// counts as unscanned: Nubi walks on a small fallback area around the user and itself (logged as degraded), offers
    /// Space Setup once per session, and opens it when asked, after the current reply has been spoken. When the app
    /// resumes, the scene is queried again and the timeout restarts. Polled on the main thread; no per-tick allocation.
    /// </summary>
    public sealed class RoomScanMonitor : IRoomScan
    {
        const string LogCategory = "room";
        /// <summary>Longer than Nubi's pop (0.4 s): until then its reported position is still the old one.</summary>
        const double ReturnSeconds = 1.0;

        readonly ISceneData _scene;
        readonly IUserPoseProvider _user;
        readonly ICharacterController _character;
        readonly IClock _clock;
        readonly ILog _log;
        readonly RoomSettings _settings;
        readonly Func<bool> _canSpeak;
        readonly RoomScanDetector _detector;
        readonly FallbackFloorArea _fallback;
        bool _offered;
        bool _scanQueued;
        double _scanQueuedAt;
        bool _fallbackRefused;
        Vector3 _returnTarget;
        double _returnUntil = double.NegativeInfinity;

        /// <param name="canSpeak">True while the conversation is Idle and nothing plays.</param>
        public RoomScanMonitor(ISceneData scene, IUserPoseProvider user, ICharacterController character, IClock clock,
                               ILog log, RoomSettings settings, Func<bool> canSpeak)
        {
            _scene = scene;
            _user = user;
            _character = character;
            _clock = clock;
            _log = log;
            _settings = settings;
            _canSpeak = canSpeak;
            _detector = new RoomScanDetector(settings.ScanTimeout);
            _fallback = new FallbackFloorArea(settings.FallbackRadius, settings.FallbackCharacterRadius,
                settings.FallbackSegments, settings.FallbackRecenterDistance, settings.FallbackMaxSpan);
        }

        public RoomScanStatus Status { get; private set; } = RoomScanStatus.Pending;
        public bool CanRequestScan => _scene.CanRequestCapture;
        /// <summary>Raised at most once per session, when the room turns out unscanned and Nubi is idle and silent.</summary>
        public event Action OfferScan;

        public bool RequestScan()
        {
            if (!CanRequestScan)
                return false;
            _scanQueued = true;
            _scanQueuedAt = _clock.Now;
            _log.Write(LogLevel.Info, LogCategory, "evt=room.scan_queued");
            return true;
        }

        /// <summary>Call when the app regains focus: back from Space Setup, or from the system settings.</summary>
        public void OnResumed()
        {
            if (Status == RoomScanStatus.Scanned)
                return;
            _scene.Requery();
            _detector.Restart(_clock.Now);
            _log.Write(LogLevel.Info, LogCategory, "evt=room.requery status=" + Status);
        }

        /// <summary>Runs until ct is cancelled, then completes as cancelled.</summary>
        public async UniTask RunAsync(CancellationToken ct)
        {
            while (true)
            {
                Tick();
                await _clock.Delay(_settings.PollInterval, ct);
            }
        }

        public void Tick()
        {
            var now = _clock.Now;
            SetStatus(_detector.Update(now, _scene.PermissionGranted, _scene.HasScannedFloor));
            UpdateFallback();

            if (Status == RoomScanStatus.Unscanned && _settings.OfferScan && CanRequestScan && !_offered && _canSpeak())
            {
                _offered = true;
                OfferScan?.Invoke();
            }
            if (_scanQueued && (_canSpeak() || now - _scanQueuedAt >= _settings.ScanRequestMaxWait.TotalSeconds))
            {
                _scanQueued = false;
                var accepted = _scene.TryRequestCapture();
                _log.Write(accepted ? LogLevel.Info : LogLevel.Warning, LogCategory,
                    "evt=room.scan_requested accepted=" + (accepted ? "yes" : "no"));
            }
        }

        void SetStatus(RoomScanStatus status)
        {
            if (status == Status)
                return;
            Status = status;
            _log.Write(status == RoomScanStatus.Unscanned ? LogLevel.Warning : LogLevel.Info, LogCategory,
                "evt=room.status status=" + status);
        }

        void UpdateFallback()
        {
            // Pending after a resume keeps the disc, so walking does not vanish while a new scan is being read.
            var wanted = _settings.FallbackFloor && !_fallbackRefused
                         && (Status == RoomScanStatus.Unscanned || (Status == RoomScanStatus.Pending && _fallback.IsPlaced));
            if (!wanted)
            {
                if (_fallback.IsPlaced)
                {
                    _fallback.Clear();
                    _scene.ClearFallbackFloor();
                    _log.Write(LogLevel.Info, LogCategory, "evt=room.fallback_floor_cleared status=" + Status);
                }
                return;
            }

            var user = _user.Head.position;
            var character = _clock.Now < _returnUntil ? _returnTarget : _character.Position;
            if (!_fallback.NeedsPlacement(user, character))
                return;
            var first = !_fallback.IsPlaced;
            var reachable = _fallback.Place(user, character);
            if (!_scene.SetFallbackFloor(_fallback.Center, _fallback.Outline))
            {
                _fallback.Clear();
                _fallbackRefused = true;
                _log.Write(LogLevel.Warning, LogCategory, "evt=room.fallback_floor_unavailable walkable=none");
                return;
            }
            if (first)
                _log.Write(LogLevel.Warning, LogCategory,
                    "evt=room.degraded walkable=fallback radius_m=" +
                    _settings.FallbackRadius.ToString("0.0#", CultureInfo.InvariantCulture));
            else if (_log.IsEnabled(LogLevel.Debug))
                _log.Write(LogLevel.Debug, LogCategory, "evt=room.fallback_floor_moved");
            if (!reachable)
                ReturnCharacter(character);
        }

        /// <summary>Nubi was left farther away than the area stretches. A visible pop back beside the user beats both
        /// alternatives: an invisible warp by EnsureOnNavMesh reads as a glitch, and a long walk over unscanned floor
        /// goes through whatever furniture is in the way.</summary>
        void ReturnCharacter(Vector3 character)
        {
            var target = _fallback.ReturnPoint(character);
            target.y = character.y;
            _character.Relocate(target);
            _returnTarget = target;
            _returnUntil = _clock.Now + ReturnSeconds;
            _log.Write(LogLevel.Info, LogCategory, "evt=room.character_returned from_m=" +
                Vector3.Distance(new Vector3(character.x, 0f, character.z), _fallback.Center)
                    .ToString("0.0", CultureInfo.InvariantCulture));
        }
    }
}
