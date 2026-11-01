using UnityEngine.XR.ARSubsystems;

namespace Jolly.XR.World
{
    /// <summary>Names Space Setup plane and box classifications with the lower-case furniture words of SceneSurface.Label.
    /// There are no room labels in the scene model (xr-stack.md §4.4), so "kitchen" has none by design.</summary>
    public static class SurfaceLabels
    {
        static readonly (BoundingBoxClassifications Flag, string Label)[] Boxes =
        {
            (BoundingBoxClassifications.Couch, "couch"),
            (BoundingBoxClassifications.Table, "table"),
            (BoundingBoxClassifications.Bed, "bed"),
            (BoundingBoxClassifications.Chair, "chair"),
            (BoundingBoxClassifications.Storage, "storage"),
            (BoundingBoxClassifications.Screen, "screen"),
            (BoundingBoxClassifications.Lamp, "lamp"),
            (BoundingBoxClassifications.Plant, "plant"),
        };

        static readonly (PlaneClassifications Flag, string Label)[] Planes =
        {
            (PlaneClassifications.Couch, "couch"),
            (PlaneClassifications.Table, "table"),
            (PlaneClassifications.Seat, "chair"),
        };

        /// <summary>The first furniture label among the classifications, or null.</summary>
        public static string ForBox(BoundingBoxClassifications classifications)
        {
            foreach (var (flag, label) in Boxes)
            {
                if ((classifications & flag) != 0)
                    return label;
            }
            return null;
        }

        /// <inheritdoc cref="ForBox"/>
        public static string ForPlane(PlaneClassifications classifications)
        {
            foreach (var (flag, label) in Planes)
            {
                if ((classifications & flag) != 0)
                    return label;
            }
            return null;
        }
    }
}
