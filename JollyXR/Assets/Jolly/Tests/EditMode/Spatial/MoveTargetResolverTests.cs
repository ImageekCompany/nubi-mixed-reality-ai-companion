using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class MoveTargetResolverTests
    {
        /// <summary>Scene geometry as a list of hits per test; walkable floor is a disc on the floor plane.</summary>
        sealed class PointWorld : IWorldQuery
        {
            public Vector3? Hit;
            public float FloorY;
            public Vector2 WalkableCenter;
            public float WalkableRadius = 3f;
            public readonly List<SceneSurface> Surfaces = new();
            public float LastSampleRadius;

            public WalkableArea WalkableArea => WalkableArea.Scanned;

            public bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit)
            {
                hit = Hit ?? default;
                return Hit.HasValue && Vector3.Distance(ray.origin, hit) <= maxDistance;
            }

            public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
            {
                LastSampleRadius = radius;
                point = new Vector3(near.x, FloorY, near.z);
                return Vector2.Distance(new Vector2(near.x, near.z), WalkableCenter) <= WalkableRadius;
            }

            public void GetSurfaces(List<SceneSurface> into)
            {
                into.Clear();
                into.AddRange(Surfaces);
            }
        }

        static readonly Vector3 Head = new(0f, 1.6f, 0f);

        PointWorld _world;
        RecordingCharacterController _character;
        PointMoveSettings _settings;
        MoveTargetResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _world = new PointWorld();
            _character = new RecordingCharacterController { Position = new Vector3(0f, 0f, 1f) };
            _settings = new PointMoveSettings();
            _resolver = new MoveTargetResolver(_world, _character, _settings, new LocomotionSettings());
        }

        static Ray Toward(Vector3 point) => new(Head, point - Head);

        [Test]
        public void FloorHit_OnTheNavMesh_IsAFloorTargetThere()
        {
            var floor = new Vector3(1f, 0.02f, 1.2f);
            _world.Hit = floor;

            Assert.That(_resolver.TryResolve(Toward(floor), out var target), Is.True);

            Assert.That(target.Kind, Is.EqualTo(MoveTargetKind.Floor));
            Assert.That(Vector3.Distance(target.Point, new Vector3(1f, 0f, 1.2f)), Is.LessThan(1e-4f));
            Assert.That(_world.LastSampleRadius, Is.EqualTo(_settings.FloorSnapRadius), "the ring stays under the ray");
        }

        [Test]
        public void FloorHit_OffTheNavMesh_IsNoTarget()
        {
            var floor = new Vector3(4f, 0f, 1f);
            _world.Hit = floor;

            Assert.That(_resolver.TryResolve(Toward(floor), out _), Is.False);
        }

        [Test]
        public void WallHit_IsNoTarget()
        {
            var wall = new Vector3(0f, 1.2f, 2.5f);
            _world.Hit = wall;

            Assert.That(_resolver.TryResolve(Toward(wall), out _), Is.False);
        }

        [Test]
        public void RayIntoTheAir_WithNothingHit_IsNoTarget()
        {
            Assert.That(_resolver.TryResolve(new Ray(Head, new Vector3(0f, 0.2f, 1f)), out _), Is.False);
        }

        [Test]
        public void NoSceneGeometry_UsesTheFloorPlaneAtTheCharactersFeet()
        {
            _character.Position = new Vector3(0f, -0.05f, 1f);
            _world.FloorY = -0.05f;

            Assert.That(_resolver.TryResolve(Toward(new Vector3(0.5f, -0.05f, 1.5f)), out var target), Is.True);

            Assert.That(target.Kind, Is.EqualTo(MoveTargetKind.Floor));
            Assert.That(Vector3.Distance(target.Point, new Vector3(0.5f, -0.05f, 1.5f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void NoSceneGeometry_FloorPastTheMaximumDistance_IsNoTarget()
        {
            _world.WalkableRadius = 100f;

            Assert.That(_resolver.TryResolve(Toward(new Vector3(0f, 0f, 9f)), out _), Is.False);
        }

        [Test]
        public void TableTop_SendsTheCharacterBesideTheEdgeNearestTheHit()
        {
            // 1.2 x 0.75 x 0.8 m table centred at z 2: its near edge is z 1.6.
            _world.Surfaces.Add(new SceneSurface("table", new Pose(new Vector3(0f, 0.375f, 2f), Quaternion.identity),
                new Vector3(1.2f, 0.75f, 0.8f)));
            var top = new Vector3(0.3f, 0.75f, 1.7f);
            _world.Hit = top;

            Assert.That(_resolver.TryResolve(Toward(top), out var target), Is.True);

            Assert.That(target.Kind, Is.EqualTo(MoveTargetKind.Surface));
            Assert.That(target.SurfaceLabel, Is.EqualTo("table"));
            var clearance = new LocomotionSettings().SurfaceClearance;
            Assert.That(Vector3.Distance(target.Point, new Vector3(0.3f, 0f, 1.6f - clearance)), Is.LessThan(1e-4f));
        }

        [Test]
        public void CouchSeatPlane_IsATopToo()
        {
            _world.Surfaces.Add(new SceneSurface("couch", new Pose(new Vector3(-1f, 0.45f, 2f), Quaternion.identity),
                new Vector3(2f, 0f, 0.9f)));
            var seat = new Vector3(-1.2f, 0.46f, 2.3f);
            _world.Hit = seat;

            Assert.That(_resolver.TryResolve(Toward(seat), out var target), Is.True);

            Assert.That(target.SurfaceLabel, Is.EqualTo("couch"));
            Assert.That(target.Point.z, Is.GreaterThan(2.45f), "the back edge is nearest the hit");
        }

        [Test]
        public void HitAboveTheFloorBesideAnySurface_IsNoTarget()
        {
            _world.Surfaces.Add(new SceneSurface("table", new Pose(new Vector3(0f, 0.375f, 2f), Quaternion.identity),
                new Vector3(1.2f, 0.75f, 0.8f)));
            var lamp = new Vector3(2f, 0.75f, 2f);
            _world.Hit = lamp;

            Assert.That(_resolver.TryResolve(Toward(lamp), out _), Is.False);
        }

        [Test]
        public void SurfaceSideBelowItsTop_IsNoTarget()
        {
            _world.Surfaces.Add(new SceneSurface("storage", new Pose(new Vector3(0f, 0.5f, 2f), Quaternion.identity),
                Vector3.one));
            var side = new Vector3(0f, 0.5f, 1.5f);
            _world.Hit = side;

            Assert.That(_resolver.TryResolve(Toward(side), out _), Is.False);
        }
    }
}
