using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Memory;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// The room line of the context block (ADR-0025): "Room: scanned. Furniture: table 1 m, couch 2.5 m." Built only from
    /// our own label words and template, sanitised like place names. One entry per label (its instance nearest to the
    /// user), nearest first, distances rounded to half a metre; entries that do not fit in maxChars are left out whole.
    /// Empty while the scan state is still unknown.
    /// </summary>
    public sealed class RoomContext
    {
        const int LabelChars = 24;

        readonly IRoomScan _scan;
        readonly IWorldQuery _world;
        readonly IUserPoseProvider _user;
        readonly RoomSettings _settings;
        readonly List<SceneSurface> _surfaces = new();
        readonly List<(string Label, float Distance)> _nearest = new();

        public RoomContext(IRoomScan scan, IWorldQuery world, IUserPoseProvider user, RoomSettings settings)
        {
            _scan = scan;
            _world = world;
            _user = user;
            _settings = settings;
        }

        public string Build(int maxChars)
        {
            var text = _scan.Status switch
            {
                RoomScanStatus.Scanned => Scanned(maxChars),
                RoomScanStatus.Unscanned => _settings.FallbackFloor
                    ? "Room: not scanned (no Space Setup); walking is limited to about " +
                      Meters(_settings.FallbackRadius) + " around the user."
                    : "Room: not scanned (no Space Setup); walking is unavailable.",
                RoomScanStatus.NoPermission => "Room: scene permission denied; walking is unavailable.",
                _ => string.Empty,
            };
            return text.Length <= maxChars ? text : string.Empty;
        }

        string Scanned(int maxChars)
        {
            _world.GetSurfaces(_surfaces);
            _nearest.Clear();
            var user = _user.Head.position;
            foreach (var surface in _surfaces)
            {
                var label = PromptText.Sanitize(surface.Label, LabelChars).ToLowerInvariant();
                if (label.Length == 0)
                    continue;
                var delta = surface.Pose.position - user;
                var distance = new Vector2(delta.x, delta.z).magnitude;
                var index = _nearest.FindIndex(n => n.Label == label);
                if (index < 0)
                    _nearest.Add((label, distance));
                else if (distance < _nearest[index].Distance)
                    _nearest[index] = (label, distance);
            }
            if (_nearest.Count == 0)
                return "Room: scanned, no furniture labelled.";

            _nearest.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var sb = new StringBuilder("Room: scanned. Furniture: ");
            var head = sb.Length;
            foreach (var (label, distance) in _nearest)
            {
                var entry = label + " " + Meters(distance);
                var separator = sb.Length == head ? "" : ", ";
                if (sb.Length + separator.Length + entry.Length + 1 > maxChars)
                    break;
                sb.Append(separator).Append(entry);
            }
            return sb.Length == head ? "Room: scanned." : sb.Append('.').ToString();
        }

        static string Meters(float meters) =>
            Mathf.Max(0.5f, Mathf.Round(meters * 2f) / 2f).ToString("0.#", CultureInfo.InvariantCulture) + " m";
    }
}
