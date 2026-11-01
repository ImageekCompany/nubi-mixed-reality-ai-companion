using System;
using System.Collections.Generic;
using System.IO;
using Jolly.App.Platform;
using Jolly.Character;
using Jolly.Contracts;

namespace Jolly.App
{
    /// <summary>
    /// Picks the character before the graph is built (ADR-0026). The first usable of: the id in
    /// <c>persistentDataPath/character.txt</c>, the id build.sh baked in, then <see cref="JollyConfig.defaultCharacter"/>.
    /// An unknown or unusable id is logged and skipped; when nothing binds, the prefab keeps the Nubi it ships with.
    /// Never throws: a bad local file must not stop the app.
    /// </summary>
    public static class CharacterSelection
    {
        const string Category = "character";

        public static void Apply(NubiCharacterController character, JollyConfig config, CharacterChoice choice,
                                 AppPaths paths, ILog log)
        {
            if (character == null)
                return;
            try
            {
                var available = Available(config, choice);
                var chosen = Resolve(available, config.defaultCharacter, ReadOverride(paths.CharacterOverride, log),
                                     choice?.bakedId, log);
                if (chosen == null)
                {
                    log.Write(LogLevel.Warning, Category, "evt=character.no_default keeps=" + Id(character.Definition));
                    return;
                }
                if (Use(character, chosen, log))
                    return;
                if (chosen != config.defaultCharacter && config.defaultCharacter != null)
                    Use(character, config.defaultCharacter, log);
            }
            catch (Exception e)
            {
                log.Write(LogLevel.Error, Category, "evt=character.select_failed keeps=" + Id(character.Definition), e);
            }
        }

        /// <summary>The first usable of the requested ids, else <paramref name="fallback"/> (which may be null).</summary>
        public static CharacterDefinition Resolve(IReadOnlyList<CharacterDefinition> available, CharacterDefinition fallback,
                                                  string overrideId, string bakedId, ILog log)
        {
            foreach (var (source, id) in new[] { ("override", overrideId), ("build", bakedId) })
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var definition = Find(available, id.Trim());
                if (definition == null)
                {
                    log.Write(LogLevel.Warning, Category, $"evt=character.unknown source={source} id={id.Trim()}");
                    continue;
                }
                if (!definition.IsUsable(out var reason))
                {
                    log.Write(LogLevel.Warning, Category,
                              $"evt=character.unusable source={source} id={definition.id} reason=\"{reason}\"");
                    continue;
                }
                return definition;
            }
            return fallback;
        }

        static bool Use(NubiCharacterController character, CharacterDefinition definition, ILog log)
        {
            if (character.TryUse(definition, out var error))
            {
                log.Write(LogLevel.Info, Category, $"evt=character.selected id={definition.id}");
                return true;
            }
            log.Write(LogLevel.Warning, Category, $"evt=character.bind_failed id={definition.id} reason=\"{error}\"");
            return false;
        }

        static List<CharacterDefinition> Available(JollyConfig config, CharacterChoice choice)
        {
            var all = new List<CharacterDefinition>();
            Add(all, config.defaultCharacter);
            foreach (var definition in config.characters ?? Array.Empty<CharacterDefinition>())
                Add(all, definition);
            foreach (var definition in choice?.localCharacters ?? Array.Empty<CharacterDefinition>())
                Add(all, definition);
            return all;
        }

        static void Add(List<CharacterDefinition> all, CharacterDefinition definition)
        {
            if (definition != null && !all.Contains(definition))
                all.Add(definition);
        }

        static CharacterDefinition Find(IReadOnlyList<CharacterDefinition> available, string id)
        {
            foreach (var definition in available)
            {
                if (definition != null && string.Equals(definition.id, id, StringComparison.OrdinalIgnoreCase))
                    return definition;
            }
            return null;
        }

        static string ReadOverride(string path, ILog log)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            }
            catch (IOException e)
            {
                log.Write(LogLevel.Warning, Category, "evt=character.override_unreadable", e);
                return null;
            }
        }

        static string Id(CharacterDefinition definition) => definition != null ? definition.id : "none";
    }
}
