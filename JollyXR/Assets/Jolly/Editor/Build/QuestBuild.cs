using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Jolly.Editor.Build
{
    /// <summary>
    /// Batchmode APK build for Quest.
    /// <c>Unity -batchmode -projectPath JollyXR -buildTarget Android -executeMethod Jolly.Editor.Build.QuestBuild.BuildApk
    /// [-nubiOut Builds/Android/Nubi.apk] [-nubiScenes Assets/A.unity;Assets/B.unity] [-nubiDevelopment]</c>
    /// Exits the editor with 0 on success and 1 on failure.
    /// </summary>
    public static class QuestBuild
    {
        const string DefaultOutput = "Builds/Android/Nubi.apk";
        const string LogTag = "[QuestBuild]";

        public static void BuildApk()
        {
            var exitCode = 1;
            try
            {
                exitCode = Build() ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        [MenuItem("Jolly/Build/Build Quest APK")]
        static void BuildFromMenu() => Build();

        static bool Build()
        {
            QuestProjectSetup.Apply();
            if (!QuestProjectSetup.LogValidation(applyAutomaticFixes: false))
                Debug.LogWarning($"{LogTag} OpenXR validation reports errors; building anyway, see log above");

            var output = GetArg("-nubiOut") ?? DefaultOutput;
            var scenesArg = GetArg("-nubiScenes");
            var scenes = scenesArg != null
                ? scenesArg.Split(';', StringSplitOptions.RemoveEmptyEntries)
                : EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError($"{LogTag} No scenes: add enabled scenes to Build Settings or pass -nubiScenes");
                return false;
            }

            var options = BuildOptions.None;
            if (HasArg("-nubiDevelopment"))
                options |= BuildOptions.Development;

            if (!AddressablesContentBuild.Run())
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            EditorUserBuildSettings.buildAppBundle = false;

            Debug.Log($"{LogTag} Building {output} scenes=[{string.Join(", ", scenes)}] options={options}");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options,
            });

            var summary = report.summary;
            Debug.Log($"{LogTag} Result={summary.result} size={summary.totalSize} bytes time={summary.totalTime} " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings} path={summary.outputPath}");
            return summary.result == BuildResult.Succeeded;
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static bool HasArg(string name) => Environment.GetCommandLineArgs().Contains(name);
    }
}
