using System;
using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Hands
{
    /// <summary>
    /// Shows each hand mesh only while its hand has been tracked for <see cref="TrackingDebounce.DefaultHoldSeconds"/>
    /// (the XR Hands mesh controllers' own show/hide on every tracking change is turned off), and logs the hand pipeline's
    /// state so a device run proves it without anyone wearing the headset (ADR-0030): <c>evt=hands.subsystem</c> (an
    /// XRHandSubsystem is running), <c>evt=hands.tracked</c> (the debounced tracked state of each hand) and
    /// <c>evt=hands.visual</c> (a hand mesh is rendering). Tracking is read every frame; the subsystem and the visuals are
    /// polled at 4 Hz. A line is written only on change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandTrackingMonitor : MonoBehaviour
    {
        const double PollSeconds = 0.25;
        static readonly List<XRHandSubsystem> Subsystems = new();

        readonly TrackingDebounce _left = new();
        readonly TrackingDebounce _right = new();
        ILog _log;
        IClock _clock;
        Func<Handedness, bool> _isTracked;
        XRHandMeshController[] _visuals;
        XRHandSubsystem _subsystem;
        double _nextPoll;
        int _running = -1;
        int _visible = -1;

        /// <param name="visualsRoot">Searched once, inactive children included, for the XR Hands mesh controllers.</param>
        public void Construct(ILog log, IClock clock, Transform visualsRoot)
        {
            _log = log;
            _clock = clock;
            _isTracked ??= SubsystemTracks;
            _visuals = visualsRoot.GetComponentsInChildren<XRHandMeshController>(true);
            foreach (var visual in _visuals)
            {
                visual.showMeshWhenTrackingIsAcquired = false;
                visual.hideMeshWhenTrackingIsLost = false;
            }
            _log.Write(LogLevel.Info, "xr", "evt=hands.monitor visuals=" + _visuals.Length);
        }

        /// <summary>Replaces the running XRHandSubsystem as the raw tracking source (no XR runtime runs in Editor tests).</summary>
        public void ReadTrackingFrom(Func<Handedness, bool> isTracked) => _isTracked = isTracked;

        void Update()
        {
            if (_log == null)
                return;
            var now = _clock.Now;
            var poll = now >= _nextPoll;
            if (poll)
            {
                _nextPoll = now + PollSeconds;
                PollSubsystem();
            }

            var changed = _left.Update(_isTracked(Handedness.Left), now);
            changed |= _right.Update(_isTracked(Handedness.Right), now);
            if (changed)
                _log.Write(LogLevel.Info, "xr", Line("evt=hands.tracked", Bits(_left.Stable, _right.Stable)));
            ShowTrackedHands();

            if (!poll)
                return;
            var visible = VisibleHands();
            if (visible != _visible)
            {
                _visible = visible;
                _log.Write(LogLevel.Info, "xr", Line("evt=hands.visual", visible));
            }
        }

        void PollSubsystem()
        {
            _subsystem = RunningSubsystem();
            var running = _subsystem != null ? 1 : 0;
            if (running == _running)
                return;
            _running = running;
            _log.Write(LogLevel.Info, "xr", running == 1
                ? "evt=hands.subsystem state=running id=" + _subsystem.subsystemDescriptor.id
                : "evt=hands.subsystem state=stopped");
        }

        bool SubsystemTracks(Handedness side) =>
            _subsystem != null && _subsystem.running &&
            (side == Handedness.Left ? _subsystem.leftHand.isTracked : _subsystem.rightHand.isTracked);

        /// <summary>A mesh controller the hand visualizer switched off (meshes not drawn) is left alone.</summary>
        void ShowTrackedHands()
        {
            foreach (var visual in _visuals)
            {
                if (visual == null || !visual.isActiveAndEnabled || visual.handMeshRenderer == null
                    || visual.handTrackingEvents == null)
                    continue;
                var tracked = visual.handTrackingEvents.handedness == Handedness.Left ? _left.Stable : _right.Stable;
                if (visual.handMeshRenderer.enabled != tracked)
                    visual.handMeshRenderer.enabled = tracked;
            }
        }

        int VisibleHands()
        {
            var bits = 0;
            foreach (var visual in _visuals)
            {
                if (visual == null || !visual.isActiveAndEnabled)
                    continue;
                var mesh = visual.handMeshRenderer;
                if (mesh == null || !mesh.enabled || !mesh.gameObject.activeInHierarchy || visual.handTrackingEvents == null)
                    continue;
                bits |= visual.handTrackingEvents.handedness == Handedness.Left ? 1 : 2;
            }
            return bits;
        }

        static XRHandSubsystem RunningSubsystem()
        {
            SubsystemManager.GetSubsystems(Subsystems);
            XRHandSubsystem found = null;
            foreach (var candidate in Subsystems)
            {
                if (candidate.running)
                {
                    found = candidate;
                    break;
                }
            }
            Subsystems.Clear();
            return found;
        }

        static int Bits(bool left, bool right) => (left ? 1 : 0) | (right ? 2 : 0);

        static string Line(string evt, int bits) =>
            evt + " left=" + ((bits & 1) != 0 ? "true" : "false") + " right=" + ((bits & 2) != 0 ? "true" : "false");
    }
}
