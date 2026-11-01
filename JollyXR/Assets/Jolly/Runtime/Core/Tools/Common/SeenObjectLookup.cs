using System;
using Jolly.Contracts;
using Jolly.Core.Memory;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Resolves what the model calls a seen object: its id (obj_3), else its label, most recent first. A label matches
    /// exactly or as whole words, case-insensitively, so "mug" finds "white enamel mug" but "mu" does not; an exact match
    /// anywhere wins over a word match. The match is touched through TryGetObject, so it counts as recently used.
    /// </summary>
    static class SeenObjectLookup
    {
        const int MaxQueryChars = 40;

        public static bool TryFind(ISceneMemory scene, string idOrLabel, out SeenObject seen)
        {
            if (scene.TryGetObject(idOrLabel, out seen))
                return true;
            var query = PromptText.Sanitize(idOrLabel, MaxQueryChars).ToLowerInvariant();
            if (query.Length == 0)
                return false;

            string id = null;
            foreach (var o in scene.Objects)
            {
                if (string.Equals(o.Label, query, StringComparison.OrdinalIgnoreCase))
                {
                    id = o.Id;
                    break;
                }
                if (id == null && ContainsWords(o.Label, query))
                    id = o.Id;
            }
            return id != null && scene.TryGetObject(id, out seen);
        }

        static bool ContainsWords(string label, string words) =>
            !string.IsNullOrEmpty(label)
            && (" " + label.ToLowerInvariant() + " ").Contains(" " + words + " ", StringComparison.Ordinal);
    }
}
