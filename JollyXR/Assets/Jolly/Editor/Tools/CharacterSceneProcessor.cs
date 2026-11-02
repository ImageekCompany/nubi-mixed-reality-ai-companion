using System;
using System.Collections.Generic;
using System.IO;
using Jolly.App;
using Jolly.Character;
using Jolly.Editor.Character;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jolly.Editor.Tools
{
    /// <summary>
    /// Fills <see cref="CharacterChoice"/> on the scene's JollyBootstrap in the processed copy of the scene only (Unity
    /// calls this for each scene in a build and for scene loads in Play mode), so the saved Main.unity never references
    /// a git-ignored local character (ADR-0026).
    /// In Play mode every local character is offered, for persistentDataPath/character.txt to pick. A player build
    /// carries none unless <c>-nubiCharacter &lt;id&gt;</c> (build.sh --character) asks for one, which then becomes the
    /// baked default; an id that matches nothing fails the build.
    /// </summary>
    public sealed class CharacterSceneProcessor : IProcessSceneWithReport
    {
        public const string CharacterArg = "-nubiCharacter";
        /// <summary>Path of the JSON to embed, set only by build.sh --embed-secrets (ADR-0049). An environment variable,
        /// so the path stays out of the logged command line; the values are never logged.</summary>
        public const string EmbedSecretsVariable = "NUBI_EMBED_SECRETS_FILE";

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            var bootstrap = FindBootstrap(scene);
            if (bootstrap == null)
                return;

            var local = LocalCharacters.FindAll();
            string baked = null;
            if (report != null)
            {
                baked = CommandLineValue(CharacterArg);
                local = local.FindAll(d => baked != null && string.Equals(d.id, baked, StringComparison.OrdinalIgnoreCase));
                if (baked != null && local.Count == 0 && !IsCommitted(bootstrap, baked))
                    throw new BuildFailedException($"{CharacterArg} '{baked}' matches no character definition.");
                Debug.Log($"[CharacterSceneProcessor] scene={scene.name} character={baked ?? "default"} local={local.Count} " +
                          $"paths=[{string.Join(", ", local.ConvertAll(AssetDatabase.GetAssetPath))}]");
            }

            var so = new SerializedObject(bootstrap);
            var array = so.FindProperty("characters.localCharacters");
            array.arraySize = local.Count;
            for (var i = 0; i < local.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = local[i];
            so.FindProperty("characters.bakedId").stringValue = baked ?? "";
            var secretsFile = report != null ? Environment.GetEnvironmentVariable(EmbedSecretsVariable) : null;
            so.FindProperty("embeddedSecretsJson").stringValue = string.IsNullOrEmpty(secretsFile) ? "" : ReadSecrets(secretsFile);
            if (report != null)
                Debug.Log($"[CharacterSceneProcessor] scene={scene.name} embedded_secrets={(string.IsNullOrEmpty(secretsFile) ? 0 : 1)}");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static string ReadSecrets(string path)
        {
            if (!File.Exists(path))
                throw new BuildFailedException($"{EmbedSecretsVariable} names a file that does not exist.");
            return File.ReadAllText(path);
        }

        static JollyBootstrap FindBootstrap(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<JollyBootstrap>(out var bootstrap))
                    return bootstrap;
            }
            return null;
        }

        static bool IsCommitted(JollyBootstrap bootstrap, string id)
        {
            var config = new SerializedObject(bootstrap).FindProperty("config").objectReferenceValue as JollyConfig;
            if (config == null)
                return false;
            var all = new List<CharacterDefinition>(config.characters) { config.defaultCharacter };
            return all.Exists(d => d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase));
        }

        static string CommandLineValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]) ? args[i + 1].Trim() : null;
        }
    }
}
