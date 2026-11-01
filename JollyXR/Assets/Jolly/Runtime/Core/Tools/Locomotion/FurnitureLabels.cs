namespace Jolly.Core.Tools
{
    /// <summary>Maps everyday words onto the AR plane/box classifications IWorldQuery understands (contracts §7).</summary>
    static class FurnitureLabels
    {
        public static string Normalize(string word)
        {
            var w = (word ?? string.Empty).Trim().ToLowerInvariant();
            return w switch
            {
                "sofa" or "settee" => "couch",
                "desk" or "counter" => "table",
                "tv" or "television" or "monitor" => "screen",
                "shelf" or "cabinet" or "cupboard" or "dresser" => "storage",
                _ => w,
            };
        }
    }
}
