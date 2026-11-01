using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    public enum MoveTargetKind { Floor, Surface }

    /// <summary>Where a tap sends the character: a walkable floor point, and the furniture it stands beside, if any.</summary>
    public readonly struct MoveTarget
    {
        public MoveTarget(MoveTargetKind kind, Vector3 point, string surfaceLabel)
        {
            Kind = kind;
            Point = point;
            SurfaceLabel = surfaceLabel;
        }

        public MoveTargetKind Kind { get; }
        /// <summary>On the NavMesh: where the ring is drawn and where the character walks.</summary>
        public Vector3 Point { get; }
        public string SurfaceLabel { get; }
    }

    /// <summary>
    /// Turns the user's ray into a move target (ADR-0044). A hit at the character's floor height counts when walkable
    /// floor lies within the snap radius and is not at the user's feet (<see cref="PointMoveSettings.UserMinDistance"/>); a hit on a scanned table top, couch seat or box top becomes the walkable floor
    /// beside it, on the edge nearest the hit (the character never stands on furniture, <see cref="SurfaceTargets"/>).
    /// Walls, the air and floor off the NavMesh give nothing. With no scene geometry under the ray (no scene permission,
    /// or the fallback floor's small disc) the ray meets the floor plane at the character's feet instead, and the NavMesh
    /// check still decides. Allocation-free after the first surface query.
    /// </summary>
    public sealed class MoveTargetResolver
    {
        readonly IWorldQuery _world;
        readonly ICharacterController _character;
        readonly IUserPoseProvider _user;
        readonly PointMoveSettings _settings;
        readonly LocomotionSettings _locomotion;
        readonly List<SceneSurface> _surfaces = new();

        public MoveTargetResolver(IWorldQuery world, ICharacterController character, IUserPoseProvider user,
                                  PointMoveSettings settings, LocomotionSettings locomotion)
        {
            _world = world;
            _character = character;
            _user = user;
            _settings = settings;
            _locomotion = locomotion;
        }

        public bool TryResolve(Ray ray, out MoveTarget target)
        {
            target = default;
            var floorY = _character.Position.y;
            if (!TryHit(ray, floorY, out var hit))
                return false;
            if (Mathf.Abs(hit.y - floorY) <= _settings.FloorTolerance)
            {
                if (!_world.TrySampleWalkable(hit, _settings.FloorSnapRadius, out var floor) || NearUser(floor))
                    return false;
                target = new MoveTarget(MoveTargetKind.Floor, floor, null);
                return true;
            }
            if (!TryFindTop(hit, out var surface))
                return false;
            var beside = SurfaceTargets.Approach(surface, new Vector3(hit.x, floorY, hit.z), _locomotion.SurfaceClearance);
            if (!_world.TrySampleWalkable(beside, _locomotion.SampleRadius, out var point) || NearUser(point))
                return false;
            target = new MoveTarget(MoveTargetKind.Surface, point, surface.Label);
            return true;
        }

        bool NearUser(Vector3 point)
        {
            var head = _user.Head.position;
            return new Vector2(point.x - head.x, point.z - head.z).sqrMagnitude < _settings.UserMinDistance * _settings.UserMinDistance;
        }

        bool TryHit(Ray ray, float floorY, out Vector3 hit)
        {
            if (_world.TryRaycast(ray, _settings.RayMaxDistance, out hit))
                return true;
            if (ray.direction.y > -1e-3f)
                return false;
            var distance = (floorY - ray.origin.y) / ray.direction.y;
            if (distance <= 0f || distance > _settings.RayMaxDistance)
                return false;
            hit = ray.GetPoint(distance);
            return true;
        }

        bool TryFindTop(Vector3 hit, out SceneSurface surface)
        {
            surface = default;
            var best = float.PositiveInfinity;
            _world.GetSurfaces(_surfaces);
            foreach (var candidate in _surfaces)
            {
                var dy = Mathf.Abs(candidate.Top - hit.y);
                if (dy > _settings.SurfaceTolerance || dy >= best
                    || !SurfaceTargets.Contains(candidate, hit, _settings.SurfaceTolerance))
                    continue;
                best = dy;
                surface = candidate;
            }
            return !float.IsPositiveInfinity(best);
        }
    }
}
