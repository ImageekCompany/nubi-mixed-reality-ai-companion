using System;
using System.Collections.Generic;
using UnityEngine.XR.ARSubsystems;

namespace Jolly.XR.World
{
    /// <summary>Maps the furniture words the model uses (walk_to "couch") onto Space Setup plane and box classifications.
    /// There are no room labels in the scene model (xr-stack.md §4.4), so "kitchen" is unknown here by design.</summary>
    public static class SurfaceLabels
    {
        static readonly Dictionary<string, (PlaneClassifications Plane, BoundingBoxClassifications Box)> Labels =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["couch"] = (PlaneClassifications.Couch, BoundingBoxClassifications.Couch),
                ["sofa"] = (PlaneClassifications.Couch, BoundingBoxClassifications.Couch),
                ["table"] = (PlaneClassifications.Table, BoundingBoxClassifications.Table),
                ["desk"] = (PlaneClassifications.Table, BoundingBoxClassifications.Table),
                ["bed"] = (PlaneClassifications.None, BoundingBoxClassifications.Bed),
                ["screen"] = (PlaneClassifications.None, BoundingBoxClassifications.Screen),
                ["tv"] = (PlaneClassifications.None, BoundingBoxClassifications.Screen),
                ["television"] = (PlaneClassifications.None, BoundingBoxClassifications.Screen),
                ["storage"] = (PlaneClassifications.None, BoundingBoxClassifications.Storage),
                ["shelf"] = (PlaneClassifications.None, BoundingBoxClassifications.Storage),
                ["cabinet"] = (PlaneClassifications.None, BoundingBoxClassifications.Storage),
                ["lamp"] = (PlaneClassifications.None, BoundingBoxClassifications.Lamp),
                ["plant"] = (PlaneClassifications.None, BoundingBoxClassifications.Plant),
                ["chair"] = (PlaneClassifications.Seat, BoundingBoxClassifications.Chair),
            };

        public static bool TryParse(string label, out PlaneClassifications plane, out BoundingBoxClassifications box)
        {
            plane = PlaneClassifications.None;
            box = BoundingBoxClassifications.None;
            if (string.IsNullOrWhiteSpace(label) || !Labels.TryGetValue(label.Trim(), out var match))
                return false;
            (plane, box) = match;
            return true;
        }
    }
}
