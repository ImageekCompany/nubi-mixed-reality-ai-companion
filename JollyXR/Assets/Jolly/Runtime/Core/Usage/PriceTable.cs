using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Usage
{
    /// <summary>USD rates of one model (meta-model-api.md §6). Token rates are per million tokens.</summary>
    public sealed class ModelPrice
    {
        public ModelPrice(decimal inputPerMillion, decimal cachedInputPerMillion, decimal outputPerMillion,
                          decimal perImage = 0m, decimal perAudioHour = 0m, decimal perWebSearch = 0m)
        {
            InputPerMillion = inputPerMillion;
            CachedInputPerMillion = cachedInputPerMillion;
            OutputPerMillion = outputPerMillion;
            PerImage = perImage;
            PerAudioHour = perAudioHour;
            PerWebSearch = perWebSearch;
        }

        public decimal InputPerMillion { get; }
        public decimal CachedInputPerMillion { get; }
        public decimal OutputPerMillion { get; }
        public decimal PerImage { get; }
        public decimal PerAudioHour { get; }
        /// <summary>The built-in web_search tool, per call ($2.50 per 1,000 for Muse Spark, ADR-0048).</summary>
        public decimal PerWebSearch { get; }
    }

    /// <summary>Per-model prices from JollyConfig. An unknown model is priced at the highest known rate of each kind.</summary>
    public sealed class PriceTable
    {
        const decimal Million = 1_000_000m;
        const decimal SecondsPerHour = 3600m;

        readonly Dictionary<string, ModelPrice> _prices;
        readonly ModelPrice _mostExpensive;

        public PriceTable(IReadOnlyDictionary<string, ModelPrice> prices)
        {
            if (prices == null || prices.Count == 0)
                throw new ArgumentException("At least one model price is required.", nameof(prices));
            _prices = new Dictionary<string, ModelPrice>(prices.Count, StringComparer.Ordinal);
            decimal input = 0, cached = 0, output = 0, image = 0, audio = 0, search = 0;
            foreach (var pair in prices)
            {
                _prices.Add(pair.Key, pair.Value);
                input = Math.Max(input, pair.Value.InputPerMillion);
                cached = Math.Max(cached, pair.Value.CachedInputPerMillion);
                output = Math.Max(output, pair.Value.OutputPerMillion);
                image = Math.Max(image, pair.Value.PerImage);
                audio = Math.Max(audio, pair.Value.PerAudioHour);
                search = Math.Max(search, pair.Value.PerWebSearch);
            }
            _mostExpensive = new ModelPrice(input, cached, output, image, audio, search);
        }

        public ModelPrice For(string model) =>
            model != null && _prices.TryGetValue(model, out var price) ? price : _mostExpensive;

        public decimal Cost(in UsageRecord usage)
        {
            var p = For(usage.Model);
            var uncached = Math.Max(0, usage.InputTokens - usage.CachedInputTokens);
            return (uncached * p.InputPerMillion + usage.CachedInputTokens * p.CachedInputPerMillion
                    + usage.OutputTokens * p.OutputPerMillion) / Million
                   + usage.Images * p.PerImage
                   + usage.WebSearches * p.PerWebSearch
                   + (decimal)usage.AudioSeconds / SecondsPerHour * p.PerAudioHour;
        }
    }
}
