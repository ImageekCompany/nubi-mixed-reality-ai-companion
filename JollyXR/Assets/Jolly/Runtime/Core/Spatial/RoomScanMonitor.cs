using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// The room-scan flow (ADR-0024). Once USE_SCENE is granted and no floor plane appears within the timeout, the room
    /// counts as unscanned: Nubi walks on a small fallback disc around the user (logged as degraded), offers Space Setup
    /// once per session, and opens it when asked, after the current reply has been spoken. When the app resumes, the
    /// scene is queried again and the timeout restarts. Polled on the main thread; no per-tick allocation.
    /// </summary>
    public sealed class RoomScanMonitor : IRoomScan
    {
        const string LogCategory = "room";

        readonly ISceneData _scene;
        readonly IUserPoseProvider _user;
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

        /// <param name="canSpeak">True while the conversation is Idle and nothing plays.</param>
        public RoomScanMonitor(ISceneData scene, IUserPoseProvider user, IClock clock, ILog log, RoomSettings settings,
                               Func<bool> canSpeak)
        {
            _scene = scene;
            _user = user;
            _clock = clock;
            _log = log;
            _settings = settings;
            _canSpeak = canSpeak;
            _detector = new RoomScanDetector(settings.ScanTimeout);
            _fallback = new FallbackFloorArea(settings.FallbackRadius, settings.FallbackSegments,
                settings.FallbackRecenterDistance);
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

            if (Status == RoomScanStatus.Unscanned && _settings.OfferScan && !_offered && _canSpeak())
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
            if (!_fallback.NeedsPlacement(user))
                return;
            var first = !_fallback.IsPlaced;
            _fallback.Place(user);
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
        }
    }
}
