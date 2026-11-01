using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The contents of <see cref="SelfTestSession.FlagFile"/>. <c>{}</c> is the default run; <c>{"turns":20}</c>
    /// (<c>selftest.sh --turns 20</c>) runs that many online voice turns for the latency distribution instead
    /// (<see cref="Scenarios.LatencyTurnsScenario"/>). Anything unreadable is the default run.
    /// </summary>
    public readonly struct SelfTestOptions
    {
        /// <summary>Bounds the spend of a mistyped count.</summary>
        public const int MaxLatencyTurns = 50;

        SelfTestOptions(int latencyTurns) => LatencyTurns = latencyTurns;

        /// <summary>0 for the default scenario list.</summary>
        public int LatencyTurns { get; }

        public static SelfTestOptions Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;
            try
            {
                if (JToken.Parse(json) is JObject flag && flag["turns"] is JValue { Type: JTokenType.Integer } turns)
                    return new SelfTestOptions((int)Math.Clamp(turns.Value<long>(), 0, MaxLatencyTurns));
            }
            catch (JsonReaderException)
            {
            }
            return default;
        }
    }
}
