using Jolly.XR.World;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace Jolly.XR.MetaQuest
{
    /// <summary>
    /// Opens Meta's Space Setup through MetaOpenXRSessionSubsystem.TryRequestSceneCapture (MetaOpenXRSessionSubsystem.cs:31-44),
    /// which returns false without the XR_FB_scene_capture extension. Off device the session subsystem is not Meta's, so
    /// nothing can be requested. On success the app pauses on a later frame and resumes when the user finishes
    /// (meta-openxr Documentation~/features/session.md "Life cycle").
    /// </summary>
    public sealed class MetaSceneCapture : ISceneCaptureLauncher
    {
        readonly ARSession _session;

        public MetaSceneCapture(ARSession session) => _session = session;

        public bool CanRequest => _session != null && _session.subsystem is MetaOpenXRSessionSubsystem;

        public bool TryRequest() =>
            _session != null && _session.subsystem is MetaOpenXRSessionSubsystem meta && meta.TryRequestSceneCapture();
    }
}
