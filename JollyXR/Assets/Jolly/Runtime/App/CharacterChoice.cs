using System;
using Jolly.Character;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>
    /// What the build adds to the character choice. Empty in the saved Main.unity: CharacterSceneProcessor (Jolly.Editor)
    /// fills it in the processed copy of the scene only, so local, git-ignored definitions reach a build or Play mode
    /// without any committed asset referencing them (ADR-0026).
    /// </summary>
    [Serializable]
    public sealed class CharacterChoice
    {
        public CharacterDefinition[] localCharacters = Array.Empty<CharacterDefinition>();
        [Tooltip("Id baked in by build.sh --character; empty uses JollyConfig.defaultCharacter.")]
        public string bakedId = "";
    }
}
