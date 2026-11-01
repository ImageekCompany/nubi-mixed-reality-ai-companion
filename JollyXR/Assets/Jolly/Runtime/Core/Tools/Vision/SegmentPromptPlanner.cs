using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>A SAM prompt plus the hand key points that fell inside the image (for the mask sanity check).</summary>
    public sealed class PromptPlan
    {
        public PromptPlan(SegmentPrompt prompt, IReadOnlyList<Vector2Int> handPixels) { Prompt = prompt; HandPixels = handPixels; }
        public SegmentPrompt Prompt { get; }
        public IReadOnlyList<Vector2Int> HandPixels { get; }
    }

    /// <summary>
    /// The SAM prompt cascade of architecture §5.3: the pointed-at surface within 2 m as a positive point (hand joints as
    /// negative points), else the <c>object_hint</c> as text, else the image centre with the same negatives.
    /// </summary>
    public sealed class SegmentPromptPlanner
    {
        readonly IUserPoseProvider _user;
        readonly IWorldQuery _world;
        readonly ICameraProjection _projection;
        readonly VisionSettings _settings;
        readonly List<Vector3> _keyPoints = new();

        public SegmentPromptPlanner(IUserPoseProvider user, IWorldQuery world, ICameraProjection projection, VisionSettings settings)
        {
            _user = user;
            _world = world;
            _projection = projection;
            _settings = settings;
        }

        public PromptPlan Plan(EncodedImage image, string objectHint)
        {
            var hands = HandPixels(image);
            if (TryPointedPixel(image, out var pointed))
                return new PromptPlan(SegmentPrompt.ForPoints(WithNegatives(pointed, hands)), hands);
            if (!string.IsNullOrWhiteSpace(objectHint))
                return new PromptPlan(SegmentPrompt.ForText(objectHint.Trim()), hands);
            var centre = new Vector2Int(image.Width / 2, image.Height / 2);
            return new PromptPlan(SegmentPrompt.ForPoints(WithNegatives(centre, hands)), hands);
        }

        bool TryPointedPixel(EncodedImage image, out Vector2Int pixel)
        {
            pixel = default;
            return _user.TryGetPointingRay(out var ray)
                   && _world.TryRaycast(ray, _settings.PointingMaxDistance, out var hit)
                   && TryProject(image, hit, out pixel);
        }

        List<Vector2Int> HandPixels(EncodedImage image)
        {
            _user.GetHandKeyPoints(_keyPoints);
            var pixels = new List<Vector2Int>(_keyPoints.Count);
            foreach (var p in _keyPoints)
            {
                if (TryProject(image, p, out var px))
                    pixels.Add(px);
            }
            return pixels;
        }

        bool TryProject(EncodedImage image, Vector3 world, out Vector2Int pixel)
        {
            pixel = default;
            if (!_projection.WorldToPixel(world, image.Intrinsics, image.CameraPoseWorld, out var px))
                return false;
            pixel = new Vector2Int(Mathf.FloorToInt(px.x), Mathf.FloorToInt(px.y));
            return pixel.x >= 0 && pixel.y >= 0 && pixel.x < image.Width && pixel.y < image.Height;
        }

        static List<PromptPoint> WithNegatives(Vector2Int positive, List<Vector2Int> hands)
        {
            var points = new List<PromptPoint>(hands.Count + 1) { new(positive.x, positive.y) };
            foreach (var h in hands)
            {
                if (h != positive)
                    points.Add(new PromptPoint(h.x, h.y, positive: false));
            }
            return points;
        }
    }
}
