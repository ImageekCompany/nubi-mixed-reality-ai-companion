using System.Collections.Generic;
using Jolly.Character;
using UnityEditor;

namespace Jolly.Editor.Character
{
    /// <summary>
    /// Local characters live under a git-ignored folder (.gitignore, docs/characters.md): demo skins whose source may
    /// not be redistributed. Nothing committed may reference anything in it.
    /// </summary>
    public static class LocalCharacters
    {
        public const string Root = "Assets/Jolly/Local";

        public static bool IsLocal(string assetPath) =>
            assetPath != null && (assetPath == Root || assetPath.StartsWith(Root + "/", System.StringComparison.Ordinal));

        /// <summary>Every definition under <see cref="Root"/>; empty when the folder is absent (CI, a fresh clone).</summary>
        public static List<CharacterDefinition> FindAll()
        {
            var found = new List<CharacterDefinition>();
            if (!AssetDatabase.IsValidFolder(Root))
                return found;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CharacterDefinition), new[] { Root }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null)
                    found.Add(definition);
            }
            return found;
        }
    }
}
