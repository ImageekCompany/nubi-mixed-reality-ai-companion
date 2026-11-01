using System.IO;
using Jolly.App.Platform;
using Jolly.Character;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Tools
{
    /// <summary>
    /// Picks the character for Play mode the way adb picks it on the device: by writing or deleting the Editor's
    /// persistentDataPath/character.txt (ADR-0026). Tests use their own data root, so this never leaks into them.
    /// </summary>
    public static class CharacterPlayMenu
    {
        [MenuItem("Jolly/Character/Play As Selected Definition")]
        static void PlayAsSelected()
        {
            if (Selection.activeObject is not CharacterDefinition definition || string.IsNullOrEmpty(definition.id))
            {
                Debug.LogError("[CharacterPlayMenu] Select a CharacterDefinition asset with an id first.");
                return;
            }
            var path = AppPaths.ForPlayer().CharacterOverride;
            File.WriteAllText(path, definition.id);
            Debug.Log($"[CharacterPlayMenu] Play mode uses '{definition.id}' ({path}).");
        }

        [MenuItem("Jolly/Character/Play As Default")]
        static void PlayAsDefault()
        {
            var path = AppPaths.ForPlayer().CharacterOverride;
            if (File.Exists(path))
                File.Delete(path);
            Debug.Log("[CharacterPlayMenu] Play mode uses JollyConfig.defaultCharacter.");
        }
    }
}
