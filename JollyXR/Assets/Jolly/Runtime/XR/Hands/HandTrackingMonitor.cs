using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Hands
{
    /// <summary>
    /// Logs the hand pipeline's state so a device run proves it without anyone wearing the headset (ADR-0030):
    /// <c>evt=hands.subsystem</c> (an XRHandSubsystem is running), <c>evt=hands.tracked</c> (the subsystem tracks each
    /// hand) and <c>evt=hands.visual</c> (a hand mesh is rendering). Polled at 4 Hz; a line is written only on change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandTrackingMonitor : MonoBehaviour
    {
        const double PollSeconds = 0.25;
        static readonly List<XRHandSubsystem> Subsystems = new();

        ILog _log;
        IClock _clock;
        XRHandMeshController[] _visuals;
        double _nextPoll;
        int _running = -1;
        int _tracked = -1;
        int _visible = -1;

        /// <param name="visualsRoot">Searched once, inactive children included, for the XR Hands mesh controllers.</param>
        public void Construct(ILog log, IClock clock, Transform visualsRoot)
        {
            _log = log;
            _clock = clock;
            _visuals = visualsRoot.GetComponentsInChildren<XRHandMeshController>(true);
            _log.Write(LogLevel.Info, "xr", "evt=hands.monitor visuals=" + _visuals.Length);
        }

        void Update()
        {
            if (_log == null || _clock.Now < _nextPoll)
                return;
            _nextPoll = _clock.Now + PollSeconds;

            var subsystem = RunningSubsystem();
            var running = subsystem != null ? 1 : 0;
            if (running != _running)
            {
                _running = running;
                _log.Write(LogLevel.Info, "xr", running == 1
                    ? "evt=hands.subsystem state=running id=" + subsystem.subsystemDescriptor.id
                    : "evt=hands.subsystem state=stopped");
            }

            var tracked = subsystem == null ? 0 : Bits(subsystem.leftHand.isTracked, subsystem.rightHand.isTracked);
            if (tracked != _tracked)
            {
                _tracked = tracked;
                _log.Write(LogLevel.Info, "xr", Line("evt=hands.tracked", tracked));
            }

            var visible = VisibleHands();
            if (visible != _visible)
            {
                _visible = visible;
                _log.Write(LogLevel.Info, "xr", Line("evt=hands.visual", visible));
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
