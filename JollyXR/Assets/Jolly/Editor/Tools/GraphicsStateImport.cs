using System;
using System.IO;
using Jolly.App;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jolly.Editor.Tools
{
    /// <summary>
    /// Copies a self-test graphics-state trace (pulled by <c>tools/device/selftest.sh</c> from a development player) into
    /// the project and assigns it to every JollyConfig, so the next build warms those pipelines at boot
    /// (<c>GraphicsWarmup</c>, docs/perf/README.md §7). Re-record after changing shaders, materials or URP settings.
    /// Batch: <c>Unity -batchmode -projectPath JollyXR -executeMethod Jolly.Editor.Tools.GraphicsStateImport.ImportBatch
    /// -nubiTrace ../artifacts/selftest/graphics_states.graphicsstate</c>
    /// </summary>
    public static class GraphicsStateImport
    {
        const string Target = "Assets/Jolly/Content/Rendering/QuestVulkan.graphicsstate";
        const string LogTag = "[GraphicsStateImport]";

        public static void ImportBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-nubiTrace");
            var ok = at >= 0 && at + 1 < args.Length && Import(args[at + 1]);
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Import(string source)
        {
            if (!File.Exists(source))
            {
                Debug.LogError($"{LogTag} no trace at {source}");
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
            File.Copy(source, Target, overwrite: true);
            AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var states = AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>(Target);
            if (states == null)
            {
                Debug.LogError($"{LogTag} {Target} did not import as a GraphicsStateCollection");
                return false;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(JollyConfig)))
            {
                var config = AssetDatabase.LoadAssetAtPath<JollyConfig>(AssetDatabase.GUIDToAssetPath(guid));
                config.graphicsStates = states;
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} Result=Succeeded api={states.graphicsDeviceType} variants={states.variantCount} " +
                      $"states={states.totalGraphicsStateCount}");
            return true;
        }
    }
}
