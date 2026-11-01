using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Jolly.Editor.Build
{
    /// <summary>
    /// Restores the XR Hands "HandVisualizer" sample when it is missing (ADR-0030). It is under the Unity Package
    /// Distribution License, so it is git-ignored; the hand rig in Main.unity references its script and hand models by
    /// GUID. The copy keeps the .meta files and uses the Package Manager's sample path, so the GUIDs match. The CI and
    /// device scripts run tools/ci/restore-samples.sh before Unity starts; this covers a clone opened in the Editor
    /// before the package cache existed.
    /// </summary>
    [InitializeOnLoad]
    static class PackageSampleRestore
    {
        const string Package = "com.unity.xr.hands";
        const string Sample = "HandVisualizer";
        const string LogTag = "[PackageSampleRestore]";

        static PackageSampleRestore() => Restore();

        static void Restore()
        {
            var package = PackageInfo.FindForPackageName(Package);
            if (package == null)
                return;
            var dest = Path.Combine("Assets/Samples/XR Hands", package.version, Sample);
            if (Directory.Exists(dest))
                return;
            var source = Path.Combine(package.resolvedPath, "Samples~", Sample);
            if (!Directory.Exists(source))
            {
                Debug.LogError($"{LogTag} {Package}@{package.version} has no {Sample} sample at {source}");
                return;
            }
            Copy(source, dest);
            Debug.Log($"{LogTag} Restored {Sample} from {Package}@{package.version}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void Copy(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
            {
                var target = Path.Combine(dest, Path.GetFileName(file));
                File.Copy(file, target);
                File.SetAttributes(target, FileAttributes.Normal);
            }
            foreach (var dir in Directory.GetDirectories(source))
                Copy(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
