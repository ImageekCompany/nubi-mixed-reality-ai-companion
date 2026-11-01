using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Walks to a taught place, a piece of furniture from the room scan, or a seen object by id or label, in that order
    /// (architecture §5.5). Furniture is approached like go_to_surface: beside it, facing it. Returns once the motion has
    /// started; the walk runs on the controller's own token and survives barge-in.
    /// </summary>
    public sealed class WalkToTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"place\":{\"type\":\"string\",\"maxLength\":64," +
            "\"description\":\"Taught place name, seen object id or label (obj_3, mug), or furniture type\"}}," +
            "\"required\":[\"place\"],\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly ISceneMemory _scene;
        readonly IWorldQuery _world;
        readonly LocomotionSettings _settings;
        readonly List<SceneSurface> _surfaces = new();

        public WalkToTool(ICharacterController character, ISceneMemory scene, IWorldQuery world, LocomotionSettings settings)
        {
            _character = character;
            _scene = scene;
            _world = world;
            _settings = settings;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.WalkTo,
            "Walk to a named place the user taught, a seen object (its id like obj_3 or its label like 'mug') " +
            "or a furniture type (table, couch, bed, storage, screen).",
            Schema, TimeSpan.FromSeconds(3), ToolConcurrency.Locomotion);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("place", out var place);
            var output = new ToolJson();
            float eta;
            bool walking;
            if (_scene.TryGetPlace(place, out var taught))
            {
                walking = TryWalk(taught.Pose.position, _settings.WalkStopDistance, out eta);
            }
            else if (TryFindSurface(place, out var surface))
            {
                walking = SurfaceWalk.TryStart(_character, _world, surface, _settings, out eta);
            }
            else if (SeenObjectLookup.TryFind(_scene, place, out var seen))
            {
                _character.LookAt(LookTarget.At(seen.WorldPosition));
                walking = TryWalk(seen.WorldPosition, _settings.WalkStopDistance, out eta);
            }
            else
            {
                return UniTask.FromResult(ToolResult.Failure("unknown_place", place));
            }

            output.Add("status", walking ? "walking" : "unreachable").Add("place", place);
            if (walking)
                output.Add("eta_s", Mathf.CeilToInt(eta));
            return UniTask.FromResult(ToolResult.Success(output.ToString()));
        }

        bool TryWalk(Vector3 target, float stopDistance, out float eta)
        {
            eta = 0f;
            if (_world.TrySampleWalkable(target, _settings.SampleRadius, out var walkable)
                && _character.TryStartMoveTo(walkable, stopDistance, out eta))
                return true;
            _character.LookAt(LookTarget.At(target));
            return false;
        }

        bool TryFindSurface(string place, out SceneSurface surface)
        {
            _world.GetSurfaces(_surfaces);
            return SurfaceTargets.TryNearest(_surfaces, FurnitureLabels.Normalize(place), _character.Position, out surface);
        }
    }
}
