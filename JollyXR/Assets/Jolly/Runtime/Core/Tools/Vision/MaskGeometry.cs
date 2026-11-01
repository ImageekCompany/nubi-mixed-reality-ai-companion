using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>Where a mask sits in the world, and whether SAM's answer is plausible (architecture §5.3).</summary>
    public sealed class MaskGeometry
    {
        readonly IWorldQuery _world;
        readonly ICameraProjection _projection;
        readonly VisionSettings _settings;
        readonly List<float> _distances = new();

        public MaskGeometry(IWorldQuery world, ICameraProjection projection, VisionSettings settings)
        {
            _world = world;
            _projection = projection;
            _settings = settings;
        }

        /// <summary>
        /// Casts rays through the mask centroid and evenly spread interior pixels; the median hit distance places the
        /// object on the centroid ray. Without any hit it falls back to <see cref="VisionSettings.FallbackDistance"/>.
        /// </summary>
        public float Locate(SegmentedObject obj, EncodedImage image, out Vector3 world)
        {
            var mask = obj.Mask;
            var count = 0;
            double sumX = 0, sumY = 0;
            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                if (!mask[x, y])
                    continue;
                count++;
                sumX += x;
                sumY += y;
            }
            var centroid = count == 0
                ? (Vector2)obj.Box.center
                : new Vector2(obj.Box.x + (float)(sumX / count) + 0.5f, obj.Box.y + (float)(sumY / count) + 0.5f);
            var centreRay = RayThrough(centroid, image);

            _distances.Clear();
            AddHit(centreRay, centreRay);
            var stride = Mathf.Max(1, count / (_settings.InteriorSamples + 1));
            int index = 0, taken = 0;
            for (var y = 0; y < mask.Height && taken < _settings.InteriorSamples; y++)
            for (var x = 0; x < mask.Width && taken < _settings.InteriorSamples; x++)
            {
                if (!mask[x, y] || ++index % stride != 0)
                    continue;
                taken++;
                AddHit(RayThrough(new Vector2(obj.Box.x + x + 0.5f, obj.Box.y + y + 0.5f), image), centreRay);
            }

            var distance = _distances.Count == 0 ? _settings.FallbackDistance : Median(_distances);
            world = centreRay.GetPoint(distance);
            return distance;
        }

        /// <summary>No mask, a mask over more than <see cref="VisionSettings.MaxMaskCoverage"/> of the frame, or a hand
        /// key point inside the mask: the prompt probably missed the object.</summary>
        public bool IsImplausible(IReadOnlyList<SegmentedObject> objects, EncodedImage image, IReadOnlyList<Vector2Int> handPixels)
        {
            if (objects.Count == 0)
                return true;
            var best = Best(objects);
            var area = 0;
            foreach (var word in best.Mask.Bits)
                area += PopCount(word);
            if (area > _settings.MaxMaskCoverage * image.Width * image.Height)
                return true;
            foreach (var h in handPixels)
            {
                if (best.Box.Contains(h) && best.Mask[h.x - best.Box.x, h.y - best.Box.y])
                    return true;
            }
            return false;
        }

        public static SegmentedObject Best(IReadOnlyList<SegmentedObject> objects)
        {
            var best = objects[0];
            foreach (var o in objects)
            {
                if (o.Confidence > best.Confidence)
                    best = o;
            }
            return best;
        }

        Ray RayThrough(Vector2 pixel, EncodedImage image) => _projection.PixelToWorldRay(pixel, image.Intrinsics, image.CameraPoseWorld);

        /// <summary>Records the hit as a distance along the centroid ray, so a flat surface gives one consistent depth.</summary>
        void AddHit(Ray ray, Ray centre)
        {
            if (_world.TryRaycast(ray, _settings.RaycastMaxDistance, out var hit))
                _distances.Add(Vector3.Dot(hit - centre.origin, centre.direction));
        }

        static float Median(List<float> values)
        {
            values.Sort();
            var mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) * 0.5f;
        }

        static int PopCount(ulong v)
        {
            var count = 0;
            for (; v != 0; v &= v - 1)
                count++;
            return count;
        }
    }
}
