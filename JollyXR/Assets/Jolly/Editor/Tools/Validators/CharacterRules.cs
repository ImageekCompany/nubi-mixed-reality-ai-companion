using System;
using System.Collections.Generic;
using Jolly.App;
using Jolly.Character;
using Jolly.Editor.Character;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Tools.Validators
{
    /// <summary>
    /// JollyConfig's character list, and the rig contract of every CharacterDefinition in the project, local ones
    /// included when present: a local character that breaks the contract fails the gate on the machine that has it.
    /// </summary>
    public static class CharacterRules
    {
        public static void Check(JollyConfig config, List<string> problems)
        {
            CheckConfig(config, problems);
            foreach (var path in CharacterDefinitionValidator.FindAll())
            {
                foreach (var issue in CharacterDefinitionValidator.Validate(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path)))
                {
                    if (issue.Level == CharacterIssueLevel.Error)
                        problems.Add($"{path}: {issue.Message}");
                    else
                        Debug.LogWarning($"[ContentValidator] {path}: {issue.Message}");
                }
            }
        }

        static void CheckConfig(JollyConfig config, List<string> problems)
        {
            if (config.defaultCharacter == null)
                problems.Add("JollyConfig.defaultCharacter is not set");
            else if (Array.IndexOf(config.characters, config.defaultCharacter) < 0)
                problems.Add("JollyConfig.defaultCharacter is not in JollyConfig.characters");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in config.characters)
            {
                if (definition == null)
                {
                    problems.Add("JollyConfig.characters has an empty entry");
                    continue;
                }
                if (!ids.Add(definition.id))
                    problems.Add($"JollyConfig.characters has id '{definition.id}' twice");
                var path = AssetDatabase.GetAssetPath(definition);
                if (LocalCharacters.IsLocal(path))
                    problems.Add($"JollyConfig.characters references the git-ignored {path}; local characters are " +
                                 "added at build time (docs/characters.md)");
            }
        }
    }
}
