using System;
using System.Collections.Generic;
using System.Text;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Memory
{
    /// <summary>
    /// The scene part of the context block: "Seen recently: obj_3 white mug (near sofa, 2 min ago); … Places: sofa, desk."
    /// Built only from sanitised labels and our own template; entries that do not fit in maxChars are left out whole.
    /// </summary>
    static class SceneContext
    {
        const float NearPlaceMeters = 1.5f;

        public static string Build(IReadOnlyList<SeenObject> objects, IReadOnlyList<PlaceRecord> places, DateTimeOffset now,
                                   int maxChars)
        {
            var objectsPart = new StringBuilder();
            foreach (var o in objects)
            {
                var entry = $"{o.Id} {o.Label} ({Where(o, places)}{Age(now - o.SeenAt)})";
                if (!TryAppend(objectsPart, objectsPart.Length == 0 ? "Seen recently: " : "; ", entry, maxChars - 1))
                    break;
            }
            if (objectsPart.Length > 0)
                objectsPart.Append('.');

            var placesPart = new StringBuilder();
            var budget = maxChars - objectsPart.Length - (objectsPart.Length > 0 ? 1 : 0) - 1;
            foreach (var p in places)
            {
                if (!TryAppend(placesPart, placesPart.Length == 0 ? "Places: " : ", ", p.Name, budget))
                    break;
            }
            if (placesPart.Length > 0)
                placesPart.Append('.');

            if (objectsPart.Length > 0 && placesPart.Length > 0)
                objectsPart.Append(' ');
            return objectsPart.Append(placesPart).ToString();
        }

        static bool TryAppend(StringBuilder sb, string separator, string entry, int budget)
        {
            if (sb.Length + separator.Length + entry.Length > budget)
                return false;
            sb.Append(separator).Append(entry);
            return true;
        }

        static string Where(SeenObject o, IReadOnlyList<PlaceRecord> places)
        {
            PlaceRecord nearest = null;
            var best = NearPlaceMeters;
            foreach (var p in places)
            {
                var delta = p.Pose.position - o.WorldPosition;
                var distance = new Vector2(delta.x, delta.z).magnitude;
                if (distance <= best)
                {
                    best = distance;
                    nearest = p;
                }
            }
            return nearest == null ? string.Empty : "near " + nearest.Name + ", ";
        }

        static string Age(TimeSpan age) =>
            age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min ago"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours} h ago"
            : $"{(int)age.TotalDays} d ago";
    }
}
