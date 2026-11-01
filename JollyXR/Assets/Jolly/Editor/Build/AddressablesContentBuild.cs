using System;
using System.Reflection;
using UnityEngine;

namespace Jolly.Editor.Build
{
    /// <summary>
    /// Builds Addressables player content (the Localization tables) before the player build. Addressables 2.11 makes
    /// "build with player" an EditorPrefs value by default (architecture §10), so CI builds it explicitly.
    /// Bound by name: the asmdef graph (AssemblyGraphTests) does not list Unity.Addressables.Editor for this assembly.
    /// Editor-only, so no IL2CPP stripping concern.
    /// </summary>
    static class AddressablesContentBuild
    {
        const string LogTag = "[QuestBuild]";
        const string Assembly = ", Unity.Addressables.Editor";

        public static bool Run()
        {
            var defaults = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject" + Assembly);
            var settings = Type.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettings" + Assembly);
            var resultType = Type.GetType("UnityEditor.AddressableAssets.Build.AddressablesPlayerBuildResult" + Assembly);
            var exists = defaults?.GetProperty("SettingsExists", BindingFlags.Public | BindingFlags.Static);
            var build = settings?.GetMethod("BuildPlayerContent", BindingFlags.Public | BindingFlags.Static, null,
                new[] { resultType?.MakeByRefType() ?? typeof(void) }, null);
            if (exists == null || build == null)
            {
                Debug.LogError($"{LogTag} Addressables editor API not found; cannot build player content");
                return false;
            }
            if (!(bool)exists.GetValue(null))
            {
                Debug.Log($"{LogTag} No Addressables settings in the project; skipping content build");
                return true;
            }

            var args = new object[1];
            build.Invoke(null, args);
            var error = resultType.GetProperty("Error")?.GetValue(args[0]) as string;
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"{LogTag} Addressables content build failed: {error}");
                return false;
            }
            Debug.Log($"{LogTag} Addressables content built");
            return true;
        }
    }
}
