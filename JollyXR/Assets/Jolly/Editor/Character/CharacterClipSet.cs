using System;
using System.Collections.Generic;
using Jolly.Character;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Character
{
    /// <summary>
    /// A definition's clips by canonical name: its <see cref="CharacterDefinition.clips"/> entry when it has one,
    /// otherwise the model take of the same name.
    /// </summary>
    public static class CharacterClipSet
    {
        public static Dictionary<string, AnimationClip> Resolve(CharacterDefinition definition)
        {
            var clips = new Dictionary<string, AnimationClip>();
            if (definition.model != null)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(definition.model)))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        clips[clip.name] = clip;
                }
            }
            foreach (var binding in definition.clips ?? Array.Empty<ClipBinding>())
            {
                if (binding != null && !string.IsNullOrEmpty(binding.name) && binding.clip != null)
                    clips[binding.name] = binding.clip;
            }
            return clips;
        }

        /// <summary>The required canonical clips <paramref name="clips"/> lacks.</summary>
        public static List<string> Missing(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var missing = new List<string>();
            foreach (var name in CharacterClips.Required)
            {
                if (!clips.ContainsKey(name))
                    missing.Add(name);
            }
            return missing;
        }
    }
}
