namespace Jolly.Core.Tools
{
    /// <summary>Maps everyday words onto the furniture labels of the room scan (SceneSurface.Label, contracts §7).</summary>
    static class FurnitureLabels
    {
        public static string Normalize(string word)
        {
            var w = (word ?? string.Empty).Trim().ToLowerInvariant();
            return w switch
            {
                "sofa" or "settee" => "couch",
                "desk" or "counter" => "table",
                "seat" or "armchair" or "stool" => "chair",
                "tv" or "television" or "monitor" => "screen",
                "shelf" or "cabinet" or "cupboard" or "dresser" or "wardrobe" => "storage",
                _ => w,
            };
        }
    }
}
