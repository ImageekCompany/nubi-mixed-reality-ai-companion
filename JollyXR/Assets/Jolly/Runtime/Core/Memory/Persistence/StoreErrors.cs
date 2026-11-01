using System;
using Newtonsoft.Json;

namespace Jolly.Core.Memory
{
    static class StoreErrors
    {
        /// <summary>What a hand-parsed store file throws when it is truncated or edited by hand.</summary>
        public static bool IsCorruptFile(Exception e) =>
            e is JsonException or FormatException or InvalidCastException or InvalidOperationException or ArgumentException;
    }
}
