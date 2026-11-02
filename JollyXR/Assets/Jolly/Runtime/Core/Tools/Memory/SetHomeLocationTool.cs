using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Location;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Stores the city (and country) the user says they live in, with their other preferences, so web search answers
    /// "the weather" for that city (ADR-0048). Only a city is kept, never a street or a position.
    /// </summary>
    public sealed class SetHomeLocationTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"city\":{\"type\":\"string\",\"maxLength\":64,\"description\":\"City or town only, never a street or address.\"}," +
            "\"country\":{\"type\":\"string\",\"maxLength\":2,\"description\":\"ISO 3166-1 alpha-2 code, e.g. GB, if known.\"}}," +
            "\"required\":[\"city\"],\"additionalProperties\":false}";

        public ToolDefinition Definition { get; } = new(ToolNames.SetHomeLocation,
            "Remember the city the user lives in when they tell you, e.g. \"I live in Krakow\", so searches for weather " +
            "or opening hours are about their home. Do not call it for places they only visit or ask about.",
            Schema, TimeSpan.FromSeconds(1), ToolConcurrency.Parallel);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("city", out var rawCity);
            args.TryGetString("country", out var rawCountry);
            var city = UserLocation.CleanCity(rawCity);
            if (city == null)
                return UniTask.FromResult(ToolResult.Failure("invalid_arguments", "city"));
            var country = UserLocation.CleanCountry(rawCountry);
            context.Preferences.SetHomeLocation(city, country);
            return UniTask.FromResult(ToolResult.Success(
                new ToolJson().Add("status", "saved").Add("city", city).Add("country", country).ToString()));
        }
    }
}
