using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Chat
{
    /// <summary>
    /// <c>prompt_cache_key</c> for a request: a hash of what stays the same across turns (model, instructions, tool
    /// names), so requests sharing that prefix are routed to the same cache. Without a key, identical 2.7k-token prefixes
    /// were never cached (live 2026-11-01: cached 0 on repeats); with one, the repeat read 2,673 cached tokens and its
    /// first text came 0.25 s sooner (docs/research/meta-model-api.md §1.4).
    /// </summary>
    static class PromptCacheKey
    {
        const ulong Offset = 14695981039346656037;
        const ulong Prime = 1099511628211;

        public static string For(string model, ModelRequest request)
        {
            var hash = Add(Offset, model);
            hash = Add(hash, request.Instructions);
            if (request.Tools != null)
            {
                foreach (var tool in request.Tools)
                    hash = Add(hash, tool.Name);
            }
            return "jolly-" + hash.ToString("x16");
        }

        /// <summary>FNV-1a over UTF-16 code units, with a separator so ("ab","c") and ("a","bc") differ.</summary>
        static ulong Add(ulong hash, string text)
        {
            if (text != null)
            {
                foreach (var c in text)
                    hash = (hash ^ c) * Prime;
            }
            return (hash ^ 0xFFFF) * Prime;
        }
    }
}
