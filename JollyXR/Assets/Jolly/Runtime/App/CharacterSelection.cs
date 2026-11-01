using System;
using System.Collections.Generic;
using System.IO;
using Jolly.App.Platform;
using Jolly.Character;
using Jolly.Contracts;

namespace Jolly.App
{
    /// <summary>
    /// Picks the character before the graph is built (ADR-0026). The first that binds of: the id in
    /// <c>persistentDataPath/character.txt</c>, the id build.sh baked in, then <see cref="JollyConfig.defaultCharacter"/>.
    /// An unknown, unusable or unbindable id is logged and skipped; when nothing binds, the prefab keeps the Nubi it ships
    /// with. Never throws: a bad local file must not stop the app.
    /// </summary>
    public static class CharacterSelection
    {
        const string Category = "character";

        public delegate bool Binder(CharacterDefinition definition, out string error);

        public static void Apply(NubiCharacterController character, JollyConfig config, CharacterChoice choice,
                                 AppPaths paths, ILog log)
        {
            if (character == null)
                return;
            try
            {
                var bound = Bind(Available(config, choice), config.defaultCharacter,
                                 ReadOverride(paths.CharacterOverride, log), choice?.bakedId, character.TryUse, log);
                if (bound == null && config.defaultCharacter == null)
                    log.Write(LogLevel.Warning, Category, "evt=character.no_default keeps=" + Id(character.Definition));
            }
            catch (Exception e)
            {
                log.Write(LogLevel.Error, Category, "evt=character.select_failed keeps=" + Id(character.Definition), e);
            }
        }

        /// <summary>
        /// Binds the first usable requested id, then <paramref name="fallback"/>, skipping any that failed to bind already.
        /// Returns the bound definition, or null when none binds.
        /// </summary>
        public static CharacterDefinition Bind(IReadOnlyList<CharacterDefinition> available, CharacterDefinition fallback,
                                               string overrideId, string bakedId, Binder bind, ILog log)
        {
            var failed = new List<CharacterDefinition>();
            foreach (var (source, id) in Requests(overrideId, bakedId))
            {
                var definition = Usable(available, source, id, log);
                if (definition != null && !failed.Contains(definition))
                {
                    if (TryBind(definition, bind, log))
                        return definition;
                    failed.Add(definition);
                }
            }
            return fallback != null && !failed.Contains(fallback) && TryBind(fallback, bind, log) ? fallback : null;
        }

        /// <summary>The first usable of the requested ids, else <paramref name="fallback"/> (which may be null).</summary>
        public static CharacterDefinition Resolve(IReadOnlyList<CharacterDefinition> available, CharacterDefinition fallback,
                                                  string overrideId, string bakedId, ILog log)
        {
            foreach (var (source, id) in Requests(overrideId, bakedId))
            {
                var definition = Usable(available, source, id, log);
                if (definition != null)
                    return definition;
            }
            return fallback;
        }

        static IEnumerable<(string Source, string Id)> Requests(string overrideId, string bakedId)
        {
            if (!string.IsNullOrWhiteSpace(overrideId))
                yield return ("override", overrideId.Trim());
            if (!string.IsNullOrWhiteSpace(bakedId))
                yield return ("build", bakedId.Trim());
        }

        static CharacterDefinition Usable(IReadOnlyList<CharacterDefinition> available, string source, string id, ILog log)
        {
            var definition = Find(available, id);
            if (definition == null)
            {
                log.Write(LogLevel.Warning, Category, $"evt=character.unknown source={source} id={id}");
                return null;
            }
            if (!definition.IsUsable(out var reason))
            {
                log.Write(LogLevel.Warning, Category,
                          $"evt=character.unusable source={source} id={definition.id} reason=\"{reason}\"");
                return null;
            }
            return definition;
        }

        static bool TryBind(CharacterDefinition definition, Binder bind, ILog log)
        {
            if (bind(definition, out var error))
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
