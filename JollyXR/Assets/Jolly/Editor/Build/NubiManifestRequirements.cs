using System.Collections.Generic;
using Unity.XR.Management.AndroidManifest.Editor;
using UnityEngine.XR.OpenXR;

namespace Jolly.Editor.Build
{
    /// <summary>
    /// Adds the permissions Nubi needs on top of what the OpenXR / meta-openxr features already inject.
    /// HEADSET_CAMERA is also added by ARCameraFeature when Camera Image Support is on; listing it here keeps
    /// the app correct if that toggle is ever lost. Duplicates are merged by XR Management's manifest processor.
    /// </summary>
    // ReSharper disable once UnusedType.Global — discovered by XR Management via TypeCache.
    class NubiManifestRequirements : IAndroidManifestRequirementProvider
    {
        static readonly string[] Permissions =
        {
            "horizonos.permission.HEADSET_CAMERA",
            "android.permission.RECORD_AUDIO",
            "com.oculus.permission.USE_SCENE",
            "android.permission.INTERNET",
        };

        public ManifestRequirement ProvideManifestRequirement()
        {
            var elements = new List<ManifestElement>();
            foreach (var permission in Permissions)
            {
                elements.Add(new ManifestElement
                {
                    ElementPath = new List<string> { "manifest", "uses-permission" },
                    Attributes = new Dictionary<string, string> { { "name", permission } },
                });
            }

            return new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<System.Type> { typeof(OpenXRLoader) },
                NewElements = elements,
                // Unity adds BLUETOOTH whenever the Microphone class is used; Meta store certification rejects it.
                RemoveElements = new List<ManifestElement>
                {
                    new()
                    {
                        ElementPath = new List<string> { "manifest", "uses-permission" },
                        Attributes = new Dictionary<string, string> { { "name", "android.permission.BLUETOOTH" } },
                    },
                },
            };
        }
    }
}
