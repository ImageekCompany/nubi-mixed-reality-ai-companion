using System;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Memory
{
    /// <summary>
    /// Persistence DTOs of <c>scene.json</c>: public float fields, because Newtonsoft loops on <c>Vector3.normalized</c>
    /// and contract types have private setters (ADR-0015). Times are UTC unix milliseconds.
    /// </summary>
    public sealed class SeenObjectDto
    {
        public string id;
        public string label;
        public float x, y, z;
        public string anchor;
        public long seen_ms;
        public string summary;

        public static SeenObjectDto From(SeenObject o) => new()
        {
            id = o.Id, label = o.Label, x = o.WorldPosition.x, y = o.WorldPosition.y, z = o.WorldPosition.z,
            anchor = o.AnchorId, seen_ms = o.SeenAt.ToUnixTimeMilliseconds(), summary = o.Summary,
        };

        public SeenObject ToObject(Vector3 position) => new()
        {
            Id = id, Label = label, WorldPosition = position, AnchorId = anchor,
            SeenAt = DateTimeOffset.FromUnixTimeMilliseconds(seen_ms), Summary = summary,
        };
    }

    public sealed class PlaceDto
    {
        public string name;
        public float x, y, z, qx, qy, qz, qw;
        public string anchor;
        public long created_ms;

        public static PlaceDto From(PlaceRecord p) => new()
        {
            name = p.Name, x = p.Pose.position.x, y = p.Pose.position.y, z = p.Pose.position.z,
            qx = p.Pose.rotation.x, qy = p.Pose.rotation.y, qz = p.Pose.rotation.z, qw = p.Pose.rotation.w,
            anchor = p.AnchorId, created_ms = p.CreatedAt.ToUnixTimeMilliseconds(),
        };

        public PlaceRecord ToRecord(Pose pose) => new()
        {
            Name = name, Pose = pose, AnchorId = anchor, CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(created_ms),
        };
    }
}
